# Medição da etapa 0 (BACKLOG 6.0): ciclo, cadência e tempo por etapa da IA real

**Data da análise:** 16/11/2026. **Branch:** `feat/intelligence-lab-e2e-validation` (HEAD `d27e9a9`, árvore limpa). **Nenhum código, configuração ou banco foi alterado; nada foi executado contra o Bridge.**

**Sessão medida** (feita pela equipe; esta análise só lê os arquivos):
- `.\tools\Start-IAApiComLog.ps1 -HistoricoCausal -PerfDiag`, Development, Debug, PID 112928, `http://localhost:5145`, regime combinado **desligado** (`dados/logs/iaapi_20261116_151930.config.json`).
- IAApi no ar de **15:19:30 a ~16:46:34** de 16/11 (≈ 87 min). Simulador no **preset 1** (relato da equipe; os rótulos não foram conferidos aqui).
- Tags ativas: PK2.TAG22–TAG33 (12) e PK7.TAG1 (1).
- Arquivos: `dados/logs/iaapi_20261116_151930.log` (702 MB, 9,65 milhões de linhas; `.err.log` vazio) e `dados/ia_real_etapa0_preset1.csv` (247 decisões do PK2, exportadas às ~16:44).

> **17/11: a sessão 6.0b (reuso do histórico) está na §10 ("seção 6.0b").** Ciclo do PK2 de 131 s para **61 s**, avaliação de cada tag a cada **2,2 min** e idade p95 de **1,7 min**. Também ficam confirmadas a 35.2 (sem `MapaEstados`) e a 36 (log com horário e sem SQL do EF).

**Como ler as marcas:** **[log]** = confirmado no log ou no CSV desta sessão; **[código]** = confirmado lendo o código atual; **[inferência]** = conclusão tirada dos dois, sem prova direta; **[hipótese]** = a verificar; **[histórico]** = número de documento anterior, não revalidado.

---

## 0. Resumo

| Pergunta | Resposta curta |
|---|---|
| Ciclo do PK2 | **~2 min 11 s** (média de 131 s, de 94 a 162 s) antes do LiveMonitor; **306 s** e **≥ 697 s** com ele. Um ciclo começa a cada **~3,5 min** (171–238 s) |
| Intervalo entre avaliações da mesma tag | **3,5 min** (média de 3,47; p95 de 3,91; máx. de 4,14) antes do LiveMonitor; **3,8 / 4,1 / 16,2 min** na sessão inteira. Antes era **~10 min** (P26) |
| Onde vai o tempo de uma predição | **91 %** em **duas** buscas de 24 h ao Bridge (preparação 47 % + tendência 45 %); detectores **0,04 %**; finalização 3 %. **Hipótese confirmada, com correção:** não é "a" busca de 24 h, são **duas** por tag |
| Idade da leitura na decisão (PK2) | p50 de **1,7 min**, p95 de **3,2 min**, máx. de **12,0 min** (antes do LiveMonitor: p95 de 2,8 e máx. de 3,5 min) |
| `HistoricoCausal` | **0** entradas inválidas; 270 de 270 predições com `HISTÓRICO RECORTADO`, 0 `NÃO RECORTADO`. Nota 0: **2 de 269 (0,7 %)**, ambas avaliações válidas, contra **76 %** em 12/11. Sem o recorte, **181 de 270 (67 %)** seriam inválidas |
| Timeouts, erros e parada | **0 timeouts** no monitor; **1 falha** (TAG27, HTTP 500 do Bridge); **4 timeouts** de 60 s nas consultas do LiveMonitor; 270 erros de esquema do SQLite (`MapaEstados`); 9 HTTP 400 do LM Studio. **O monitor não parou** (P27 não se repetiu) |
| Aceite (e) da tarefa 22 | ✅ **Contagens 12/12 e 1/1 nos 21 lotes, 0 falhas**; tempos de 5,6 s (PK2) e 4,4 s (PK7) na mediana, na mesma ordem dos tempos de 05/11 |
| Metas da 6.0 | **Atingidas:** timeouts no monitor e entrada inválida/nota 0. **Não atingidas:** cadência (17/h × 60/h), cobertura (24 % × 90 %), ciclo (131 s × 60 s), idade p95 (3,0 × 2 min) |
| **Achado novo e decisivo** | A partir de **16:27**, o **LiveMonitor** dobrou a carga do Bridge (até 12 buscas de 24 h ao mesmo tempo) e disparou **~958 mil** consultas ao SQLite. O tempo do monitor ficou **4–5× maior**, e todos os timeouts da sessão vieram daí (§6). Registrado como **P42** (tarefa 34); a migration ausente como **P43** (tarefa 35); o log como **P44** (tarefa 36) |

---

## 1. Método

- **Ciclos:** linhas `Bridge lote cliente …` e `Predições cliente …` de `MonitoramentoTagsService`. **Limitação:** o log **não tem data e hora no início das linhas** (o `Logging__Console__FormatterOptions__TimestampFormat` definido pelo script não teve efeito; causa não investigada [hipótese]). O tempo foi ancorado nos campos `PredictionStartedUtc`/`PredictionCompletedUtc` do `PERF_DIAG Stage=Monitoring.Reading`. Início do ciclo = início da primeira tag − duração do lote.
- **Etapas:** 12.591 linhas `PERF_DIAG`, agrupadas por `TraceId`. A árvore de `ParentSpanId` foi conferida: todas as etapas têm o pai esperado (`Prediction.Total` → `Preparation`/`Decision`/`PostDecision`/`Finalization`; `Preparation.History` e `ValueTrend.History` → `Bridge.Transport`). As durações são **inclusivas**; os percentuais somam só etapas irmãs (`Docs/performance-diagnostics.md`).
- **Idade da leitura:** `ReadingAgeAtStartMs` veio nulo porque `ReadingKind=Unspecified` (comportamento previsto no código, sem fuso presumido). Foi calculada como `PredictionStartedUtc − (ReadingOriginal + 3 h)`, com hora local = UTC−3 (`InicioProcesso` no `config.json` termina em `-03:00`). **A leitura tem resolução de 1 min** (ex.: `15:19:00`), então cada idade inclui de 0 a 60 s de truncamento: a idade real pode ser até 1 min menor.
- **Antes e depois do LiveMonitor:** corte em **16:25** (19:25 UTC), início do último ciclo completo antes da primeira chamada externa (16:27:17).
- Scripts: Python, só leitura do log, fora do repositório (scratchpad da sessão). Comandos: `grep`/`awk` no log e `python -I`. Nenhum teste foi rodado (não havia código a testar).

---

## 2. Ciclo e cadência (pergunta 1)

### 2.1 Laço do monitor

Ordem confirmada no log **[log]** e no código **[código]** (`MonitoramentoTagsService.ExecuteAsync`): lote do PK7 → PK7.TAG1 → lote do PK2 → 12 tags do PK2, em sequência → `Task.Delay(1 min)`.

