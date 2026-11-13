# Inventário do banco do PK2 (BACKLOG, tarefa 2)

**Data:** 13/11/2026. **Cliente:** PK2. **Branch:** `feat/intelligence-lab-e2e-validation` (HEAD `f810711`).
**Fonte:** cópia do `ia_config.db` local da IAApi, feita em 13/11 às 15:03 com a IAApi parada. Só leitura; nada foi alterado no banco nem no código.

**Como ler o status:** **confirmado no código** (lido no código-fonte); **confirmado nos dados** (consulta à cópia do banco ou aos CSVs exportados); **reproduzido** (as classes de produção rodadas offline deram o mesmo número que a IA real gravou); **hipótese** (inferência ainda não demonstrada).

---

## Resumo

1. **A diferença "2,1 contra 20–35" é, em grande parte, de escala** (confirmado no código e nos dados). O campo `Score` que a IA real grava, e que a exportação usa como `nota_final`, é a **nota do ensemble dividida por 20** (`EnsembleDetectionService.Detectar`). O replay grava a nota de 0 a 100. A nota real máxima de 2,1 equivale a **42**, o mesmo máximo do replay na TAG22 (42,1).
2. **38 das 50 decisões reais (76 %) não foram avaliações** (confirmado no código; reproduzido). Os valores atuais são buscados uma vez, em lote, no início do ciclo. O histórico de 24 h de cada tag é buscado depois, quando chega a vez dela. Quando o histórico já tem um ponto mais novo que a leitura do lote, a validação marca a entrada como inválida, e a IA grava **nota 0 e "sem anomalia"**, sem aviso (P19). Somando com a cadência de ~10 min por tag (P26), a IA real avaliou de fato **12 dos ~576 minutos-tag** do período (≈ 2 %).
3. **O perfil de produção do PK2 é de 30/10 e foi treinado com 161 a 232 pontos** (confirmado nos dados). Com 1 leitura por minuto, isso dá ~2,7–3,9 h, embora a janela configurada seja de 24 h. Foi treinado com a B-02 quase sempre parada: TAG28 e TAG32 têm média ~0 e deadband de 2 e 0,04 (**perfil colapsado**). Nunca foi retreinado: só existe treino manual.
4. **Não há rótulo ativo, regime, contexto de tag, comportamento aprendido nem modelo IF para o PK2** (confirmado nos dados). Pesos e limiares são os globais do `appsettings.json`. A decisão do PK2 é o ensemble legado, porque o runtime e a fusão multivariada só valem para o PK7. O grupo multivariado do PK2 roda só em *shadow*.
5. **Replay com o perfil real do banco (feito):** as 50 decisões reais foram **reproduzidas com diferença < 0,1 na escala 0–100**, usando as classes de produção, o perfil do banco e o histórico até a hora do processamento. Avaliando as mesmas leituras no próprio minuto, o perfil do banco dá nota média de **32** (máx. 53, 0 alarmes), contra **35** (máx. 66, 7 alarmes) do replay `p3_sem`. A diferença de perfil pesa sobretudo nas tags da B-02 (TAG28/30/32).

**Consequência principal:** em 12/11, a IA real estava, na prática, **sem detecção na maior parte do tempo**. Não por limiar ou peso, mas porque a maioria das leituras era descartada como inválida antes dos detectores. O fato de não ter alarmado não diz nada sobre alarme falso nem sobre sensibilidade.

---

## 1. Tags com IA ativa e tipo configurado

Fonte: `TagsIaConfig` (12 linhas do PK2, mais 1 do PK7) e `TagsContextoIa` (0 linhas).

| Tag | `IaAtiva` | Configurada em | Tipo (`TagsContextoIa.TipoRepresentacao`) |
|---|---|---|---|
| PK2.TAG22 … PK2.TAG33 (12 tags, todas) | 1 | 30/10 17:08 (todas no mesmo instante) | **sem cadastro**: vale o padrão `"perfil-comportamental"` |

