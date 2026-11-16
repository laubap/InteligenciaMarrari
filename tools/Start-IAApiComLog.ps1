<#
.SYNOPSIS
    Compila e sobe a IAApi em segundo plano (janela escondida), com a saida
    gravada em arquivo com data e hora, e salva o PID para o Stop-IAApi.ps1.

.DESCRIPTION
    Automatiza o passo "a" de P27 ("Como confirmar", Docs/PROBLEMAS_ENCONTRADOS.md)
    e a execucao aprovada da BACKLOG 6: a saida deixa de depender da janela do
    console (hipotese 1 do P27: console bloqueado no modo "Selecionar") e os
    logs Information ficam guardados com data e hora.

    ATENCAO: a IAApi sobe com o hosted service MonitoramentoTagsService, que
    le o Bridge, roda a deteccao e GRAVA decisoes no banco (ia_config.db).
    So execute numa sessao de teste autorizada.

    Passos:
      0. Recusa subir se a -Url nao for local (localhost, 127.0.0.1 ou [::1]):
         a IAApi nao tem autenticacao (P34, Docs/PROBLEMAS_ENCONTRADOS.md).
      1. Recusa subir se o PID salvo ainda estiver vivo ou se a porta da -Url
         ja estiver em uso.
      2. dotnet build IAApi.csproj -c <Configuracao> (pule com -SemBuild).
      3. Define as variaveis de ambiente abaixo SO para o processo filho
         (os valores anteriores da sessao sao restaurados no final).
      4. Start-Process dotnet bin\<Configuracao>\net10.0\IAApi.dll, com
         -WindowStyle Hidden, diretorio de trabalho = raiz do repositorio
         (o SQLite usa o caminho relativo "ia_config.db"), stdout e stderr
         redirecionados para dados\logs.
      5. Confere apos alguns segundos se o processo continua vivo e grava
         dados\logs\iaapi.pid.json (lido pelo Stop-IAApi.ps1) e uma copia
         dados\logs\iaapi_<data>.config.json ao lado do log.

    Variaveis de ambiente definidas (sobrepoem appsettings*.json):
      ASPNETCORE_ENVIRONMENT                               = -Ambiente (Development)
      ASPNETCORE_URLS                                      = -Url (http://localhost:5145)
      Logging__Console__FormatterOptions__TimestampFormat  = "yyyy-MM-dd HH:mm:ss "
      Logging__Console__FormatterOptions__ColorBehavior    = Disabled
      AnomalyDetection__HistoricoCausal__Habilitado        = true so com -HistoricoCausal
      AnomalyDetection__RegimeCombinado__Habilitado        = true so com -RegimeCombinado
      IAAPI_PERF_DIAGNOSTICS                               = 1 so com -PerfDiag (senao 0)
    As tres chaves sao SEMPRE definidas (false/0 quando o switch nao e passado),
    para que o estado fique explicito e registrado no config.json do log. No
    caso do PERF_DIAG, o 0 explicito tambem impede que um
    IAAPI_PERF_DIAGNOSTICS=1 esquecido na sessao do PowerShell ligue o
    diagnostico sem querer.

    Arquivos (pasta dados\ e ignorada pelo git):
      dados\logs\iaapi_<yyyyMMdd_HHmmss>.log         stdout (logs do console)
      dados\logs\iaapi_<yyyyMMdd_HHmmss>.err.log     stderr
      dados\logs\iaapi_<yyyyMMdd_HHmmss>.config.json parametros desta subida
      dados\logs\iaapi.pid.json                      PID da ultima subida

    Limites: o log cresce sem rotacao; o processo nao tem janela, entao a
    parada e pelo Stop-IAApi.ps1 (encerramento forcado, sem Ctrl+C).

.PARAMETER HistoricoCausal
    Liga AnomalyDetection:HistoricoCausal:Habilitado (P31). Desligado por padrao.

.PARAMETER RegimeCombinado
    Liga AnomalyDetection:RegimeCombinado:Habilitado (RC-8). Desligado por padrao.

.PARAMETER PerfDiag
    Liga o PERF_DIAG (IAAPI_PERF_DIAGNOSTICS=1; Docs/performance-diagnostics.md),
    para a etapa 0 da BACKLOG 6 (6.0). Desligado por padrao. So instrumenta:
    nao muda a decisao de anomalia, mas acrescenta linhas "PERF_DIAG" ao log e
    um pequeno custo por predicao. O PERF_DIAG so liga com o ambiente
    Development; com -PerfDiag e outro -Ambiente, o script recusa subir.

.PARAMETER Configuracao
    Debug (padrao, o mesmo do "dotnet run") ou Release.

.PARAMETER Url
    Endereco de escuta. Padrao: http://localhost:5145 (perfil "http" do
    Properties/launchSettings.json, o mesmo que o LiveMonitor usa).
    So aceita um endereco http/https em localhost, 127.0.0.1 ou [::1] (P34);
    0.0.0.0, *, + ou IP da rede sao recusados antes de qualquer acao.

