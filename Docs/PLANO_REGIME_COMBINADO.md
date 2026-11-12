# Plano: regime combinado para as tags das bombas

**Data:** 12/11/2026. **Cliente de referência:** PK2 (simulador CP300). **Situação:** proposta. **Nenhum código, configuração ou threshold foi alterado.**
**Problema:** o baseline por regime aceita **uma** tag de regime por tag analisada. As tags das bombas (B-01: TAG27/29/31; B-02: TAG28/30/32) precisam distinguir pelo menos três estados: **bomba parada**, **bomba sozinha** e **bomba em paralelo com a outra**.
**Fontes:** `Docs/PROBLEMAS_ENCONTRADOS.md` (PROB: P02, P03, P08, P20, P25), `Docs/PROPOSTA_BASELINE_ESTAVEL.md` (PROP), `Docs/RESULTADO_PRESET1.md` (RES §3.3, §4) e `Docs/RESULTADO_PRESET6.md` (RES6 §2.3, §2.6).
**Regra fixa:** TAG60–TAG81 (gabarito do simulador) **nunca** entram como tag analisada nem como tag de regime.

---

## 1. Ponto de partida

### 1.1 O que o código faz hoje (confirmado no código)

| Peça | Arquivo : método | Limitação |
|---|---|---|
| Configuração | `Models/TagRegimeOperationalIa.cs` (`TagRegimeOperacionalIa`: um campo `TagRegime`) | Uma tag de regime por linha |
| Unicidade | `Data/AppDbContext.cs : OnModelCreating` (índice único `ClienteId + TagName`) | Uma linha por tag analisada |
| Busca | `Services/AnomalyDetection/OperationalRegimeBaselineProvider.cs : Resolve` | Uma consulta `BuscarHistoricoPorPeriodo` para a única `TagRegime` |
| Seleção | `Services/AnomalyDetection/OperationalRegimeHistoryResolver.cs : Resolve` → `FindCausalRegimePoint`, `BelongsToSameState` | Compara **um** valor `double` com tolerância; estado = igualdade exata |
| Estatística | `Services/AnomalyDetection/OperationalRegimeBaselineService.cs : Resolve` → `TagProfileStatisticsCalculator.Calculate` (IQR) | Independe do número de tags (recebe o histórico já selecionado) |
| Diagnóstico | `src/IAApi.Engine/Observability/DiagnosticContracts.cs` (`BaselineDiagnostic.RegimeValue`: `double?`) | Um valor só |
| Uso | `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs : PrepararAsync` | Troca média, desvio, deadband **e o histórico** quando o regime se aplica (PROP §1) |

**Custo atual (confirmado no código):** `FindCausalRegimePoint` percorre a série de regime desde o início para **cada** ponto da tag analisada. São ≈ n²/2 comparações por leitura (≈1 milhão com 1.440 pontos). Com duas ou três tags de regime, isso multiplica.

### 1.2 O que os dados mostram (confirmado no CSV `dados/cp300_10a11nov.csv`, 10/11 12:30 → 11/11 17:39)

| TAG41 (estação) | TAG42 (B-01) | TAG43 (B-02) | TAG46 (set-point) | Minutos | Classe para a B-01 | Classe para a B-02 |
|---|---|---|---|---|---|---|
| 2 (1 bomba) | 0 | 2 | 450 / 380 | 300 / 240 | parada | sozinha |
| 2 (1 bomba) | 2 | 0 | 450 / 380 | 491 / 60 | sozinha | parada |
| 3 (2 bombas) | 2 | 2 | 450 | 647 | paralelo | paralelo |
| 2 | 2 / 3 | 3 / 2 | 450 | 2 / 2 | transição (Parando) | transição |

- **TAG41 = 3 ⇔ as duas bombas em 2 (Operando)**, sem exceção no arquivo. **TAG41 = 2 não diz qual bomba**: com TAG41 sozinha, "B-01 sozinha" e "B-01 parada" ficam no mesmo regime. **TAG41 só funciona junto com o estado da própria bomba.**
- Valores definidos mas **não observados**: TAG41 = 0/1/4/5/6/7 e TAG42/43 = 1 (Partindo), 4 (Falha), 5 (Manutenção). No trip do preset 6 (16:52), a TAG42 foi para **0**, não para 4 (RES6 §1.3).
- O set-point noturno (380) só aparece com **uma** bomba. "B-01 sozinha à noite" tem 60 min em 24 h, acima do mínimo de 30.
- Na janela de treino do preset 6, as três classes têm amostras de sobra para as duas bombas: parada 445–466, sozinha 445–466, paralelo 520.

### 1.3 Requisitos

1. Separar **parada / sozinha / paralelo** para cada bomba; opcionalmente, o set-point (TAG46) como terceira dimensão (PROB P03 de madrugada).
2. Agrupar estados de transição ou raros sem fragmentar o histórico (PROB P08).
3. **Funcionar nos dois modos:** hoje (24 h móveis) e no futuro (referência treinada por período e por regime; PROP §4, peças A e B). Para isso, o regime precisa de uma **chave estável e serializável** (por exemplo, `B01=sozinha`), que vire a chave dos perfis treinados por regime.
4. Sem regime combinado configurado, o comportamento fica **idêntico** ao atual (regressão byte a byte no replay).
5. Recusar TAG60–TAG81 em qualquer componente do regime.

---

## 2. Opções

### Opção A: regime combinado por tupla de N tags, com agrupamento de estados

**Ideia.** A configuração de uma tag analisada passa a ter **N componentes** (tags de regime). Cada ponto do histórico recebe uma tupla de estados alinhada causalmente (um `FindCausalRegimePoint` por componente). O ponto entra no baseline se a tupla for igual à atual. Cada componente pode ter um **mapa de estados** opcional (por exemplo, `0,3,4,5→0; 1→1; 2→2`) para juntar parada, parando, falha e manutenção.

**Configuração para as bombas:**

| TagName | Componentes (ordem) | Mapa de estados | Classes resultantes |
|---|---|---|---|
| TAG27, TAG29, TAG31 | TAG42; TAG43 | TAG42: `2→2; *→0`; TAG43: `2→2; *→0` | (0,·) parada; (2,0) sozinha; (2,2) paralelo |
| TAG28, TAG30, TAG32 | TAG43; TAG42 | idem | idem para a B-02 |
| Variante | + TAG46 (sem mapa) | — | sozinha × 450/380 |
| Variante com TAG41 | TAG42; TAG41 | TAG41: `3→3; *→2` | equivalente nos dados (§1.2) |

Com o mapa, "parada" junta B-01 parada com a outra sozinha e as duas paradas. Para a bomba parada isso basta: as tags ficam ~0 e a tag já cai em `Discrete` (PROB P20).

**O que muda no código:**

