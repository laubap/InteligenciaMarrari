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
5. **A detecção não piora nem melhora:** TAG29 às 15:32, como antes. Em `comb`/`combs` há um intervalo em 15:33–15:34 sem alarme (84 contra 86 minutos de falha), e o alarme volta contínuo de 15:35 a 16:51.
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