.PARAMETER Ambiente
    ASPNETCORE_ENVIRONMENT. Padrao: Development (o mesmo do launchSettings).

.PARAMETER SemBuild
    Nao compila; usa a DLL que ja esta em bin\<Configuracao>\net10.0.

.PARAMETER PastaLogs
    Pasta dos logs e do arquivo de PID. Padrao: <raiz>\dados\logs.

.PARAMETER EsperaSegundos
    Segundos de espera antes de conferir se o processo continua vivo (padrao 5).

.EXAMPLE
    .\tools\Start-IAApiComLog.ps1

.EXAMPLE
    .\tools\Start-IAApiComLog.ps1 -HistoricoCausal -RegimeCombinado

.EXAMPLE
    .\tools\Start-IAApiComLog.ps1 -HistoricoCausal -PerfDiag

    Sessao da etapa 0 da BACKLOG 6 (6.0): recorte causal (P31) e PERF_DIAG ligados.

.EXAMPLE
    Get-Content (Get-Content .\dados\logs\iaapi.pid.json | ConvertFrom-Json).Log -Wait -Tail 50
#>
[CmdletBinding()]
param(
    [switch]$HistoricoCausal,
    [switch]$RegimeCombinado,
    [switch]$PerfDiag,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuracao = 'Debug',
    [string]$Url = 'http://localhost:5145',
    [string]$Ambiente = 'Development',
    [switch]$SemBuild,
    [string]$PastaLogs,
    [ValidateRange(1, 120)]
    [int]$EsperaSegundos = 5
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# P34: a IAApi nao tem autenticacao. So aceita escutar na propria maquina.
# Testado por tools\Start-IAApiComLog.Tests.ps1 (le so esta funcao).
function Test-EnderecoLocal([string]$Endereco) {
    $uri = $null
    if (-not [uri]::TryCreate($Endereco, [UriKind]::Absolute, [ref]$uri)) { return $false }
    if (@('http', 'https') -notcontains $uri.Scheme) { return $false }
    if ($uri.HostNameType -eq [UriHostNameType]::Dns) { return $uri.Host -eq 'localhost' }
    # IP: o .NET Framework expande o IPv6 ([::1] vira 0000:...:0001); comparar como endereco.
    $ip = $null
    if (-not [System.Net.IPAddress]::TryParse($uri.DnsSafeHost, [ref]$ip)) { return $false }
    return $ip.Equals([System.Net.IPAddress]::Loopback) -or $ip.Equals([System.Net.IPAddress]::IPv6Loopback)
}

# 0. Endereco so local (antes de qualquer outra acao).
if (-not (Test-EnderecoLocal $Url)) {
    throw (("Endereco '{0}' recusado: a IAApi nao tem autenticacao (P34) e so pode subir em " +
        "localhost, 127.0.0.1 ou [::1] (ex.: http://localhost:5145). Nao subi nada.") -f $Url)
}

# 0b. O PERF_DIAG so liga em Development (PerformanceDiagnostics compara o
# ambiente sem diferenciar maiusculas). Recusar antes de criar qualquer arquivo,
# para nao rodar uma sessao de medicao sem medicao.
if ($PerfDiag -and -not [string]::Equals($Ambiente, 'Development', [StringComparison]::OrdinalIgnoreCase)) {
    throw (("-PerfDiag exige -Ambiente Development (recebido: '{0}'): fora dele o PERF_DIAG " +
        "fica desligado mesmo com IAAPI_PERF_DIAGNOSTICS=1. Nao subi nada.") -f $Ambiente)
}

$raiz = Split-Path -Parent $PSScriptRoot
$projeto = Join-Path $raiz 'IAApi.csproj'
if (-not (Test-Path -LiteralPath $projeto)) {
    throw "IAApi.csproj nao encontrado em '$raiz'. O script deve ficar em <raiz>\tools."
}
if (-not $PastaLogs) { $PastaLogs = Join-Path $raiz 'dados\logs' }
if (-not (Test-Path -LiteralPath $PastaLogs)) {
    New-Item -ItemType Directory -Path $PastaLogs -Force | Out-Null
}
$arquivoPid = Join-Path $PastaLogs 'iaapi.pid.json'

# 1a. Instancia anterior ainda viva?
if (Test-Path -LiteralPath $arquivoPid) {
    $anterior = Get-Content -LiteralPath $arquivoPid -Raw | ConvertFrom-Json
    $procAnterior = Get-Process -Id $anterior.Pid -ErrorAction SilentlyContinue
    if ($procAnterior) {
        $mesmo = $false
        try {
            $inicioSalvo = [datetime]::Parse($anterior.InicioProcesso, $null,
                [System.Globalization.DateTimeStyles]::RoundtripKind)
            $mesmo = [math]::Abs(($procAnterior.StartTime - $inicioSalvo).TotalSeconds) -lt 2
        } catch { $mesmo = $true }
        if ($mesmo) {
            throw ("A IAApi ja esta rodando (PID {0}, log {1}). Pare com tools\Stop-IAApi.ps1 antes." -f
                $anterior.Pid, $anterior.Log)
        }
    }
}

# 1b. Porta livre?
$porta = ([uri]$Url).Port
$ouvindo = $null
try {
    $ouvindo = Get-NetTCPConnection -LocalPort $porta -State Listen -ErrorAction Stop |
        Select-Object -First 1
} catch { $ouvindo = $null }
if ($ouvindo) {
    throw ("A porta {0} ja esta em uso (PID {1}). Outra IAApi rodando? Nao subi nada." -f
        $porta, $ouvindo.OwningProcess)
}

# 2. Build
$dll = Join-Path $raiz ("bin\{0}\net10.0\IAApi.dll" -f $Configuracao)
if (-not $SemBuild) {
    Write-Host "dotnet build IAApi.csproj -c $Configuracao ..."
    & dotnet build $projeto -c $Configuracao --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build falhou (codigo $LASTEXITCODE). Nao subi nada." }
}
if (-not (Test-Path -LiteralPath $dll)) {
    throw "DLL nao encontrada: $dll. Rode sem -SemBuild."
}
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source

# 3. Variaveis de ambiente (so para o processo filho)
$carimbo = Get-Date -Format 'yyyyMMdd_HHmmss'
$log = Join-Path $PastaLogs ("iaapi_{0}.log" -f $carimbo)
$logErro = Join-Path $PastaLogs ("iaapi_{0}.err.log" -f $carimbo)
$arquivoConfig = Join-Path $PastaLogs ("iaapi_{0}.config.json" -f $carimbo)

$variaveis = [ordered]@{
    'ASPNETCORE_ENVIRONMENT'                              = $Ambiente
    'ASPNETCORE_URLS'                                     = $Url
    'Logging__Console__FormatterOptions__TimestampFormat' = 'yyyy-MM-dd HH:mm:ss '
    'Logging__Console__FormatterOptions__ColorBehavior'   = 'Disabled'
    'AnomalyDetection__HistoricoCausal__Habilitado'       = $(if ($HistoricoCausal) { 'true' } else { 'false' })
    'AnomalyDetection__RegimeCombinado__Habilitado'       = $(if ($RegimeCombinado) { 'true' } else { 'false' })
    'IAAPI_PERF_DIAGNOSTICS'                              = $(if ($PerfDiag) { '1' } else { '0' })
}

$salvas = @{}
foreach ($nome in $variaveis.Keys) {
    $salvas[$nome] = [Environment]::GetEnvironmentVariable($nome, 'Process')
}

# 4. Subida
$proc = $null
try {
    foreach ($nome in $variaveis.Keys) {
        [Environment]::SetEnvironmentVariable($nome, $variaveis[$nome], 'Process')
    }
    $proc = Start-Process -FilePath $dotnet `
        -ArgumentList ('"{0}"' -f $dll) `
        -WorkingDirectory $raiz `
        -WindowStyle Hidden `
        -RedirectStandardOutput $log `
        -RedirectStandardError $logErro `
        -PassThru
}
finally {
    foreach ($nome in $salvas.Keys) {
        [Environment]::SetEnvironmentVariable($nome, $salvas[$nome], 'Process')
    }
}

# 5. Conferencia e registro do PID
Start-Sleep -Seconds $EsperaSegundos
$proc.Refresh()
if ($proc.HasExited) {
    Write-Warning ("A IAApi encerrou logo apos subir (codigo {0}). Ultimas linhas:" -f $proc.ExitCode)
    foreach ($arq in @($logErro, $log)) {
        if (Test-Path -LiteralPath $arq) { Get-Content -LiteralPath $arq -Tail 20 }
    }
    throw "Subida falhou. Logs: $log / $logErro"
}

$registro = [ordered]@{
    Pid             = $proc.Id
    InicioProcesso  = $proc.StartTime.ToString('o')
    Log             = $log
    LogErro         = $logErro
    Dll             = $dll
    Url             = $Url
    Ambiente        = $Ambiente
    Configuracao    = $Configuracao
    HistoricoCausal = [bool]$HistoricoCausal
    RegimeCombinado = [bool]$RegimeCombinado
    PerfDiag        = [bool]$PerfDiag
    Variaveis       = $variaveis
}
$json = $registro | ConvertTo-Json -Depth 3
Set-Content -LiteralPath $arquivoPid -Value $json -Encoding UTF8
Set-Content -LiteralPath $arquivoConfig -Value $json -Encoding UTF8

Write-Host ''
Write-Host ("IAApi rodando: PID {0}, {1}" -f $proc.Id, $Url)
Write-Host ("  HistoricoCausal = {0}; RegimeCombinado = {1}; PerfDiag = {2}" -f
    [bool]$HistoricoCausal, [bool]$RegimeCombinado, [bool]$PerfDiag)
Write-Host ("  Log:      {0}" -f $log)
Write-Host ("  Erros:    {0}" -f $logErro)
Write-Host ("  PID em:   {0}" -f $arquivoPid)
Write-Host ("  Acompanhar: Get-Content -LiteralPath '{0}' -Wait -Tail 50" -f $log)
Write-Host  '  Parar:      .\tools\Stop-IAApi.ps1'