| Arquivo : método | Mudança |
|---|---|
| `Models/TagRegimeOperationalIa.cs` | Nova entidade filha `TagRegimeComponenteIa` (`ConfiguracaoId`, `Ordem`, `TagRegime`, `MapaEstados` opcional, `ToleranciaEstado`). `TagRegime` atual continua sendo o componente 1 (compatível) |
| `Data/AppDbContext.cs : OnModelCreating` + nova migration | Tabela e relação; índice único atual mantido |
| `OperationalRegimeBaselineProvider.cs : Resolve` | Carrega os componentes; uma consulta `BuscarHistoricoPorPeriodo` por componente; recusa componente TAG60–81 |
| `OperationalRegimeBaselineService.cs : Resolve` | Recebe a lista de séries (sobrecarga; a assinatura atual delega) |
| `OperationalRegimeHistoryResolver.cs : Resolve`, `FindCausalRegimePoint`, `BelongsToSameState` | Estado = tupla mapeada; alinhamento por componente; **trocar a busca linear por dois ponteiros** (mesmo resultado, O(n)) |
| `OperationalRegimeHistoryResolution` / `OperationalRegimeBaselineResolution` | Novo `CurrentRegimeKey` (`"TAG42=2|TAG43=0"`); `CurrentRegimeValue` = componente 1, para compatibilidade |
| `DiagnosticContracts.cs` (`BaselineDiagnostic`) e `PredictionDetectionPreparationService.cs : CriarBaselineEfetivo` | Campo `RegimeKey` (aditivo) |
| `tools/Cp300OfflineReplay/Cp300Replay.cs : ParseRegimes`, `ValidateRegimes`, coluna `valor_regime` | Sintaxe `TAG27=TAG42+TAG43`, mapa opcional, coluna `chave_regime` |

- **Risco:** médio.
  - A migration no banco do PK2 exige aprovação.
  - Sem mapa, tuplas raras fragmentam o histórico (P08).
  - O custo de alinhamento sobe se não houver o refactor de dois ponteiros (P10/P11).
- **Esforço:** ≈ 4–6 dias (runtime 3–4, ferramenta e testes 1–2).
- **Com a referência estável:** a chave da tupla mapeada é determinística e vira diretamente a chave dos perfis por regime (`PerfilRegimeIa(ClienteId, TagName, ChaveRegime, …)`, PROP §4-A). O treino por período agrupa os pontos pela mesma chave.

### Opção B: tag de regime derivada (virtual), sem mexer no resolver

**Ideia.** Definir uma **série derivada** calculada a partir de tags de estado, por exemplo `RB01 = 0 parada / 1 sozinha / 2 paralelo` a partir de TAG42 e TAG43. Um *decorator* de `IHistoricoService`, usado só pelo provider de regime, reconhece o nome derivado em `BuscarHistoricoPorPeriodo`, busca as tags-fonte, alinha e devolve a série de códigos. A configuração atual continua igual: `TagRegime = "RB01"`.

| Arquivo : método | Mudança |
|---|---|
| Novo `Services/AnomalyDetection/DerivedRegimeHistoryService.cs : BuscarHistoricoPorPeriodo` | Decorator que monta a série derivada; demais nomes passam direto |
| Nova tabela `RegimesDerivadosIa` (`ClienteId`, `Nome`, `Definicao` JSON com fontes e regras, `Versao`) + migration | Definição auditável e versionada |
| `Services/AnomalyDetection/DependencyInjection/AnomalyDetectionServiceCollectionExtensions.cs` | Injetar o decorator **só** no `OperationalRegimeBaselineProvider` |
| Resolver, service, diagnóstico e ferramenta | **Sem mudança** (o valor continua `double`) |

**Configuração:** `TagsRegimesOperacionaisIa`: TAG27/29/31 → `RB01`; TAG28/30/32 → `RB02`. Em `RegimesDerivadosIa`: `RB01` = regras sobre TAG42 e TAG43 (ou TAG42 e TAG41).

- **Risco:** médio.
  - A lógica fica escondida na camada de dados.
  - O nome virtual pode colidir com uma tag real; é preciso proibir o prefixo `TAG` e o intervalo TAG60–81.
  - O alinhamento é feito duas vezes (decorator e resolver).
  - Mais consultas ao Bridge (duas fontes por tag derivada; P10).
  - Mudar a regra muda o significado dos códigos; a versão é obrigatória para os perfis treinados.
- **Esforço:** ≈ 2–3 dias.
- **Com a referência estável:** funciona (a chave é `RB01=1`), mas o significado da chave depende da versão da definição. Perfis treinados com uma versão não podem ser usados com outra.

### Opção C: classes de regime por regras, nativas no resolver (estação + bomba)

**Ideia.** Em vez de tuplas, a configuração lista **classes nomeadas com condições** sobre várias tags, avaliadas por ponto em ordem de prioridade, por exemplo:
- `B01_PARALELO`: TAG41 = 3 e TAG42 = 2;
- `B01_SOZINHA`: TAG41 = 2 e TAG42 = 2;
- `B01_PARADA`: TAG42 ≠ 2;
- `TRANSICAO`: resto.

A classe vira a chave. Opcionalmente, um sufixo de set-point (`_SP380`).

| Arquivo : método | Mudança |
|---|---|
| Nova entidade `TagRegimeClasseIa` (`ConfiguracaoId`, `Classe`, `Prioridade`, `Condicoes` JSON) + `AppDbContext` + migration | Regras por tag analisada (ou compartilhadas por grupo de tags) |
| `OperationalRegimeBaselineProvider.cs : Resolve` | Busca todas as tags citadas nas regras |
| `OperationalRegimeHistoryResolver.cs : Resolve` | Avalia a classe de cada ponto (alinhamento por tag, dois ponteiros); seleciona os pontos da classe atual |
| Resolutions, `BaselineDiagnostic`, `CriarBaselineEfetivo` | `RegimeKey` = nome da classe |
| Ferramenta de replay | Arquivo de regras (`--regime-classes regras.json`) e coluna `chave_regime` |

- **Risco:** médio-alto. É a mais expressiva, mas tem mais código e mais configuração; uma regra mal escrita classifica errado em silêncio (precisa de um relatório de cobertura por classe).
- **Esforço:** ≈ 6–8 dias.
- **Com a referência estável:** a melhor chave (nome semântico). Mas a referência continua dependendo da versão das regras.

### 2.4 Comparação

| Critério | A: tupla + mapa | B: tag derivada | C: classes por regras |
|---|---|---|---|
| Parada / sozinha / paralelo | Sim (com mapa) | Sim | Sim |
| Set-point como 3ª dimensão | Sim (3º componente) | Sim (na regra) | Sim (sufixo) |
| Transições sem fragmentar | Com mapa | Com regra | Com classe `TRANSICAO` |
| Mudança no resolver/diagnóstico | Média | Nenhuma | Grande |
| Lógica visível na configuração do regime | Sim | Não (fica no decorator) | Sim |
| Chave para a referência estável | Determinística (`TAG42=2\|TAG43=0`) | Código + versão | Nome + versão |
| Generaliza para outras plantas | Bem (qualquer N tags discretas) | Bem | Melhor (condições arbitrárias) |
| Esforço | 4–6 dias | 2–3 dias | 6–8 dias |
| Risco | Médio | Médio | Médio-alto |