| Medida | PK2 | PK7 |
|---|---|---|
| Ciclos | 21 (20 completos + 1 interrompido no fim) | 21 |
| **Duração do ciclo** (lote + tags), 20 completos | média de **139,7 s**; p50 de 130,0 s; p95/máx. de **306,4 s**; mín. de 94,3 s | média de 20,6 s; p50 de 16,7 s; p95 de 21,5 s; máx. de 90,5 s |
| Duração, antes do LiveMonitor (19 ciclos) | média de **130,9 s** (94,3–162,1 s) | 13,1–21,5 s |
| Ciclo de 16:25 (LiveMonitor ligado às 16:27) | 306,4 s | — |
| Ciclo de 16:33 (interrompido) | 9 tags em **696,6 s** (11,6 min); a TAG31 estava em andamento quando o processo parou | 90,5 s (16:31) |
| **Intervalo entre inícios de ciclo** | média de **220,8 s** (3,7 min); p50 de 210,5 s; máx. de **456,9 s** (7,6 min); antes do LiveMonitor, 171–238 s | média de 217,1 s; p50 de 208,9 s; máx. de 380,0 s |

Conta do laço antes do LiveMonitor **[inferência]**: PK7 ~17 s + PK2 ~131 s + 60 s de espera ≈ **208 s**, o que bate com a mediana de 210 s.

### 2.2 Intervalo entre avaliações da mesma tag

Diferença entre `PredictionStartedUtc` consecutivos da mesma tag **[log]**:

| Conjunto | n | Média | p50 | p95 | Máx. |
|---|---|---|---|---|---|
| **PK2, antes do LiveMonitor** | 216 | **3,47 min** | 3,49 | **3,91** | 4,14 |
| PK2, sessão inteira | 237 | 3,80 min | 3,49 | 4,11 | **16,15** (TAG30, ciclo interrompido) |
| PK7.TAG1 | 20 | 3,6 min | 3,5 | 6,7 | 6,7 |

- Por tag (PK2), o máximo cresce com a posição no ciclo: TAG22 8,1 min → TAG30 16,1 min (efeito do ciclo de 16:33).
- **Avaliações por tag por hora (PK2):** 248 avaliações concluídas ÷ 12 tags ÷ 1,42 h = **14,6/h** (antes do LiveMonitor, 60 ÷ 3,47 = **17,3/h**). O CSV tem 20–21 decisões por tag.
- **Minutos-tag avaliados:** **248 de ~1.022 possíveis (24,3 %)**, todos com entrada válida (§5). Em 12/11, eram ≈ 2 % (P26 + P31).
- **Comparação com 12/11 (P26):** de ~10 min por tag para **3,5 min**. Diferenças conhecidas: 0 timeouts e `HistoricoCausal` ligado. O motivo de o Bridge ter respondido mais rápido hoje (4–5 s por busca contra 60–110 s por tag sob carga em 12/11) não foi medido **[hipótese: menos carga concorrente no Bridge até 16:27; ver §6]**.

---

## 3. Onde vai o tempo de cada predição (pergunta 2)

### 3.1 PK2 antes do LiveMonitor (228 predições concluídas; média de 10.494,6 ms por predição)

| Etapa (irmãs, sem dupla contagem) | Média (ms) | % |
|---|---|---|
| **Histórico de 24 h na preparação** (`Preparation.History`) | **4.895,4** | **46,6 %** |
| **Histórico de 24 h na tendência pós-decisão** (`ValueTrend.History`) | **4.688,6** | **44,7 %** |
| Baseline comportamental (`Preparation.BehaviorBaseline`) | 231,1 | 2,2 % |
| Baseline por regime (`Preparation.RegimeBaseline`; inclui a falha de esquema, §6.3) | 51,4 | 0,5 % |
| Resto da preparação (conversão, recorte, limpeza, `DetectorInput`, `Console.WriteLine`) | 180,5 | 1,7 % |
| **Detectores** (`Decision.Ensemble`) | **4,1** | **0,04 %** |
| Resto da decisão (contexto funcional, resolução, fusão, interpretação) | 39,7 | 0,4 % |
| Pós-decisão sem o histórico (relacionadas, risco, feedback, mensagem) | 20,7 | 0,2 % |
| **Finalização** (`Prediction.Finalization`; p50 de 56 ms, p95 de 2.318 ms, máx. de 5.992 ms) | 347,2 | 3,3 % |
| Antes da preparação (configuração, *shadow* multivariado, perfil, contexto) | 18,8 | 0,2 % |
| Restante dentro do `Prediction.Total` | 17,1 | 0,2 % |
| Fora do `Prediction.Total`, dentro do laço (`Monitoring.Reading` − `Total`) | 0,1 | ≈ 0 % |
| **Dois históricos somados** | **9.584,0** | **91,3 %** |

Dentro de cada histórico **[log]**:
- `Bridge.Transport` (preparação): média de 4.586 ms; p50 de 4.466 ms; p95 de 5.852 ms; máx. de 9.072 ms. Quase tudo é `Bridge.HttpBuffered` (espera e download da resposta: média de 4.573 ms).
- Decodificação (`Bridge.Decode`): p50 de 7 ms, mas média de 251 ms e p95 de 1,6 s (picos isolados) **[hipótese: GC ou CPU]**.
- Cada consulta devolve **1.436–1.439 pontos** (24 h a 1 ponto/min).
- `HistoricalRequests = 2` em 269 de 270 predições (1 na que falhou).

### 3.2 Outros recortes

| Conjunto | Média por predição | Dois históricos | Detectores | Finalização |
|---|---|---|---|---|
| PK2, sessão inteira (248) | 13.346 ms | 93,3 % | 0,03 % | 2,5 % |
| **PK2, com o LiveMonitor (20)** | **45.849 ms** | **98,3 %** (21,9 s + 23,2 s) | 0,01 % | 0,3 % |
| PK7.TAG1 (21) | 14.451 ms | 89,9 % | 1,1 % | 1,7 % |

### 3.3 Conclusão sobre a hipótese

**Confirmada no log, com uma correção.** O histórico de 24 h domina o tempo (91 % antes do LiveMonitor, 98 % com ele), mas são **duas buscas idênticas por tag**, cada uma com cerca de metade do tempo. A segunda vem da tendência pós-decisão (`TendenciaValorService.cs:50`), porque o `PredictionHistoryReuse` estava desligado (não faz parte do `-PerfDiag`) **[código + log]**. Os detectores custam ~4 ms. O resto não ligado ao Bridge soma **~0,9 s por tag**, dos quais 0,35 s de finalização e 0,23 s de baseline comportamental.

---

## 4. Idade da leitura na decisão (pergunta 3)

Idade = hora UTC da predição − (`ReadingOriginal` + 3 h), em minutos **[log + inferência do fuso]**. Lembrar do truncamento de até 1 min (§1).

| Conjunto | No início da predição: média / p50 / p95 / máx. | Na decisão (fim): média / p50 / p95 / máx. |
|---|---|---|
| **PK2, sessão inteira** (249) | 1,6 / 1,5 / **3,0** / **10,4** | 1,9 / 1,7 / **3,2** / **12,0** |
| PK2, antes do LiveMonitor (228) | 1,5 / 1,5 / 2,7 / 3,4 | 1,7 / 1,7 / 2,8 / 3,5 |
| PK2, com o LiveMonitor (21) | 3,4 / 2,2 / 8,7 / 10,4 | 4,2 / 3,1 / 10,4 / 12,0 |
| PK7.TAG1 (21) | 0,5 / 0,6 / 0,9 / 0,9 | 0,8 / 0,8 / 1,1 / 1,4 |

