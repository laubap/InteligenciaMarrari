# tools

Ferramentas locais de apoio à IAApi. Cada pasta tem o próprio README quando existe; esta página documenta os scripts da raiz de `tools/`.

| Item | Para quê |
|---|---|
| `Start-IAApiComLog.ps1` | Sobe a IAApi em segundo plano com log em arquivo, com data e hora (abaixo) |
| `Stop-IAApi.ps1` | Para a IAApi iniciada pelo script acima, usando o PID salvo (abaixo) |
| `Cp300Export/` | Exporta histórico do Bridge (`Export-Cp300Historico.ps1`) e decisões da IA real (`Export-AnomaliasIaReal.ps1`) |
| `Cp300OfflineReplay/` | Replay offline CP300 com o código de produção ([README](Cp300OfflineReplay/README.md)) |
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
| `Logging__Console__FormatterOptions__TimestampFormat` | `yyyy-MM-dd HH:mm:ss ` |
| `Logging__Console__FormatterOptions__ColorBehavior` | `Disabled` (sem códigos de cor no arquivo) |
| `AnomalyDetection__HistoricoCausal__Habilitado` | `true` só com `-HistoricoCausal`; senão `false` |
| `AnomalyDetection__RegimeCombinado__Habilitado` | `true` só com `-RegimeCombinado`; senão `false` |

As duas chaves de detecção são **sempre** definidas, inclusive como `false`. Assim, o estado de cada subida fica explícito no `config.json` do log. Elas não existem hoje em `appsettings*.json`, e o padrão das classes de opções também é `false`.

Outros parâmetros: `-Configuracao Debug|Release`, `-PastaLogs`. Ajuda completa: `Get-Help .\tools\Start-IAApiComLog.ps1 -Full`.

### Arquivos gerados (`dados\`, ignorado pelo git)

| Arquivo | Conteúdo |
|---|---|
| `dados\logs\iaapi_<yyyyMMdd_HHmmss>.log` | Saída do console (logs com data e hora) |
| `dados\logs\iaapi_<yyyyMMdd_HHmmss>.err.log` | stderr |
| `dados\logs\iaapi_<yyyyMMdd_HHmmss>.config.json` | Parâmetros, chaves e variáveis desta subida |
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

- O log cresce sem rotação. Em testes longos, confira o espaço em disco.
- O script não liga o `IAAPI_PERF_DIAGNOSTICS`, que precisa de aprovação própria (BACKLOG 6).
- Mudar uma chave exige parar e subir de novo, porque as variáveis de ambiente de um processo já iniciado não mudam.
- Validado só quanto à sintaxe (parser do PowerShell 5.1, 13/11/2026). **Ainda não foi executado.**
