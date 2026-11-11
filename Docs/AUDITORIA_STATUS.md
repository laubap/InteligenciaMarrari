# Auditoria inicial: status

Data: 2026-11-11. Sessão apenas de leitura: nenhum código, configuração ou banco foi alterado. Arquivos criados: `Docs/MAPA_DETECCAO.md`, `Docs/AUDITORIA_STATUS.md` e `Docs/BACKLOG.md`.

> Observação de permissão: o `CLAUDE.md` diz que, na primeira sessão, só `docs/MAPA_DETECCAO.md` pode ser criado. A solicitação desta sessão autorizou explicitamente os três arquivos. Como o Windows não diferencia maiúsculas de minúsculas, `docs/` e `Docs/` são a mesma pasta; usei `Docs/`, que é a grafia que o git registra.

## 1. Branch e git status

- Branch: `feat/intelligence-lab-e2e-validation` (main = `main`). Último commit: `7128764 feat: add gradual drift intelligence lab scenario`.
- **51 entradas não commitadas**: 29 arquivos modificados (multivariado, observabilidade, `PredictionService`, serviços de orquestração, `BridgeHistoricoService`, `MonitoramentoTagsService`, DI, testes) e 22 não rastreados. Entre os não rastreados estão `CLAUDE.md`, `Docs/*.md`, `Services/Diagnostics/` (PERF_DIAG), `Services/Monitoring/` (lote por cliente), `PredictionHistoryReuse*`, `tools/IAApi.LiveMonitor/` inteiro e 5 arquivos `.patch` na raiz.
- **Risco**: o código que roda hoje (coleta em lote, PERF_DIAG, reuso de histórico, LiveMonitor) não está em nenhum commit. Os testes abaixo valem para esta árvore local, não para um commit identificável.

## 2. Testes offline

Isolamento verificado antes de rodar [código]: SQLite em memória (`Data Source=:memory:`) ou em pasta temporária; Bridge simulado (`http://bridge.test` com handler falso); os processos filhos executam apenas ferramentas do próprio repo (`tools/IntelligenceLab*`) com fixtures sintéticas; os testes do LiveMonitor usam `fetch` simulado (`vi.stubGlobal`). Nenhum teste usa `appsettings.json` real, `ia_config.db` ou o endereço do Bridge real.

| Suíte | Comando | Resultado |
|---|---|---|
| `IAApi.Tests` (net10.0, xUnit) | `dotnet test IAApi.Tests/IAApi.Tests.csproj --no-restore` | **783 aprovados, 0 falhas, 0 ignorados** (20 s) |
| LiveMonitor (Vitest) | `npx --no-install vitest run` em `tools/IAApi.LiveMonitor` | **28 aprovados** em 4 arquivos (domain, polling, proxy, testRuns) |

Áreas de teste em `IAApi.Tests/`: raiz (cerca de 60 arquivos: multivariado, runtime decision/canário, consistência de controllers, wiring de deadband/behavior no `PredictionService`, PERF_DIAG, reuso de histórico), `Detection/`, `IntelligenceLab/` (replay, cenários sintéticos flow drop e gradual drift), `Monitoring/`, `Services/`.
Outros projetos: `src/IAApi.Engine`, `src/IAApi.Application`, `src/IAApi.Infrastructure`, `tools/IAApi.Benchmarks`, `tools/IntelligenceLab{Comparer,Evaluator,Replay,ScenarioGenerator}`.

Não executado: API, monitor conectado, benchmarks (dependem de bases fora do repo), qualquer coisa ligada ao CP300.

**Limite**: os testes passarem prova o comportamento sintético, não a detecção no CP300.

## 3. Handoff: confirmado / divergente / não encontrado

**`docs/IAAPI_HANDOFF_CLAUDE.md` não foi encontrado**: não está na pasta, nem no histórico git (`git log --all`), nem nas pastas irmãs de `IA MARRARI/`. Por isso **não existe a seção "Próxima sessão" nem a tabela de cenários do CP300** para conferir. Esta auditoria seguiu as prioridades do `CLAUDE.md`.

O que dá para checar a partir do `CLAUDE.md` e dos documentos existentes em `Docs/`:

| Afirmação | Situação |
|---|---|
| Ciclo do monitor ≈ 60 s + processamento de todas as tags (README do LiveMonitor) | **Confirmado no código**: `MonitoramentoTagsService.ExecuteAsync`, `Task.Delay(1 min)` após processamento sequencial |
| Coleta em lote por cliente no Bridge | **Confirmado no código**, mas ainda não commitado |
| PERF_DIAG não altera decisão, ligado por variável de ambiente | **Confirmado no código** (`IAAPI_PERF_DIAGNOSTICS`); teste `PerformanceDiagnosticsTests` passou |
| Cliente do teste CP300 é PK2 (README LiveMonitor) | **Histórico a validar**. O canário runtime/fusão está só para PK7, então PK2 usa a decisão legada do ensemble [config] |
| MaxLag multivariado discretizado em minutos (`concurrency-architecture-audit.md`) | Não reverificado (fora do foco univariado) |
| Parâmetros ativos na máquina de teste iguais aos do repo | **Não verificável** sem acesso ao ambiente: o `appsettings` do repo pode diferir do implantado |

Divergências encontradas no próprio código:
- Os pesos no `appsettings.json` diferem dos defaults de `AnomalyDetectionOptions` (por exemplo, Z 0,15 contra 0,20). Vale o arquivo de configuração.
- `AnomalyDetection:MinHistoryPoints` (30) **não é usado** no caminho de produção, só em `EnsembleV2Simulator`.
- O registro gravado (`AnomaliaDetectada.DataDeteccao`) usa a hora do processamento, não a hora da leitura. O LiveMonitor não consegue mostrar a latência real.

## 4. Cenários CP300 × pontos do mapa

**Tabela original indisponível.** Abaixo estão as **hipóteses por tipo de falha típico**, a reaplicar quando a tabela for fornecida. Nada aqui foi demonstrado: tudo é **[hipótese]** apoiado em comportamento confirmado no código. Os números (#) referem-se às linhas do fluxo em `MAPA_DETECCAO.md`.

| Tipo de cenário (provável) | Pontos do mapa que podem explicar a falha | O que medir para confirmar ou descartar |
|---|---|---|
| **Degrau / mudança de patamar não detectada** | #10 diluição (só parte dos detectores reage, média < 60); #9 deadband zera Z/MAD/EWMA; LevelShift precisa de 10+ pontos recentes; #5 baseline do perfil com desvio inflado | Para a tag e o intervalo do teste: `DetalhesDetectores` (score normalizado e aplicabilidade de cada detector), score final, deadband, média e desvio usados (logs `BASELINE …` e diagnóstico do IntelligenceLab/PERF_DIAG `DetectorResults`) |
| **Pico curto / transitório perdido** | L1+L3: a leitura é amostrada no máximo 1 vez por ciclo de 60 s ou mais, então um pico mais curto que o ciclo pode nunca ser lido; RoC sozinho pesa 0,10 | Duração do pico no simulador × intervalo real entre leituras por tag (`ReadingAgeAtStartMs` e diferença entre `PredictionStartedUtc` consecutivos no PERF_DIAG) |
| **Deriva lenta (drift)** | Janela de 24 h acompanha a deriva; baseline por regime ou comportamento recalculado do próprio histórico absorve a deriva; EWMA e LevelShift comparam contra janelas que já estão derivando | Origem do baseline aplicado em cada leitura (Perfil, Regime ou Comportamento); média do perfil vs. média das últimas 24 h; data do último treino de `TagPerfilIa` |
| **Queda de vazão / ida a zero** | #8 série com muitos zeros ou poucos valores distintos pode ser classificada como Discrete e bloquear todos os detectores; #7 histórico vazio após sanitização | Tipo comportamental e confiança no `DetectorResult.Metricas` (`tipoComportamental`, `confiancaTipoComportamental`, `quantidadeDetectoresAplicaveis`) |
| **Valor congelado / sensor travado** | Desvio ~0 → deadband; detectores de amplitude não reagem a ausência de variação; Persistence é o único candidato | Score do Persistence e aplicabilidade; desvio usado |
| **Tag nunca avaliada ou detecção atrasada** | #3 tag não encontrada no lote → exceção; falha do lote pula o cliente; sem `TagPerfilIa` → exceção; #7 `DataHoraAtual` < último ponto do histórico → inválido; L5 timeout de 60 s | Logs `Falha ao monitorar tag`, `Falha na consulta em lote`, `Predição finalizada … Falha`; explicação `entrada inválida` no resultado; comparação de `ReadingOriginal` com `HistoryLastOriginal` no PERF_DIAG (fuso/Kind) |
| **Anomalia detectada, mas aparece tarde ou na hora errada no LiveMonitor** | L1–L7; `DataDeteccao = DateTime.Now`, hora da leitura não gravada | PERF_DIAG `Monitoring.Reading` (idade da leitura, duração por tag); duração total do ciclo por cliente (`Predições cliente … em X ms`) |

**Próximo passo para fechar esta seção:** receber a tabela do CP300 (cenário, tag, horário de início e fim, resultado esperado e observado) e os logs ou o export de diagnóstico desses intervalos. Com isso, cada linha acima vira confirmada ou descartada por cenário.
