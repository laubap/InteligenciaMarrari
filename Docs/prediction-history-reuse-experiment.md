# Experimento: reutilização do histórico bruto da predição

## Investigação anterior à implementação

- `PredictionDetectionPreparationService.PrepararAsync` e `TendenciaValorService.Analisar` chamam `IHistoricoService.BuscarHistorico(clienteId, tagName, tipoTag)`, com os mesmos argumentos encaminhados por PredictionService → PostDecision → Trend.
- `AddBridgeServices` registra `IHistoricoService` como singleton `BridgeHistoricoService`. O Bridge consulta `/historico?tagName=...&horas=...`, usando `Bridge:BaseUrl` e `Bridge:HorasHistorico`; cliente é validado, tipo não é enviado nesse endpoint. As consultas têm a mesma fonte na DI atual, mas não são atomicamente o mesmo snapshot. Serviços construídos manualmente podem ter fontes diferentes.
- O histórico retornado pelo serviço já passou pela conversão/filtragem/deduplicação do Bridge. “Bruto” aqui significa exatamente a lista entregue por IHistoricoService, antes de qualquer transformação da preparação.
- A preparação cria HistoricoDetectorPoint e novas listas; os providers de regime/comportamento selecionam outras coleções, sem mutação encontrada na lista bruta ou em suas leituras. O DetectorInput sanitiza a coleção derivada. Não é seguro usar esse histórico do detector como substituto para tendência.
- O provider de regime pode consultar outra tag por período. Essa consulta é distinta e deve permanecer. Treinamento (`PerfilTagService`/`DadosTreinamentoTagService`), controllers de histórico e `TagTendenciaController` também fazem consultas independentes e ficam fora do experimento. Replay e testes montam os mesmos serviços manualmente; sem opção explícita, mantêm o caminho original.
- A segunda consulta ocorre depois da preparação/decisão e pode incluir leituras novas, excluir pontos antigos por deslocamento de janela, refletir correções/backfills ou falhar mesmo que a primeira tenha funcionado. Uma configuração dinâmica do Bridge também pode mudar entre consultas.
- O caminho atual não fixa a janela histórica em request.DataHora e a tendência não filtra pelo horário da predição. O experimento deve preservar os timestamps e valores do primeiro snapshot, sem inventar um corte temporal ou uma conversão de fuso. O momento da aquisição local não prova o instante atômico de um snapshot no PowerServer.

**Diferença de semântica aceita somente no modo experimental:** detector e tendência passam a usar a mesma aquisição bruta inicial. A tendência deixa de observar uma eventual atualização ou falha da segunda consulta. Não há equivalência absoluta com duas consultas reais em instantes diferentes. A tendência participa da resposta/mensagem; ela pode diferir por essa razão, embora algoritmos e decisões permaneçam inalterados.

## Implementação e validação

### Ativação e desligamento