- **Idade no fim do lote:** média de 0,5 min, máx. de 1,0 min. Ou seja, o Bridge entrega o valor do minuto corrente, e o meio minuto médio é o truncamento. **O atraso vem da fila sequencial, não do Bridge** **[inferência]**.
- **Por posição no ciclo (PK2, início):** 1ª tag com média de 0,5 min → 6ª com 1,7 → 12ª com 2,5 min. Cada tag espera as anteriores (P11).
- Contra 12/11 (P18): de **0,2–17,2 min** para **0,0–12,0 min**; sem o LiveMonitor, para **0,0–3,5 min**.
- **Atraso total no pior caso** (sem o LiveMonitor) **[inferência]**: intervalo entre avaliações (até 4,1 min) + idade na decisão (até 3,5 min) ≈ **7–8 min** entre o início de uma falha e a primeira decisão sobre ela. Em 12/11, eram ~10–27 min.

---

## 5. Efeito do `HistoricoCausal` (pergunta 4)

| Medida | 12/11 (P31, sem recorte) | 16/11 (com recorte) |
|---|---|---|
| Linhas `HISTORICO RECORTADO NA LEITURA` | — | **270 de 270** predições que chegaram à preparação (PK2 249 + PK7 21) **[log]** |
| Linhas `HISTÓRICO NÃO RECORTADO (TOLERÂNCIA)` | — | **0** (maior avanço: 12 min, abaixo da tolerância de 30) |
| Entrada inválida | **38 de 50 (76 %)** | **0** (nenhum caso acima da tolerância; nas 2 notas 0, todos os detectores estavam aplicáveis) |
| Nota exatamente 0 | 38 de 50 (76 %) | **2 de 269 (0,7 %)** no PERF_DIAG; **2 de 247** no CSV |
| Notas 0 que eram avaliação válida | — | **2 de 2:** PK2.TAG29 às 15:28 e PK2.TAG27 às 16:20, com Z, MAD, EWMA, RoC, persistência e mudança de patamar `Aplicavel=true` e nota 0; IF não aplicável **[log]** |

Pontos removidos por predição (`Removidos` / `AvancoMin`) **[log]**:
- **1 ponto, avanço 0 min:** 89 casos (33 %). Só o ponto do próprio minuto, que o recorte com `<` sempre tira. **Sem o recorte, essas entradas seriam válidas.**
- **2 pontos, avanço 1 min:** 113 (42 %). **3 pontos, avanço 2 min:** 56 (21 %). **4 a 12 pontos:** 12 (4 %; os maiores no ciclo com o LiveMonitor).
- **Sem o recorte**, as **181 de 270 (67 %)** predições com avanço ≥ 1 min teriam um ponto do histórico **depois** da leitura e seriam recusadas pelo `DetectionInputValidator` **[inferência pelo código: recusa quando a leitura é anterior ao último ponto]**. Isso é coerente com os 76 % de 12/11, num dia de ciclo mais lento.

**Conclusão:** o aceite de campo da tarefa 25 ("fração de entradas inválidas ≈ 0 numa sessão com o simulador") foi **atingido** nesta sessão. Ficam pendentes a opção (a) (tornar visível o caso acima da tolerância, que não ocorreu) e os alarmes com a B-02 ligada (P32).

**Fora do escopo desta medição:** o CSV tem **9 anomalias** (TAG26 4×, de 15:41 a 15:52; TAG30 15:59; TAG32 16:03; TAG27 16:16; TAG31 16:17; TAG28 16:41; notas de 60,3 a 75,8). **Nenhuma foi avaliada contra o gabarito do preset 1.** Isso exige o histórico do simulador com os rótulos e o cuidado do P32 (perfil colapsado de TAG28/32).

---

## 6. Timeouts, erros e parada do monitor (pergunta 5)

### 6.1 No laço do monitor

| Evento | Quantidade | Detalhe |
|---|---|---|
| Timeout de 60 s (`HttpClient`) no histórico ou no lote | **0** | Maior `Bridge.Transport` do monitor: 54,4 s (preparação) e 48,8 s (tendência), ambos com o LiveMonitor **[log]** |
| Falha de tag | **1** | PK2.TAG27, 16:40:07, após 44,2 s: o Bridge respondeu **HTTP 500** "Servidor inválido ou não encontrado", com `TimeoutException: The open operation did not complete within the allotted timeout of 00:00:03` **dentro do Bridge**. A leitura ficou **sem decisão**, visível só no log (P18/tarefa 18) **[log]**. Causa: o Bridge não abriu a conexão com o servidor de dados em 3 s **[hipótese: sobrecarga do Bridge, §6.2]** |
| Falha de lote | **0** | 42 lotes, todos com sucesso |
| "Erro geral no monitoramento" | **0** | — |

### 6.2 Fora do monitor: o LiveMonitor (achado novo)

A partir de **16:27:17** (primeira requisição HTTP, que gerou o aviso `HttpsRedirectionMiddleware`), aparecem dois padrões que não vêm do laço **[log]**:

1. **212 buscas de 24 h ao Bridge sem predição como pai** (`Bridge.Transport` com `ParentSpanId=(null)`):
   - 17 a 18 por tag do PK2, de 16:27:19 a 16:45:50;
   - média de **36,6 s** cada (p95 de 55,8 s), contra 4,6 s do monitor antes;
   - **4 timeouts de 60 s**: TAG31 às 16:41:42 e TAG33 às 16:42:46, 16:43:50 e 16:44:54;
   - **até 12 simultâneas** (mediana de ~6 em amostras de 5 s).
   - **Origem [código]:** o LiveMonitor consulta `/monitor-api/history` → `GET /api/tags/historico/{cliente}/{tag}?horas=2` (`tools/IAApi.LiveMonitor/server/proxy.ts:33`) para cada tag selecionada, 3 de cada vez, a cada 15 s (`MONITOR_HISTORY_MS`, padrão 15.000). O `TagHistoricoController` chama `BuscarHistorico`, que busca as **24 h** inteiras (`Bridge:HorasHistorico`), e só depois filtra 2 h.
   - **Por que até 12 simultâneas, e não 3 [código + inferência]:** o proxy aborta a requisição após **12 s** (`proxy.ts:66-67`), mas o `BuscarHistorico` é síncrono e não recebe cancelamento. Com o Bridge levando ~36 s, cada consulta abandonada continua no Bridge enquanto o LiveMonitor já pediu a próxima.
2. **~958 mil consultas `SELECT … FROM "FeedbackAnomaliasIa"`** ao SQLite. Só 270 delas (1 por predição) estão em `PostDecision.Feedback`; as outras ficam intercaladas com as chamadas HTTP.
   - **Origem [código]:** `GET /api/anomalias` (`Controllers/AnomaliaController.cs:85-116`) carrega **todas** as decisões de **todos os clientes**, sem filtro nem paginação (~3.200 linhas; o último id do CSV é 3198), e chama `GerarAnaliseFeedbacks` **uma vez por linha** (N+1).
   - O LiveMonitor consulta `/monitor-api/decisions` → `/api/anomalias` a cada **2 s** (`MONITOR_POLL_MS`).
   - Com `LogLevel:Default = Information`, cada consulta vai ao log: daí os **702 MB** (~36 MB/min depois de 16:27).

