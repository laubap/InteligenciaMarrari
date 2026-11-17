# tools

Ferramentas locais de apoio à IAApi. Cada pasta tem o próprio README quando existe; esta página documenta os scripts da raiz de `tools/`.

| Item | Para quê |
|---|---|
| `Start-IAApiComLog.ps1` | Sobe a IAApi em segundo plano com log em arquivo, com data e hora (abaixo) |
| `Stop-IAApi.ps1` | Para a IAApi iniciada pelo script acima, usando o PID salvo (abaixo) |
| `Cp300Export/` | Exporta histórico do Bridge (`Export-Cp300Historico.ps1`) e decisões da IA real (`Export-AnomaliasIaReal.ps1`) |
| `Cp300OfflineReplay/` | Replay offline CP300 com o código de produção ([README](Cp300OfflineReplay/README.md)) |
| `CalibracaoAvisosTreino/` | Script Python offline que calibrou os limites dos avisos do treino do perfil a partir do CSV do CP300 ([README](CalibracaoAvisosTreino/README.md)) |
| `IAApi.LiveMonitor/` | Monitor local, somente leitura ([README](IAApi.LiveMonitor/README.md)) |
| `IntelligenceLab*/`, `IAApi.Benchmarks/` | Laboratório de inteligência e benchmarks ([README do replay](IntelligenceLabReplay/README.md)) |

## Subir e parar a IAApi com log em arquivo

> **Atenção:** a IAApi sobe com o `MonitoramentoTagsService`, que lê o Bridge, roda a detecção e **grava decisões** em `ia_config.db`. Use só numa sessão de teste autorizada.

Origem: passo **a** do P27 ("Como confirmar", [Docs/PROBLEMAS_ENCONTRADOS.md](../Docs/PROBLEMAS_ENCONTRADOS.md)) e execução aprovada da BACKLOG 6. Sem janela de console, a saída não trava se alguém clicar na janela (hipótese 1 do P27). Os logs `Information` ficam guardados com data e hora.

### Subir

```powershell
cd 'C:\Users\Laura Marrari\Documents\IA MARRARI\IAApi_RECUPERADO'
.\tools\Start-IAApiComLog.ps1                                     # chaves desligadas (padrão)
.\tools\Start-IAApiComLog.ps1 -HistoricoCausal                    # só P31
.\tools\Start-IAApiComLog.ps1 -HistoricoCausal -RegimeCombinado   # as duas
.\tools\Start-IAApiComLog.ps1 -HistoricoCausal -PerfDiag          # etapa 0 da BACKLOG 6 (6.0): P31 + PERF_DIAG
.\tools\Start-IAApiComLog.ps1 -HistoricoCausal -PerfDiag -ReusarHistorico   # sessão 6.0b: igual à 6.0, com o reuso do histórico
```

O script:

1. Recusa subir se a instância anterior (PID salvo) ainda estiver viva ou se a porta já estiver em uso.
2. Roda `dotnet build IAApi.csproj -c Debug`. Para pular, use `-SemBuild`.
3. Inicia `dotnet bin\<Configuracao>\net10.0\IAApi.dll` com `Start-Process -WindowStyle Hidden`. O diretório de trabalho é a raiz do repositório, porque o SQLite usa o caminho relativo `ia_config.db`. stdout e stderr vão para `dados\logs`.
4. Espera 5 s (`-EsperaSegundos`), confere se o processo continua vivo e grava o PID.

Variáveis de ambiente definidas só para o processo da IAApi. Os valores anteriores da sessão do PowerShell são restaurados. Essas variáveis têm precedência sobre `appsettings*.json`.

