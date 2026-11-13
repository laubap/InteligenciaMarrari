<#
.SYNOPSIS
    Exporta as decisoes gravadas pela IAApi local (AnomaliaDetectada) para um
    CSV que o subcomando "placar" do replay offline CP300 consegue ler.

.DESCRIPTION
    SOMENTE LEITURA. Faz um unico GET no mesmo endpoint que o LiveMonitor usa
    para as decisoes (tools/IAApi.LiveMonitor/server/proxy.ts:
    /monitor-api/decisions -> /api/anomalias):

        GET {ApiUrl}/api/anomalias      (Controllers/AnomaliaController.cs : Listar)

    O endpoint devolve TODAS as linhas de AnomaliaDetectada (todos os clientes,
    sem filtro nem paginacao, da mais recente para a mais antiga). O filtro por
    cliente e por periodo e feito aqui, antes de gravar; linhas de outros
    clientes nao vao para o arquivo. A tabela recebe uma linha por decisao
    (anomalia ou nao): PredictionFinalizationService.Finalizar chama
    AnomaliaHistoricoService.Registrar em toda leitura processada.

    HORA (P18 em Docs/PROBLEMAS_ENCONTRADOS.md): AnomaliaDetectada.DataDeteccao
    e DateTime.Now no momento do PROCESSAMENTO, nao a hora da leitura. O
    endpoint nao informa a hora da leitura. Por isso:
      - data_processamento = DataDeteccao como veio da API (com segundos);
      - data_hora          = minuto de (DataDeteccao - DefasagemMinutos).
                             Com o padrao 0, e o minuto do processamento.
                             E a coluna que o placar usa; NAO e a hora da leitura.
      - base_data_hora     = como data_hora foi obtida.
    O filtro -Inicio/-Fim tambem e sobre a hora do processamento.

    Saida (UTF-8, virgula, ponto decimal, booleanos 1/0), uma linha por decisao:
        data_hora,cliente,tag,valor,decisao_final,nota_final,erro,
        data_processamento,base_data_hora,id,tipo_tag,criticidade,classificacao_risco
    O placar exige data_hora, tag e decisao_final; erro sai sempre vazio (a
    tabela so tem decisoes concluidas).

    Por padrao so aceita ApiUrl em loopback (localhost, 127.0.0.1, ::1).

.PARAMETER ApiUrl
    URL base da IAApi local. Padrao: http://localhost:5145 (o mesmo do LiveMonitor).

.PARAMETER Cliente
    ClienteId a manter. Padrao: PK2.

.PARAMETER Inicio
    Inicio do periodo (hora do processamento, relogio da IAApi), inclusivo.
    Ex.: '2026-11-11 15:00'.

.PARAMETER Fim
    Fim do periodo (hora do processamento), inclusivo.

.PARAMETER DefasagemMinutos
    Minutos subtraidos de DataDeteccao para formar data_hora (padrao 0). So use
    um valor diferente de 0 se a defasagem leitura -> processamento tiver sido
    medida; o valor fica registrado em base_data_hora.

.PARAMETER Saida
    CSV de saida. Padrao: dados\anomalias_ia_<cliente>_<inicio>_<fim>.csv.
    Se ja existir, exige -Sobrescrever.

.PARAMETER TimeoutSeg
    Timeout do GET (padrao 300 s; o endpoint nao pagina e calcula um resumo
    de feedback por linha).

.PARAMETER PermitirRemoto
    Aceita ApiUrl fora do loopback. Use so com autorizacao.

.EXAMPLE
    .\tools\Cp300Export\Export-AnomaliasIaReal.ps1 -Inicio '2026-11-11 15:06' -Fim '2026-11-11 17:40'

    dotnet run --project tools/Cp300OfflineReplay -- placar `
        --replay .\dados\anomalias_ia_PK2_20261111_1506_20261111_1740.csv `
        --gabarito .\dados\cp300_10a11nov.csv --saida .\dados\placar_preset6_ia_real