**Efeito no monitor [log; causalidade: inferência forte por coincidência no tempo e pelo mecanismo]:**
- `Preparation.History`: média de **4,9 s → 21,9 s**; `ValueTrend.History`: **4,7 s → 23,2 s**. O aumento está no `Bridge.HttpBuffered` (espera do Bridge), não no código da IAApi: o "restante" dentro do `Total` ficou em ~40 ms.
- Predição do PK2: **10,5 s → 45,8 s**. Ciclo: **131 s → 306 s e ≥ 697 s**. Idade máxima da leitura: **3,5 → 12,0 min**. Lote do PK2: 5,6 s → 35,2 s; lote do PK7: 4,4 s → 23,5 s.
- A falha 500 da TAG27 e os 4 timeouts aconteceram todos nessa janela.
- **Hipóteses complementares (não medidas):** fila de threads do .NET ocupada pelas chamadas síncronas abandonadas; disputa de disco e CPU pelo log de 700 MB.

### 6.3 Outros erros

- **270 × `SQLite Error 1: 'no such column: t.MapaEstados'`** (um por predição), na consulta de `TagsRegimesOperacionaisIa`, dentro de `Preparation.RegimeBaseline` **[log]**.
  - **[código]:** a coluna vem da migration `20261113160248_RC5_AddTagRegimeComponente` (13/11), e o projeto não aplica migrations ao subir (não há `Migrate()`).
  - O `OperationalRegimeBaselineProvider` (linhas 104–120) captura a exceção e devolve "não aplicado", isto é, o baseline global.
  - **Efeito hoje:** o mesmo de "sem configuração", porque o PK2 não tinha regime cadastrado em 13/11 (INV §3) **[histórico]**. Mas **o banco local está sem a migration RC-5**: com ela pendente, a RC-12 (regime combinado) falharia em silêncio, sempre caindo no global **[inferência]**. Não se abriu o banco para conferir.
- **9 × `LM Studio retornou HTTP 400`**, uma por anomalia. O serviço usou o fallback por regras, com custo ≤ 456 ms **[log]**. Causa no LM Studio não investigada **[hipótese: modelo não carregado]**.

### 6.4 O monitor parou? (P27)

**Não.** As linhas PERF_DIAG são contínuas de 15:19:36 a 16:46:34, **sem nenhuma lacuna maior que 90 s** **[log]**.
- Última decisão: PK2.TAG30 às 16:45:00. A TAG31 ainda estava em processamento quando o log termina, no meio de uma linha, às 16:46:34.
- Isso é compatível com o encerramento do processo pela equipe **[inferência]**.
- O P27 **não se repetiu** em ~87 min, agora com a saída em arquivo e sem janela de console. Isso **não descarta** as hipóteses do P27 (12/11 parou após ~50 min). Só mostra que, sem a janela de console, a parada não aconteceu nesta sessão.

---

## 7. Aceite (e) da tarefa 22: linhas `Bridge lote` (pergunta 6)

| Cliente | Lotes | Tags solicitadas | Falhas | Tempo (ms): p50 / média / mín. / p95 / máx. | Referência de 05/11 [histórico] |
|---|---|---|---|---|---|
| PK2 | 21 | **12 em todos** | **0** | **5.588** / 6.368 / 1.057 / 6.600 / 35.169 | 29,4 s |
| PK7 | 21 | **1 em todos** | **0** | **4.366** / 6.118 / 4.119 / 11.087 / 23.522 | 4,5 s |

- **Contagens normais:** 12 e 1 são exatamente as tags ativas. Todas as 270 predições receberam leitura do lote (`ReadingOriginal` preenchido; nenhum "tag não encontrada"). **[log]**
- **Tempos normais:** mesma ordem de grandeza da referência anterior (PK7 igual; PK2 mais rápido). Os dois máximos (35,2 s e 23,5 s) são do período com o LiveMonitor.
- **Nenhuma linha de log do `IHttpClientFactory`** para os clientes do Bridge (só as do `ILlmProvider`): o `RemoveAllLoggers()` funcionou como previsto **[log]**.
- **Veredito:** aceite (e) **cumprido**. O endereço vindo de `Bridge:BaseUrl` funcionou no ambiente de teste, com o mesmo comportamento observável.
- **Observação [hipótese]:** o lote do PK7, com 1 tag, nunca ficou abaixo de 4,1 s, e o do PK2, com 12 tags, chegou a 1,1 s. O custo parece estar no Bridge, por servidor de dados, e não no número de tags.

---

## 8. Comparação com as metas da 6.0 (pergunta 7)

As metas valem para 6.2 e 6.3. Aqui ficam como **linha de base**.

| Métrica | Meta | Medido em 16/11 (sessão inteira; antes do LiveMonitor) | Situação |
|---|---|---|---|
| Avaliações por tag por hora | ≈ 60/h | **14,6/h**; 17,3/h | ✗ (≈ 1/4 da meta) |
| Minutos-tag avaliados ÷ possíveis | ≥ 90 % | **24,3 %** (248/1.022); ≈ 29 % | ✗ (era ≈ 2 % em 12/11) |
| Duração do ciclo do PK2 | ≤ 60 s | **p50 de 130 s, máx. de 306 s**; 94–162 s | ✗ |
| Tiques pulados | registrados | não se aplica (6.3 não implementada) | — |
| Idade da leitura p95 | < 2 min | **3,0 min** no início e 3,2 min na decisão; 2,7 e 2,8 min | ✗ (perto, sem o LiveMonitor) |
| Tempo do histórico e consultas por predição | queda medida | **linha de base:** 2 consultas por predição; 4,9 s + 4,7 s (91 %) | base registrada |
| Timeouts por hora | 0 | **0 no monitor**; 1 falha 500 do Bridge; 4 timeouts no LiveMonitor | ✓ no monitor |
| Nota 0 e entrada inválida | ≈ 0 | **0 inválidas; 2 notas 0 válidas (0,7 %)** | ✓ |
| Diferença janela × busca completa | só na borda | não se aplica (6.1) | — |

**Orçamento para chegar a 1 min [inferência]:**
- 60 s − lote do PK2 (~5,6 s) − PK7 (~17 s hoje) ≈ 37 s para 12 tags, ou seja, **≈ 3 s por tag**.
- Hoje, a parte que não depende do Bridge custa **~0,9 s por tag**. Sobram ~2 s por tag para buscar o histórico.
- Se o PK7 também passar pela janela incremental, ele cai para ~1–2 s, e a folga sobe para ~4 s por tag.

---

## 9. A janela incremental (6.1 a 6.3) continua sendo a melhor proposta?

**Sim, como solução estrutural.** O tempo está quase todo em buscar 24 h no Bridge (91 %), e a janela ataca exatamente isso. A medição também respondeu a um "a confirmar" da 6.1: o `/historico` devolve **1 ponto por minuto** (1.436–1.439 em 24 h), e não só as mudanças de valor. Mas a medição pede **quatro ajustes na ordem e no aceite**. Nada disso está aprovado.

1. **Medir primeiro o reuso do histórico, sem código novo.** Metade do tempo é a **segunda** busca de 24 h (tendência). O `PredictionHistoryReuse` já existe e a elimina.
   - Numa sessão igual a esta, com `IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY=1` (exige aprovação), a expectativa **[inferência]** é: ~10,5 s → ~5,9 s por tag e ~131 s → ~76 s por ciclo. Ainda não chega a 1 min, mas dá o ganho imediato e separa os dois efeitos antes da 6.1.
   - Com o reuso ligado, o "intervalo mínimo entre trechos" da 6.1 (sugestão de 30 s) deixa de ser necessário.