**TAG41 × TAG43 como segunda tag (vale para A, B e C):** nos dados as duas são equivalentes (§1.2). A **TAG43** diz o estado exato da outra bomba e é simétrica entre B-01 e B-02. A **TAG41** carrega estados da estação (4 nível baixo, 5 falha, 6 manutenção, 7 emergência) que podem ser úteis como classes próprias, mas não foram observados. **Sugestão:** usar TAG43/TAG42 (a outra bomba) e avaliar a TAG41 depois, como classe "estação em falha/emergência".

---

## 3. Como testar primeiro no replay

### 3.1 Etapa 0: teste da hipótese sem mudar código (vale para as três opções)

A ferramenta de replay já aceita **qualquer** tag do CSV como regime, exceto TAG60–81 (`ValidateRegimes`). Dá para gerar, **fora do repositório** (script no scratchpad; saída em `dados/`, que não é versionada), uma cópia do CSV com séries derivadas e rodar o replay atual:

| Série derivada | Definição | Emula |
|---|---|---|
| `RB01T` / `RB02T` | `10·TAG42 + TAG43` / `10·TAG43 + TAG42` (tupla exata) | Opção A sem mapa |
| `RB01` / `RB02` | 0 = própria ≠ 2; 1 = própria = 2 e outra ≠ 2; 2 = ambas = 2 | Opções A com mapa, B e C |
| `RB01S` / `RB02S` | `RB01 + 10·(TAG46 = 380)` | Variante com set-point |

Os nomes não começam com `TAG`, então não colidem com tags reais nem com o gabarito.

**Execuções**, com o regime recomendado atual (TAG28/30/32 ← TAG43 é **substituído**; TAG26 ← TAG45 e TAG24 ← TAG46 ficam):

| Cenário | Treino | Configs |
|---|---|---|
| Preset 1 | `--treino-ate 2026-11-11T12:55` | recomendado (referência) / + `RB01T,RB02T` / + `RB01,RB02` / + `RB01S,RB02S` |
| Madrugada | `--treino-ate 2026-11-11T00:00` | idem |
| Preset 6 | `--treino-ate 2026-11-11T15:06 --permitir-falha-no-treino` (12 min do preset 1 no treino) e, para comparar, `--treino-inicio/--treino-fim` num período limpo | idem |

**Critérios de aceite (minutos-tag de alarme, B-01 + B-02):**

| Medida | Hoje (regime recomendado) | Meta |
|---|---|---|
| B-01 (TAG27/29/31), preset 1, fora da falha | 23 (RES §4.2) | ≤ 5 |
| B-01, madrugada | 566 (RES §4.3) | ≤ 30 |
| B-02 (TAG28/30/32), preset 6, B-02 sozinha após o failover | 56 (RES6 §2.3) | ≤ 5 |
| Preset 6, falsos fora da falha (todas as tags) | 27 (RES6 §2.4) | ≤ 27 |
| Preset 6, TAG29: primeiro alarme | 15:32, 94 ‰ (RES6 §2.6) | ≤ 15:32, sem alarme da TAG29 antes da falha |
| Fallbacks para o global (`origem_baseline = Global`) | — | Contar e listar; esperar só transições |
| Conferência | 0 divergências | 0 divergências |

**O que esperar** [hipótese]:
- Pela estimativa com proxies (RES §3.3), os alarmes da B-01 caem para 0–1 min por tag.
- A TAG29 **não** deve detectar mais cedo: contra "B-01 sozinha" (164,6 ± 28,2), 199 fica a 1,2σ e 253 a 3,1σ. O ganho esperado está nos **falsos alarmes**, não no tempo de detecção.

### 3.2 Etapa 1: implementar a opção escolhida na ferramenta e no runtime, atrás de configuração

- **Regressão:** sem componentes extras, os replays atuais (preset 1, madrugada, preset 6) ficam **idênticos byte a byte** às saídas atuais.
- **Equivalência:** com a opção implementada, os replays reproduzem os resultados da Etapa 0 (mesmas classes ⇒ mesmos pontos selecionados).
- **Unitários** (`IAApi.Tests/Services/AnomalyDetection/OperationalRegimeHistoryResolverTests.cs`, `…ProviderTests.cs`, `…ServiceTests.cs`, já existentes):
  - tupla com dois componentes;
  - mapa de estados;
  - componente sem leitura na tolerância ⇒ ponto descartado;
  - menos de 30 ⇒ não aplicado;
  - componente TAG60–81 ⇒ recusado;
  - dois ponteiros = busca linear (mesmo resultado).

### 3.3 Etapa 2: produção

Só com aprovação: migration no banco do PK2, cadastro das configurações e ativação por tag, com rollback por `Ativo = false`.

---

## 4. Recomendação

**Opção A (tupla de N tags com mapa de estados opcional), precedida da Etapa 0.**

- **Etapa 0 primeiro:** confirma ou descarta, sem mudar código, que separar parada, sozinha e paralelo resolve P02, P03 e o P03 espelhado na B-02 (RES6 §2.3). Também diz se o set-point precisa ser a terceira dimensão.
- **A em vez de B:** a lógica fica explícita na configuração do regime, sem nomes virtuais nem decorator na camada de dados. A chave é determinística, o que é o mais simples para a referência treinada por regime (PROP F1). O esforço extra é de ~2 dias.
- **A em vez de C:** cobre o caso das bombas (e o set-point) com menos código. O mapa de estados resolve as transições. Se outra planta precisar de condições arbitrárias, C pode ser construída depois sobre A (classes = tuplas mapeadas com nome).
- **Junto com A**, trocar a busca linear do resolver por dois ponteiros. Isso compensa o custo de N componentes e ajuda P10/P11; medir com PERF_DIAG.

**Compatibilidade com a proposta de baseline estável:**
- Hoje (24 h móveis), a tupla seleciona os pontos do mesmo estado nas 24 h.
- No futuro, a mesma chave seleciona o perfil treinado por período para aquele estado. O relatório de treino por período (`pontos por estado`) passa a mostrar a contagem por chave.
- Os riscos de absorção (P25) e de IQR dentro do regime (P01) **continuam** no modo de 24 h móveis. O regime combinado reduz a mistura de estados, mas não substitui a referência estável.

---

## 5. Resultado da etapa 0

**Data:** 12/11/2026. **Nível de evidência:** **confirmado no replay** (código de produção sobre dados do simulador, `tools/Cp300OfflineReplay`). Não é validação em planta: sem Isolation Forest, multivariado nem latência. **Nenhum código do projeto foi alterado.**

### 5.1 Séries derivadas (regras)

Arquivo: `dados/cp300_10a11nov_derivado.csv` = `dados/cp300_10a11nov.csv` + 4 séries por minuto (1.742 minutos), geradas por `e0_derivar.py` (scratchpad da sessão, fora do repositório). Os nomes não começam com `TAG`: não colidem com tags reais nem com o gabarito TAG60–TAG81, que **não** foi usado em nenhuma série.

