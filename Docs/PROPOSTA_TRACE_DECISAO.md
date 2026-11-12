# Proposta: trace de decisão da IA real

Data: 2026-11-12 · Branch: `feat/intelligence-lab-e2e-validation` (HEAD `82c78c8`; working tree só com 5 `.patch` não rastreados).

Objetivo: acompanhar a IAApi rodando durante um cenário do simulador, tag por tag e minuto a minuto, e depois comparar **IA real × replay × gabarito** com o subcomando `placar`.

Esta proposta **não altera código**. Nada foi executado: nem a API, nem testes.

> **Status (2026-11-12):** implementação do trace **não aprovada; adiada para depois do regime combinado** (`PLANO_REGIME_COMBINADO.md`). Até lá, a solução provisória é o script `tools/IntelligenceLabDump/Dump-Diagnostics.ps1` (seção 6), que só lê o endpoint de diagnóstico já existente.

Legenda de evidência:
- **[código]**: confirmado lendo o código nesta sessão.
- **[config]**: valor de `appsettings.json` do repositório. Não prova o que está ativo na máquina de teste.
- **[hipótese]**: inferência ainda não demonstrada.

Referências: `MAPA_DETECCAO.md` (MAPA), `PROBLEMAS_ENCONTRADOS.md` P10 (latência do Bridge), P11 (ciclo sequencial), P18 (registro com a hora do processamento).

---

## 1. Observabilidade que já existe

### 1.1 Resumo

| Fonte | Como liga | O que registra | Onde fica | Persistência |
|---|---|---|---|---|
| **PERF_DIAG** | `ASPNETCORE_ENVIRONMENT=Development` **e** `IAAPI_PERF_DIAGNOSTICS=1` (exatamente `1`), lidos uma vez na inicialização | Uma linha de log por etapa medida (duração, status, TraceId/SpanId), mais uma linha `Monitoring.Reading` por tag | Log `Information`, categoria `IAApi.Services.Diagnostics.PerformanceDiagnostics` | Só o provedor de log (console). Não há arquivo |
| **Diagnóstico do Intelligence Lab** (univariado) | `IntelligenceLab:Enabled=true` + 4 chaves `IntelligenceLab:Version:*` obrigatórias | Um registro por leitura: valor, hora da leitura, baseline, detectores, ensemble, decisão runtime e shadow multivariado | Memória (`BoundedDiagnosticBuffer`), lido via `GET /api/intelligence-lab/diagnostics` | **Só memória**: capacidade padrão de 100, máximo de 10.000; os mais antigos são descartados; perde tudo ao reiniciar |
| **Diagnóstico shadow multivariado** | Igual ao anterior (mesmo buffer) | Eventos do vetor: leitura aceita, vetor fechado ou descartado, Mahalanobis executado | Mesmo buffer, via `GET /api/intelligence-lab/diagnostics/multivariate-shadow` | Só memória, fila separada |
| **Logs comuns** | Sempre ligados | Duração do lote no Bridge, início e fim de cada tag, falhas | Console (provedores padrão; nenhum provedor de arquivo no código) | Só se o console for redirecionado |
| **Tabela `AnomaliaDetectada`** | Sempre | **Toda** leitura processada (anomalia ou não) | Banco da IAApi | Permanente |

### 1.2 Detalhe por fonte

**PERF_DIAG** [código] — `Services/Diagnostics/PerformanceDiagnostics.cs`, registrado como singleton em `Services/DependencyInjection/IAApiApplicationServiceCollectionExtensions.cs:31`. Uso descrito em `Docs/performance-diagnostics.md`.
- Etapas medidas: `Prediction.Total`, `Prediction.ActiveConfiguration`, `Prediction.MultivariateShadow`, `Prediction.TrainedProfile`, `Prediction.OperationalContext`, `Prediction.Preparation` (com sub-etapas, incluindo as chamadas HTTP de histórico em `BridgeHistoricoService`), `Prediction.Decision` (sub-etapas em `PredictionDecisionOrchestrationService`), `Prediction.PostDecision`, `Prediction.Finalization`.
- Cada linha traz: `DurationMs`, `StartedUtc`, `TimestampUtc`, `Status`, `TraceId`, `SpanId`, `ParentSpanId`, `HistoricalRequests`, `HistorySource`, horários de início e fim da aquisição do histórico, primeiro e último ponto do histórico, `PredictionReference` (hora usada como "agora").
- A linha de `Prediction.Total` também traz `DetectorScore`, `DetectorAnomaly` (**decisão do ensemble**, não a final do runtime), `ValueTrend` e `DetectorResults` (JSON com nome, aplicável, votou, nota original e nota normalizada de cada detector).
- `Monitoring.Reading` (`MonitoramentoTagsService.ExecuteAsync`, linhas 94–137): hora original da leitura, `Kind`, início e fim da predição e `ReadingAgeAtStartMs`. **A idade só é calculada se o `DataHora` vier com `Kind = Utc`**; com `Unspecified` ou `Local`, sai vazia (`MonitoringReading`, linha 135).
- Não tem: baseline usado, média/desvio, deadband, tipo da tag, decisão final do runtime, resultado do shadow, número do ciclo, ordem da tag no ciclo.