2. **Medir o custo de um trecho curto antes de depender dele.** A 6.1 supõe que buscar 10–15 min é bem mais barato que 24 h. O log mostra indícios de custo fixo por requisição no Bridge: lote de 1 tag do PK7 sempre ≥ 4,1 s; `Bridge.Transport` mínimo de 0,26 s; pedaço de 2 h com 1–5 s em 12/11, no P10.
   - Se o trecho custar ~1 s, a 6.1 + 6.2 cabe no orçamento de ~3 s por tag.
   - Se custar ~4 s, **não cabe** (12 × 4 s ≈ 48 s + lote + PK7 > 60 s). Aí a **6.5** (completar a janela com o lote, que traz 12 tags em ~5,6 s) passa de "futuro" a necessária, ou o endpoint em lote no Bridge.
   - **Mudança proposta no aceite da 6.1:** registrar `Bridge.Transport` do trecho curto (p50 e p95) e decidir 6.5 × 6.3 com esse número.
3. **Controlar a carga externa antes de medir 6.1, 6.2 ou 6.3.** O LiveMonitor, sozinho, multiplicou o tempo do monitor por 4–5 e causou todos os timeouts. Propostas (nenhuma aprovada; registradas como **P42 / tarefa 34**):
   - **(a)** `TagHistoricoController` buscar só as `horas` pedidas (`BuscarHistoricoPorPeriodo`), em vez de 24 h. O contrato HTTP não muda; reduz ~12× o volume pedido por consulta do LiveMonitor.
   - **(b)** `GET /api/anomalias` com filtro por cliente e limite, ou o LiveMonitor numa rota limitada. Hoje são ~3.200 consultas ao SQLite a cada 2 s, e o `Export-AnomaliasIaReal.ps1` usa a mesma rota. Mudar o contrato exige cuidado com o export.
   - **(c)** Cancelamento ou concorrência limitada no histórico do LiveMonitor (o proxy abandona após 12 s, mas o Bridge continua).
   - **(d)** Nas sessões de medição, registrar se o LiveMonitor estava aberto, ou medir uma fase sem ele.
   - O escopo do decorador da 6.1 (só o laço do monitor) **não** resolve isso, de propósito: os controllers continuam indo direto ao Bridge.
4. **Corrigir o ambiente antes da 6.4 e da RC-12:**
   - aplicar a migration RC-5 no banco local (sem isso, o regime nunca é aplicado; §6.3);
   - na sessão, baixar o log do EF (`Microsoft.EntityFrameworkCore.Database.Command`) para Warning e fazer o carimbo de data e hora funcionar. Hoje o log não tem hora e cresce ~36 MB/min com o LiveMonitor.
   - Os dois são mudança de configuração ou banco e exigem aprovação. Registrados como **P43 / tarefa 35** e **P44 / tarefa 36**.

**Incorporado ao BACKLOG em 16/11:**
- a mudança 1 virou a **6.0b**;
- a 2 virou a regra de decisão no aceite da 6.1 e a condição da 6.5;
- a 3 virou condição de sessão na 6.0b, 6.1 e 6.2, além da tarefa 34;
- a 4 virou dependência da 6.4 e da RC-12 na tarefa 35.

**O que não muda:** os valores aprovados (sobreposição de 10 min, recarga completa a cada 60 min, cadência de 1 min), o isolamento por cliente e o modo Comparar antes do Usar. A **6.3** continua dependendo da 6.2: com o ciclo de hoje (131 s), a cadência fixa só pularia tiques.

---

## 10. Seção 6.0b: reuso do histórico da predição (sessão de 17/11)

**Data da análise:** 17/11/2026. **Branch:** `feat/intelligence-lab-e2e-validation` (HEAD `f4020ec`). Nenhum código, configuração ou banco foi alterado, e nada foi executado contra o Bridge ou a API. A análise só leu o log, o CSV e uma cópia do banco.

### 10.0 Sessão, fontes e método

- **Sessão** (feita pela equipe):
  - comando: `.\tools\Start-IAApiComLog.ps1 -HistoricoCausal -PerfDiag -ReusarHistorico`, em Development/Debug, PID 105992;
  - regime combinado **desligado**; `IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY=1` (`dados/logs/iaapi_20261117_114435.config.json`);
  - processo de 11:44:35 a **13:01:35** (última linha do log, ≈ 77 min);
  - simulador em operação normal (relato da equipe);
  - tags: PK2.TAG22–TAG33 (12) e PK7.TAG1 (1), as mesmas da 6.0.
- **LiveMonitor fechado** (relato da equipe, confirmado no log):
  - nenhuma busca ao Bridge fora de uma predição (0 `Bridge.Transport` com `ParentSpanId=(null)`);
  - uma única requisição HTTP externa, às 13:01:29 (o aviso `HttpsRedirectionMiddleware`), que é o export.
- **Bridge fora do ar no início [log]:**
  - 4 falhas de lote (PK7 e PK2) de 11:44:42 a 11:45:54, com "connection refused" em `localhost:5001`;
  - primeira predição às 11:47:02. Essas falhas não entram nas contas abaixo.
- **Log:** `dados/logs/iaapi_20261117_114435.log`, com 15,6 MB e 65.417 linhas; `.err.log` vazio.
- **Decisões:** vêm de uma **cópia do `ia_config.db`**, como no inventário do PK2.
  - Arquivos da cópia: `dados/sessao_6_0b/ia_config_copia_20261117_6_0b.db`, `-wal` e `-shm`, com `SHA256SUMS.txt`; a pasta é ignorada pelo git.
  - Conferência de que a IAApi estava parada antes de copiar:
    - o PID 105992 não existia;
    - nada escutava nas portas 5145 e 7295;
    - os três arquivos abriram em modo exclusivo;
    - o único `dotnet run` ativo era o do Bridge (`PowerServerCurrentTagsPropGet`).
  - O SHA-256 do original foi igual antes e depois da cópia, e igual ao da cópia.
  - As consultas usaram `sqlite3` com `mode=ro`, só na cópia; `quick_check = ok`.
  - Resultado: **448 decisões** (PK2 413, PK7 35), ids 3200–3647, de 11:47:08 a 13:01:34.
- **O `dados/ia_real_6_0b.csv` foi gerado**, às 13:01:30, com 412 linhas do PK2.
  - Ele bate com o banco em 412 de 412 linhas (mesma nota e mesma decisão).
  - Falta só o id 3647 (PK2.TAG26, 13:01:34), gravado depois do export.
- **Método:** o mesmo do §1. Duas diferenças:
  - os ciclos agora também têm a hora das linhas `Bridge lote` e `Predições cliente`;
  - o log foi lido com reposição de caracteres, porque continua na página de código do console (36.3).
  - Script em Python, só leitura, fora do repositório (scratchpad da sessão), rodado com `python -I`.
- **Base de comparação:** a etapa 0 **antes do LiveMonitor** (16/11, 15:19–16:25, preset 1, 228 predições do PK2). Diferenças de condição:
  - o simulador (preset 1 × operação normal);
  - nesta sessão já valem a **35.1** (RC-5 aplicada) e a **36.2** (horário no log e SQL do EF em `Warning`). As três mudanças agiram juntas e **não foram isoladas**.