| Série | Regra (valores do mesmo minuto) | Valores e minutos no arquivo |
|---|---|---|
| `RB01` | 0 = TAG42 ≠ 2 (B-01 parada, parando ou partindo); 1 = TAG42 = 2 e TAG43 ≠ 2 (**B-01 sozinha**); 2 = TAG42 = 2 e TAG43 = 2 (**paralelo**) | 0: 542, 1: 553, 2: 647 |
| `RB02` | Igual, trocando TAG42 ↔ TAG43 | 0: 553, 1: 542, 2: 647 |
| `RB01S` | `RB01` + 10 se TAG46 = 380 (set-point noturno) | 0: 302, 1: 493, 2: 647, 10: 240, 11: 60 |
| `RB02S` | `RB02` + 10 se TAG46 = 380 | 0: 493, 1: 302, 2: 647, 10: 60, 11: 240 |

O paralelo nunca ocorreu com o set-point 380, por isso não existe o código 12.

### 5.2 Execuções

13 tags (TAG22–TAG33 e TAG44), o mesmo arquivo de entrada nas 12 execuções. Saídas em `dados/e0_<cenário>_<config>.csv` e `.log`.

| Config | `--regime` |
|---|---|
| `sem` | — |
| `rec` (regime recomendado atual) | TAG28/30/32 = TAG43, TAG26 = TAG45, TAG24 = TAG46 |
| `comb` (regime combinado) | TAG27/29/31 = RB01, TAG28/30/32 = RB02, TAG26 = TAG45, TAG24 = TAG46 |
| `combs` (combinado + set-point) | TAG27/29/31 = RB01S, TAG28/30/32 = RB02S, TAG26 = TAG45, TAG24 = TAG46 |

| Cenário | Treino e replay |
|---|---|
| Preset 1 | `--treino-ate 2026-11-11T12:55` → 14:25 |
| Madrugada | `--treino-ate 2026-11-11T00:00` → 05:30 |
| Preset 6 | `--treino-ate 2026-11-11T15:06 --permitir-falha-no-treino` → 17:40 (o treino contém os 12 min da obstrução do preset 1) |

**Conferência:** 12 de 12 execuções com 0 erros e 0 divergências. **Regressão:** `sem` e `rec` ficaram idênticas, linha a linha, aos replays anteriores (`replay_preset1_*`, `replay_madrugada_*`, `replay_preset6_*`: 0 diferenças).

### 5.3 Antes/depois contra as metas

Minutos-tag com alarme. ✅ = atende a meta; ❌ = não atende.

| Medida | Meta | `sem` | `rec` (hoje) | `comb` | `combs` |
|---|---|---|---|---|---|
| B-01 (TAG27/29/31), preset 1, fora da falha | ≤ 5 | 23 | 23 | 6 ❌ | **4 ✅** |
| B-01, madrugada | ≤ 30 | 566 | 566 | **6 ✅** | 82 ❌ (79 por fallback na 1ª noite; 3 com regime) |
| B-02 (TAG28/30/32), madrugada (sem meta) | — | 152 | 10 | 130 | 32 (30 por fallback na 1ª noite; 2 com regime) |
| B-02 sozinha após o failover, preset 6 (16:52–17:11) | ≤ 5 | 10 | 56 | 40 ❌ | **0 ✅** |
| Preset 6, falsos fora da falha (todas as tags) | ≤ 27 | 175 | 27 | **20 ✅** | **20 ✅** |
| Preset 6, TAG29: primeiro alarme | ≤ 15:32 | 15:32 | 15:32 | **15:32 ✅** | **15:32 ✅** |
| Preset 6, TAG29 antes da falha | 0 | 0 | 0 | 0 | 0 |
| Preset 6, alarmes no failover (B-01 parada, B-02 sozinha) | — | 64 | 110 | 40 | **0** |
| **Total de alarmes:** preset 1 / madrugada / preset 6 | — | 245 / 1.282 / 325 | 41 / 660 / 223 | 33 / 220 / 144 | **31 / 198 / 104** |

### 5.4 Leitura

1. **Separar parada, sozinha e paralelo resolve a B-01** (P03): madrugada 566 → 6 (`comb`); preset 1 23 → 4–6.
2. **O set-point é necessário para a B-02.**
   - Em `comb`, a B-02 sozinha mistura dia (450) e noite (380). Ela alarmou na madrugada (TAG28/32, 00:43–01:58, 130 min) e no failover do preset 6 (40 min).
   - Em `combs`, os dois casos somem: preset 6 = 0 no failover; madrugada = 2 com regime.
3. **Os 79 + 30 alarmes da madrugada em `combs` são artefato do início do arquivo.** O CSV começa às 12:30 do dia anterior, então não havia uma noite anterior.
   - Os estados noturnos (`RB0xS` = 10/11) tinham menos de 30 amostras e caíram no perfil global (P08): B-01 00:47–01:12 e B-02 00:01–00:30, todos com `origem_baseline = Global`.
   - É o mesmo efeito da TAG24 em 00:01–00:30 (P05).
   - Com uma noite anterior no histórico, provavelmente não aconteceriam [**hipótese**; confirmar com um CSV de 48 h].
4. **O failover deixa de gerar alarmes** em `combs`: a B-01 parada vira regime próprio (classificada `Discrete`, detectores bloqueados; P20), e a B-02 sozinha de dia tem baseline próprio.
5. **A detecção não piora nem melhora:** TAG29 às 15:32, como antes. Em `comb`/`combs` há um intervalo em 15:33–15:34 sem alarme (84 contra 86 minutos de falha), e o alarme volta contínuo de 15:35 a 16:51. **Ressalva:** esse intervalo atrasa a detecção confirmada de 15:34 para 15:37 (ver §5.6).
6. **Sinal novo no preset 1** [hipótese, P06/P21]. Em `comb` e `combs`, as 4 tags das bombas alarmam às **13:37–13:38**, dentro da obstrução (777 ‰), quando a B-02 parte e as duas ficam em paralelo.
   - A frequência foi 435 contra 396 ± 7 no regime "paralelo" (≈5σ), e a vibração da B-01 202 contra 142 ± 9.
   - Na partida normal das 13:59–14:01, as mesmas tags ficaram com nota ≤ 6.
   - Pode ser o primeiro efeito visível da obstrução, mas são 2 minutos, num minuto de transição, num único evento. Não conta como detecção até ser repetido.
7. **Sobram fora das bombas:** TAG26 (vazão alta com a válvula aberta, P04) e TAG24/TAG22 (reset e primeira noite). Esses não dependem do regime combinado.

### 5.5 Resumo

- **A variante com set-point (`combs`: própria bomba × outra bomba × set-point) atende todas as metas**, menos a da B-01 de madrugada.
- Na madrugada, 79 dos 82 alarmes vêm do fallback para o perfil global na primeira noite do arquivo, por falta de histórico noturno. Com regime aplicado, são **3**.
- Em relação ao regime recomendado atual: menos 25 % de alarmes no preset 1, menos 70 % na madrugada e menos 53 % no preset 6. Os alarmes do failover zeram, e a detecção da TAG29 continua às 15:32.
- **Decisão:** a Opção A deve suportar **3 componentes** (estado próprio, estado da outra bomba e TAG46), com mapa de estados.
- **Antes de implementar:** confirmar com um CSV que tenha uma noite anterior, para eliminar o artefato do fallback.