**Diagnóstico do Intelligence Lab** [código]
- Captura: `Services/AnomalyDetection/Observability/PredictionDiagnosticCapture.TryCapture`, chamada no fim de `PredictionService.Detectar` (linhas 298–316), **depois** da finalização. É *fail-open*.
- Contrato: `src/IAApi.Engine/Observability/DiagnosticContracts.cs : IntelligenceLabReadingDiagnostic`. Campos: `ClienteId`, `TagName`, `ReadingTimestampOriginal` + `Kind`, `ReadingTimestampUtc` (só se UTC), `ReadingValue`, `Baseline` (tipo, aplicado, motivo, média, desvio, deadband, confiança, amostras, valor do regime, código do comportamento), `Detectors` (nota original e normalizada, peso, votou), `Ensemble` (nota, limiar, detectores aplicáveis, alertas, soma de pesos), `RuntimeDecision` (legado × final, fonte, motivos), `Multivariate` (resultados shadow finalizados nesta leitura: grupo, nota, limiar, Mahalanobis², minuto, idade), `Timing` (**só o total**), `Context.ReceivedAtUtc`, `CompletedAtUtc`.
- Declarado como **parcial**. Seções que o próprio código marca como ausentes: `Acquisition`, `HistoryQuality`, `DetectorThresholdsAndCalculationReferences`, `RuntimeFunctionalContext`, `HttpCorrelationAndRunId`, `StageObservations` (tempo por etapa).
- Endpoint: `Controllers/IntelligenceLabDiagnosticsController.cs`. Exige ambiente `Development`, chamada de loopback, `IntelligenceLab:ReadAccessKey` com ≥ 32 caracteres e o cabeçalho `X-Intelligence-Lab-Key` (`IntelligenceLabLocalReadAccess`). Devolve no máximo 100 resumos por chamada, sem filtro de período; o detalhe exige uma chamada por ID.
- [config] `IntelligenceLab:Enabled=false` no repositório.

**Shadow multivariado** [código] — `MultivariateShadowRuntimeCoordinator.ProcessarLeituraAsync` e `MultivariateGroupBufferService` publicam `MultivariateShadowDiagnostic` (`src/IAApi.Engine/Observability/MultivariateShadowDiagnostic.cs`): tipo de evento, `VectorId`, grupo, `MinuteIndex`, tags esperadas, leituras (com hora de origem e `SourceAnalysisCorrelationId`), vetor completo, motivo, Mahalanobis executado, anomalia, nota, limiar, `RecordedAtUtc`. Um vetor gera vários eventos e pode fechar na leitura de outra tag. A correlação com o univariado é pelo `correlationId` gerado em `PredictionService` (linhas 101–103), e só existe quando o coletor está ligado.