- A tabela `TagsIaConfig` **não tem coluna de tipo**. O tipo vem de `TagsContextoIa`, que está vazia; o código usa `contexto?.TipoRepresentacao ?? "perfil-comportamental"` (`Services/PredictionService.cs:176-178`). A classificação real (contínua/discreta) fica com o `TagBehaviorProfileResolver`, igual ao replay.
- **Nenhum rótulo (TAG60–TAG81) ativo**, nem TAG34–TAG51 (estados): o monitor avalia só TAG22–TAG33.
- Todas as decisões do período foram gravadas com `TipoTag = perfil-comportamental`.

## 2. Perfis treinados (`PerfisIa`)

Uma linha por tag. Nenhuma tag tem mais de um perfil.

| Tag | Treino (30/10) | Pontos recebidos → usados (outliers) | % zeros | Média | Desvio | Mín–Máx | Deadband (confiança) | Suspeito? Motivo |
|---|---|---|---|---|---|---|---|---|
| TAG22 | 14:10 | 161 → 130 (31) | 0 | 605,3 | 121,9 | 73–798 | 25 (75) | **sim**: ~2,7 h de dados; desvio 12 % maior que o do treino limpo (109) |
| TAG23 | 15:22 | 230 → 202 (28) | 0 | 1346,8 | 71,3 | 1166–1474 | 21 (75) | sim: ~3,8 h de dados |
| TAG24 | 15:22 | 231 → 185 (46) | 0 | 426,8 | 32,8 | 375–457 | 10 (75) | **sim**: desvio 12× o do treino limpo (2,6); média 23 abaixo |
| TAG25 | 15:22 | 231 → 231 (0) | 16 | 422,3 | 273,1 | 0–832 | 74,5 (85) | sim: mistura vazão com e sem bomba (desvio inflado) |
| TAG26 | 15:22 | 231 → 231 (0) | 30 | 470,7 | 418,0 | 0–946 | 39 (85) | sim: mistura válvula aberta e fechada |
| TAG27 | 15:22 | 231 → 185 (46) | 0 | 189,4 | 40,5 | 139–281 | 23,8 (75) | sim: só "B-01 ligada" (o IQR tirou 20 %) |
| TAG28 | 15:23 | 231 → 176 (55) | **77** | **0,35** | **0,72** | 0–3 | **2** (58) | **sim: perfil colapsado** (B-02 parada) |
| TAG29 | 15:23 | 232 → 186 (46) | 0 | 161,1 | 31,2 | 116–241 | 31,6 (75) | sim: só "B-01 ligada" |
| TAG30 | 15:23 | 232 → 177 (55) | 16 | 24,2 | 12,6 | 0–48 | 18,3 (65) | sim: B-02 parada |
| TAG31 | 15:23 | 232 → 186 (46) | 0 | 398,7 | 45,8 | 336–487 | 23,4 (75) | sim: só "B-01 ligada" |
| TAG32 | 15:23 | 232 → 177 (55) | **97** | **0,03** | **0,17** | 0–1 | **0,04** (25) | **sim: perfil colapsado** (B-02 parada) |
| TAG33 | 15:23 | 232 → 232 (0) | 12 | 75,7 | 50,9 | 0–154 | 18 (85) | sim: ~3,9 h de dados |

- **Período usado:** o banco não guarda o período; só a data do treino e a contagem. O treino usa as últimas `Bridge:HorasHistorico` = 24 h antes da chamada (`PerfilTagService.TreinarPerfil`, P13). Com 161–232 pontos recebidos, o Bridge tinha **~2,7–3,9 h** de histórico naquele momento (se a cadência era de 1/min) [inferência].
- **Retreino:** o perfil só é treinado por `POST` em `PerfilTagController` (confirmado no código: nenhum serviço de fundo chama `TreinarPerfil`). Todas as decisões de 30/10 a 13/11 usaram estes perfis.
- **Como a produção usa o perfil** (confirmado no código, `PredictionDetectionPreparationService.PrepararAsync`): sem regime e sem comportamento aplicados, média, desvio, deadband e confiança vêm do `PerfisIa`. Pelo P25, Z-Score e EWMA usam o perfil, enquanto MAD, Persistência e Taxa de variação usam a mediana das 24 h móveis.
- `VariacaoMinimaCalculadaAutomaticamente = 1` em todas: o deadband é o sugerido pelo treino, não um valor aprovado pelo cliente.

