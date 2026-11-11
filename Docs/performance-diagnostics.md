# Diagnóstico de performance da predição

Este documento registra a etapa inicial de instrumentação. O experimento posterior de reutilização opcional do histórico bruto está documentado em [prediction-history-reuse-experiment.md](prediction-history-reuse-experiment.md), incluindo a diferença entre snapshots e o gate próprio de Development.

Instrumentação exclusivamente de observabilidade, opt-in, sem cache, reutilização de histórico ou concorrência nova. Não foi realizado commit.

## Ativação

No PowerShell, antes de iniciar uma nova instância da aplicação:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:IAAPI_PERF_DIAGNOSTICS = '1'
dotnet run
```

O ambiente efetivo do host deve ser `Development`. Um `DOTNET_ENVIRONMENT` ou perfil de lançamento pode influenciar esse ambiente. O valor da variável diagnóstica deve ser exatamente `1`; valores ausentes, `0`, `true` e outros valores desabilitam a coleta. Fora de Development, `1` também não habilita o diagnóstico. Os logs são emitidos em Information na categoria `IAApi.Services.Diagnostics.PerformanceDiagnostics`; o provedor/filtro de logging deve permitir essa categoria.

Para desabilitar e iniciar novamente:

```powershell
$env:IAAPI_PERF_DIAGNOSTICS = '0'
dotnet run
```

A decisão de ativação é lida uma vez, na criação do serviço singleton; alterar a variável exige reiniciar a instância. Nenhuma configuração de cliente, tag, CP300 ou grupo multivariado é alterada.

## Arquivos desta implementação

As alterações locais preexistentes foram preservadas. Esta lista descreve somente os arquivos tocados nesta tarefa.

| Arquivo | Justificativa |
| --- | --- |
| `Services/Diagnostics/PerformanceDiagnostics.cs` | Gate Development + variável, Stopwatch, logs estruturados, correlação e isolamento de falhas de logging. |
| `Services/DependencyInjection/IAApiApplicationServiceCollectionExtensions.cs` | Registra uma instância compartilhada do mecanismo; ausência de ambiente conhecido mantém o diagnóstico desligado. |
| `Services/PredictionService.cs` | Total e etapas de configuração, shadow, perfil, contexto, preparação, decisão, análise posterior e finalização. |
| `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs` | Histórico inicial, conversão, resolução dos dois baselines, montagem/sanitização do DetectorInput e snapshot diagnóstico. |
| `Services/BridgeHistoricoService.cs` | Transporte HTTP bloqueante existente, leitura da resposta bufferizada, desserialização, conversão, status HTTP e contagens. |
| `Services/AnomalyDetection/Orchestration/PredictionDecisionOrchestrationService.cs` | Ensemble, contexto funcional, decisão operacional, fusão multivariada e interpretação. |
| `Services/AnomalyDetection/Orchestration/PredictionPostDecisionAnalysisService.cs` | Análises relacionadas, risco, interpretação multivariada posterior, tendências, feedback e formatação. |
| `Services/AnomalyDetection/Orchestration/PredictionTrendAnalysisService.cs` | Separa tendência de risco da tendência de valor. |
| `Services/TendenciaValorService.cs` | Separa consulta histórica do cálculo de tendência de valor, preservando retornos antecipados. |
| `Services/MonitoramentoTagsService.cs` | Acrescenta timestamps da leitura e da predição, preservando cronômetros, ordem sequencial e intervalo existentes. |
| `IAApi.Tests/PerformanceDiagnosticsTests.cs` | Gate, hierarquia, logging defeituoso, falhas, cancelamentos, fail-open, transporte Bridge simulado e timestamps. |
| `IAApi.Tests/IntelligenceLab/PredictionServiceObservabilityEquivalenceTests.cs` | Compara respostas, detector, histórico persistido e quantidade de consultas com o diagnóstico ligado/desligado. |
| `Docs/performance-diagnostics.md` | Operação, interpretação dos logs, validação e limitações. |

## Formato real dos logs

Exemplo efetivamente capturado no teste local do mecanismo, com cliente/tag fictícios e operação trivial; as durações não representam o CP300:

```text
PERF_DIAG Client=C Tag=T Stage=Child DurationMs=0.0015 TimestampUtc=2026-11-05T18:55:12.4010049+00:00 StartedUtc=2026-11-05T18:55:12.4010031+00:00 Status=Succeeded TraceId=5c25f7b5817840a18e755741478382e6 SpanId=d2b7418dbf034492ba69cd4128db11dd ParentSpanId=5c25f7b5817840a18e755741478382e6 ReceivedRecords=(null) ValidRecords=(null) HttpStatus=(null)
PERF_DIAG Client=C Tag=T Stage=Total DurationMs=0.0271 TimestampUtc=2026-11-05T18:55:12.4010270+00:00 StartedUtc=2026-11-05T18:55:12.4010000+00:00 Status=Succeeded TraceId=5c25f7b5817840a18e755741478382e6 SpanId=5c25f7b5817840a18e755741478382e6 ParentSpanId=(null) ReceivedRecords=(null) ValidRecords=(null) HttpStatus=(null)
```

Os mesmos campos são usados nas etapas reais, como `Prediction.Total`, `Prediction.Preparation`, `Preparation.History`, `ValueTrend.History`, `Bridge.Transport` e `Bridge.Decode`. `Status` é `Succeeded`, `Failed` ou `Cancelled`. Não são anexadas exceções, mensagens de erro, URLs, payloads ou credenciais aos novos logs. Os logs legados permanecem com seu comportamento existente.

`Bridge.Decode` registra `ReceivedRecords` após desserializar e `ValidRecords` após a conversão, filtragem e deduplicação existentes. Quando não é possível chegar a uma dessas etapas, o respectivo campo fica nulo. `Bridge.Transport` registra `HttpStatus` quando a resposta foi obtida e lida.

`Monitoring.Reading` acrescenta `ReadingOriginal`, `ReadingKind`, `ReadingUtc`, `PredictionStartedUtc`, `PredictionCompletedUtc` e `ReadingAgeAtStartMs`. Datas ausentes, padrão, Local ou Unspecified não são convertidas para um fuso presumido. A idade fica nula nesses casos e quando uma leitura UTC está no futuro. O timestamp original e seu Kind continuam visíveis para investigação. Durações usam Stopwatch, independentemente de ajustes no relógio de parede.

## Hierarquia e interpretação

`TraceId` correlaciona a predição; `SpanId` identifica uma medição e `ParentSpanId` identifica sua medição externa. O contexto é isolado por fluxo assíncrono e não é consumido pelos detectores. Nenhum DbContext é armazenado nesse mecanismo.

As durações são **inclusivas**. `Prediction.Total` inclui preparação, decisão, análise posterior, finalização e a captura diagnóstica preexistente. `Preparation.History` e `ValueTrend.History` distinguem os chamadores sem mudar `IHistoricoService`. As medições internas do Bridge herdam o vínculo com o chamador. Uma falha interna de baseline pode ter Status=Failed, enquanto a preparação e a predição concluem com sucesso pelo fail-open existente.

Para agregar, some apenas etapas irmãs de um mesmo pai. Não some o total com seus filhos, nem `Preparation.History` com seus descendentes do Bridge. O restante do tempo do pai inclui código entre etapas, logs legados e o custo do próprio diagnóstico. Etapas condicionais ausentes não geram uma duração artificial de zero. Não há agregação automática que duplique essas contagens.

O `GetAsync` continua usando seu comportamento bufferizado padrão: `Bridge.HttpBuffered` inclui obtenção da resposta e download do corpo. `Bridge.ReadBufferedResponse` mede o `ReadAsStringAsync` subsequente sobre o conteúdo bufferizado. Não representa uma segunda transferência de rede. Mudar para ResponseHeadersRead permitiria outra decomposição, mas alteraria a semântica atual e não foi feito.

## Validação

Os testes usam Bridge simulado por HttpMessageHandler, dados sintéticos e SQLite isolado em memória. Não acessam o CP300 para validar a instrumentação.

- Testes direcionados finais: 25 aprovados, 0 falhos, 0 ignorados.
- Suíte completa final: 741 aprovados, 0 falhos, 0 ignorados.
- No cenário de predição comparado: uma execução do detector, um resultado persistido e duas consultas históricas em ambos os modos, uma na preparação e uma na tendência.
- Bridge simulado: uma requisição em cada modo, incluindo sucesso, erro HTTP, timeout e JSON inválido; respostas convertidas e tratamento de exceções preservados.
- Cancelamento mantém a instância/token original onde propagados; timeout do Bridge mantém o encapsulamento preexistente.
- Falha de consulta da memória comportamental mantém o baseline global pelo fail-open existente.
- Falhas do logger não alteram resultados, falhas operacionais ou cancelamentos.

Comandos usados (saída separada porque havia uma IAApi em execução bloqueando a saída Debug usual):

```powershell
dotnet test IAApi.Tests/IAApi.Tests.csproj --no-restore -m:1 -nr:false -p:OutputPath=bin/PerfDiagnostics/ -p:UseSharedCompilation=false --filter 'FullyQualifiedName~PerformanceDiagnostics|FullyQualifiedName~PredictionServiceObservabilityEquivalenceTests' --logger 'trx;LogFileName=perf-targeted.trx' --verbosity minimal
dotnet test IAApi.Tests/IAApi.Tests.csproj --no-build --no-restore -p:OutputPath=bin/PerfDiagnostics/ --logger 'trx;LogFileName=perf-full.trx' --verbosity minimal
```

A execução do testhost e o formatador exigiram execução fora do sandbox devido à comunicação local bloqueada. Relatórios TRX: `IAApi.Tests/TestResults/perf-targeted.trx` e `IAApi.Tests/TestResults/perf-full.trx`.

## Riscos e limites

- Com diagnóstico ligado, Stopwatch, identificadores, contexto de correlação e logging acrescentam custo. Um provedor de logs lento pode aumentar a latência observada. Os números devem ser interpretados como execução instrumentada; overhead não foi quantificado em carga industrial.
- Desligado, o mecanismo não inicia cronômetros, não cria spans/identificadores e não emite PERF_DIAG. Permanecem pequenas chamadas e alocações de delegates dos invólucros.
- Não foi inferido fuso para timestamps locais ou sem Kind confiável; nesses casos não há estimativa de idade.
- A decomposição da transferência HTTP é limitada pelo buffering legado, descrito acima.
- A compilação emitiu avisos preexistentes de código e pacotes NuGet. Não foram alteradas dependências nesta tarefa.
- Os testes comprovam os cenários executados e a compatibilidade da suíte; não demonstram equivalência operacional completa no PK2/CP300. Não foi feita auditoria em carga real nem medição dos gargalos industriais com a nova instrumentação.

Esta implementação não altera lógica de detecção, Ensemble, modelos, thresholds, baselines, regimes, decisões, contratos HTTP, persistência, estado operacional, agendamento, intervalos ou concorrência. As consultas ao Bridge e suas esperas bloqueantes permanecem; nenhuma requisição ou consulta adicional foi introduzida. As regras de cancelamento, exceções e fail-open existentes foram preservadas. As alterações locais anteriores em outros componentes não fazem parte desta implementação.