### 5.6 Ponto a investigar na implementação da Opção A: atraso da detecção confirmada da TAG29 (preset 6)

**O fato:** o primeiro alarme continua às 15:32, mas a **detecção confirmada** da TAG29 passou de **15:34** (`rec`) para **15:37** (`comb`/`combs`). O atraso vem do intervalo de 15:33–15:34 sem alarme (§5.4, item 5). Em detecção confirmada, o regime combinado **piorou** o resultado em 3 minutos. O item 5 de §5.4 só vale para o primeiro alarme.

**O que já se vê nas saídas da etapa 0** (`dados/e0_preset6_{rec,comb,combs}.csv`, TAG29; confirmado no replay, ainda sem explicação):

| Minuto | Valor | `rec`: nota final / decisão | `comb` | `combs` |
|---|---|---|---|---|
| 15:32 | 253 | 68,5 / 1 | 65,0 / 1 | 64,3 / 1 |
| 15:33 | 256 | 60,9 / 1 | 56,0 / 0 | 54,8 / 0 |
| 15:34 | 249 | 64,4 / 1 | 55,1 / 0 | 53,5 / 0 |
| 15:35 | 268 | 73,4 / 1 | 62,1 / 1 | 61,0 / 1 |

Limiar = 60.
- Em `rec`, o baseline é `Global` (1.434 pontos, sem regime para a TAG29).
- Em `comb`/`combs`, o baseline é `OperationalRegime` com `valor_regime = 1` (B-01 sozinha) em todos esses minutos: 494–495 pontos em `comb` e 434–435 em `combs`. Não houve troca de regime nem fallback no intervalo.
- Nos três casos, a "Taxa de variação" cai de ~79–89 em 15:32 para ~6–7 em 15:33. Em `comb`/`combs`, Z-Score e EWMA já estão saturados em ~100 e não compensam a queda. Em `rec`, a nota fica pouco acima do limiar (60,9).

**A investigar:**
1. Confirmar, no runtime da Opção A (não só no replay com séries derivadas), qual chave de regime e qual baseline estavam ativos às 15:33 e às 15:34. Conferir média, desvio, pontos e `origem_baseline`.
2. Explicar por que a nota final ficou abaixo de 60 com Z-Score e EWMA em 100. Abrir a contribuição de cada detector (Mediana e MAD, Taxa de variação, Persistência, Mudança de patamar) e comparar com `rec`. Hipótese a validar: o baseline do regime tem desvio e MAD menores, o que satura os detectores de nível. Assim, a nota passa a depender dos detectores de dinâmica, que caem quando o valor estabiliza.
3. Verificar se a Persistência e a Mudança de patamar deveriam ter subido num desvio sustentado de ~+85 sobre a média do regime (~170).
4. **Critério de aceite da Etapa 1:** a detecção confirmada da TAG29 no preset 6 não pode ficar depois de 15:34. Se ficar, documentar a causa antes de seguir. **Não** ajustar limiar, pesos ou gates sem aprovação.

---

## 6. Plano de implementação (Opção A com três componentes)

**Data:** 12/11/2026. **Situação:** plano; **nada implementado e nada aprovado.** Cada tarefa exige aprovação explícita antes de começar (`CLAUDE.md`).
**Escopo:** o regime de cada tag de bomba passa a ser uma tupla de **3 componentes** (estado da própria bomba, estado da outra bomba e TAG46), com mapa de estados opcional por componente (§5.5). Entram também a busca do resolver com dois ponteiros e a regressão byte a byte com a chave desligada.

**Pontos conferidos nesta sessão (confirmado no código):**
- `OperationalRegimeHistoryResolver.Resolve` chama `FindCausalRegimePoint` uma vez por ponto da tag analisada, e cada chamada percorre a série de regime desde o índice 0.
- `OperationalRegimeBaselineProvider.Resolve` lê **uma** linha de `TagsRegimesOperacionaisIa` e faz **uma** consulta `BuscarHistoricoPorPeriodo`. Ainda **não** recusa TAG60–TAG81 (o item 11 do backlog não foi implementado).
- `AddOperationalRegimeServices` registra resolver, service e provider como `Scoped`, sem opções próprias. As opções de detecção vêm da seção `AnomalyDetection`.
- **Não há migration automática** no startup da API (`Database.Migrate` não é chamado em nenhum lugar). O replay usa `EnsureCreatedAsync` no SQLite em memória, então uma tabela nova aparece lá sem migration.
- O replay grava as colunas fixas de `Cp300Replay.Colunas`, que terminam em `valor_regime` e `motivo_baseline`.
- `ReplayDiagnosticJsonlExporter` serializa com `JsonIgnoreCondition.Never` (um campo novo nulo aparece no JSON).

### 6.1 A chave

- **Nome proposto:** `AnomalyDetection:RegimeCombinado:Habilitado`, padrão **`false`**, numa classe de opções nova. Não muda `AnomalyDetectionOptions` nem o `appsettings.json`: o padrão vem do código.
- **Com a chave desligada:**
  - o provider **não lê** a tabela de componentes e **ignora** `MapaEstados`;
  - o resolver segue pelo caminho atual (um componente, tolerância numérica);
  - `RegimeKey` fica nulo e **não aparece** em JSON, log nem CSV;
  - a ferramenta de replay recusa `+` e mapa, e não grava a coluna `chave_regime`.
  - **Exigência:** saídas do replay idênticas byte a byte às atuais (tarefa RC-2).
- **Com a chave ligada**, só usa regime combinado a tag que tiver componentes cadastrados; as demais seguem iguais.
- **Rollback:** desligar a chave (volta ao comportamento atual sem tocar no banco) ou `Ativo = false` por tag (volta ao baseline global daquela tag).

### 6.2 Decisões de projeto (fechar na RC-4)

| Decisão | Opções | Recomendação |
|---|---|---|
| Onde fica o componente 1 | (a) `TagRegime` atual + coluna nova `MapaEstados` (nula) na tabela atual; a tabela filha guarda só os componentes 2..N. (b) Tabela filha com os componentes 1..N, e `TagRegime` tem de bater com o componente 1 | **(a)**: a linha atual continua válida sozinha, sem duplicar o componente 1 |
| Tupla pura × "parada domina" | Tupla pura: (0,0,·) e (0,2,·) são classes diferentes. Com dominância, "própria parada" ignora os outros componentes, como o `RB01` da etapa 0 | **Tupla pura** na primeira versão. Nos dados de 10–11/11 a B-01 parada sempre teve a B-02 em 2 (§1.2), então deve dar o mesmo resultado; **conferir na RC-0** |
| Valor fora do mapa, sem `*` | (a) passa sem mudar; (b) ponto descartado | **(a)**, e o relatório de cobertura do replay lista esses valores |
| Comparação com mapa | Mapear e comparar exato × comparar com `ToleranciaEstado` | Mapear (a entrada do mapa casa com `ToleranciaEstado`) e comparar exato |
| Lag e tolerância de alinhamento | Por componente × da configuração | **Da configuração** (os mesmos para os 3 componentes); por componente só se a RC-0 mostrar necessidade |
| Formato da chave | `TAG42=2\|TAG43=0\|TAG46=380`: valores **mapeados**, `InvariantCulture`, na ordem dos componentes | Esse; é a futura chave dos perfis por regime (PROP §4-A) |