## 3. Regimes e outras configurações que mudam pesos, limiares ou detectores

| Onde | O que há para o PK2 | Efeito na decisão |
|---|---|---|
| `TagsRegimesOperacionaisIa` | **0 linhas** (para nenhum cliente) | Baseline sempre global (perfil). Nenhum rótulo usado como `TagRegime` (P14 descartado no PK2) |
| `TagsContextoIa` | 0 linhas | Tipo padrão; sem deadband aprovado pelo cliente |
| `TagsComportamentosIa` (+ ocorrências, snapshots) | 0 do PK2 (2 do PK7) | Baseline comportamental nunca se aplica no PK2 |
| `ModelosTagsIaStatus` | 0 linhas | **Isolation Forest não aplicável**: sem snapshot, o detector devolve "não aplicável" (`IsolationForestDetector`). No disco só há `ModelsML/Multivariate/PK2` |
| `ConfiguracoesIA`, `JanelasTreinamentoTagsIa`, `TagDependenciasIa`, `EquipamentosIa`/`EquipamentoTagsIa`, `ProcessosIa`/`ProcessoTagsIa`, `MotivosAnomaliaIA`, `FeedbackAnomaliasIa` | 0 linhas | Nenhum |
| `GruposAnaliseIa` + `GrupoAnaliseTagsIa` | 1 grupo ativo, `CP300-B01-Piloto` (Mahalanobis; TAG27, TAG29, TAG31; `PesoEnsemble` 1,0; `Sensibilidade` 0,5) | Só *shadow*: a fusão e a promoção multivariadas não valem para o PK2 (linha abaixo) |
| `appsettings.json` → `AnomalyDetection` | Pesos globais Z/MAD/EWMA 0,15, RoC/Persist./Patamar 0,10, IF 0,25; limiar 60. **Não há pesos por cliente** | Iguais aos do replay (que lê o mesmo arquivo; sem mudança no git desde 23/10) |
| `appsettings.json` → `RuntimeDecision`, `MultivariateDecisionFusion` | Ligados, mas `CanaryOnly` com `AllowedClientIds = ["PK7"]` | PK2 fica no legado: as 50 decisões têm `RuntimeDecisionSource = LegacyCanaryExcluded` e motivo `ClientNotEnabledForRuntimeDecision` (confirmado nos dados) |
| `appsettings.json` → `MultivariateOperationalPromotion` | `Enabled: false` | Nenhum |

### 3.1 Hipóteses do BACKLOG 2

| Hipótese | Resultado |
|---|---|
| Rótulo ativo ou usado como `TagRegime` (vazamento do gabarito) | **Descartada** no banco atual: só TAG22–33 ativas; nenhum regime cadastrado |
| Tipo explícito que bloqueie detectores | **Descartada**: `TagsContextoIa` vazia |
| Perfil colapsado (média 0 / desvio 0) nas tags de bomba | **Confirmada em parte**: TAG28 (0,35 ± 0,72) e TAG32 (0,03 ± 0,17). TAG30 não (24 ± 13) |
| Perfil treinado com ~2 h de dados | **Confirmada** (~2,7–3,9 h, em 30/10) |

---

## 4. Replay × IA real: por que 2,1 contra 20–35

### 4.1 Diferenças de configuração

| Item | Replay `p3_sem` (RES3 §2.1) | IA real em 12/11 |
|---|---|---|
| Perfil | Treinado pelo replay em [11/11 18:00, 12/11 14:15), 1.215 pontos, `PerfilTagService` real | `PerfisIa` de 30/10, 161–232 pontos |
| Leitura avaliada | Cada minuto do CSV, no próprio minuto | Valor do **lote do início do ciclo**, avaliado 0,2–17,2 min depois (P18), ~1 vez a cada 10 min por tag (P26) |
| Histórico de 24 h | Termina **antes** da leitura | Termina **na hora da busca**, que pode ser depois da leitura |
| Pesos, limiar, detectores, IF, tipo, regime | `appsettings.json`; IF desligado; sem contexto; sem regime | Idênticos na prática (seção 3) |
| Campo comparado | `nota_final` = `ScoreNormalizado` (0–100; `Cp300Replay.cs:680`) | `nota_final` da exportação = `score` da API = `AnomaliaDetectada.Score` = **nota ÷ 20** |

