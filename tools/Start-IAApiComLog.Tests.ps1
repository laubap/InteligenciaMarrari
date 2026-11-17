<#
.SYNOPSIS
    Teste offline do Start-IAApiComLog.ps1: recusa de endereco nao local (P34) e
    variaveis de ambiente da sessao (P44, tarefa 36).

.DESCRIPTION
    NUNCA sobe a IAApi. Secoes:
      1. le o script pela arvore sintatica, carrega so a funcao Test-EnderecoLocal
         e confere enderecos aceitos e recusados;
      2. confere que a checagem do -Url e a primeira acao do script: antes dela
         so podem vir Set-StrictMode, $ErrorActionPreference e a propria funcao;
      3. executa o script de verdade, mas com um "dotnet" FALSO na frente do PATH
         (um .cmd temporario que so imprime as variaveis de ambiente), sem build,
         com logs numa pasta temporaria e porta livre. Confere FormatterName=simple,
         horario, filtro do SQL do EF, -ReusarHistorico (0 por padrao, 1 so com o
         switch, recusa fora de Development), variaveis esquecidas na sessao e o
         config.json. Pula a secao se bin\Debug\net10.0\IAApi.dll nao existir.

    Nao ha infraestrutura de teste para scripts no repositorio (nem Pester em uso);
    por isso este arquivo e um script simples, com codigo de saida 0 (passou) ou
    1 (falhou).

.EXAMPLE
    powershell -NoProfile -File .\tools\Start-IAApiComLog.Tests.ps1
#>
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'Start-IAApiComLog.ps1'
$tokens = $null
$erros = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$erros)
if ($erros -and $erros.Count -gt 0) {
    Write-Host "FALHOU: o script tem erro de sintaxe: $($erros[0].Message)"
    exit 1
}

$falhas = 0
function Confere([bool]$condicao, [string]$descricao) {
    if ($condicao) { Write-Host "  ok    $descricao" }
    else { Write-Host "  FALHA $descricao"; $script:falhas++ }
}

# 1. Funcao Test-EnderecoLocal
$funcao = $ast.Find({
        param($n)
        $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
        $n.Name -eq 'Test-EnderecoLocal'
    }, $false)
if (-not $funcao) {
    Write-Host 'FALHOU: funcao Test-EnderecoLocal nao encontrada no script.'
    exit 1
}
. ([scriptblock]::Create($funcao.Extent.Text))

Write-Host 'Enderecos aceitos:'
foreach ($u in @(
        'http://localhost:5145',
        'http://localhost:5145/',
        'http://LOCALHOST:5145',
        'https://localhost:7295',
        'http://127.0.0.1:5145',
        'http://[::1]:5145')) {
    Confere (Test-EnderecoLocal $u) $u
}

Write-Host 'Enderecos recusados:'
foreach ($u in @(
        '',
        'http://0.0.0.0:5145',
        'http://*:5145',
        'http://+:5145',
        'http://[::]:5145',
        'http://192.168.0.10:5145',
        'http://10.0.1.99:5145',
        'http://minha-maquina:5145',
        'http://localhost.exemplo.com:5145',
        'http://127.0.0.1.nip.io:5145',
        'http://localhost@10.0.0.1:5145',
        'http://localhost:5145;http://0.0.0.0:5145',
        'ftp://localhost:5145',
        'localhost:5145')) {
    Confere (-not (Test-EnderecoLocal $u)) ("'{0}'" -f $u)
}

# 2. A checagem vem antes de qualquer outra acao
Write-Host 'Ordem no script:'
$comandos = @($ast.EndBlock.Statements)
$indiceCheck = -1
for ($i = 0; $i -lt $comandos.Count; $i++) {
    $s = $comandos[$i]
    if ($s -is [System.Management.Automation.Language.IfStatementAst] -and
        $s.Extent.Text -match 'Test-EnderecoLocal\s+\$Url') {
        $indiceCheck = $i
        break
    }
}
Confere ($indiceCheck -ge 0) 'existe if (-not (Test-EnderecoLocal $Url)) no nivel de cima'
if ($indiceCheck -ge 0) {
    $check = $comandos[$indiceCheck]
    Confere ($check.Extent.Text -match '\bthrow\b') 'a checagem lanca erro (throw)'
    for ($i = 0; $i -lt $indiceCheck; $i++) {
        $t = $comandos[$i].Extent.Text
        $permitido = ($t -match '^Set-StrictMode\b') -or
            ($t -match '^\$ErrorActionPreference\s*=') -or
            ($comandos[$i] -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                $comandos[$i].Name -eq 'Test-EnderecoLocal')
        Confere $permitido ("antes da checagem so ha preparacao: '{0}'" -f ($t -split "`n")[0].Trim())
    }
}