**Configuração alvo das bombas** (cadastrada só na RC-12):

| TagName | Componente 1 (mapa) | Componente 2 (mapa) | Componente 3 (mapa) |
|---|---|---|---|
| TAG27, TAG29, TAG31 | TAG42 (`2→2; *→0`) | TAG43 (`2→2; *→0`) | TAG46 (`380→380; *→450`) |
| TAG28, TAG30, TAG32 | TAG43 (`2→2; *→0`) | TAG42 (`2→2; *→0`) | TAG46 (`380→380; *→450`) |

### 6.3 Tarefas, em ordem

Coluna "muda comportamento":
- **não**: só documento, análise, teste ou ferramenta offline;
- **não (provado)**: código de runtime que tem de dar resultado idêntico, garantido por teste de equivalência e pela regressão da RC-2;
- **só com a chave**: código novo que só age com `RegimeCombinado:Habilitado = true` (padrão `false`);
- **sim**: ativação, só na última tarefa.

| # | Tarefa | Depende de | Muda comportamento |
|---|---|---|---|
| RC-0 | CSV de 48 h e equivalência da tupla (dados) | — | não |
| RC-1 | Análise do atraso da TAG29 com as saídas da etapa 0 | — | não |
| RC-2 | Referência congelada e regressão byte a byte | — | não |
| RC-3 | Resolver com dois ponteiros | RC-2 | não (provado) |
| RC-4 | Chave, mapa de estados e chave de regime (classes isoladas) | RC-0 | não |
| RC-5 | Modelo `TagRegimeComponenteIa` + migration | RC-4 | não |
| RC-6 | Resolver com N componentes | RC-3, RC-4 | não (provado) |
| RC-7 | Bloqueio de rótulos por cliente (backlog 11) | — | não (lista vazia por padrão) |
| RC-8 | Service e provider com componentes, atrás da chave | RC-5, RC-6, RC-7 | só com a chave |
| RC-9 | `RegimeKey` no diagnóstico e no log | RC-8 | só com a chave (observabilidade) |
| RC-10 | Replay com regime combinado | RC-8, RC-9 | não (ferramenta) |
| RC-11 | Equivalência com a etapa 0, critério da TAG29 e custo | RC-0, RC-1, RC-10 | não |
| RC-12 | Ativação no ambiente de teste do PK2 | RC-11 + aprovação | **sim** |

RC-0, RC-1, RC-2 e RC-7 são independentes entre si. RC-3 pode começar logo depois da RC-2.

#### RC-0. CSV de 48 h e equivalência da tupla (dados, sem código)
- **Objetivo:**
  1. Exportar um CSV com **uma noite anterior** (≥ 48 h, cobrindo os presets 1 e 6) e repetir a etapa 0 (`rec` × `combs`), para eliminar o artefato do fallback da primeira noite (§5.4 item 3, §5.5).
  2. No mesmo script fora do repositório, gerar a série da **tupla pura** (`10·map(TAG42) + map(TAG43) + 100·(TAG46 = 380)`, e o espelho para a B-02) e comparar minuto a minuto com `RB01S`/`RB02S`. Isso fecha a decisão "tupla pura × parada domina" (§6.2).
- **Arquivos:** nenhum do repositório. Exportação com `tools/Cp300Export/Export-Cp300Historico.ps1`, feita pela equipe **com autorização** (acessa o Bridge). Script no scratchpad, saídas em `dados/`. Resultado registrado como §5.7 deste documento.
- **Aceite:**
  - B-01 de madrugada em `combs` ≤ 30 minutos-tag, **sem** `origem_baseline = Global` por falta de histórico noturno;
  - a tupla pura classifica igual a `RB0xS` em todos os minutos, ou existe a lista dos minutos diferentes e a decisão tomada.
- **Muda comportamento:** não.

#### RC-1. Análise do atraso da detecção confirmada da TAG29 (sem código)
- **Objetivo:** responder os itens 2 e 3 de §5.6 **antes** de mexer no código, com as saídas que já existem (`dados/e0_preset6_{rec,comb,combs}.csv`). O replay já grava, por detector, `nota_detector`, `peso`, `contribuicao`, `media`, `desvio` e `pontos_historico`.
  - Abrir a contribuição de cada detector às 15:32–15:35 nos três casos.
  - Verificar por que Persistência e Mudança de patamar não sobem num desvio sustentado de ~+85.
  - Marcar a hipótese "o baseline do regime, mais estreito, satura os detectores de nível, e a nota passa a depender dos detectores de dinâmica" como confirmada ou descartada.
- **Arquivos:** só leitura dos CSVs e dos detectores em `src/IAApi.Engine/Detection/`. Resultado no fim de §5.6.
- **Aceite:** tabela por detector e minuto (nota, peso, contribuição) para `rec` × `combs`, com a conclusão marcada como **confirmado no replay** ou **hipótese**. Nenhum limiar, peso ou gate alterado.
- **Muda comportamento:** não.

#### RC-2. Referência congelada e regressão byte a byte
- **Objetivo:** ter, antes de qualquer mudança de runtime, uma forma repetível de provar "idêntico ao atual".
  1. **Referência com dados do simulador (fora do Git):** com o código atual, rodar os replays `sem` e `rec` do preset 1, da madrugada e do preset 6 (comandos de §5.2), mais os da RC-0. Guardar num manifesto os comandos e o SHA-256 de cada `.csv`.
  2. **Script de comparação** (novo, `tools/Cp300OfflineReplay/Compare-Replay.ps1`): roda os mesmos comandos e compara os `.csv` **byte a byte** (hash) e os `.log` linha a linha, ignorando só linhas de tempo de execução, se existirem (verificar).
  3. **Teste automatizado, sem dados industriais:** em `IAApi.Tests/Cp300Replay/Cp300OfflineReplayTests.cs`, um teste golden que roda o replay sobre um CSV sintético pequeno (bomba, outra bomba e set-point) **com uma configuração `--regime` simples** e compara a saída com um arquivo de referência versionado.
- **Arquivos:** `tools/Cp300OfflineReplay/Compare-Replay.ps1` (novo), `IAApi.Tests/Cp300Replay/Cp300OfflineReplayTests.cs`, `IAApi.Tests/Cp300Replay/Golden/` (novo). O manifesto e as saídas reais ficam em `dados/` (não versionado).
- **Aceite:** o script rodado duas vezes no código atual dá 0 diferenças; o teste golden passa; a suíte completa continua verde.
- **Muda comportamento:** não.