### 4.2 As três causas

**(a) Escala: o valor gravado é a nota dividida por 20.** Confirmado no código e nos dados.
- Código: `Services/AnomalyDetection/EnsembleDetectionService.cs:406-410`, `Score = scoreFinalNormalizado / 20.0` e `ScoreNormalizado = scoreFinalNormalizado`. `PredictionService` passa `resultadoDetector.Score` para `AnomaliaHistoricoService.Registrar`.
- Exportação: `tools/Cp300Export/Export-AnomaliasIaReal.ps1:227` grava `score` na coluna `nota_final`.
- Dados: as 131 anomalias do PK2 no banco (30/10–05/11) têm `Score` de 3,0 a 3,73, ou seja, notas de 60 a 75, todas acima do limiar 60.
- Nos 12 casos válidos do período, a nota real × 20 vai de 14,6 a 42,0 (média 29,1), a mesma faixa do replay.
- O **placar não é afetado**: ele usa só `decisao_final`. Mas a tabela de notas da RES3 §2.4b compara escalas diferentes.

**(b) Entrada inválida: 38 das 50 decisões não passaram pelos detectores.** Confirmado no código e reproduzido.
- **Cadeia no código:**
  - `MonitoramentoTagsService.ExecuteAsync` busca os valores atuais **em lote** no início do ciclo (linha 54).
  - `CurrentValuePredictionService.DetectarComValorAtual` envia `DataHora = valorAtual.DataHora`, a hora da leitura (linhas 83-84).
  - `PredictionDetectionPreparationService.PrepararAsync` busca o histórico de 24 h da tag **naquele momento** (`BridgeHistoricoService.BuscarHistorico`, `horas=24`).
  - `DetectionInputValidator.Validate` (linhas 78-84): se a leitura é anterior ao último ponto do histórico, dá erro "A leitura atual é anterior ao último ponto do histórico".
  - `EnsembleDetectionService.CriarResultadoEntradaInvalida` devolve nota 0, sem anomalia.
- **Nos dados:** os 12 não zeros são exatamente as decisões em que a busca do histórico ainda não tinha o ponto do minuto seguinte: o ciclo de 14:38 (idade da leitura 0,2–1,0 min) e a primeira tag do ciclo de 14:21. Com o perfil do banco, nenhuma leitura **válida** do período teria nota abaixo de 13,8. Logo, os 38 zeros só podem ser entrada inválida.
- **Fora do período** (hipótese, não reproduzida): a fração de decisões do PK2 com nota exatamente 0 saltou de 4–29 % (30/10–04/11, quando houve 131 anomalias) para 67 % em 05/11 e 76–91 % em 11–13/11, sem nenhuma anomalia desde 06/11. É compatível com o mesmo mecanismo, a partir do momento em que o ciclo ficou mais lento ou passou a usar o lote.

**(c) Perfil diferente.** Confirmado no replay com o perfil real.
- Com as leituras avaliadas no próprio minuto, o perfil do banco dá nota média de 32,2 (máx. 53,0), contra 35,2 (máx. 65,7) do replay.
- Nas tags da B-01 e do processo, as notas ficam próximas (± 10).
- Nas tags da B-02, a diferença é grande: no replay, a B-02 parada (TAG30/32 em ~0–45) fica longe da média do treino limpo, que inclui a B-02 ligada. No perfil de 30/10, que só conhece a B-02 parada, essa condição é "normal".
- Por isso o replay deu **7 alarmes** (P02) e o perfil do banco **0**.

### 4.3 Replay do trecho com o perfil real do banco