### 10.1 Resumo: etapa 0 × 6.0b

| Medida (PK2, salvo indicação) | Etapa 0, antes do LiveMonitor | 6.0b | Variação |
|---|---|---|---|
| **Duração do ciclo** (lote + 12 tags) | média de 130,9 s (94–162) | **média de 60,8 s**; p50 de 61,8; p95 de 68,9; 37–70 (34 ciclos completos) | **−54 %** |
| Intervalo entre inícios de ciclo | 171–238 s (p50 de 210,5 na sessão) | média de **130,5 s**; p50 de 132,8; 104–140 | −38 % |
| **Intervalo entre avaliações da mesma tag** | média de 3,47 min; p50 de 3,49; p95 de 3,91; máx. de 4,14 | **média de 2,17 min**; p50 de 2,19; p95 de 2,30; máx. de 2,38 | −37 % |
| Idem, PK7.TAG1 | média de 3,6 min | média de 2,17 min | −40 % |
| Avaliações por tag por hora | 17,3/h | **27,8/h** (413 ÷ 12 ÷ 1,24 h) | +61 % |
| Minutos-tag avaliados ÷ possíveis | ≈ 29 % | **46,3 %** (413 de ~892) | +17 p.p. |
| **Tempo por predição** | 10.495 ms | **4.713 ms** (p50 de 4.440; p95 de 6.170; máx. de 8.527) | **−55 %** |
| Consultas de histórico por predição | 2 (em 269 de 270) | **1 (em 448 de 448)** | −1 |
| Idade da leitura no início: p50 / p95 / máx. (min) | 1,5 / 2,7 / 3,4 | **0,94 / 1,67 / 1,90** | p95 −38 % |
| Idade na decisão: p50 / p95 / máx. (min) | 1,7 / 2,8 / 3,5 | **1,03 / 1,73 / 1,97** | p95 −38 % |
| Timeouts no monitor | 0 | **0** | = |
| Falhas de tag | 0 (1 na sessão, com o LiveMonitor) | **0** | = |
| Entradas inválidas / nota 0 | 0 / 2 de 228 | **0 / 3 de 413 (0,7 %)**, todas avaliações válidas | = |
| `no such column: MapaEstados` | 270 (sessão inteira) | **0** | resolvido (35.2) |
| Log | sem horário; 702 MB em 87 min | **com horário; 15,6 MB em 77 min** | resolvido (36) |

### 10.2 Ciclo e cadência [log]

- **Laço:** PK7 ~9,8 s (lote de 4,6 s + predição de 5,2 s) + PK2 ~60,8 s + 60 s de espera ≈ **130,6 s**. Isso bate com os 130,5 s medidos entre inícios de ciclo.
  - O trabalho do laço, sem a espera, é de **~70,5 s**.
- **PK2:** 35 ciclos, sendo 34 completos (12 de 12 em todas as linhas `Predições cliente PK2`) e 1 interrompido pela parada (5 tags).
- **Intervalo por tag:** quase constante. O máximo por tag fica entre 2,32 e 2,38 min em todas as 12 tags.
- **Sem travamentos:** a maior lacuna entre linhas PERF_DIAG é de **68 s**, que é a espera do laço.
- **Contra 12/11 (P26):** de ~10 min por tag para **2,2 min**.

### 10.3 Onde vai o tempo de cada predição (PK2, 413 predições concluídas)

| Etapa (irmãs, sem dupla contagem) | Etapa 0: média em ms (%) | 6.0b: média em ms (%) |
|---|---|---|
| Histórico de 24 h na preparação (`Preparation.History`) | 4.895,4 (46,6 %) | **4.599,8 (97,6 %)** |
| Histórico de 24 h na tendência (`ValueTrend.History`) | 4.688,6 (44,7 %) | **não existe** (`ValueTrend.ReuseRawHistory` de 0,1 ms, com `HistorySource=ReusedPredictionRaw` em 413 de 413) |
| Baseline comportamental | 231,1 (2,2 %) | 26,8 (0,6 %) |
| Baseline por regime | 51,4 (0,5 %), com a falha de esquema | 0,3 (0,0 %), sem a falha |
| Resto da preparação | 180,5 (1,7 %) | 27,6 (0,6 %) |
| Detectores (`Decision.Ensemble`) | 4,1 | 2,3 |
| Resto da decisão | 39,7 | 14,4 |
| Pós-decisão sem o histórico | 20,7 | 2,3 |
| **Finalização** (p50 / p95) | 347,2 (56 / 2.318) | **37,4 (6,3 / 103)** |
| Antes da preparação | 18,8 | 1,5 |
| Restante dentro do `Prediction.Total` | 17,1 | 0,6 |
| **Total** | **10.494,6** | **4.713,0** |
| Parte que não depende do Bridge (`Total` − `Preparation.History`) | ~911 | **~113** |

- **`Bridge.Transport` da preparação:** a média passou de 4.586 para **4.508 ms**, o p50 de 4.466 para 4.247, o p95 de 5.852 para 5.760 e o máximo de 9.072 para 7.996.
  - Quase todo esse tempo é espera e download (`Bridge.HttpBuffered`).
  - A decodificação caiu de 251 para 70,5 ms em média (p95 de 1,6 s para 176 ms).
  - Pontos por consulta: 1.431 no PK2 e 1.433 no PK7 (eram 1.436–1.439).
- **Para onde foram os 5,78 s economizados por tag:**
  - a segunda busca: **−4,69 s (81 %)**;
  - a parte fora do Bridge: **−0,80 s (14 %)**;
  - a primeira busca: −0,30 s (5 %).
- **Por que a parte fora do Bridge caiu [inferência; não isolado]:**
  - o baseline por regime não lança mais exceção em toda predição (35.1: de 51 para 0,3 ms);
  - com o SQL do EF em `Warning` (36.2), as etapas que gravam ou leem no SQLite (finalização e baseline comportamental) deixaram de mandar cada comando ao log, que é escrito de forma síncrona no arquivo.
  - As três mudanças vieram juntas. Corrige o que o P44 dizia em 16/11 ("o custo de logging não apareceu no monitor"): pelo jeito, aparecia na finalização (p95 de 2,3 s).

### 10.4 Idade da leitura na decisão [log + fuso UTC−3, como no §4]

| Conjunto | No início: média / p50 / p95 / máx. (min) | Na decisão: média / p50 / p95 / máx. (min) |
|---|---|---|
| **PK2** (413) | 0,95 / 0,94 / **1,67** / 1,90 | 1,03 / 1,03 / **1,73** / 1,97 |
| PK7.TAG1 (35) | 0,55 / 0,58 / 0,98 / 1,00 | 0,64 / 0,68 / 1,08 / 1,09 |

- **Por posição no ciclo (PK2, no início):** 1ª tag com 0,51 min → 6ª com 0,93 → 12ª com **1,39** (antes, 0,5 → 1,7 → 2,5). A fila sequencial pesa a metade, porque cada predição leva a metade do tempo.
- **Atraso total no pior caso [inferência]:** intervalo (até 2,4 min) + idade na decisão (até 2,0 min) ≈ **4,4 min** entre o início de uma falha e a primeira decisão sobre ela. Eram 7–8 min na etapa 0 e 10–27 min em 12/11.
- Vale o mesmo truncamento de até 1 min do §1.