O experimento é desabilitado por padrão e independente do gate de diagnóstico. Para habilitar numa nova instância Development, no PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY = '1'
$env:IAAPI_PERF_DIAGNOSTICS = '1'
dotnet run
```

O ambiente **efetivo do host** deve ser Development. Perfis de lançamento e DOTNET_ENVIRONMENT podem influenciá-lo. Somente o valor exato `1` habilita o experimento; ausente, `0`, `true` e outros valores mantêm duas consultas. Production/Staging nunca o habilitam, mesmo com `1`. Sem ambiente conhecido na DI, assume-se Production. A opção é imutável e lida uma vez na criação do singleton; reinicie a instância após mudar a variável.

Para voltar ao caminho original:

```powershell
$env:IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY = '0'
dotnet run
```

Não foi alterado nenhum ambiente/configuração da aplicação em execução durante esta implementação.

### Escopo e proteção do snapshot

Quando habilitado, `PredictionDetectionPreparationService` copia timestamp/valor de cada registro imediatamente após a primeira aquisição e a verificação de cancelamento existente, antes de preparar DetectorInput ou resolver baselines. Os valores ficam em registros privados imutáveis. A tendência recebe uma cópia independente desses valores: mudanças na lista ou nas instâncias originais posteriores à captura não a contaminam.

O snapshot é uma variável local da predição, encaminhada explicitamente em Preparation → Prediction → PostDecision → Trend → TendenciaValor. Não fica em singleton, AsyncLocal, dicionário, cache ou campo de serviço. Não é reutilizado pela predição seguinte. O contexto AsyncLocal preexistente dos diagnósticos continua contendo somente metadados de observabilidade.

`TendenciaValorService` só aceita a reutilização se sua própria opção estiver ligada e cliente, tag e tipo forem idênticos (comparação ordinal), além de a instância de IHistoricoService ser exatamente a da aquisição. Snapshot ausente, incompatível, fonte diferente ou opção desligada preservam a consulta original. A DI atual fornece a mesma fonte singleton aos dois serviços. Os consumidores independentes continuam consultando seu próprio histórico.

Nenhum filtro novo é aplicado ao bruto: valores não finitos, nulls, duplicatas, ordem original e DateTimeKind são preservados pela cópia. A ordenação, médias, limiar e mensagens de tendência continuam no mesmo cálculo compartilhado pelos dois caminhos. Não são usados o subconjunto de regime, o histórico comportamental ou o histórico sanitizado do detector.

A consulta distinta do regime por período permanece. “Uma consulta” significa uma consulta recente da tag principal por predição aplicável; regimes ou análises relacionadas podem legitimamente ter suas próprias requisições.

### Contratos e diferenças observáveis

- IHistoricoService, rotas, DTOs HTTP e formato dos resultados não mudam. Métodos internos da cadeia de serviços recebem argumento opcional de snapshot, e PreparationResult ganha uma propriedade interna à cadeia; chamadas existentes recompiladas continuam funcionando sem fornecer esse argumento.
- A tendência usa a aquisição inicial, com seus limites temporais originais, mesmo se houver registros posteriores ao horário da leitura analisada. Não foi introduzido um filtro causal inexistente no caminho anterior.
- Timestamps de aquisição são horários UTC locais que delimitam a chamada de IHistoricoService; não representam uma garantia de transação atômica no Bridge/PowerServer. Timestamps das leituras e referência da predição são registrados no formato original e com Kind, sem inferir fuso.
- A tendência, sua mensagem e trechos da mensagem final podem mudar se a segunda aquisição teria dados diferentes. A decisão do detector não passa a usar dados da tendência.
- Se somente a segunda consulta falharia, o caminho original continua falhando; o caminho experimental pode concluir, pois não tenta essa consulta. Esta diferença faz parte da remoção deliberada da segunda aquisição. Falhas da primeira consulta continuam propagadas pelo mesmo caminho.
- Não há retry ou nova consulta de fallback para mascarar dados ruins de um snapshot correspondente. Se o primeiro histórico for vazio/insuficiente/inválido para preparação, a predição continua falhando antes da tendência. Se a tendência não tolerar um registro bruto, preserva-se seu comportamento anterior com esse mesmo conteúdo.
- As verificações de cancelamento existentes permanecem nos mesmos pontos; o Bridge continua bloqueante e sem novo token/timeout. Não há promessa nova de interromper uma chamada HTTP em andamento.

### PERF_DIAG para comparar os modos

Além das durações e correlação existentes:

- `Prediction.Total`: `HistoricalRequests` conta envios HTTP históricos reais pelo Bridge, inclusive tentativas que falham e consultas de regime por período. A contagem é inclusiva como as durações: não somar pais com filhos. Uma implementação alternativa de IHistoricoService sem Bridge não incrementa esse contador; testes também conferem as chamadas diretamente na fonte.
- `Prediction.Total`: `DetectorScore`, `DetectorAnomaly`, `DetectorResults` (somente nome/aplicabilidade/voto/scores individuais) e `ValueTrend` registram os resultados para comparação. Não são registrados explicações, payloads históricos ou dicionários de métricas.
- `Preparation.History` e `ValueTrend.History`: `HistorySource=Fetched`, início/fim da aquisição, quantidade e limites dos timestamps originais.
- `Preparation.RawHistorySnapshot`: custo da cópia inicial quando o experimento está ligado.
- `ValueTrend.ReuseRawHistory`: custo de disponibilizar a cópia, `HistorySource=ReusedPredictionRaw`, início/fim da aquisição **inicial** e a referência original da predição. Nesse trecho `HistoricalRequests=0`.
- `ValueTrend.Calculate`: o mesmo cálculo em ambos os modos. Quando há reutilização, não existe `ValueTrend.History` nem seu segundo `Bridge.Transport`.

Comparações devem usar predições equivalentes e considerar aquecimento, logging e variação da fonte. Dois requests separados não têm o mesmo TraceId. O resultado individual do detector deve ser comparado pelos campos de resultado, não por duração.

Trechos reais da validação com Bridge simulado e 40 pontos fixos (campos intermediários omitidos):

```text
PERF_DIAG Client=CLIENT-A Tag=TAG-A Stage=ValueTrend.ReuseRawHistory ... HistoricalRequests=0 HistorySource=ReusedPredictionRaw HistoryAcquiredStartedUtc=2026-11-05T19:18:07.9360590+00:00 HistoryAcquiredCompletedUtc=2026-11-05T19:18:07.9404839+00:00 HistoryFirstOriginal=2026-11-05T11:20:00.0000000Z HistoryFirstKind=Utc HistoryLastOriginal=2026-11-05T11:59:00.0000000Z HistoryLastKind=Utc PredictionReferenceOriginal=2026-11-05T12:00:00.0000000Z
PERF_DIAG Client=CLIENT-A Tag=TAG-A Stage=Prediction.Total ... HistoricalRequests=1 ... DetectorScore=0 DetectorAnomaly=False ValueTrend=alta
PERF_DIAG Client=CLIENT-A Tag=TAG-A Stage=Prediction.Total ... HistoricalRequests=2 ... DetectorScore=0 DetectorAnomaly=False ValueTrend=alta
```

Esses logs são de testes sintéticos; não são uma medição de ganho no CP300. O modo reutilizado pode ser mais lento numa amostra trivial devido ao custo da cópia/aquecimento; não houve benchmark industrial.

### Arquivos alterados nesta etapa

As alterações locais de etapas anteriores foram preservadas. `MonitoramentoTagsService` não foi modificado nesta etapa.

| Arquivo | Finalidade |
| --- | --- |
| `Services/AnomalyDetection/Orchestration/PredictionHistoryReuseOptions.cs` | Gate experimental Development + variável, desligado por padrão. |
| `Services/AnomalyDetection/Orchestration/PredictionRawHistorySnapshot.cs` | Snapshot privado e imutável de valores brutos, identidade da fonte e tempos de aquisição. |
| `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs` | Captura opcional antes das transformações; expõe o snapshot apenas no resultado da preparação. |
| `Services/PredictionService.cs` | Encaminha o snapshot da mesma chamada e anota resultados diagnósticos. |
| `Services/AnomalyDetection/Orchestration/PredictionPostDecisionAnalysisService.cs` | Encaminhamento explícito para a análise de tendências. |
| `Services/AnomalyDetection/Orchestration/PredictionTrendAnalysisService.cs` | Encaminhamento somente para tendência de valor; risco permanece no caminho anterior. |
| `Services/TendenciaValorService.cs` | Escolhe reutilização ou consulta original e usa o mesmo cálculo nos dois modos. |
| `Services/DependencyInjection/IAApiApplicationServiceCollectionExtensions.cs` | Registra a opção imutável, sem alterar configuração de clientes/tags. |
| `Services/Diagnostics/PerformanceDiagnostics.cs` | Metadados temporais, resultados e contagem inclusiva de requisições históricas. |
| `Services/BridgeHistoricoService.cs` | Incrementa somente o contador diagnóstico antes do envio HTTP existente. |
| `IAApi.Tests/PredictionHistoryReuseTests.cs` | Testes direcionados da cadeia real, transporte simulado, snapshots, falhas e shadow. |
| `Docs/prediction-history-reuse-experiment.md` | Investigação, diferença de semântica, operação, validação e limites. |
| `Docs/performance-diagnostics.md` | Referência para este experimento posterior à instrumentação inicial. |

### Testes e limites

Resultados finais: **67 testes direcionados aprovados, 0 falhos, 0 ignorados**; **783 testes na suíte completa aprovados, 0 falhos, 0 ignorados**. Os direcionados estão incluídos na suíte completa; 42 casos novos foram adicionados nesta etapa. `git diff --check` também passou. Relatórios TRX: `IAApi.Tests/TestResults/history-reuse-targeted.trx` e `IAApi.Tests/TestResults/history-reuse-full.trx`.

Cobertura adicionada: gate; modo original e produção; uma versus duas requisições HTTP; equivalência com fonte fixa; diferença proposital com fonte móvel; cópia protegida contra mutação; isolamento por cliente/tag/predição; fonte ou parâmetros incompatíveis; vazio/null/inválidos; cancelamento antes da chamada, após aquisição e após detecção; falha de Bridge e falha exclusiva da segunda aquisição; regime transformado sem contaminar tendência bruta; fechamento de vetor e execução Mahalanobis shadow com os mesmos scores/resultados. O teste shadow cria exclusivamente modelos sintéticos em pasta temporária e não altera arquivos de treinamento/modelos da aplicação.

Comandos usados, em saída de build separada da aplicação em execução:

```powershell
dotnet test IAApi.Tests/IAApi.Tests.csproj --no-restore -m:1 -nr:false -p:OutputPath=bin/HistoryReuse/ -p:UseSharedCompilation=false --filter 'FullyQualifiedName~PredictionHistoryReuseTests|FullyQualifiedName~PerformanceDiagnosticsTests|FullyQualifiedName~PredictionServiceObservabilityEquivalenceTests' --logger 'trx;LogFileName=history-reuse-targeted.trx' --verbosity minimal
dotnet test IAApi.Tests/IAApi.Tests.csproj --no-build --no-restore -p:OutputPath=bin/HistoryReuse/ --logger 'trx;LogFileName=history-reuse-full.trx' --verbosity minimal
```

A execução dos testes usou a permissão externa ao sandbox por causa da comunicação local do testhost. Permanecem avisos de compilação e de pacotes NuGet já presentes; dependências não foram alteradas.

O experimento aumenta memória/CPU por predição proporcionalmente ao tamanho do histórico para proteger a cópia; não há limite/truncamento novo. Os diagnósticos também acrescentam custo quando habilitados. A validação cobre cenários sintéticos e regressões da suíte, sem provar equivalência absoluta entre snapshots reais em instantes diferentes e sem validar ganho de latência no PK2/CP300.

Não foram alterados algoritmos de detecção, Ensemble, baselines, thresholds, modelos, decisões, treinamento, execução shadow, regras de persistência ou agendamento. O conteúdo derivado da tendência pode diferir e seguir os caminhos já existentes de resposta/finalização. Nenhum paralelismo, cache global ou outra otimização foi implementado. Nenhum commit foi realizado.