#>
[CmdletBinding()]
param(
    [string]$ApiUrl = 'http://localhost:5145',
    [string]$Cliente = 'PK2',
    [Parameter(Mandatory = $true)][string]$Inicio,
    [Parameter(Mandatory = $true)][string]$Fim,
    [ValidateRange(0, 1440)][int]$DefasagemMinutos = 0,
    [string]$Saida,
    [switch]$Sobrescrever,
    [ValidateRange(5, 3600)][int]$TimeoutSeg = 300,
    [switch]$PermitirRemoto
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$inv = [System.Globalization.CultureInfo]::InvariantCulture
$formatos = [string[]]@('yyyy-MM-dd HH:mm', 'yyyy-MM-dd HH:mm:ss', "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", 'yyyy-MM-dd')

function Converter-Data([string]$texto, [string]$nome) {
    $d = [DateTime]::MinValue
    if (-not [DateTime]::TryParseExact($texto, $formatos, $inv, [System.Globalization.DateTimeStyles]::None, [ref]$d)) {
        throw "$nome invalido: '$texto'. Use yyyy-MM-dd HH:mm[:ss]."
    }
    $d
}

# DataDeteccao vem sem fuso (DateTime.Now, Kind Unspecified). Le os campos do
# relogio como estao, sem converter fuso. No PowerShell 7 o ConvertFrom-Json
# pode entregar [DateTime]; no 5.1 entrega texto.
function Converter-DataApi($valor) {
    if ($null -eq $valor) { return $null }
    if ($valor -is [DateTime]) {
        if ($valor.Kind -eq [DateTimeKind]::Utc) {
            throw "DataDeteccao veio em UTC ($valor); o script espera hora sem fuso, como a IAApi grava."
        }
        return [pscustomobject]@{ Data = $valor; Texto = $valor.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", $inv) }
    }
    $texto = [string]$valor
    $m = [regex]::Match($texto, '^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(\.\d+)?$')
    if (-not $m.Success) {
        throw "DataDeteccao em formato inesperado: '$texto' (esperado sem fuso, ex.: 2026-11-11T15:32:10.123)."
    }
    $d = [DateTime]::ParseExact($m.Groups[1].Value, "yyyy-MM-dd'T'HH:mm:ss", $inv)
    if ($m.Groups[2].Success) {
        $d = $d.AddTicks([long][Math]::Round([double]('0' + $m.Groups[2].Value) * [TimeSpan]::TicksPerSecond))
    }
    [pscustomobject]@{ Data = $d; Texto = $texto }
}

# Campo pelo nome camelCase (padrao do ASP.NET Core) ou PascalCase.
function Campo($obj, [string]$nome) {
    $pascal = $nome.Substring(0, 1).ToUpperInvariant() + $nome.Substring(1)
    # No 5.1 o JavaScriptSerializer devolve Dictionary[string,object], que nao
    # expoe Contains(chave) publicamente (implementacao explicita de
    # IDictionary); o PowerShell nao acha a sobrecarga. Usa ContainsKey.
    if ($obj -is [System.Collections.Generic.IDictionary[string, object]]) {
        foreach ($n in @($nome, $pascal)) { if ($obj.ContainsKey($n)) { return $obj[$n] } }
        return $null
    }
    if ($obj -is [System.Collections.IDictionary]) {
        foreach ($n in @($nome, $pascal)) { if (([System.Collections.IDictionary]$obj).Contains($n)) { return $obj[$n] } }
        return $null
    }
    foreach ($n in @($nome, $pascal)) {
        $p = $obj.PSObject.Properties[$n]
        if ($null -ne $p) { return $p.Value }
    }
    $null
}

function Csv([object]$v) {
    if ($null -eq $v) { return '' }
    if ($v -is [bool]) { if ($v) { return '1' } else { return '0' } }
    if ($v -is [double] -or $v -is [single] -or $v -is [decimal]) { return ([double]$v).ToString('G9', $inv) }
    if ($v -is [int] -or $v -is [long]) { return ([long]$v).ToString($inv) }
    $s = [string]$v
    if ($s -match '[",\r\n]') { return '"' + $s.Replace('"', '""') + '"' }
    $s
}

function Ler-Json([string]$json) {
    if ($PSVersionTable.PSVersion.Major -ge 6) { return ($json | ConvertFrom-Json) }
    # Windows PowerShell 5.1: ConvertFrom-Json tem limite de tamanho; o
    # JavaScriptSerializer com MaxJsonLength alto nao tem, e mantem datas como texto.
    Add-Type -AssemblyName System.Web.Extensions
    $ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $ser.MaxJsonLength = [int]::MaxValue
    $ser.RecursionLimit = 256
    , $ser.DeserializeObject($json)
}

# --- Validacao dos parametros ---------------------------------------------
$ini = Converter-Data $Inicio '-Inicio'
$fimData = Converter-Data $Fim '-Fim'
if ($fimData -lt $ini) { throw '-Fim anterior a -Inicio.' }
if ([string]::IsNullOrWhiteSpace($Cliente)) { throw '-Cliente vazio.' }

$uri = [Uri]$ApiUrl
if ($uri.Scheme -notin @('http', 'https') -or $uri.UserInfo) { throw 'ApiUrl deve ser HTTP(S), sem credenciais.' }
if (-not $uri.IsLoopback -and -not $PermitirRemoto) {
    throw "ApiUrl '$ApiUrl' nao e local. O script e para a IAApi local; use -PermitirRemoto so com autorizacao."
}

$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $Saida) {
    $Saida = Join-Path 'dados' ("anomalias_ia_{0}_{1}_{2}.csv" -f $Cliente, $ini.ToString('yyyyMMdd_HHmm', $inv), $fimData.ToString('yyyyMMdd_HHmm', $inv))
}
$saidaPath = if ([System.IO.Path]::IsPathRooted($Saida)) { $Saida } else { Join-Path $raiz $Saida }
if ((Test-Path -LiteralPath $saidaPath) -and -not $Sobrescrever) {
    throw "Ja existe $saidaPath. Use -Sobrescrever."
}

# --- Consulta (unico GET) -------------------------------------------------
$url = $uri.GetLeftPart([UriPartial]::Authority) + '/api/anomalias'
Write-Host "GET $url (timeout $TimeoutSeg s)"
$relogio = [Diagnostics.Stopwatch]::StartNew()
$resp = Invoke-WebRequest -Uri $url -Method Get -UseBasicParsing -TimeoutSec $TimeoutSeg -MaximumRedirection 0 `
    -Headers @{ Accept = 'application/json' }
# Decodifica como UTF-8 sem depender do charset informado.
$json = [System.Text.Encoding]::UTF8.GetString($resp.RawContentStream.ToArray())
$relogio.Stop()
Write-Host ("Resposta HTTP {0}, {1:N0} bytes, {2:N1} s" -f [int]$resp.StatusCode, $json.Length, $relogio.Elapsed.TotalSeconds)

$registros = @(Ler-Json $json)
if ($registros.Count -eq 1 -and $registros[0] -is [System.Array]) { $registros = @($registros[0]) }

# --- Filtro e conversao ---------------------------------------------------
$base = if ($DefasagemMinutos -eq 0) { 'processamento' } else { "processamento-menos-$DefasagemMinutos-min" }
$total = 0; $outrosClientes = 0; $foraPeriodo = 0
$linhas = New-Object System.Collections.Generic.List[object]
foreach ($r in $registros) {
    $total++
    if ([string](Campo $r 'clienteId') -cne $Cliente) { $outrosClientes++; continue }
    $proc = Converter-DataApi (Campo $r 'dataDeteccao')
    if ($null -eq $proc) { throw "Registro $(Campo $r 'id') sem DataDeteccao." }
    if ($proc.Data -lt $ini -or $proc.Data -gt $fimData) { $foraPeriodo++; continue }

    $ref = $proc.Data.AddMinutes(-$DefasagemMinutos)
    $minuto = New-Object DateTime ($ref.Ticks - ($ref.Ticks % [TimeSpan]::TicksPerMinute))
    $ehAnomalia = Campo $r 'ehAnomalia'
    if ($ehAnomalia -isnot [bool]) { throw "Registro $(Campo $r 'id'): ehAnomalia ausente ou nao booleano." }

    $linhas.Add([pscustomobject]@{
        Ordem = $proc.Data
        Tag   = [string](Campo $r 'tagName')
        Id    = Campo $r 'id'
        Texto = @(
            $minuto.ToString("yyyy-MM-dd'T'HH:mm:ss", $inv),
            $Cliente,
            [string](Campo $r 'tagName'),
            (Campo $r 'valor'),
            $ehAnomalia,
            (Campo $r 'score'),
            '',
            $proc.Texto,
            $base,
            (Campo $r 'id'),
            (Campo $r 'tipoTag'),
            (Campo $r 'criticidade'),
            (Campo $r 'classificacaoRisco')
        ) | ForEach-Object { Csv $_ }
    })
}

$cabecalho = 'data_hora,cliente,tag,valor,decisao_final,nota_final,erro,data_processamento,base_data_hora,id,tipo_tag,criticidade,classificacao_risco'
$conteudo = New-Object System.Text.StringBuilder
[void]$conteudo.Append($cabecalho).Append("`n")
foreach ($l in ($linhas | Sort-Object Ordem, Tag, Id)) {
    [void]$conteudo.Append(($l.Texto -join ',')).Append("`n")
}

$pasta = Split-Path -Parent $saidaPath
if ($pasta) { New-Item -ItemType Directory -Force -Path $pasta | Out-Null }
[System.IO.File]::WriteAllText($saidaPath, $conteudo.ToString(), (New-Object System.Text.UTF8Encoding($false)))

$anomalias = @($linhas | Where-Object { $_.Texto[4] -eq '1' }).Count
$tags = @($linhas | ForEach-Object { $_.Tag } | Sort-Object -Unique).Count
Write-Host ("Registros na API: {0} | outros clientes: {1} | fora do periodo: {2} | gravados: {3} ({4} tags, {5} anomalias)" -f `
    $total, $outrosClientes, $foraPeriodo, $linhas.Count, $tags, $anomalias)
Write-Host "data_hora = minuto do PROCESSAMENTO (base: $base), nao a hora da leitura (P18)."
Write-Host "Saida: $saidaPath"