#### RC-3. Resolver com dois ponteiros
- **Objetivo:** dentro do laço, trocar a busca linear de `FindCausalRegimePoint` por um ponteiro que só avança. O alinhamento cai de ≈ n²/2 para O(n + m) comparações, **com o mesmo resultado**.
  - A lista da tag analisada já está ordenada e o `LagMinutos` é fixo, então os instantes esperados são crescentes e o ponteiro da série de regime nunca volta.
  - Manter exatamente a regra atual: o candidato é o **último** ponto com `DataHora ≤ esperado` (com timestamps repetidos, o último na ordem do `OrderBy`, que é estável), seguido dos mesmos testes de idade (`< 0` e `> tolerância`).
  - O ponto atual (`currentTimestamp − Lag`) continua com busca própria (linear, ou binária com a mesma regra), porque pode estar fora da ordem do laço.
- **Arquivos:** `Services/AnomalyDetection/OperationalRegimeHistoryResolver.cs`; `IAApi.Tests/Services/AnomalyDetection/OperationalRegimeHistoryResolverTests.cs`.
- **Aceite:**
  - teste de equivalência com a implementação linear atual, copiada para o teste como oráculo. Séries aleatórias com semente fixa, timestamps repetidos, buracos maiores que a tolerância, tolerância 0, lag > 0, entrada fora de ordem e pontos inválidos. Mesmos `Applied`, `Reason` e contagens, e **mesma lista** de pontos selecionados;
  - RC-2 com 0 diferenças;
  - custo medido antes/depois: tempo do replay do preset 6 e, numa sessão autorizada, `PERF_DIAG` da etapa `Preparation.RegimeBaseline`. Só registrar o número; a meta é "não piora".
- **Muda comportamento:** não (provado pelos aceites acima). Por isso fica sem chave. Se a equivalência falhar, a tarefa não é entregue.

#### RC-4. Chave, mapa de estados e chave de regime (classes isoladas)
- **Objetivo:**
  1. Fechar as decisões de §6.2 com os resultados da RC-0.
  2. Criar `RegimeCombinadoOptions` (`Habilitado = false`) e registrá-la em `AddAnomalyDetectionConfiguration` a partir de `AnomalyDetection:RegimeCombinado`, **sem** mexer no `appsettings.json`.
  3. Criar `OperationalRegimeStateMap`: parse e aplicação do mapa, em sintaxe ASCII (`2:2;*:0`), com erro para sintaxe inválida, valores não finitos e entradas repetidas.
  4. Criar `OperationalRegimeKey`: monta `TAG42=2|TAG43=0|TAG46=380` com `InvariantCulture`.
- **Arquivos:** novos `Services/AnomalyDetection/RegimeCombinadoOptions.cs`, `OperationalRegimeStateMap.cs` e `OperationalRegimeKey.cs`; `Services/AnomalyDetection/DependencyInjection/AnomalyDetectionServiceCollectionExtensions.cs`; testes novos em `IAApi.Tests/Services/AnomalyDetection/`.
- **Aceite:** testes do parse (válido, inválido, `*`, tolerância), da chave (cultura pt-BR × invariante, ordem) e das opções (padrão `false`, leitura da seção). Nada chama essas classes ainda; RC-2 com 0 diferenças.
- **Muda comportamento:** não.

#### RC-5. Modelo e migration
- **Objetivo:** entidade `TagRegimeComponenteIa` (`Id`, `ConfiguracaoId`, `Ordem` ≥ 2, `TagRegime`, `MapaEstados` nulo, `ToleranciaEstado`), relação com `TagRegimeOperacionalIa`, índice único (`ConfiguracaoId`, `Ordem`) e, se a opção (a) de §6.2 for a escolhida, coluna nula `MapaEstados` na tabela atual. O índice único atual (`ClienteId`, `TagName`) não muda.
- **Arquivos:** `Models/TagRegimeOperationalIa.cs` (ou um arquivo novo para a entidade filha), `Data/AppDbContext.cs : OnModelCreating`, migration nova em `Migrations/` e `AppDbContextModelSnapshot.cs`.
- **Aceite:**
  - migration aplicada (`Up`) e revertida (`Down`) num SQLite vazio de teste;
  - testes atuais do provider, service e resolver passam sem mudança;
  - RC-2 com 0 diferenças (o replay cria a tabela vazia via `EnsureCreated`);
  - **não aplicar** no banco do PK2 (isso é a RC-12).
- **Muda comportamento:** não (tabela vazia, nada lê).

#### RC-6. Resolver com N componentes
- **Objetivo:** nova sobrecarga `Resolve(configuração, componentes, histórico alvo, séries de regime, instante atual)`:
  - um ponteiro por componente (técnica da RC-3);
  - estado do ponto = tupla de valores **mapeados**; componente sem leitura na tolerância ⇒ ponto descartado (não conta como alinhado);
  - estado atual: todos os componentes precisam existir, senão "não aplicado" com motivo;
  - `OperationalRegimeHistoryResolution` ganha `CurrentRegimeKey` (nulo no caminho atual); `CurrentRegimeValue` continua sendo o componente 1;
  - **a assinatura atual não muda.** Ela só pode delegar à sobrecarga (com 1 componente sem mapa) se o teste de equivalência provar resultado igual.
- **Arquivos:** `Services/AnomalyDetection/OperationalRegimeHistoryResolver.cs`; `OperationalRegimeHistoryResolverTests.cs`.
- **Aceite (unitários de §3.2):**
  - tupla com 2 e com 3 componentes; mapa de estados;
  - componente sem leitura na tolerância ⇒ ponto descartado;
  - menos que `MinimoAmostrasPorRegime` ⇒ não aplicado;
  - 1 componente sem mapa pela sobrecarga = caminho atual (mesmo oráculo da RC-3);
  - num trecho sintético com a lógica das bombas, a tupla pura reproduz as classes esperadas;
  - RC-2 com 0 diferenças.
- **Muda comportamento:** não (provado). A sobrecarga só é chamada a partir da RC-8.

#### RC-7. Bloqueio de rótulos por cliente (backlog 11)
- **Objetivo:** implementar o item 11 do backlog já pensando em vários componentes: lista configurável, **por cliente**, de tags proibidas como regime, **vazia por padrão**. O provider recusa ("não aplicado", com motivo claro) uma `TagRegime` da lista. A mesma função vale para os componentes na RC-8.
- **Arquivos:** `Services/AnomalyDetection/OperationalRegimeBaselineProvider.cs`; local da lista por cliente a definir na tarefa (seção de configuração ou tabela de configuração do cliente já existente); `OperationalRegimeBaselineProviderTests.cs`.
- **Aceite:** com a lista TAG60–TAG81 e `TagRegime = TAG73`, não aplica e registra o motivo; com tag válida ou lista vazia, resultado idêntico ao atual; RC-2 com 0 diferenças.
- **Muda comportamento:** não com a lista vazia (padrão). A lista do PK2 só é cadastrada na RC-12.