| Variável | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `-Ambiente` (padrão `Development`, como no `launchSettings.json`) |
| `ASPNETCORE_URLS` | `-Url` (padrão `http://localhost:5145`, o mesmo do LiveMonitor) |
| `Logging__Console__FormatterName` | `simple` (**obrigatório** para o horário sair; ver abaixo) |
| `Logging__Console__FormatterOptions__TimestampFormat` | `yyyy-MM-dd HH:mm:ss ` |
| `Logging__Console__FormatterOptions__ColorBehavior` | `Disabled` (sem códigos de cor no arquivo) |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command` | `Warning` (some o SQL do EF; ver abaixo) |
| `AnomalyDetection__HistoricoCausal__Habilitado` | `true` só com `-HistoricoCausal`; senão `false` |
| `AnomalyDetection__RegimeCombinado__Habilitado` | `true` só com `-RegimeCombinado`; senão `false` |
| `IAAPI_PERF_DIAGNOSTICS` | `1` só com `-PerfDiag`; senão `0` |
| `IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY` | `1` só com `-ReusarHistorico`; senão `0` |

As duas chaves de detecção e as duas variáveis `IAAPI_*` são **sempre** definidas, inclusive como `false`/`0`. Assim, o estado de cada subida fica explícito no `config.json` do log. As chaves de detecção não existem hoje em `appsettings*.json`, e o padrão das classes de opções também é `false`.

**Data e hora no log (P44, tarefa 36).**
- **Causa (confirmada em teste):** sem `Logging:Console:FormatterName`, o provedor de console escolhe o formatador `simple` copiando as opções **antigas** (`Logging:Console:TimestampFormat`) e **descarta** `Logging:Console:FormatterOptions`. O script de 16/11 só definia `FormatterOptions__TimestampFormat`, então o horário nunca saiu (`info: Microsoft.Hosting.Lifetime[14]`, sem data). O `ColorBehavior` caiu no mesmo descarte; não deu para notar porque a saída redirecionada para arquivo já não tem cores.
- **Correção:** o script passou a definir também `FormatterName = simple`. O teste que prova a causa e a correção é `IAApi.Tests/Monitoring/ConsoleLogFormatoSessaoTests.cs`.
- **O que o horário cobre:** as linhas do `ILogger` (`info:`, `warn:`, `fail:`, inclusive `PERF_DIAG`). Os `Console.WriteLine` do código (`HISTORICO RECORTADO`, `LIMPEZA DE HISTORICO`, interpretação) continuam **sem** horário (tarefa 17).
- **Formato esperado:** `2026-11-17 10:00:00 info: Microsoft.Hosting.Lifetime[14]`, com a mensagem na linha seguinte.

**SQL do EF em `Warning` (P44).** A categoria `Microsoft.EntityFrameworkCore.Database.Command` em `Information` gravou 960 mil linhas `Executed DbCommand` (702 MB em 87 min) no log de 16/11. Com o filtro, essas linhas somem. As falhas do EF (`Failed executing DbCommand`, avisos de migration pendente como as do P43) continuam no log. O filtro vale **só para a sessão**: o `appsettings*.json` não muda e as demais categorias seguem em `Information`.

**`-ReusarHistorico` (reuso do histórico da predição, desligado por padrão).** Liga `IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY=1` ([Docs/prediction-history-reuse-experiment.md](../Docs/prediction-history-reuse-experiment.md)), para a sessão 6.0b da BACKLOG 6.
- **O que faz:** a tendência de valor deixa de fazer a segunda consulta de histórico ao Bridge e usa a mesma aquisição da predição. A decisão do detector não muda; a tendência (e trechos da mensagem) pode diferir.
- **Parâmetro próprio:** é independente do `-PerfDiag`. Para a 6.0b, use os dois juntos (`-HistoricoCausal -PerfDiag -ReusarHistorico`).
- **Só em Development:** o gate do experimento exige Development e o valor exato `1`. Com `-ReusarHistorico` e outro `-Ambiente`, o script **recusa subir** antes de criar qualquer arquivo.
- **Variável sempre definida:** sem o switch, fica em `0`. Por isso, **definir `$env:IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY = '1'` na janela antes de chamar o script não funciona mais**: o script a sobrescreve com `0` no processo filho. Use `-ReusarHistorico`.
- **Registro:** o `config.json` grava `ReusarHistorico` e a variável.

**`-PerfDiag` (PERF_DIAG, desligado por padrão).** Liga a instrumentação descrita em [Docs/performance-diagnostics.md](../Docs/performance-diagnostics.md). Foi aprovada para a etapa 0 da BACKLOG 6 (6.0).
- **O que faz:** só mede. Não muda a decisão de anomalia. Acrescenta ao log linhas `PERF_DIAG ...` (`Monitoring.Reading`, `Prediction.Total`, `Preparation.History`, `Bridge.Transport` etc.), com um pequeno custo por predição.
- **Valor da variável:** é sempre definida. Sem o switch, fica em `0`, o que impede que um `IAAPI_PERF_DIAGNOSTICS=1` esquecido na sessão do PowerShell ligue o diagnóstico sem querer. O `config.json` registra `PerfDiag`.
- **Só em Development:** o PERF_DIAG só liga com o ambiente `Development`. Com `-PerfDiag` e outro `-Ambiente`, o script **recusa subir** antes de criar qualquer arquivo, para não rodar uma sessão de medição sem medição.
- **Filtro de log:** as linhas saem em `Information`, na categoria `IAApi.Services.Diagnostics.PerformanceDiagnostics`. O `appsettings*.json` atual (`Default: Information`) já deixa passar.

Outros parâmetros: `-Configuracao Debug|Release`, `-PastaLogs`. Ajuda completa: `Get-Help .\tools\Start-IAApiComLog.ps1 -Full`.

### Arquivos gerados (`dados\`, ignorado pelo git)

| Arquivo | Conteúdo |
|---|---|
| `dados\logs\iaapi_<yyyyMMdd_HHmmss>.log` | Saída do console (logs com data e hora) |
| `dados\logs\iaapi_<yyyyMMdd_HHmmss>.err.log` | stderr |
| `dados\logs\iaapi_<yyyyMMdd_HHmmss>.config.json` | Parâmetros, chaves (inclusive `PerfDiag` e `ReusarHistorico`) e variáveis desta subida |
| `dados\logs\iaapi.pid.json` | PID e hora de início da última subida (lido pelo `Stop-IAApi.ps1`) |

Acompanhar o log:

```powershell
Get-Content (Get-Content .\dados\logs\iaapi.pid.json | ConvertFrom-Json).Log -Wait -Tail 50
```

### Parar

```powershell
.\tools\Stop-IAApi.ps1 -Diagnostico   # só mostra PID, CPU, threads, memória e as últimas linhas do log
.\tools\Stop-IAApi.ps1 -WhatIf        # mostra o que faria
.\tools\Stop-IAApi.ps1                # encerra
```

- Antes de encerrar, confere se o PID ainda é a mesma IAApi: processo `dotnet`, hora de início igual à salva e `IAApi.dll` na linha de comando. Se algo não bater (PID reutilizado), **não encerra nada** (código de saída 2).
- O encerramento é **forçado** (`Stop-Process -Force`), porque o processo não tem janela para receber Ctrl+C. Um ciclo do monitor em andamento é interrompido e a decisão da tag em processamento pode não ser gravada.
- **Investigando o P27** (monitor parado sem aviso)? Rode `-Diagnostico` **antes** de parar e guarde a saída. Isso cobre o passo c do P27 (última linha do log, PID, CPU, threads).

### Limites

- O log cresce sem rotação. Com o SQL do EF em `Warning` o volume cai muito, mas em testes longos confira o espaço em disco (tarefa 36.4, opcional).
- O arquivo sai na página de código do console (cp850 nesta máquina), então acentos aparecem trocados se for lido como UTF-8 (tarefa 36.3, ainda não feita).
- Mudar uma chave exige parar e subir de novo, porque as variáveis de ambiente de um processo já iniciado não mudam.
- **Validação (17/11/2026):** `powershell -NoProfile -File .\tools\Start-IAApiComLog.Tests.ps1` → 51 verificações, todas aprovadas. Além do endereço local (P34), o teste **executa o script** com um `dotnet` falso no PATH (um `.cmd` temporário que só imprime as variáveis), sem build, com logs numa pasta temporária e porta livre: nada da IAApi sobe, e não há acesso a Bridge, banco ou rede. Confere `FormatterName`, horário, filtro do EF, `-ReusarHistorico` (padrão `0`, `1` só com o switch, recusa fora de Development), variáveis esquecidas na sessão e o `config.json`.
- **Ainda não validado numa subida real:** que a IAApi de fato escreva `2026-11-17 hh:mm:ss info: ...` com essas variáveis e que `Executed DbCommand` desapareça. O teste do host (`ConsoleLogFormatoSessaoTests`) cobre o provedor de console com a mesma configuração, mas só a próxima sessão confirma no log real.