- **Como foi feito (offline):** um programa de rascunho fora do repositório (cópia em `dados/inventario_pk2/perfil_real_harness_Program.cs.txt`). Ele instancia as classes de produção usadas no detalhamento do replay: `PredictionDetectionPreparationService` sem regime e sem comportamento, como no PK2, e `EnsembleDetectionService` com os 6 detectores e o `appsettings.json`, sem IF.
- **Dados de entrada:**
  - histórico: `dados/cp300_09a13nov.csv`;
  - perfis: `PerfisIa` da cópia do banco;
  - decisões: as 50 de `AnomaliasDetectadas` (12/11 14:21–15:09), com o minuto da leitura de cada ciclo da RES3 §2.4a (14:20, 14:32, 14:38, 14:41, 14:52).
  - O valor gravado pela IA bateu com o CSV nesse minuto em 50 de 50.
- **Conferência:** o mesmo programa, treinando o perfil como o `p3_sem` fez, deu perfis idênticos aos do `dados/p3_sem.log` e notas idênticas às do `dados/p3_sem.csv` (diferença máxima de 0,0005 em 50 leituras).
- **Cenários:**
  1. replay atual;
  2. perfil do banco com a leitura avaliada no próprio minuto (histórico até a leitura);
  3. **"como a IA real"**: perfil do banco, `DataHora` = minuto da leitura e histórico até o instante da busca. Esse instante é desconhecido; testamos de 0 a 90 s antes de `DataDeteccao`.

| Tag (decisões) | IA real (nota × 20): média / máx. | Replay `p3_sem` | Perfil do banco, no minuto da leitura |
|---|---|---|---|
| TAG22 (4) | 14,8 / 42,0 | 27,4 / 42,1 | 26,4 / 41,5 |
| TAG23 (5) | 3,7 / 18,6 | 30,0 / 47,0 | 26,5 / 40,7 |
| TAG24 (5) | 2,9 / 14,6 | 7,1 / 24,1 | 18,8 / 19,3 |
| TAG25 (4) | 5,1 / 20,3 | 17,5 / 26,1 | 26,2 / 34,8 |
| TAG26 (4) | 6,5 / 26,0 | 29,1 / 35,2 | 32,9 / 39,1 |
| TAG27 (4) | 10,3 / 41,1 | 47,4 / 53,2 | 47,7 / 53,0 |
| TAG28 (3) | 11,6 / 34,7 | 46,4 / 47,8 | 23,7 / 38,6 |
| TAG29 (3) | 13,7 / 41,1 | 51,6 / 54,8 | 47,0 / 50,3 |
| TAG30 (3) | 7,1 / 21,3 | 60,5 / 65,0 | 35,2 / 35,7 |
| TAG31 (5) | 7,7 / 38,6 | 38,6 / 47,4 | 41,5 / 47,5 |
| TAG32 (5) | 6,7 / 33,3 | 63,6 / 65,7 | 38,3 / 52,9 |
| TAG33 (5) | 0,0 / 0,0 | 21,0 / 30,4 | 27,0 / 36,9 |
| **Total (50)** | **7,0 / 42,0** (38 zeros) | **35,2 / 65,7** (7 alarmes) | **32,2 / 53,0** (0 alarmes; mín. 13,9) |

- **Cenário "como a IA real":** com um corte fixo de 20 s, 49 de 50 decisões têm o mesmo padrão zero/não zero e 47 de 50 a mesma nota (diferença < 0,05). Com o melhor corte de cada decisão, entre 0 e 90 s, as **50 de 50** batem com diferença < 0,1. A maior diferença é na TAG25 às 14:38: 20,32 real contra 20,25.
- Nenhum corte fixo explica todas as 50, como esperado. O histórico é buscado antes do `DataDeteccao`, que é gravado no fim da predição, e o atraso varia de tag para tag (2 a 110 s, P26).
- Saída completa: `dados/inventario_pk2/perfil_real_50_decisoes_12nov.csv` (uma linha por decisão e cenário, com as notas por detector).

### 4.4 O que isso muda na leitura dos testes

