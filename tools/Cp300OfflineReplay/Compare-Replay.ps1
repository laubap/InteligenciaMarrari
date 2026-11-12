<#
.SYNOPSIS
    RC-2: referencia congelada e regressao byte a byte do replay offline CP300.

.DESCRIPTION
    Modo -Gerar: roda as execucoes de referencia com o codigo atual e grava um
    manifesto (comandos, SHA-256 da entrada, do appsettings.json e de cada .csv
    de saida) e uma copia de cada .csv/.log em <pasta do manifesto>.

    Modo padrao (comparar): roda de novo os comandos do manifesto numa pasta
    nova e compara
      - cada .csv byte a byte (SHA-256);
      - cada .log linha a linha, ignorando so a linha "Saida: <caminho>", que
        traz o caminho absoluto do arquivo gerado. O replay nao grava hora de
        execucao nem duracao no log (conferido em Program.cs e Cp300Replay.cs).

    Isolamento: so chama o replay offline (SQLite em memoria, historico lido do
    CSV). Nao acessa Bridge, PSi4, CLP nem ia_config.db. As saidas ficam em
    dados/ (fora do Git).

    Codigo de saida: 0 = 0 diferencas; 1 = diferencas ou falha de execucao;
    2 = uso incorreto (manifesto ausente, entrada alterada etc.).

.EXAMPLE
    # Uma vez, no codigo de referencia:
    .\tools\Cp300OfflineReplay\Compare-Replay.ps1 -Gerar

    # Depois de cada mudanca (ou duas vezes seguidas, para o aceite da RC-2):
    .\tools\Cp300OfflineReplay\Compare-Replay.ps1
