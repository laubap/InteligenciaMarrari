# Intelligence Lab — replay histórico isolado (primeiro incremento)

O executor usa `PerfilTagService` e `PredictionService` da IAApi, SQLite **em memória**, seis detectores estatísticos reais e `InMemoryDiagnosticCollector`. Não inicia a API/Bridge, não conecta ao PowerServer e não altera bancos operacionais.

**Atenção:** não executa Isolation Forest, multivariada, memória comportamental, nem interpretação por LLM. O runtime funcional de decisão está explicitamente desabilitado, como nos testes de equivalência. Portanto, é uma prova de captura e equivalência para **essa configuração**, não uma auditoria de toda a IA de produção. Os dados PK7 não têm rótulos de verdade-terreno; anomalias não são automaticamente acertos.

## Execução

No PowerShell dentro de `IAApi_RECUPERADO`:

```powershell
$base = "$HOME\Downloads\IAApi-Replay-Industrial"
dotnet run --project .\Tools\IntelligenceLabReplay\IAApi.IntelligenceLabReplay.csproj -- `
  "$base\PK7_TAG1_20261015.json" `
  "$base\PK7_TAG1_20261017.json" `
  "$base\resultados" `
  20
```

Se o teste de 20 leituras funcionar, remova o `20` para processar as 1.066. Cada execução compara dois bancos totalmente isolados (observabilidade off/on). A análise é cronológica e apenas leituras anteriores alimentam o histórico da leitura seguinte; não inventamos leituras para as lacunas.

O relatório `intelligence-lab-replay-report.json` fica somente na pasta de saída local, sem ser incluído no Git. Não compartilhe o arquivo sem revisar dados de clientes e tags.

Os horários originais dos JSONs não trazem fuso. **Neste ensaio** adotamos explicitamente o deslocamento fixo **UTC-03:00**, convertido para UTC antes de passar ao `PredictionService`. Confirme que essa suposição corresponde à configuração do Bridge; não alteramos os arquivos originais.

A versão diagnóstica deste executor é apenas um identificador de laboratório, não a versão efetiva dos algoritmos.

**Não faça commit antes de confirmar build e resultado do smoke test.**

## Fase 3B.1 — Exportacao das decisoes para o avaliador

O executor tambem grava `intelligence-lab-replay-evaluation-readings.json` com os
resultados finais efetivamente observados no `PredictionService`, o score
**normalizado do diagnostico** e o `DiagnosticId` associado a cada leitura.
A exportacao so acontece depois de a equivalencia observabilidade off/on
ser validada. O cliente no SQLite temporario usa o prefixo sintetico `SIM-`.

**Este arquivo ainda NAO e um relatorio de eficacia.** `DatasetLabeled=false` e
`EffectivenessEvaluated=false`; nao cria eventos nem periodos normais por
inferencia, e nao calcula TP/FP/FN. O `RunId` aqui identifica o conteudo das
previsoes do replay, nao uma identidade completa e verificavel de commit,
configuracao e modelos. A proxima etapa deve associar rotulos externos
independentes e registrar uma identidade completa de execucao antes de avaliar.

Mantem as limitacoes do replay atual: seis detectores estatisticos,
sem Isolation Forest, multivariada, memoria comportamental nem runtime
funcional habilitado. Nao use o arquivo para alegar validacao de producao.