### 10.5 `HistoricoCausal` e nota 0 [log]

- **448 de 448** predições com `HISTORICO RECORTADO NA LEITURA`; **0** `NÃO RECORTADO`.
- **Pontos removidos:**
  - 1 ponto (avanço de 0 min): 259 (58 %);
  - 2 pontos (avanço de 1 min): 189 (42 %);
  - **nenhum caso com 3 ou mais** (eram 25 % na etapa 0). O ciclo mais curto deixa a leitura mais nova.
- **Nota 0:** 3 de 448 predições, todas no PK2 (3 de 413, 0,7 %): TAG31 (leituras de 11:53 e 12:07) e TAG29 (12:09).
  - Nas três, Z, MAD, EWMA, taxa de variação, persistência e mudança de patamar estavam aplicáveis, com nota 0. O IF não era aplicável.
  - São **avaliações válidas**, como as 2 da etapa 0.
- **Entradas inválidas: 0.**

### 10.6 Timeouts, erros e falhas [log]

| Evento | Quantidade | Detalhe |
|---|---|---|
| Timeout de 60 s no histórico ou no lote | **0** | Maior `Bridge.Transport`: 8,0 s. Nenhuma linha de `TaskCanceled` ou `Timeout` |
| Buscas ao Bridge com erro | **0** | 448 de 448 `Bridge.Transport` com HTTP 200 |
| Falha de tag ou "Erro geral no monitoramento" | **0** | 448 predições concluídas; a 449ª (TAG27, 13:01:35) foi interrompida pela parada (§10.10) |
| Falha de lote | 4, só no início | Bridge ainda fora do ar (§10.0). Depois, todos os lotes com sucesso: 12 tags no PK2 e 1 no PK7, com p50 de 4,1 s nos dois |
| `no such column` / `SQLite Error` | **0 / 0** | era 270 (§10.8) |
| `LM Studio retornou HTTP 400` | 52 | Uma por anomalia, com fallback por regras, como em 16/11 (§6.3). Causa não investigada |

**Fora do escopo desta medição: 52 anomalias do PK2 (12,6 %).** Elas se concentram depois de 12:25 nas vazões das bombas:
- TAG27, 29 e 31 caem para perto de 0;
- TAG28, 30 e 32 sobem (médias do banco por faixa de horário).

É a **troca de bombas** do rodízio, e não uma falha [inferência]. É o mesmo problema já registrado de perfil colapsado e bomba reserva (P32, tarefas 9 e 27). **Nenhuma foi avaliada contra o gabarito do simulador.**

### 10.7 Tarefa 36: formato do log [log]

- **Horário:** **18.527 de 18.527** cabeçalhos do `ILogger` (`info:`, `warn:`, `fail:`) começam com `yyyy-MM-dd HH:mm:ss`; nenhum ficou sem horário. A correção da 36.1/36.2 funcionou numa subida real.
- **SQL do EF:** **0** linhas `Executed DbCommand`. Não houve falha do EF para conferir se o `fail:` continua aparecendo.
- **Tamanho:** 15,6 MB em 77 min, **~12 MB/h**, contra 702 MB em 87 min em 16/11.
- **Ainda sem horário:** as 46.890 linhas de `Console.WriteLine` (`BUSCANDO HISTÓRICO`, `HISTORICO RECORTADO`, interpretação; tarefa 17).
- **Codificação:** o arquivo continua na página de código do console (`Predi�ao`; 36.3 aberta).
- **Efeito colateral:** com o EF em `Warning`, o log não mostra mais as consultas ao SQLite. A contagem usada no P42 (~958 mil `SELECT … FeedbackAnomaliasIa`) não pode mais ser refeita pelo log. O aceite da tarefa 34 precisa de outra medida, como o tempo do monitor e do `GET /api/anomalias`.

### 10.8 Tarefa 35.2: a migration RC-5 no uso real [log + cópia do banco]

- **0** ocorrências de `MapaEstados`, `no such column` ou `SQLite Error` no log, contra 270 em 16/11.
- **`Preparation.RegimeBaseline`:** `Status=Succeeded` em 448 de 448 predições, média de 0,3 ms (era 51,4 ms, com a falha).
- **"Baseline estatístico: global"** em 448 de 448.
- **Na cópia do banco:**
  - 33 migrations, a última sendo a RC-5;
  - coluna `MapaEstados` presente;
  - `TagsRegimesOperacionaisIa` e `TagsRegimesComponentesIa` com **0 linhas**.
  - Ou seja, o caminho agora é "sem configuração" → baseline global, o mesmo resultado de antes e por um caminho sem exceção.
- **Decisão do PK2:** continua no baseline global, como antes. Não houve comparação predição a predição: os dados das sessões são outros.
- **PK7:** sem regime cadastrado, então não há antes e depois a registrar.

### 10.9 O reuso atingiu a estimativa de ~76 s por ciclo?

**Sim, e foi além.** O ciclo do PK2 ficou em **60,8 s** em média (estimativa de ~76 s), e a predição em **4,7 s** (estimativa de ~5,9 s). O reuso respondeu pelo previsto, que era tirar a segunda busca: −4,69 s por tag, com `HistoricalRequests = 1` em todas as predições. O ganho a mais (~0,8 s por tag) veio da parte fora do Bridge, junto com a 35.1 e a 36.2 (§10.3).

Aceite da 6.0b:
- `HistoricalRequests = 1` em todas as predições: ✅ (448 de 448);
- nenhum `Bridge.Transport` sob `ValueTrend.History`: ✅;
- queda do ciclo e do intervalo por tag medida contra a 6.0: ✅;
- entradas inválidas em 0: ✅.

**Sozinho, o reuso não chega a 1 min**, como previsto: 2,17 min por tag.

### 10.10 O que muda na decisão da 6.1 (regra do aceite)

**Orçamento refeito [inferência a partir do log]:**
- Para um laço de 60 s: 60 − lote do PK2 (4,3 s) − PK7 (9,8 s) = 45,9 s para 12 tags, ou **3,8 s por tag**. Tirando a parte fora do Bridge (0,11 s), sobram **~3,7 s para o histórico**.
- Com a folga que a 6.3 exige (ciclo ≤ ~50 s): (50 − 14,1) ÷ 12 − 0,11 ≈ **2,9 s**.
- A busca de 24 h custa hoje **4,25 s no p50 e 5,76 s no p95**. Faltam ~1 s por tag no p50 e ~2–3 s no p95.

**Indício forte de custo fixo por requisição no Bridge:** o tamanho do pedido quase não muda o tempo.

| Requisição | Fonte | Tamanho | p50 | p95 |
|---|---|---|---|---|
| Lote de valores atuais, PK7 | log da 6.0b | 1 tag | 4,1 s | 7,5 s |
| Lote de valores atuais, PK2 | log da 6.0b | 12 tags | 4,1 s | 5,6 s |
| `/historico` de 24 h (monitor) | log da 6.0b (n = 448) | ~1.431 pontos | 4,26 s | 5,76 s |
| `/historico` em pedaços de 2 h (export CP300, 9–13/11) | `dados/cp300_09a13nov.csv.pedacos.csv` (n = 2.881) | ~120 pontos | 4,35 s | 6,16 s |
| `/historico` em pedaços curtos (export) | idem (n = 43) | ≤ 60 pontos | 4,18 s | 5,78 s |