#>
[CmdletBinding()]
param(
    # Manifesto da referencia. A pasta dele guarda as copias de referencia.
    [string]$Manifesto = 'dados\rc2_referencia\manifesto.json',
    # Gera (ou substitui, com -Sobrescrever) a referencia em vez de comparar.
    [switch]$Gerar,
    [switch]$Sobrescrever,
    # So no -Gerar: CSV de entrada das execucoes padrao (o mesmo da etapa 0).
    [string]$Entrada = 'dados\cp300_10a11nov_derivado.csv',
    # Pasta das saidas desta rodada. Padrao: dados\rc2_rodadas\<data_hora>.
    [string]$Pasta,
    # Restringe a algumas execucoes pelo nome (ex.: preset6_rec).
    [string[]]$Somente,
    [string]$Configuracao = 'Debug',
    # Nao recompila o replay antes de rodar.
    [switch]$SemBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projeto = Join-Path $raiz 'tools\Cp300OfflineReplay\IAApi.Cp300OfflineReplay.csproj'
$utf8SemBom = New-Object System.Text.UTF8Encoding($false)

function Caminho([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return Join-Path $raiz $p
}

function Sha256([string]$p) { (Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash.ToLowerInvariant() }

# Execucoes padrao: sem e rec dos tres cenarios da etapa 0 (PLANO_REGIME_COMBINADO.md, 5.2).
# As da RC-0 (CSV de 48 h) entram aqui quando o CSV existir.
$tags = 'TAG22,TAG23,TAG24,TAG25,TAG26,TAG27,TAG28,TAG29,TAG30,TAG31,TAG32,TAG33,TAG44'
$rec = @('--regime', 'TAG28=TAG43,TAG30=TAG43,TAG32=TAG43,TAG26=TAG45,TAG24=TAG46')
$cenarios = @(
    @{ Nome = 'preset1';   Args = @('--treino-ate', '2026-11-11T12:55', '--fim', '2026-11-11T14:25') },
    @{ Nome = 'madrugada'; Args = @('--treino-ate', '2026-11-11T00:00', '--fim', '2026-11-11T05:30') },
    @{ Nome = 'preset6';   Args = @('--treino-ate', '2026-11-11T15:06', '--fim', '2026-11-11T17:40', '--permitir-falha-no-treino') }
)

function ExecucoesPadrao {
    foreach ($c in $cenarios) {
        foreach ($cfg in @('sem', 'rec')) {
            $a = @('--cliente', 'PK2', '--tags', $tags) + $c.Args
            if ($cfg -eq 'rec') { $a += $rec }
            [pscustomobject]@{ nome = "$($c.Nome)_$cfg"; args = $a }
        }
    }
}

function Rodar($execucao, [string]$entrada, [string]$pastaSaida) {
    $csv = Join-Path $pastaSaida "$($execucao.nome).csv"
    $log = Join-Path $pastaSaida "$($execucao.nome).log"
    $argv = @('--entrada', $entrada, '--saida', $csv) + @($execucao.args)
    Write-Host ("  {0} ..." -f $execucao.nome) -NoNewline
    $inicio = Get-Date
    # Diretorio atual = raiz do repo: o replay le o appsettings.json daqui.
    $linhas = & dotnet run --project $projeto -c $Configuracao --no-build --no-launch-profile -- @argv
    $codigo = $LASTEXITCODE
    [System.IO.File]::WriteAllLines($log, [string[]]@($linhas), $utf8SemBom)
    Write-Host (" codigo {0}, {1:N0} s" -f $codigo, ((Get-Date) - $inicio).TotalSeconds)
    if ($codigo -ne 0) { throw "Execucao $($execucao.nome) terminou com codigo $codigo (log: $log)" }
    [pscustomobject]@{ Csv = $csv; Log = $log }
}

function LinhasComparaveis([string]$log) {
    @(Get-Content -LiteralPath $log -Encoding UTF8 | Where-Object { $_ -notmatch '^Saida: ' })
}

Push-Location $raiz
try {
    $manifestoPath = Caminho $Manifesto
    $pastaRef = Split-Path -Parent $manifestoPath
    $appsettings = Join-Path $raiz 'appsettings.json'

    if (-not $SemBuild) {
        Write-Host "Compilando o replay ($Configuracao)..."
        & dotnet build $projeto -c $Configuracao --nologo -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "dotnet build falhou ($LASTEXITCODE)" }
    }

    if ($Gerar) {
        if ((Test-Path -LiteralPath $manifestoPath) -and -not $Sobrescrever) {
            Write-Error "Ja existe $manifestoPath. Use -Sobrescrever para gerar outra referencia."
            exit 2
        }
        $entradaPath = Caminho $Entrada
        if (-not (Test-Path -LiteralPath $entradaPath)) { Write-Error "Entrada ausente: $entradaPath"; exit 2 }
        New-Item -ItemType Directory -Force -Path $pastaRef | Out-Null

        $execucoes = @(ExecucoesPadrao)
        if ($Somente) { $execucoes = @($execucoes | Where-Object { $Somente -contains $_.nome }) }

        $commit = (& git -C $raiz rev-parse HEAD).Trim()
        $sujo = [bool](& git -C $raiz status --porcelain --untracked-files=no)
        Write-Host "Gerando referencia em $pastaRef (commit $commit$(if ($sujo) { ', com mudancas locais' }))"
        $itens = foreach ($e in $execucoes) {
            $r = Rodar $e $entradaPath $pastaRef
            [pscustomobject]@{
                nome       = $e.nome
                args       = @($e.args)
                comando    = "dotnet run --project tools/Cp300OfflineReplay -- --entrada $Entrada --saida <pasta>\$($e.nome).csv " + (($e.args | ForEach-Object { if ($_ -match ',') { "'$_'" } else { $_ } }) -join ' ')
                csv_sha256 = Sha256 $r.Csv
                csv_bytes  = (Get-Item -LiteralPath $r.Csv).Length
                log        = Split-Path -Leaf $r.Log
            }
        }
        $m = [pscustomobject]@{
            tarefa             = 'RC-2'
            gerado_em          = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss')
            commit             = $commit
            mudancas_locais    = $sujo
            configuracao       = $Configuracao
            entrada            = $Entrada
            entrada_sha256     = Sha256 $entradaPath
            appsettings_sha256 = Sha256 $appsettings
            execucoes          = @($itens)
        }
        [System.IO.File]::WriteAllText($manifestoPath, ($m | ConvertTo-Json -Depth 5), $utf8SemBom)
        Write-Host "Manifesto: $manifestoPath ($(@($itens).Count) execucoes)"
        exit 0
    }

    # --- Comparar ---------------------------------------------------------
    if (-not (Test-Path -LiteralPath $manifestoPath)) {
        Write-Error "Manifesto ausente: $manifestoPath. Gere a referencia com -Gerar."
        exit 2
    }
    $m = Get-Content -LiteralPath $manifestoPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $entradaPath = Caminho $m.entrada
    if (-not (Test-Path -LiteralPath $entradaPath)) { Write-Error "Entrada ausente: $entradaPath"; exit 2 }
    if ((Sha256 $entradaPath) -ne $m.entrada_sha256) {
        Write-Error "A entrada $($m.entrada) mudou desde a referencia (SHA-256 diferente). A comparacao nao vale."
        exit 2
    }
    if ((Sha256 $appsettings) -ne $m.appsettings_sha256) {
        Write-Warning 'appsettings.json mudou desde a referencia: pesos/limiares podem explicar diferencas.'
    }

    if (-not $Pasta) { $Pasta = Join-Path 'dados\rc2_rodadas' (Get-Date).ToString('yyyyMMdd_HHmmss') }
    $pastaSaida = Caminho $Pasta
    New-Item -ItemType Directory -Force -Path $pastaSaida | Out-Null

    $execucoes = @($m.execucoes)
    if ($Somente) { $execucoes = @($execucoes | Where-Object { $Somente -contains $_.nome }) }
    Write-Host "Comparando $($execucoes.Count) execucoes com $manifestoPath (referencia: commit $($m.commit))"
    Write-Host "Saidas desta rodada: $pastaSaida"

    $diferencas = 0
    $resultado = foreach ($e in $execucoes) {
        $r = Rodar $e $entradaPath $pastaSaida
        $hash = Sha256 $r.Csv
        $csvOk = $hash -eq $e.csv_sha256

        $refLog = Join-Path $pastaRef $e.log
        $a = LinhasComparaveis $refLog
        $b = LinhasComparaveis $r.Log
        # Linha a linha, na mesma posicao; linhas a mais ou a menos tambem contam.
        $nDif = [Math]::Abs($a.Count - $b.Count)
        for ($i = 0; $i -lt [Math]::Min($a.Count, $b.Count); $i++) {
            if (-not [string]::Equals($a[$i], $b[$i], [StringComparison]::Ordinal)) { $nDif++ }
        }
        $logOk = $nDif -eq 0
        if (-not $csvOk) { $diferencas++ }
        if (-not $logOk) { $diferencas++ }
        [pscustomobject]@{
            Execucao = $e.nome
            Csv      = if ($csvOk) { 'igual' } else { "DIFERENTE ($hash)" }
            Log      = if ($logOk) { 'igual' } else { "DIFERENTE ($nDif linhas)" }
        }
    }
    $resultado | Format-Table -AutoSize | Out-String | Write-Host
    if ($diferencas -eq 0) {
        Write-Host "RESULTADO: 0 diferencas em $($execucoes.Count) execucoes."
        exit 0
    }
    Write-Host "RESULTADO: $diferencas diferenca(s). Referencia em $pastaRef; saidas em $pastaSaida."
    exit 1
}
finally {
    Pop-Location
}