#### RC-8. Service e provider com componentes, atrás da chave
- **Objetivo:**
  - `OperationalRegimeBaselineProvider.Resolve`: com `RegimeCombinado:Habilitado = false`, **nenhuma** linha nova é lida e o caminho atual fica intacto. Com `true` e componentes cadastrados: carregar os componentes em ordem, recusar componentes da lista da RC-7, fazer uma consulta `BuscarHistoricoPorPeriodo` por componente com a mesma janela; falha em qualquer consulta ⇒ "não aplicado" (fallback global, como hoje).
  - `OperationalRegimeBaselineService.Resolve`: sobrecarga com a lista de séries; a assinatura atual não muda.
  - `OperationalRegimeBaselineResolution` ganha `CurrentRegimeKey`.
- **Arquivos:** `OperationalRegimeBaselineProvider.cs`, `OperationalRegimeBaselineService.cs`; `OperationalRegimeBaselineProviderTests.cs`, `OperationalRegimeBaselineServiceTests.cs`.
- **Aceite:**
  - chave desligada **com** componentes cadastrados = resultado idêntico a sem componentes, inclusive sem consulta extra ao histórico (conferir com um `IHistoricoService` falso que conta as chamadas);
  - chave ligada: 3 consultas, tupla correta, componente proibido recusado, falha de consulta ⇒ fallback;
  - RC-2 com 0 diferenças.
- **Muda comportamento:** **só com a chave** ligada **e** componentes cadastrados.

#### RC-9. `RegimeKey` no diagnóstico e no log
- **Objetivo:** campo aditivo `RegimeKey` em `BaselineDiagnostic` (`src/IAApi.Engine/Observability/DiagnosticContracts.cs`), preenchido em `PredictionDetectionPreparationService.PrepararAsync` / `CriarBaselineEfetivo`, e a chave na linha de log do regime aplicado. É só instrumentação: não entra em nenhuma decisão.
- **Cuidado com contrato e regressão:** `ReplayDiagnosticJsonlExporter` usa `JsonIgnoreCondition.Never`. Onde o diagnóstico for serializado (exportador, endpoint de diagnóstico), a propriedade nova precisa de `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` para o JSON ficar igual com a chave desligada. Levantar todos os pontos de serialização na tarefa.
- **Arquivos:** `DiagnosticContracts.cs`, `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs`; testes do diagnóstico e da preparação.
- **Aceite:** com a chave desligada, JSON do diagnóstico e log idênticos aos atuais (teste); com a chave ligada, `RegimeKey` presente; RC-2 com 0 diferenças.
- **Muda comportamento:** não na decisão. A saída de diagnóstico só muda com a chave ligada.

#### RC-10. Replay com regime combinado
- **Objetivo:** em `tools/Cp300OfflineReplay`:
  - opção `--regime-combinado`, que liga a chave no replay; sem ela, `+` e `--regime-mapa` são recusados com erro;
  - sintaxe `--regime TAG27=TAG42+TAG43+TAG46` e `--regime-mapa "TAG42=2:2;*:0"` (o mapa vale para aquela tag de regime em todas as configurações);
  - `ValidateRegimes`: recusar TAG60–TAG81 em **qualquer** componente; componente que não existe no CSV ⇒ erro;
  - cadastrar os componentes no SQLite em memória;
  - coluna `chave_regime` **só** com `--regime-combinado` (sem ela, `Colunas` não muda e a saída é byte a byte a atual);
  - no `.log`, cobertura por chave na janela de treino (pontos por classe e valores fora do mapa).
- **Arquivos:** `tools/Cp300OfflineReplay/Cp300Replay.cs` (`ParseRegimes`, `ValidateRegimes`, `Colunas`, montagem do banco), `Program.cs` (uso), `README.md`; `IAApi.Tests/Cp300Replay/Cp300OfflineReplayTests.cs`.
- **Aceite:** testes de parse (válido, TAG73 em qualquer posição, `+` sem `--regime-combinado`); teste golden da RC-2 inalterado; um teste golden novo com `--regime-combinado` sobre o CSV sintético.
- **Muda comportamento:** não (ferramenta offline).

#### RC-11. Equivalência com a etapa 0, critério da TAG29 e custo
- **Objetivo:** rodar a Opção A real no replay e conferir contra a etapa 0.
  1. **Regressão:** RC-2 completa com o código final e a chave desligada: 0 diferenças, byte a byte.
  2. **Equivalência:** preset 1, madrugada e preset 6 (e o CSV de 48 h da RC-0) com `--regime-combinado` e a configuração de §6.2, comparados com `e0_*_combs`. Todas as colunas iguais, exceto `valor_regime` (a etapa 0 grava o código `RB0xS`; a Opção A grava o componente 1), `motivo_baseline` e a coluna nova `chave_regime`. Qualquer diferença tem de estar só nos minutos listados na RC-0.
  3. **Metas de §3.1** com os números da Opção A, incluindo a contagem de `origem_baseline = Global`.
  4. **TAG29 (§5.6, itens 1 e 4):** chave de regime, média, desvio, pontos e `origem_baseline` às 15:33 e 15:34. A detecção confirmada **não pode ficar depois de 15:34**. Se ficar, documentar a causa (com o resultado da RC-1) e **parar** antes da RC-12. Não ajustar limiar, pesos nem gates.
  5. **Custo:** tempo do replay do preset 6 com a chave desligada × ligada (3 componentes), já com a RC-3.
- **Arquivos:** nenhum código. Resultado como §7 deste documento; saídas em `dados/`.
- **Aceite:** itens 1 a 5, com nível de evidência **confirmado no replay** (sem IF, multivariado nem latência; não é validação em planta).
- **Muda comportamento:** não.

#### RC-12. Ativação no ambiente de teste do PK2 (última etapa; exige aprovação explícita)
- **Objetivo:**
  - aplicar a migration da RC-5 no banco do PK2, com backup antes;
  - cadastrar a lista de rótulos TAG60–TAG81 do PK2 (RC-7);
  - cadastrar os componentes e mapas de §6.2 para TAG27–TAG32;
  - ligar `AnomalyDetection:RegimeCombinado:Habilitado = true` **só** nesse ambiente.
- **Arquivos:** configuração do ambiente e dados do banco do PK2; nenhum código.
- **Aceite:**
  - num preset 1 e num preset 6 do simulador, o diagnóstico (`tools/IntelligenceLabDump/Dump-Diagnostics.ps1`) mostra `RegimeKey` e `origem_baseline = OperationalRegime` nas tags das bombas, sem erros;
  - alarmes das bombas na mesma ordem de grandeza do replay (RC-11);
  - registrar como **validação no simulador**, não em planta real.
- **Rollback:** chave em `false` (imediato, sem tocar no banco); depois `Ativo = false` por tag; `Down` da migration só se necessário.
- **Muda comportamento:** **sim**. É a única tarefa que muda a detecção.

### 6.4 Fora deste plano
- Referência treinada por período e por regime (PROP §4): vai usar a chave da RC-4, mas é outra entrega.
- TAG41 como classe "estação em falha/emergência" (§2.4).
- Correções da TAG26, TAG24 e TAG22 (§5.4, item 7) e o item 13 do backlog.
- O sinal de 13:37–13:38 no preset 1 (§5.4, item 6): só repetir a observação na RC-11; não conta como detecção.