- Na 6.0b, 369 das 448 buscas levaram entre 4 e 6 s, e só 27 ficaram abaixo de 2 s.
- **Ressalva:** os pedaços do export vêm de outra sessão, com outra carga. O trecho curto da 6.1 (10–15 min, ~15 pontos) ainda não foi medido na IAApi.

**Consequência para a regra da 6.1 [inferência]:**
- Os dados disponíveis põem o trecho curto no ramo **"≥ ~3 s"** (p95 provável de 5–6 s). Nesse caso, a 6.2 sozinha **não ganha quase nada**: troca uma busca de ~4,3 s por outra de ~4 s.
- O caminho para 1 min passa a ser a **6.5** (completar a janela com o valor do lote, que já vem em ~4 s para as 12 tags), um endpoint em lote no Bridge, ou baixar o custo fixo no próprio Bridge.
  - Hipótese a investigar no Bridge, fora deste repositório: cada requisição abre uma conexão nova com o servidor de dados. Em 16/11, o erro 500 do Bridge citava `open operation … timeout 00:00:03`.
- **Limites da regra:**
  - o superior (~3 s) se confirma com o novo orçamento e a folga da 6.3;
  - o inferior pode subir de ~2 s para **~2,5 s**, porque a parte fora do Bridge caiu de 0,9 para 0,1 s.
- **Proposta (não aprovada):**
  - manter a **6.1 em modo Comparar**, que não muda a decisão e é a base da janela que a 6.5 também usa. O objetivo é **confirmar** o custo do trecho curto na IAApi;
  - não planejar a sessão da 6.2 enquanto o p95 do trecho não ficar ≤ ~2,5 s;
  - se a 6.1 confirmar ~4 s, seguir para a 6.5 em modo Comparar, ou investigar antes o custo fixo no Bridge.

### 10.11 Só a cadência fixa (6.3), com o reuso, já daria 1 avaliação por tag por minuto?

**Não.** O trabalho do laço é de **~70,5 s** (PK7 ~9,8 s + PK2 ~60,8 s), maior que 60 s. O resultado depende de como o tique atrasado é tratado:

| Leitura da 6.3 | Como fica o laço | Intervalo por tag | Avaliações/h; cobertura |
|---|---|---|---|
| **Como está no BACKLOG** ("pula o tique, sem acumular": espera o próximo tique) | Um ciclo a cada 120 s, com Warning em todos | ~2,0 min (hoje 2,17) | ~30/h; ~50 % |
| Tique atrasado dispara na hora (comportamento padrão do `PeriodicTimer`: um tique perdido faz a próxima espera terminar logo) | Ciclos emendados, ~71 s cada | **~1,2 min** | ~51/h; **~85 %** |

- Nas duas leituras, **não chega a 1 min**. A condição da própria 6.3 ("ciclo ≤ ~50 s") também não é atendida: o PK2 sozinho leva 61 s.
- A segunda leitura chega perto, mas tem custos:
  - **sobe a carga no Bridge em ~1,8×** (15 requisições a cada ~71 s, em vez de a cada ~131 s). Isso não foi medido, e o P42 mostrou que o Bridge degrada com carga;
  - a idade da leitura não melhora, porque depende da posição na fila e não da cadência (p95 de ~1,7 min).
- **Conclusão:**
  - para a meta de 1 min, a 6.3 sozinha **não dispensa** a 6.1/6.5 (ou a 6.2, se o trecho curto se mostrar barato). Pelos números de hoje, o que falta é tirar a requisição por tag (6.5) ou o custo fixo do Bridge;
  - **para a demo**, a responsável decide se basta:
    - o reuso sozinho: 2,2 min por tag, idade p95 de 1,7 min, pior caso de ~4,4 min;
    - ou a variante "sem espera quando atrasado": ~1,2 min por tag. Seria mudança de cadência, com aprovação própria e uma sessão para medir a carga do Bridge.
- **Atenção para a demo:** o reuso só liga em **Development** (trava do experimento, `Docs/prediction-history-reuse-experiment.md`). Se a demo não rodar em Development, levar o reuso para configuração exige aprovação própria (6.2).

### 10.12 Metas da 6.0 com o reuso

| Métrica | Meta | 6.0b | Situação |
|---|---|---|---|
| Avaliações por tag por hora | ≈ 60/h | 27,8/h | ✗ (≈ metade; era ≈ 1/4) |
| Minutos-tag avaliados ÷ possíveis | ≥ 90 % | 46,3 % | ✗ |
| Duração do ciclo do PK2 | ≤ 60 s | média de 60,8 s; p50 de 61,8 s; máx. de 70,3 s | ✗ (no limite) |
| Idade da leitura p95 | < 2 min | **1,67 min no início; 1,73 min na decisão** | ✓ (primeira vez) |
| Tempo do histórico e consultas por predição | queda medida | 2 → 1 consulta; 9,6 s → 4,6 s | ✓ |
| Timeouts por hora | 0 | 0 | ✓ |
| Nota 0 e entrada inválida | ≈ 0 | 0 inválidas; 3 notas 0 válidas (0,7 %) | ✓ |

### 10.13 A parada do processo (Stop-IAApi) → P45

- **O que se sabe [log + arquivos]:**
  - A última linha do log é de **13:01:35**, no meio da predição da **PK2.TAG27**. As últimas linhas são, em ordem:
    - "Predição iniciada";
    - o bloco `BUSCANDO HISTORICO RECENTE NO BRIDGE` (a busca síncrona de 24 h, `BridgeHistoricoService.BuscarHistorico` → `BuscarJson`, sem cancelamento);
    - o PERF_DIAG de `Prediction.OperationalContext`.
  - Não há `Bridge.Transport` dessa busca nem linha `Application is shutting down`. Isso é esperado com o `Stop-Process -Force`, que não deixa o processo encerrar normalmente.
  - **Momento da parada:** a última decisão gravada é a da TAG26, às 13:01:34,8 (id 3647). A decisão da TAG27 se perdeu, como o próprio script avisa. O export foi às 13:01:29–30.
  - O `dados/logs/iaapi.pid.json` **continua lá**, o que é coerente com a saída 3 do `Stop-IAApi.ps1` ("ainda está vivo após 15 s; arquivo de PID mantido").
  - Na hora desta análise, o processo já não existia (§10.0).
- **Leitura [inferência]:**
  - Não há nada no log depois de 13:01:35. Se o processo tivesse continuado rodando até ~13:05, a busca da TAG27 teria terminado ou estourado o timeout de 60 s, e isso apareceria no log.
  - Então o mais provável é que as threads tenham parado por volta de 13:01:35 e que o **encerramento do processo** tenha passado de 15 s.
- **Hipóteses não verificadas:**
  - E/S de rede síncrona pendente no kernel (a busca ao Bridge em andamento) atrasando o fim do processo;
  - antivírus ou indexação segurando o log ou o banco.
  - O Event Viewer não foi consultado.
- **Risco:** os procedimentos que exigem "IAApi parada" (cópia de segurança da 35.1, migration da 27.3b e esta cópia) podem começar com o processo ainda terminando e o `-wal` aberto. Nesta análise, a conferência (PID, porta, abertura exclusiva) evitou isso.
- Registrado como **P45** (`PROBLEMAS_ENCONTRADOS.md`) e como proposta **36.5** no BACKLOG. Nada foi alterado.
