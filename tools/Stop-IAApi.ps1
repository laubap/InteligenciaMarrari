<#
.SYNOPSIS
    Para a IAApi iniciada pelo Start-IAApiComLog.ps1, usando o PID salvo.

.DESCRIPTION
    Le dados\logs\iaapi.pid.json e, antes de encerrar, confere que o PID
    ainda e o mesmo processo (nome "dotnet", hora de inicio igual a salva e
    linha de comando com IAApi.dll). Se algo nao bater (PID reutilizado pelo
    Windows), NAO encerra nada.

    O encerramento e forcado (Stop-Process -Force): o processo nao tem janela
    para receber Ctrl+C. Um ciclo do monitor em andamento e interrompido, e a
    decisao da tag em processamento pode nao ser gravada.

    Se P27 estiver sendo investigado (monitor parado sem aviso), colete as
    evidencias ANTES de parar (P27, "Como confirmar", passo c): ultima linha
    do log, PID, CPU e numero de threads. Use -Diagnostico para imprimir
    esses dados sem encerrar.

    Suporta -WhatIf e -Confirm.

.PARAMETER PastaLogs
    Pasta do iaapi.pid.json. Padrao: <raiz>\dados\logs.

.PARAMETER Diagnostico
    So mostra PID, CPU, threads, memoria e as ultimas linhas do log. Nao para.

.EXAMPLE
    .\tools\Stop-IAApi.ps1

.EXAMPLE
    .\tools\Stop-IAApi.ps1 -Diagnostico
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string]$PastaLogs,
    [switch]$Diagnostico
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
if (-not $PastaLogs) { $PastaLogs = Join-Path $raiz 'dados\logs' }
$arquivoPid = Join-Path $PastaLogs 'iaapi.pid.json'

if (-not (Test-Path -LiteralPath $arquivoPid)) {
    Write-Warning "Arquivo de PID nao encontrado: $arquivoPid. Nada a parar."
    exit 1
}
$registro = Get-Content -LiteralPath $arquivoPid -Raw | ConvertFrom-Json

$proc = Get-Process -Id $registro.Pid -ErrorAction SilentlyContinue
if (-not $proc) {
    Write-Host ("O PID {0} nao esta mais rodando. Removendo {1}." -f $registro.Pid, $arquivoPid)
    if (-not $Diagnostico -and $PSCmdlet.ShouldProcess($arquivoPid, 'Remover arquivo de PID')) {
        Remove-Item -LiteralPath $arquivoPid
    }
    exit 0
}

# Conferencia de identidade (PID pode ter sido reutilizado)
$problemas = @()
if ($proc.ProcessName -ne 'dotnet') {
    $problemas += "nome do processo e '$($proc.ProcessName)', esperado 'dotnet'"
}
try {
    $inicioSalvo = [datetime]::Parse($registro.InicioProcesso, $null,
        [System.Globalization.DateTimeStyles]::RoundtripKind)
    if ([math]::Abs(($proc.StartTime - $inicioSalvo).TotalSeconds) -ge 2) {
        $problemas += ("inicio do processo {0:o} difere do salvo {1}" -f $proc.StartTime, $registro.InicioProcesso)
    }
} catch {
    $problemas += "nao foi possivel ler a hora de inicio do processo ($($_.Exception.Message))"
}
$linhaComando = $null
try {
    $linhaComando = (Get-CimInstance Win32_Process -Filter ("ProcessId = {0}" -f $proc.Id)).CommandLine
} catch { $linhaComando = $null }
if ($linhaComando -and ($linhaComando -notlike '*IAApi.dll*')) {
    $problemas += "linha de comando nao contem IAApi.dll: $linhaComando"
}
if ($problemas.Count -gt 0) {
    Write-Warning ("O PID {0} nao parece ser a IAApi iniciada pelo script. Nada foi encerrado:" -f $proc.Id)
    $problemas | ForEach-Object { Write-Warning "  - $_" }
    exit 2
}

# Evidencias (P27, passo c)
Write-Host ("IAApi PID {0}, iniciada em {1}, {2}" -f $proc.Id, $registro.InicioProcesso, $registro.Url)
Write-Host ("  HistoricoCausal = {0}; RegimeCombinado = {1}" -f $registro.HistoricoCausal, $registro.RegimeCombinado)
Write-Host ("  CPU total: {0:N1} s; threads: {1}; memoria: {2:N0} MB" -f
    $proc.CPU, $proc.Threads.Count, ($proc.WorkingSet64 / 1MB))
Write-Host ("  Log: {0}" -f $registro.Log)
if (Test-Path -LiteralPath $registro.Log) {
    $ultima = (Get-Item -LiteralPath $registro.Log).LastWriteTime
    Write-Host ("  Ultima escrita no log: {0:yyyy-MM-dd HH:mm:ss}" -f $ultima)
    Write-Host '  Ultimas linhas:'
    Get-Content -LiteralPath $registro.Log -Tail 10 | ForEach-Object { Write-Host "    $_" }
}

if ($Diagnostico) { exit 0 }

if ($PSCmdlet.ShouldProcess(("PID {0} (IAApi)" -f $proc.Id), 'Encerrar processo')) {
    Stop-Process -Id $proc.Id -Force
    try { Wait-Process -Id $proc.Id -Timeout 15 -ErrorAction Stop } catch { }
    if (Get-Process -Id $proc.Id -ErrorAction SilentlyContinue) {
        Write-Warning ("O PID {0} ainda esta vivo apos 15 s. Arquivo de PID mantido." -f $proc.Id)
        exit 3
    }
    Remove-Item -LiteralPath $arquivoPid
    Write-Host ("IAApi parada (PID {0}). Logs mantidos em {1}." -f $proc.Id, $PastaLogs)
}