- **"A IA real não alarmou" não é evidência de baixo alarme falso:** 76 % das decisões nem passaram pelos detectores. Com as que passaram, nenhuma nota chegou a 60 nesse trecho; o perfil do banco também não alarmaria nas leituras avaliadas no minuto.
- **A IA real, no estado de 12/11, não detectaria uma falha** a não ser que a falha estivesse presente exatamente numa das poucas leituras válidas (hipótese forte; depende do P26 e da causa (b)).
- **O perfil de 30/10 é um risco nos dois sentidos** [hipótese]:
  - com a B-02 **ligada**, TAG32 (deadband 0,04, desvio 0,17) e TAG28 ficam muito longe da média, e Z e EWMA saturam (alarme falso provável);
  - nas tags com desvio inflado (TAG25/26), uma falha precisa ser grande para pesar.
  - As 131 anomalias de 30/10–05/11 não foram analisadas.

## 5. Limitações

- **A cópia é de 13/11 15:03.** Inclui o `-wal` (4 MB, de 13/11 11:26), que tinha dados ainda não consolidados no `.db` (de 11/11). Sem ele, o inventário veria um banco de dois dias antes.
- **O rascunho não roda o `PredictionService` inteiro.** Ficaram fora: multivariado em *shadow*, runtime, pós-decisão e gravação. Para o PK2, nenhum deles muda a nota nem a decisão (seção 3; `LegacyCanaryExcluded` nos 50 registros). A reprodução 50/50 confirma isso para este trecho.
- **O histórico vem do CSV exportado pelo Bridge** (`GET /historico` por período), não da resposta `horas=24` que a IA recebeu na hora. O instante de corte e a hora exata que o lote devolve (`valorAtual.DataHora`) não foram observados. Assumimos o minuto do CSV, o que é coerente com a RES3 §2.4a e com a reprodução 50/50.
- **O código que rodou em 12/11 pode diferir do HEAD** (P23). O `appsettings.json` não muda no git desde 23/10.
- **O treino de 30/10 não pode ser reproduzido:** não há CSV desse dia. A interpretação "B-02 parada" vem dos números do perfil (% zeros, máx. 1–3).
- **Não verificado:** como o LiveMonitor exibe o `Score` (0–5 ou × 20).
- **É simulador, não planta real.**

## 6. Comandos e arquivos

- **Conferência de que a IAApi estava parada:** processos `dotnet`/`IAApi` (nenhum do projeto; o `dotnet run` ativo era do PowerServerCurrentTagsPropGet); portas 5145/7295 sem escuta; `ia_config.db` aberto com sucesso em modo exclusivo.
- **Cópia:** `ia_config.db`, `-wal` e `-shm` → `dados/inventario_pk2/ia_config_copia_20261113.db*` (pasta ignorada pelo git). Todas as consultas foram feitas com o módulo `sqlite3` do Python, só na cópia. Nenhum segredo nem connection string foi lido ou exibido.
- **Rascunho:** compilado com `dotnet build` no diretório temporário da sessão, referenciando `IAApi.csproj`. Lê só o CSV, o JSON de entrada (`dados/inventario_pk2/perfil_real_entrada.json`, gerado a partir da cópia) e o `appsettings.json`; SQLite em memória só para o treino de conferência. Nenhum acesso a Bridge, PSi4, CLP ou banco real.
- **Testes automatizados:** nenhum foi executado (não houve mudança de código).

## 7. Próximos passos sugeridos (nenhum aprovado)

1. **Tratar a entrada inválida sistemática** (P31) antes de qualquer novo teste da IA real; sem isso, ela não mede detecção. Opções a avaliar, com proposta, testes e rollback antes:
   - buscar o valor atual por tag, logo antes da predição;
   - ou recortar o histórico até a hora da leitura;
   - ou registrar a decisão como "não avaliada" em vez de "sem anomalia".
   Nenhuma pode mudar a decisão sem aprovação.
2. **Corrigir a escala nas análises** (P30): exportar também a nota 0–100 (`Score × 20`) e reescrever a tabela da RES3 §2.4b na mesma escala. Só ferramenta e documentação.
3. **Retreinar o perfil do PK2 com período limpo e longo**, com aprovação (treino altera a detecção; P13, P32). A ferramenta ainda não permite escolher o período em produção (BACKLOG 8).
4. **Marcar a tarefa 2 do BACKLOG como concluída** e criar os itens de P30–P32 (não feito: fora do escopo pedido).