**Logs comuns** [código] — `MonitoramentoTagsService.ExecuteAsync`: `Bridge lote cliente X: N tags solicitadas em D ms`, `Predição iniciada…`, `Predição finalizada…: Status em D ms`, `Predições cliente X: c/t tags em D ms`, e os warnings de falha (tag não encontrada no lote, perfil não treinado e outros). Fallbacks de baseline usam `Console.WriteLine` (MAPA #5). Não há id de ciclo.

**Tabela `AnomaliaDetectada`** [código] — `Services/AnomaliaHistoricoService.Registrar`: `ValorRecebido`, `Score`, `EhAnomalia`, `LegacyEhAnomalia`, `EmitNewEvidence`, `RuntimeDecisionSource`, motivos (JSON), risco, tendência, mensagem, dependências e `DataDeteccao = DateTime.Now`. **Não grava a hora da leitura (P18), as notas por detector nem o baseline.** É o que o LiveMonitor mostra (`/monitor-api/decisions`).

---

## 2. Dá para reconstruir tag × minuto?

| Informação | Onde existe hoje | Situação |
|---|---|---|
| Hora da leitura (Bridge) | PERF_DIAG `Monitoring.Reading` (`ReadingOriginal`); buffer do Lab (`ReadingTimestampOriginal`) | **Parcial**: no log só com Development + variável; no buffer só em memória. Na tabela, **não existe** (P18) |
| Hora do processamento | `DataDeteccao` na tabela (fim); PERF_DIAG `StartedUtc`/`TimestampUtc`; buffer `ReceivedAtUtc`/`CompletedAtUtc` | **Sim**, mas em fontes diferentes |
| Idade da leitura na decisão | PERF_DIAG `ReadingAgeAtStartMs` | **Só com `Kind = Utc`**. Se o Bridge devolver `Unspecified`, sai vazia [código]. O `Kind` real do Bridge não foi verificado [hipótese: `Unspecified`, pelo comentário em `PredictionDiagnosticCapture`, linha 88] |
| Nota de cada detector | PERF_DIAG `DetectorResults`; buffer `Detectors` | **Sim**, mas sem aplicabilidade/motivo no PERF_DIAG e sem contribuição ponderada em nenhum dos dois de forma direta |
| Nota final e limiar | Tabela `Score`; PERF_DIAG `DetectorScore`; buffer `Ensemble` | **Sim** |
| Decisão final (runtime) | Tabela `EhAnomalia`; buffer `RuntimeDecision.FinalAnomaly` | **Sim**. O PERF_DIAG só tem a do ensemble |
| Perfil/baseline usado (tipo, média, desvio, deadband, regime) | Buffer `Baseline` | **Só no buffer em memória** |
| Tipo da tag, confiança, bloqueio por roteamento (P20) | `DetectorResult.Metricas` (`tipoComportamental`, `confiancaTipoComportamental`, `roteamentoTipoAtivo`) | **Calculado, mas não registrado** em nenhuma fonte |
| Entrada inválida (P19) | Só log/diagnóstico interno | **Não registrado de forma consultável** |
| Pontos de histórico, gaps | PERF_DIAG `ReceivedRecords` e primeiro/último ponto | **Parcial**. A seção `HistoryQuality` do buffer não é preenchida |
| Tempo por etapa | PERF_DIAG | **Sim**, mas só no console, uma linha por etapa, e cruzar as linhas exige o `TraceId` |
| Ciclo, ordem da tag no ciclo, tempo do lote (P11) | Logs comuns, sem id | **Não dá para agrupar** de forma confiável |
| Shadow multivariado | Buffer (`Multivariate` + fila shadow) | **Só em memória** |

**Conclusão.** Juntando PERF_DIAG + buffer do Lab + tabela, quase todos os dados existem em algum lugar. Hoje, porém, **não é viável reconstruir um cenário inteiro**:
1. **Nada persiste junto.** O PERF_DIAG vai para o console; o buffer guarda no máximo 10.000 registros em memória (com 30 tags, cerca de 5 h) e é lido de 100 em 100; a tabela não tem a hora da leitura nem os detectores.
2. **Não há chave comum.** O `TraceId` do PERF_DIAG e o `DiagnosticId`/`correlationId` do Lab são gerados de forma independente.
3. **Faltam campos:** tipo e roteamento, entrada inválida, id do ciclo e ordem da tag, tempo do lote, tempo por etapa junto da decisão, e idade da leitura quando o `Kind` não é UTC.
4. **Os gates são frágeis para um ensaio**: PERF_DIAG e endpoint exigem `Development`; o Lab exige metadados de versão e chave de acesso.
5. **O formato é diferente do CSV do replay**, então o `placar` não consegue ler.

---

## 3. Proposta: trace de decisão mínimo

### 3.1 Princípios

- **Desligado por padrão**: `DecisionTrace:Enabled=false` em `appsettings.json`. Liga por configuração ou por variável de ambiente (`DecisionTrace__Enabled=true`), sem depender de `Development`.
- **Somente leitura dos objetos já calculados.** O registro é montado **depois** de `PredictionFinalizationService.Finalizar`, no mesmo ponto onde já roda `PredictionDiagnosticCapture.TryCapture`. Não recalcula o ensemble nem chama o Bridge.
- **Fail-open**: tudo dentro de `try/catch`; erro de disco nunca chega à predição.
- **Sem I/O no ciclo.** O registro vai para uma fila em memória limitada (`Channel`, `DropWrite` quando cheia, com contador de descartes). Uma tarefa de fundo grava em disco.
- **Um arquivo por dia**: `{Directory}/decision_trace_{yyyy-MM-dd}.csv`. O dia é o da hora de processamento local, com cabeçalho em cada arquivo. Diretório padrão: `logs/decision-trace/`, fora do controle de versão (adicionar ao `.gitignore`). **O arquivo tem valores industriais**: tratar como dado sensível do cliente; não versionar nem enviar.

### 3.2 Colunas

**Prefixo idêntico ao CSV do replay** (`Cp300Replay.Colunas`, mesma ordem e mesmo formato; uma linha por detector, ou uma linha sem detector quando não há detalhes):

`data_hora, cliente, tag, valor, detector, aplicavel, participou, motivo_nao_aplicavel, nota_detector, peso, contribuicao, nota_final, limiar, decisao_ensemble, decisao_final, tipo_tag, confianca_tipo, roteamento_por_tipo, deadband, origem_deadband, confianca_deadband, origem_baseline, media, desvio, pontos_historico, entrada_invalida, motivo_entrada_invalida, erro, conferencia, valor_regime, motivo_baseline`

De onde vem cada coluna na IA real (sem recálculo, exceto onde indicado):

| Coluna | Origem em `PredictionService.Detectar` |
|---|---|
| `data_hora` | `dataHoraAtual` = `DataHora` do Bridge (**hora da leitura**), no mesmo formato do replay (`yyyy-MM-ddTHH:mm:ss`) |
| `valor` | `request.Valor` (já convertido para `float`, como no replay) |
| detector, aplicável, participou, motivo, nota, peso | `resultadoDetector.DetalhesDetectores` (mesmos campos que `Cp300Replay.FillDetail` usa) |
| `contribuicao` | nota × peso / `somaPesosAplicaveis` (mesma fórmula do replay; aritmética sobre valores já calculados) |
| `nota_final`, `limiar`, `decisao_ensemble` | `resultadoDetector.ScoreNormalizado`, `Metricas["limiteEnsemble"]`, `resultadoDetector.EhAnomalia` |
| `decisao_final` | `runtimeDecision.EmitAnomaly` (o que foi gravado na tabela) |
| `tipo_tag`, `confianca_tipo`, `roteamento_por_tipo` | `Metricas["tipoComportamental"]`, `["confiancaTipoComportamental"]`, `["roteamentoTipoAtivo"]` |
| `media`, `desvio`, `pontos_historico`, `origem_baseline`, `valor_regime`, `motivo_baseline` | `preparation.EntradaDetector` e `preparation.BaselineEfetivo` |
| `deadband`, `origem_deadband`, `confianca_deadband` | `BaselineProtectionProfile.Build(entrada, media)`, a mesma chamada do replay. **É um recálculo**; aceitável só se a função for pura (confirmar na implementação). Alternativa: preencher com `BaselineEfetivo.Deadband` e marcar `origem_deadband=baseline` |
| `entrada_invalida`, `motivo_entrada_invalida` | Do resultado de entrada inválida do ensemble (`CriarResultadoEntradaInvalida`, que já devolve as métricas de qualidade). Se não trouxer o motivo, usar `DetectionInputValidator.Validate` (pura) |
| `erro` | Tipo e mensagem curta da exceção quando a tag falha (tag não encontrada no lote, perfil não treinado, timeout do Bridge) |
| `conferencia` | Constante `ia_real` (distingue do replay) |

**Colunas extras, no fim** (o `placar` lê por nome, então o prefixo continua compatível):

| Coluna | Conteúdo | Ajuda a responder |
|---|---|---|
| `hora_leitura_kind` | `Kind` do `DataHora` do Bridge | Se a idade é confiável |
| `hora_inicio_processamento` | Início da tag no monitor, ISO com fuso (`yyyy-MM-ddTHH:mm:ss.fffzzz`) | P18 |
| `hora_fim_processamento` | Fim (≈ `DataDeteccao` gravada na tabela) | P18 |
| `idade_leitura_ms` | `hora_inicio_processamento − data_hora`, calculada **no mesmo fuso local** quando o `Kind` não é UTC (marcada em `hora_leitura_kind`) | P10, P11 |
| `ciclo_id`, `ordem_no_ciclo`, `tags_no_ciclo` | Contador do loop e posição da tag no lote | P11 |
| `ms_lote_bridge` | Tempo de `BuscarValoresAtuais` do lote | P10 |
| `ms_total`, `ms_shadow`, `ms_contexto`, `ms_preparacao`, `ms_historico`, `ms_decisao`, `ms_pos_decisao`, `ms_finalizacao` | Tempo por etapa (Stopwatch); `ms_historico` é a soma das chamadas HTTP de histórico, contida em `ms_preparacao` | P10, P11 |
| `requisicoes_historico` | Quantidade de GETs de histórico nesta leitura (perfil, regime, pós-decisão) | P10 |
| `historico_primeiro`, `historico_ultimo` | Primeiro e último ponto do histórico usado | Defasagem do histórico |
| `fonte_decisao`, `legado_anomalia` | `runtimeDecision.Source`, `LegacyFinalAnomaly` | Canário/runtime |
| `mv_grupos`, `mv_minuto`, `mv_idade_min`, `mv_executado`, `mv_anomalia`, `mv_nota`, `mv_limiar`, `mv_motivo` | Resultados shadow **finalizados nesta leitura** (`multivariateShadowResults`), vários separados por `\|`; vazio se o coordenador não produziu resultado | Comparação com o univariado |
| `trace_id` | GUID por leitura; se o Intelligence Lab estiver ligado, o mesmo `diagnosticCorrelationId` | Cruzar com o buffer e o shadow |

Volume estimado: cerca de 7 linhas por leitura × 1.440 min × N tags. Com 30 tags, ~300 mil linhas por dia, entre 90 e 120 MB por dia [estimativa]. É aceitável em sessões de teste. Uma opção `DecisionTrace:LinhaPorDetector=false` poderia gerar uma linha por leitura, mas aí perde a compatibilidade direta com o replay; **não** recomendo nesta primeira versão.

### 3.3 Desenho e arquivos afetados

| Arquivo | Mudança | Tipo |
|---|---|---|
| `Services/Diagnostics/DecisionTrace/DecisionTraceOptions.cs` | **Novo**: `Enabled`, `Directory`, `QueueCapacity` | Novo |
| `Services/Diagnostics/DecisionTrace/DecisionTraceScope.cs` | **Novo**: contexto por leitura em `AsyncLocal` (como o `_current` do PERF_DIAG), com hora de início, ciclo, ordem, tempos por etapa e dados da decisão. Sem trace ligado, `Current` é `null` e nada é alocado | Novo |
| `Services/Diagnostics/DecisionTrace/DecisionTraceWriter.cs` | **Novo**: singleton + `BackgroundService`; fila limitada; gravação CSV com o mesmo escape do replay; troca de arquivo à meia-noite; `flush` periódico e no desligamento; contador de descartes e de erros de escrita | Novo |
| `Services/Diagnostics/DecisionTrace/DecisionTraceColumns.cs` | **Novo**: lista de colunas (prefixo = replay + extras) | Novo |
| `Services/DependencyInjection/IAApiApplicationServiceCollectionExtensions.cs` | Registrar opções e writer **somente se** `Enabled=true` (mesmo padrão de `AddIntelligenceLabObservability`) | Alterado (registro) |
| `Services/MonitoramentoTagsService.cs` : `ExecuteAsync` | Contador de ciclo; abrir `DecisionTraceScope` por tag (ciclo, ordem, `ms_lote_bridge`, hora da leitura do lote); no `finally`, fechar o escopo, o que enfileira a linha, inclusive com `status/erro` quando a tag falha | Alterado (≈ 15 linhas) |
| `Services/PredictionService.cs` : `Detectar` | Se `DecisionTraceScope.Current` não for nulo: marcar tempos entre as etapas existentes e, **depois** de `Finalizar`, copiar `preparation`, `resultadoDetector`, `runtimeDecision` e `multivariateShadowResults` para o escopo dentro de `try/catch` | Alterado (≈ 20 linhas, só leitura) |
| `Services/BridgeHistoricoService.cs` : `BuscarHistorico` / `BuscarHistoricoPorPeriodo` | Somar duração e quantidade das chamadas HTTP no escopo atual (uma linha, como `PerformanceDiagnostics.HistoricalRequest`) | Alterado (mínimo) |
| `appsettings.json` | `"DecisionTrace": { "Enabled": false, "Directory": "logs/decision-trace" }` | Config |
| `.gitignore` | `logs/decision-trace/` | Config |
| `IAApi.Tests/Diagnostics/DecisionTrace*Tests.cs` | **Novos** (ver 3.5) | Testes |

Não são afetados: detectores, `EnsembleDetectionService`, thresholds, baselines, runtime, fusão multivariada, `AnomaliaHistoricoService`, contratos HTTP e LiveMonitor.

Chamadas manuais pela API (`/detectar`) não abrem escopo e, portanto, não geram trace nesta versão. Podem ser incluídas depois com `origem=http`.

### 3.4 Riscos

| Risco | Nível | Mitigação |
|---|---|---|
| Alterar a decisão | **Baixo** | Leitura feita só depois de `Finalizar`; nenhum objeto da decisão é modificado; `try/catch` em volta. O único recálculo (`BaselineProtectionProfile.Build` / `Validate`) roda depois da decisão e precisa ser confirmado como puro |
| Atrasar o ciclo (piorar P11) | Baixo | Sem I/O no caminho; fila limitada com descarte contado; custo ≈ cópia de ~7 registros por tag |
| Disco cheio | Médio em sessões longas | Um arquivo por dia; desligado por padrão; opcionalmente, `MaxDias` com limpeza |
| Vazamento de dados industriais | Médio | Diretório fora do Git, documentação, sem envio remoto; ligar só em ensaio |
| Divergência de formato com o replay | Baixo | Teste de contrato do cabeçalho (3.5) |
| Fuso/`Kind` da hora da leitura | Médio | Gravar o valor original + `Kind`; o `placar` compara no mesmo relógio do gabarito (ver 4.3) |

### 3.5 Testes de aceite (offline)

1. **Desligado ≡ hoje**: com `Enabled=false`, nenhum serviço registrado, nenhum arquivo, e todos os testes atuais passam sem mudança.
2. **Ligado não muda a decisão**: com fakes de Bridge e banco em memória (como no replay), a mesma sequência de leituras dá `AnomaliaResponse` e linhas de `AnomaliaDetectada` idênticas com e sem o trace.
3. **Falha de escrita não propaga**: diretório inválido ou somente leitura ⇒ predição normal, contador de erros > 0.
4. **Contrato de colunas**: as 31 primeiras colunas são idênticas a `Cp300Replay.Colunas`.
5. **Equivalência com o replay** (o teste mais forte): ligar o trace dentro do `Cp300OfflineReplay` (que já monta um `PredictionService` real com histórico de CSV) e verificar que, para o mesmo CSV de entrada, as colunas de prefixo do trace são **idênticas** ao CSV do replay (exceto `conferencia`). Isso prova que o trace descreve a mesma coisa que o replay.
6. **Fila cheia**: descarta e conta, sem bloquear.

### 3.6 Rollback

Instantâneo: `DecisionTrace:Enabled=false` e reiniciar. Definitivo: reverter o commit. Não há migração de banco nem mudança de contrato.

### 3.7 Esforço

- Trace (código + testes 1–4 e 6): **~1 a 1,5 dia**.
- Teste 5 (equivalência com o replay): **+0,5 dia**.

---

## 4. Como o `placar` leria o trace

### 4.1 O que já funciona sem mudar nada [código]

`Cp300Placar.ReplayAlarmes.Load` (`tools/Cp300OfflineReplay/Cp300Placar.cs`, linhas ~644–690) lê o CSV **pelo nome das colunas** `data_hora`, `tag`, `decisao_final` e `erro`, arredonda para o minuto e deduplica por (tag, minuto) com `SortedSet`. Como o trace tem essas colunas com o mesmo formato, **`placar --replay decision_trace_2026-11-13.csv --gabarito dados.csv --saida ia_real` já produziria o placar da IA real**, com estas ressalvas:
- **Um arquivo por vez**: um cenário que atravessa a meia-noite exige concatenar os arquivos ou aceitar vários `--replay`.
- **Cobertura**: a IA real avalia uma vez por ciclo (≥ 60 s + processamento, P11). Haverá minutos sem leitura por tag, e o placar os trata como "não avaliados". Isso é correto, mas precisa aparecer no relatório.
- **Leitura repetida**: se o Bridge devolver o mesmo `DataHora` em dois ciclos, o minuto é contado uma vez. Também está correto.

### 4.2 Extensão proposta: comparação em três vias

Novo uso:

```
placar --replay replay.csv --ia-real decision_trace_*.csv --gabarito dados.csv --saida prefixo
       [--mapa ...] [--janela-antes 20] [--duracao-minima 3] [--tolerancia-buraco 2] [--cliente PK2]
```

Saídas adicionais:
1. **`prefixo_eventos.csv` com colunas por fonte**: `detectado_replay`, `detectado_ia_real`, `primeiro_alarme_replay`, `primeiro_alarme_ia_real` (hora da leitura), `primeiro_alarme_ia_real_processado` (hora do processamento: o que o operador viu) e `atraso_processamento_s`.
2. **`prefixo_divergencias.csv`**, uma linha por (tag, minuto) em que `decisao_final` difere entre replay e IA real, ou em que só um dos lados avaliou. Inclui as notas finais dos dois lados, `media`/`desvio`/`pontos_historico`/`origem_baseline` dos dois lados, `idade_leitura_ms`, `ordem_no_ciclo`, `entrada_invalida` e uma **causa provável** classificada por regra simples:
   - `nao_avaliado_ia_real` — minuto sem leitura na IA real (P11, ou falha do lote ou da tag);
   - `baseline_diferente` — média/desvio diferem acima de uma tolerância (perfil treinado em outro período, P13);
   - `historico_diferente` — `pontos_historico` ou `historico_ultimo` diferem (Bridge lento, timeout, gap; P10);
   - `leitura_atrasada` — `idade_leitura_ms` acima de um limite (P10, P11);
   - `entrada_invalida` (P19) ou `roteamento_diferente` (P20);
   - `nota_diferente_sem_causa` — fica para investigação.
3. **`prefixo_latencia.csv`** por tag: cobertura (% de minutos avaliados), idade da leitura (mediana, p95, máx.), `ms_total` e `ms_historico` (mediana, p95), duração do ciclo e posição média no ciclo.
4. No `.md`: uma tabela de três colunas (gabarito × replay × IA real) por evento, mais um alerta se o baseline da IA real for diferente do baseline do replay (sem isso a comparação não é justa).

Arquivos afetados (ferramenta offline, sem efeito na API):
- `tools/Cp300OfflineReplay/Program.cs`: argumento `--ia-real` (aceitando vários arquivos ou um padrão).
- `tools/Cp300OfflineReplay/Cp300Placar.cs`: generalizar `ReplayAlarmes.Load` para ler também `nota_final`, as colunas de baseline e as extras, sem falhar quando faltarem; novo comparador e novos relatórios em `Cp300PlacarRelatorio`.
- `tools/Cp300OfflineReplay/README.md` e testes do placar.

Esforço: **~1 dia** (+0,5 dia de testes com CSVs sintéticos).

### 4.3 Cuidados de comparação

- **Mesmo relógio**: `data_hora` do trace vem de `BuscarValoresAtuais`, e o gabarito vem da exportação do histórico (`Cp300Export`). Os dois saem do Bridge, mas é preciso confirmar que usam o mesmo fuso e o mesmo `Kind` [hipótese]. Primeiro passo do ensaio: comparar algumas leituras da TAG73 nos dois arquivos.
- **Mesmo baseline**: rodar o replay com o mesmo período de treino que gerou o `TagPerfilIa` da IA real. O trace grava `media`/`desvio`, o que permite conferir.
- **Mesmo `appsettings`** (P16) e mesma configuração de regime (`--regime` no replay × cadastro no banco).
- **Simulador não é planta real**: um ensaio com o trace valida o comportamento da IAApi diante do simulador, não a detecção em produção.

---

## 5. Próximos passos sugeridos (dependem de aprovação)

0. **Agora:** usar a solução provisória (seção 6) nos ensaios do regime combinado, se o Intelligence Lab puder ser ligado na máquina de teste.
1. Depois do regime combinado: aprovar o desenho do trace (seção 3) e confirmar o diretório e a retenção.
2. Implementar o trace com os testes 1–6.
3. Estender o `placar` (seção 4.2).
4. Ensaio: ligar o trace, rodar um preset já medido no replay (por exemplo, o preset 1), exportar o gabarito e rodar o placar em três vias.

---

## 6. Solução provisória: `tools/IntelligenceLabDump/Dump-Diagnostics.ps1`

Script PowerShell (compatível com Windows PowerShell 5.1) que **só faz GET** no endpoint de diagnóstico existente e grava em disco. Não altera a IAApi, a configuração nem a decisão. **Situação:** a sintaxe foi validada com o parser do PowerShell 5.1 (0 erros); **o script ainda não foi executado contra a API nem com dados**.

### 6.1 O que lê e o que grava

| Rota (GET) | Uso |
|---|---|
| `/api/intelligence-lab/health` | Contadores do buffer no início e no fim (`storedTotal`, `evictedTotal`, `bufferCapacity`), para estimar perdas |
| `/api/intelligence-lab/diagnostics?limit=100[&clienteId][&tagName]` | Lista das leituras univariadas |
| `/api/intelligence-lab/diagnostics/{id}` | Detalhe de cada leitura nova (detectores, ensemble, baseline, decisão, shadow) |
| `/api/intelligence-lab/diagnostics/multivariate-shadow?limit=100[&clienteId][&groupName]` | Fila do shadow multivariado |
| `/api/intelligence-lab/diagnostics/multivariate-shadow/{id}` | Detalhe de cada evento shadow novo |

Saídas em `dados/` (já está no `.gitignore`), com prefixo `ia_real_<yyyy-MM-dd_HHmm>` (hora de início da coleta, para não sobrescrever outra coleta do mesmo dia):
- `.json`: `meta` (parâmetros, passadas, `health` no início e no fim, possíveis lacunas) + `leituras` + `shadow`, como vieram da API;
- `.csv`: **as 31 colunas do CSV do replay, na mesma ordem**, uma linha por detector, mais colunas extras no fim (`hora_leitura_kind`, `hora_leitura_utc`, `recebido_utc`, `concluido_utc`, `ms_total`, `fonte_decisao`, `legado_anomalia`, `motivos_runtime`, `amostras_baseline`, `completude`, `secoes_ausentes`, `mv_*`, `diagnostic_id`, `correlation_id`);
- `_shadow.csv`: um evento shadow por linha (grupo, vetor, tipo de evento, minuto, vetor completo, Mahalanobis, anomalia, nota, limiar, leituras `tag=valor@hora`).

Com `-ArquivoJson <arquivo>`, o script **não acessa a API**: só regenera os CSVs a partir de um JSON já salvo.

### 6.2 "De 100 em 100, até acabar": limitação do endpoint

O endpoint **não tem paginação** [código: `IntelligenceLabDiagnosticsController.List` faz `Reverse().Take(limit)`, com `limit` ≤ 100 e só filtros exatos de cliente/tag/grupo]. Não existe "página 2". Por isso o script:
1. pede 100 sem filtro de tag, descobre as tags e depois pede **100 por tag** (e 100 por grupo shadow);
2. busca o detalhe só dos IDs ainda não vistos (deduplicação por `diagnosticId`);
3. com `-IntervaloSegundos N`, repete a cada N segundos até `-DuracaoMinutos` ou Ctrl+C, regravando os arquivos a cada passada;
4. sinaliza **possível lacuna** quando um filtro devolve 100 registros todos novos (os mais antigos podem ter ficado inalcançáveis) e quando um detalhe já foi descartado do buffer (404).

Consequências práticas:
- Uma única passada cobre no máximo as **100 últimas leituras de cada tag** (~100 min a 1 leitura/min).
- O buffer da IAApi guarda `IntelligenceLab:BufferCapacity` registros **por fila** (padrão 100, máximo 10.000) e descarta os mais antigos. Com N tags, o buffer cobre cerca de `BufferCapacity / N` minutos. **Com o padrão 100 e 30 tags, são só ~3 min.** Para um cenário, usar `BufferCapacity` alto (ex.: 10.000 ≈ 5 h com 30 tags) e `-IntervaloSegundos` bem menor que essa janela (ex.: 300).
- No fim, o script compara quantos registros o buffer gravou durante a coleta (`storedTotal`, somando univariado e shadow) com quantos foram lidos.

### 6.3 Proteção do endpoint e chave

O script **não contorna** a proteção (`Controllers/IntelligenceLabLocalReadAccess.cs`):
- a IAApi precisa estar em `Development`, com `IntelligenceLab:Enabled=true`, as quatro chaves `IntelligenceLab:Version:{ApiVersion, BuildIdentifier, CommitIdentifier, AlgorithmConfigurationVersion}` e `IntelligenceLab:ReadAccessKey` com ≥ 32 caracteres. Sem isso, a API responde 404, e o script avisa e para;
- `-BaseUrl` só aceita `localhost`, `127.0.0.1` ou `[::1]` (padrão `http://localhost:5145`, de `Properties/launchSettings.json`);
- a chave vai no cabeçalho `X-Intelligence-Lab-Key`; 401 indica chave recusada.

**Onde está a chave** [código/config, verificado sem ler valores]:
- **Na IAApi:** a configuração `IntelligenceLab:ReadAccessKey`. **Não está em nenhum `appsettings*.json` nem em `Properties/launchSettings.json` do repositório**, e o projeto não usa user-secrets (sem `UserSecretsId`). Ela precisa ser fornecida por quem sobe a API, por exemplo pela variável de ambiente `IntelligenceLab__ReadAccessKey` no processo da IAApi, ou pelo procedimento local de teste da equipe.
- **No LiveMonitor:** o mesmo valor é usado como `IAAPI_DIAGNOSTICS_KEY` no `tools/IAApi.LiveMonitor/.env` (ignorado pelo Git; não foi lido nesta sessão).

**Como passar ao script** (a chave nunca é impressa nem gravada nos arquivos):
- variável de ambiente, por padrão `IAAPI_DIAGNOSTICS_KEY` (nome configurável com `-VariavelChave`), definida na sessão do PowerShell antes de rodar; ou
- `-PedirChave`: o script pede a chave com entrada oculta (`Read-Host -AsSecureString`).

Ligar o Intelligence Lab é **mudança de configuração da IAApi** e precisa de autorização. O coletor é passivo e roda depois da finalização da decisão (`PredictionService.Detectar`, linhas 298–316), mas acrescenta trabalho em memória a cada leitura.

### 6.4 Mapeamento para as colunas do replay

| Coluna do replay | Origem no diagnóstico | Observação |
|---|---|---|
| `data_hora` | `readingTimestampOriginal` (texto truncado em segundos, sem conversão de fuso) | Hora da leitura do Bridge |
| `cliente`, `tag`, `valor` | `clienteId`, `tagName`, `readingValue` | |
| `detector`, `aplicavel`, `participou`, `motivo_nao_aplicavel`, `peso` | `ensemble.contributions[]` (`detector`, `applicable`, `participatedInAggregation`, `nonApplicabilityReasonCode`/`executionReasonCode`, `configuredWeight`) | Sem `contributions`, usa `detectors[]` (`status = Executed` ⇒ aplicável; `participou` fica vazio) |
| `nota_detector` | `contributions[].normalizedScore` ou `detectors[].normalizedScore` | |
| `contribuicao` | nota × `detectors[].weight` / `ensemble.applicableWeightSum`, só se participou | Mesma fórmula do replay |
| `nota_final`, `limiar`, `decisao_ensemble`, `decisao_final` | `ensemble.normalizedScore`, `ensemble.threshold`, `ensemble.legacyAnomaly`, `runtimeDecision.finalAnomaly` | |
| `origem_baseline`, `media`, `desvio`, `valor_regime`, `motivo_baseline` | `baseline.kind` (Global/OperationalRegime/BehaviorMemory), `mean`, `standardDeviation`, `regimeValue`, `reason` | |
| `deadband`, `confianca_deadband` | `baseline.deadband`, `baseline.deadbandConfidence`; `origem_deadband = baseline_lab` | **Diferente do replay**: o replay grava o deadband efetivo (`BaselineProtectionProfile`) |
| `pontos_historico` | `historyQuality.validPointCount` | Hoje **sempre vazio**: a seção não é preenchida pela captura |
| `tipo_tag`, `confianca_tipo`, `roteamento_por_tipo`, `entrada_invalida`, `motivo_entrada_invalida` | — | **Vazios**: não capturados pelo Lab (lacunas da seção 2) |
| `erro` | `failure.code` | Leituras que falham antes do fim (tag fora do lote, perfil não treinado) **não geram diagnóstico** |
| `conferencia` | constante `ia_real_lab` | |

### 6.5 Uso com o `placar` e limitações

- Como o CSV tem `data_hora`, `tag`, `decisao_final` e `erro` com os mesmos nomes e formato, **`placar --replay dados/ia_real_<...>.csv --gabarito <dados.csv> --saida <prefixo>` deve funcionar sem mudança** (seção 4.1). Isso ainda não foi testado.
- Continuam valendo os cuidados da seção 4.3 (mesmo relógio, mesmo baseline, mesmo `appsettings`).
- Em relação ao trace proposto, faltam: tempo por etapa (só o total), hora de início do processamento (há `recebido_utc`), ciclo e posição da tag, tempo do lote do Bridge, tipo e roteamento, entrada inválida, leituras que falharam, e garantia de não perder registros (depende do polling e do `BufferCapacity`). Para tempos por etapa, combinar com PERF_DIAG (console) quando possível.
- O arquivo contém valores industriais: manter em `dados/` (fora do Git) e não compartilhar fora da equipe autorizada.