# 3. Execucao do script com um "dotnet" FALSO (P44, tarefa 36.2)
#    O script roda de verdade num PowerShell filho, mas o "dotnet" achado no PATH e um .cmd
#    de teste que so imprime as variaveis de ambiente e fica vivo alguns segundos. Nada da
#    IAApi sobe: sem build (-SemBuild), logs numa pasta temporaria, porta livre, nenhum acesso
#    a Bridge, banco ou rede. O PID e o log gravados sao os do .cmd falso.
Write-Host 'Execucao com dotnet falso (nada da IAApi sobe):'
$raizRepo = Split-Path -Parent $PSScriptRoot
$dllReal = Join-Path $raizRepo 'bin\Debug\net10.0\IAApi.dll'
if (-not (Test-Path -LiteralPath $dllReal)) {
    # O script confere a existencia da DLL mesmo com -SemBuild; sem ela nao da para executa-lo.
    Write-Host ("  PULADO: {0} nao existe (compile uma vez a IAApi). Secao 3 nao executada." -f $dllReal)
    $secao3Pulada = $true
}
else {
    $secao3Pulada = $false
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('iaapi_start_test_' + [guid]::NewGuid().ToString('N'))
    $fakeDir = Join-Path $tmp 'fake'
    New-Item -ItemType Directory -Path $fakeDir -Force | Out-Null
    $cmdFalso = @(
        '@echo off',
        'echo FAKE_DOTNET args=%*',
        'set | findstr /B /C:"Logging__" /C:"IAAPI_" /C:"AnomalyDetection__" /C:"ASPNETCORE_"',
        'ping -n 6 127.0.0.1 >nul'   # fica vivo ~5 s (o script confere o processo apos 2 s)
    )
    Set-Content -LiteralPath (Join-Path $fakeDir 'dotnet.cmd') -Value $cmdFalso -Encoding ASCII

    $porta = $null
    $ouvinte = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try { $ouvinte.Start(); $porta = $ouvinte.LocalEndpoint.Port } finally { $ouvinte.Stop() }
    $urlTeste = "http://localhost:$porta"

    $psExe = (Get-Command powershell.exe -CommandType Application | Select-Object -First 1).Source
    $pathOriginal = [Environment]::GetEnvironmentVariable('PATH', 'Process')
    $reuseOriginal = [Environment]::GetEnvironmentVariable('IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY', 'Process')
    $perfOriginal = [Environment]::GetEnvironmentVariable('IAAPI_PERF_DIAGNOSTICS', 'Process')

    # Roda o script num PowerShell filho com o dotnet falso na frente do PATH.
    function Invoke-ScriptComDotnetFalso([string]$PastaLogs, [string[]]$Argumentos) {
        # Start-Process com saida em arquivo (nao em pipe): o .cmd falso herda os handles do filho e
        # um "| Out-String" ficaria esperando o pipe fechar ate o falso terminar.
        $saidaArq = Join-Path $tmp ('saida_{0}.txt' -f [guid]::NewGuid().ToString('N'))
        $erroArq = Join-Path $tmp ('erro_{0}.txt' -f [guid]::NewGuid().ToString('N'))
        $lista = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $script),
            '-SemBuild', '-EsperaSegundos', '2', '-Url', $urlTeste, '-PastaLogs', ('"{0}"' -f $PastaLogs)) + $Argumentos
        try {
            [Environment]::SetEnvironmentVariable('PATH', "$fakeDir;$pathOriginal", 'Process')
            $p = Start-Process -FilePath $psExe -ArgumentList $lista -Wait -PassThru -NoNewWindow `
                -RedirectStandardOutput $saidaArq -RedirectStandardError $erroArq
        }
        finally {
            [Environment]::SetEnvironmentVariable('PATH', $pathOriginal, 'Process')
        }
        $texto = (Get-Content -LiteralPath $saidaArq -Raw) + (Get-Content -LiteralPath $erroArq -Raw)
        return [pscustomobject]@{ Saida = $texto; Codigo = $p.ExitCode }
    }

    # Le o log do .cmd falso como NOME=VALOR (sem aparar o valor: o formato tem espaco no fim).
    function Read-VariaveisDoLog([string]$PastaLogs) {
        $pidJson = Get-Content -LiteralPath (Join-Path $PastaLogs 'iaapi.pid.json') -Raw | ConvertFrom-Json
        $mapa = @{}
        foreach ($linha in (Get-Content -LiteralPath $pidJson.Log)) {
            $i = $linha.IndexOf('=')
            if ($i -gt 0) { $mapa[$linha.Substring(0, $i)] = $linha.Substring($i + 1) }
        }
        return [pscustomobject]@{ Pid = $pidJson.Pid; Mapa = $mapa; Registro = $pidJson }
    }

    function Stop-ProcessoFalso($idProcesso) {
        # Limpeza: sem erro se o falso ja tiver terminado (taskkill em stderr viraria excecao).
        # Encerra a arvore (cmd + ping) pelo cmd, para o erro do taskkill nao virar excecao.
        if ($idProcesso) { & cmd.exe /c "taskkill /PID $idProcesso /T /F >nul 2>&1" }
    }

    try {
        # 3a. Padrao: formatador explicito, horario, SQL do EF em Warning, tudo desligado.
        $pastaA = Join-Path $tmp 'logsA'
        $rA = Invoke-ScriptComDotnetFalso $pastaA @()
        Confere ($rA.Codigo -eq 0) 'padrao: o script termina com codigo 0'
        $lidoA = Read-VariaveisDoLog $pastaA
        try {
            $m = $lidoA.Mapa
            Confere ($m['Logging__Console__FormatterName'] -eq 'simple') 'padrao: FormatterName = simple (sem ele o horario e descartado)'
            Confere ($m['Logging__Console__FormatterOptions__TimestampFormat'] -eq 'yyyy-MM-dd HH:mm:ss ') 'padrao: TimestampFormat = "yyyy-MM-dd HH:mm:ss "'
            Confere ($m['Logging__Console__FormatterOptions__ColorBehavior'] -eq 'Disabled') 'padrao: ColorBehavior = Disabled'
            Confere ($m['Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command'] -eq 'Warning') 'padrao: SQL do EF (Database.Command) em Warning'
            Confere ($m['IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY'] -eq '0') 'padrao: reuso do historico desligado (0)'
            Confere ($m['IAAPI_PERF_DIAGNOSTICS'] -eq '0') 'padrao: PERF_DIAG desligado (0)'
            Confere ($m['AnomalyDetection__HistoricoCausal__Habilitado'] -eq 'false') 'padrao: HistoricoCausal = false'
            Confere ($m['AnomalyDetection__RegimeCombinado__Habilitado'] -eq 'false') 'padrao: RegimeCombinado = false'
            # Nao pode haver filtro global: so a categoria do SQL do EF muda; o resto segue o appsettings.
            Confere (-not $m.ContainsKey('Logging__LogLevel__Default')) 'padrao: nao mexe em Logging:LogLevel:Default (o appsettings continua valendo)'

            # config.json: registra os mesmos valores e o estado das chaves.
            $cfgA = Get-Content -LiteralPath (Get-ChildItem -LiteralPath $pastaA -Filter 'iaapi_*.config.json' | Select-Object -First 1).FullName -Raw | ConvertFrom-Json
            Confere ($cfgA.ReusarHistorico -eq $false) 'config.json (padrao): ReusarHistorico = false'
            $vars = $cfgA.Variaveis
            Confere ($vars.'Logging__Console__FormatterName' -eq 'simple') 'config.json: registra FormatterName = simple'
            Confere ($vars.'Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command' -eq 'Warning') 'config.json: registra o filtro do SQL do EF'
            Confere ($vars.'IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY' -eq '0') 'config.json (padrao): variavel do reuso = 0'
        }
        finally { Stop-ProcessoFalso $lidoA.Pid }

        # 3b. -ReusarHistorico liga so o reuso.
        $pastaB = Join-Path $tmp 'logsB'
        $rB = Invoke-ScriptComDotnetFalso $pastaB @('-ReusarHistorico')
        Confere ($rB.Codigo -eq 0) '-ReusarHistorico: o script termina com codigo 0'
        $lidoB = Read-VariaveisDoLog $pastaB
        try {
            Confere ($lidoB.Mapa['IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY'] -eq '1') '-ReusarHistorico: variavel do reuso = 1 (valor exato que o gate exige)'
            Confere ($lidoB.Mapa['IAAPI_PERF_DIAGNOSTICS'] -eq '0') '-ReusarHistorico: nao liga o PERF_DIAG junto'
            Confere ($lidoB.Mapa['Logging__Console__FormatterName'] -eq 'simple') '-ReusarHistorico: continua com horario e FormatterName'
            $cfgB = Get-Content -LiteralPath (Get-ChildItem -LiteralPath $pastaB -Filter 'iaapi_*.config.json' | Select-Object -First 1).FullName -Raw | ConvertFrom-Json
            Confere ($cfgB.ReusarHistorico -eq $true) 'config.json (-ReusarHistorico): ReusarHistorico = true'
            Confere ($cfgB.Variaveis.'IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY' -eq '1') 'config.json (-ReusarHistorico): variavel do reuso = 1'
        }
        finally { Stop-ProcessoFalso $lidoB.Pid }

        # 3c. Variaveis esquecidas na sessao do PowerShell nao vazam para a IAApi.
        $pastaC = Join-Path $tmp 'logsC'
        try {
            [Environment]::SetEnvironmentVariable('IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY', '1', 'Process')
            [Environment]::SetEnvironmentVariable('IAAPI_PERF_DIAGNOSTICS', '1', 'Process')
            $rC = Invoke-ScriptComDotnetFalso $pastaC @()
        }
        finally {
            [Environment]::SetEnvironmentVariable('IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY', $reuseOriginal, 'Process')
            [Environment]::SetEnvironmentVariable('IAAPI_PERF_DIAGNOSTICS', $perfOriginal, 'Process')
        }
        $lidoC = Read-VariaveisDoLog $pastaC
        try {
            Confere ($rC.Codigo -eq 0) 'sessao com variaveis esquecidas: o script termina com codigo 0'
            Confere ($lidoC.Mapa['IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY'] -eq '0') 'IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY=1 esquecido na sessao e forcado a 0 sem o switch'
            Confere ($lidoC.Mapa['IAAPI_PERF_DIAGNOSTICS'] -eq '0') 'IAAPI_PERF_DIAGNOSTICS=1 esquecido na sessao e forcado a 0 sem o switch'
        }
        finally { Stop-ProcessoFalso $lidoC.Pid }

        # 3d. -ReusarHistorico fora de Development: recusa antes de criar qualquer arquivo.
        $pastaD = Join-Path $tmp 'logsD'
        $rD = Invoke-ScriptComDotnetFalso $pastaD @('-ReusarHistorico', '-Ambiente', 'Production')
        Confere ($rD.Codigo -ne 0) '-ReusarHistorico -Ambiente Production: o script recusa (codigo diferente de 0)'
        Confere ($rD.Saida -match 'ReusarHistorico exige -Ambiente Development') '-ReusarHistorico -Ambiente Production: mensagem de recusa esperada'
        Confere (-not (Test-Path -LiteralPath $pastaD)) '-ReusarHistorico -Ambiente Production: nenhuma pasta/arquivo criado'
    }
    finally {
        [Environment]::SetEnvironmentVariable('PATH', $pathOriginal, 'Process')
        Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($falhas -gt 0) {
    Write-Host "RESULTADO: $falhas falha(s)."
    exit 1
}
Write-Host 'RESULTADO: todos os testes passaram.'
exit 0
