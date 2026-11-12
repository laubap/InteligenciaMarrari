<#
.SYNOPSIS
    Copia para disco os diagnosticos do Intelligence Lab de uma IAApi LOCAL
    (univariado + fila do shadow multivariado). Somente leitura (GET).

.DESCRIPTION
    Solucao provisoria ate existir o trace de decisao (Docs/PROPOSTA_TRACE_DECISAO.md).

    Le, somente com GET:
      /api/intelligence-lab/health                              (contadores do buffer)
      /api/intelligence-lab/diagnostics?limit=100[&clienteId&tagName]
      /api/intelligence-lab/diagnostics/{id}                    (detalhe de cada leitura nova)
      /api/intelligence-lab/diagnostics/multivariate-shadow?limit=100[&clienteId&groupName]
      /api/intelligence-lab/diagnostics/multivariate-shadow/{id}

    O endpoint NAO tem paginacao: cada chamada devolve os 100 registros mais
    recentes (por filtro). Por isso o script:
      * pede 100 sem filtro, depois 100 por tag e 100 por grupo shadow encontrados
        (ou informados em -Tags / -GruposShadow);
      * busca o detalhe so dos IDs ainda nao vistos (deduplicacao por DiagnosticId);
      * com -IntervaloSegundos > 0, repete ate -DuracaoMinutos ou Ctrl+C,
        regravando os arquivos a cada passada (Ctrl+C nao perde o que ja foi lido).
    Uma tag com mais de 100 leituras novas entre duas passadas perde as mais
    antigas; isso e sinalizado como "possivel lacuna". O buffer da IAApi tambem
    descarta os mais antigos ao encher (BufferCapacity): o script compara
    StoredTotal/EvictedTotal do /health no inicio e no fim.

    Saidas (UTF-8 sem BOM), em -Saida (padrao: dados/):
      ia_real_<yyyy-MM-dd_HHmm>.json         tudo (meta + leituras + shadow), como veio da API
      ia_real_<yyyy-MM-dd_HHmm>.csv          uma linha por detector, colunas do CSV do replay CP300
                                             (mesma ordem) + colunas extras no fim
      ia_real_<yyyy-MM-dd_HHmm>_shadow.csv   uma linha por evento shadow multivariado

    Com -ArquivoJson, NAO acessa a API: so regenera os CSVs a partir de um JSON ja salvo.

    PROTECAO DO ENDPOINT (nao e contornada):
      * a IAApi precisa estar em Development, com IntelligenceLab:Enabled=true,
        as 4 chaves IntelligenceLab:Version:* e IntelligenceLab:ReadAccessKey (>= 32 caracteres);
      * a chamada precisa vir de loopback: -BaseUrl so aceita localhost/127.0.0.1/[::1];
      * a chave vai no cabecalho X-Intelligence-Lab-Key. O script le a chave da
        variavel de ambiente indicada em -VariavelChave (padrao IAAPI_DIAGNOSTICS_KEY,
        o mesmo nome usado pelo LiveMonitor) ou pede com -PedirChave (entrada oculta).
        A chave nunca e impressa nem gravada.

.EXAMPLE
    # Uma passada (ultimos <=100 registros por tag)
    $env:IAAPI_DIAGNOSTICS_KEY = '<mesma chave configurada na IAApi>'
    .\tools\IntelligenceLabDump\Dump-Diagnostics.ps1 -ClienteId PK2

.EXAMPLE
    # Durante um cenario: a cada 5 min por 3 h, pedindo a chave sem eco
    .\tools\IntelligenceLabDump\Dump-Diagnostics.ps1 -ClienteId PK2 -PedirChave -IntervaloSegundos 300 -DuracaoMinutos 180

.EXAMPLE
    # Offline: regenerar os CSVs de um JSON salvo
    .\tools\IntelligenceLabDump\Dump-Diagnostics.ps1 -ArquivoJson dados\ia_real_2026-11-13_0930.json
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5145',
    [string]$VariavelChave = 'IAAPI_DIAGNOSTICS_KEY',
    [switch]$PedirChave,
    [string]$ClienteId,
    [string[]]$Tags,
    [string[]]$GruposShadow,
    [ValidateRange(0, 86400)][int]$IntervaloSegundos = 0,
    [ValidateRange(0, 10080)][int]$DuracaoMinutos = 0,
    [string]$Saida = 'dados',
    [string]$ArquivoJson
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$Inv = [System.Globalization.CultureInfo]::InvariantCulture

# Colunas do CSV do replay (tools/Cp300OfflineReplay/Cp300Replay.cs : Cp300Replay.Colunas), mesma ordem.
$ColunasReplay = @(
    'data_hora', 'cliente', 'tag', 'valor',
    'detector', 'aplicavel', 'participou', 'motivo_nao_aplicavel',
    'nota_detector', 'peso', 'contribuicao',
    'nota_final', 'limiar', 'decisao_ensemble', 'decisao_final',
    'tipo_tag', 'confianca_tipo', 'roteamento_por_tipo',
    'deadband', 'origem_deadband', 'confianca_deadband', 'origem_baseline',
    'media', 'desvio', 'pontos_historico',
    'entrada_invalida', 'motivo_entrada_invalida', 'erro', 'conferencia',
    'valor_regime', 'motivo_baseline')
$ColunasExtras = @(
    'hora_leitura_kind', 'hora_leitura_utc', 'recebido_utc', 'concluido_utc', 'ms_total',
    'fonte_decisao', 'legado_anomalia', 'motivos_runtime', 'amostras_baseline', 'completude', 'secoes_ausentes',
    'mv_grupos', 'mv_minuto', 'mv_idade_min', 'mv_aplicavel', 'mv_anomalia', 'mv_nota', 'mv_limiar', 'mv_motivo',
    'diagnostic_id', 'correlation_id')
$ColunasShadow = @(
    'registrado_utc', 'cliente', 'grupo', 'vector_id', 'evento', 'minute_index', 'vetor_completo',
    'mahalanobis_executado', 'anomalia', 'nota', 'limiar', 'mahalanobis2', 'motivo',
    'tags_esperadas', 'leituras', 'diagnostic_id')

# Enums serializados como numero (sem JsonStringEnumConverter na IAApi); aceita texto tambem.
$EnumBaseline = @{ 1 = 'Global'; 2 = 'OperationalRegime'; 3 = 'BehaviorMemory' }
$EnumDetector = @{ 1 = 'Executed'; 2 = 'NotApplicable'; 3 = 'Disabled'; 4 = 'Failed' }
$EnumCompleto = @{ 1 = 'Complete'; 2 = 'Partial'; 3 = 'Lost' }
$EnumEvento   = @{ 1 = 'ReadingAccepted'; 2 = 'VectorFinalized'; 3 = 'VectorDiscarded'
                   4 = 'MahalanobisExecuted'; 5 = 'MahalanobisNotApplicable'; 6 = 'MahalanobisFailed' }

# ---------------------------------------------------------------- utilitarios

function Get-Prop($obj, [string]$name) {
    if ($null -eq $obj) { return $null }
    $p = $obj.PSObject.Properties[$name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

function Get-EnumName($value, [hashtable]$map) {
    if ($null -eq $value) { return '' }
    if ($value -is [string]) { return $value }
    $key = [int]$value
    if ($map.ContainsKey($key)) { return $map[$key] }
    return [string]$key
}

function Format-Num($value) {
    if ($null -eq $value) { return '' }
    if (($value -is [string]) -and ($value -eq '')) { return '' }
    if ($value -is [string]) { return $value }   # ex.: "NaN" / "Infinity"
    return ([double]$value).ToString('G9', $Inv)
}

function Format-Bool($value) {
    if ($null -eq $value) { return '' }
    if ([bool]$value) { return '1' }
    return '0'
}

# Hora como no replay (yyyy-MM-ddTHH:mm:ss), sem converter fuso: o texto original e preservado.
function Format-Hora($value) {
    if ($null -eq $value) { return '' }
    if ($value -is [datetime]) { return $value.ToString("yyyy-MM-dd'T'HH:mm:ss", $Inv) }
    $s = [string]$value
    if ($s -match '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}') { return $Matches[0] }
    return $s
}

# Instantes UTC/offset: texto original (ISO), sem reinterpretar.
function Format-Instante($value) {
    if ($null -eq $value) { return '' }
    if ($value -is [datetime]) { return $value.ToString('o', $Inv) }
    return [string]$value
}

function Format-Ms($timespan) {
    if ($null -eq $timespan) { return '' }
    try { return ([TimeSpan]::Parse([string]$timespan, $Inv).TotalMilliseconds).ToString('0.###', $Inv) }
    catch { return '' }
}

function Escape-Csv([string]$value) {
    if ($null -eq $value) { return '' }
    if ($value.IndexOfAny([char[]]@(',', '"', "`n", "`r")) -ge 0) {
        return '"' + $value.Replace('"', '""') + '"'
    }
    return $value
}

function Join-Csv([object[]]$fields) {
    return (($fields | ForEach-Object { Escape-Csv ([string]$_) }) -join ',')
}

function Write-Utf8([string]$path, [string[]]$lines) {
    $full = [System.IO.Path]::GetFullPath($path)
    [System.IO.File]::WriteAllLines($full, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

# ---------------------------------------------------------------- conversao para CSV

function Convert-LeituraParaLinhas($d) {
    $ctx = Get-Prop $d 'context'
    $base = Get-Prop $d 'baseline'
    $ens = Get-Prop $d 'ensemble'
    $rt = Get-Prop $d 'runtimeDecision'
    $hq = Get-Prop $d 'historyQuality'
    $fail = Get-Prop $d 'failure'
    $comp = Get-Prop $d 'completeness'
    $mv = @(Get-Prop $d 'multivariate' | Where-Object { $null -ne $_ })
    $detectores = @(Get-Prop $d 'detectors' | Where-Object { $null -ne $_ })
    $contribs = @(Get-Prop $ens 'contributions' | Where-Object { $null -ne $_ })
    $somaPesos = Get-Prop $ens 'applicableWeightSum'

    $porNome = @{}
    foreach ($x in $detectores) { $porNome[[string](Get-Prop $x 'name')] = $x }

    $comum = [ordered]@{
        data_hora        = Format-Hora (Get-Prop $d 'readingTimestampOriginal')
        cliente          = Get-Prop $d 'clienteId'
        tag              = Get-Prop $d 'tagName'
        valor            = Format-Num (Get-Prop $d 'readingValue')
        nota_final       = Format-Num (Get-Prop $ens 'normalizedScore')
        limiar           = Format-Num (Get-Prop $ens 'threshold')
        decisao_ensemble = Format-Bool (Get-Prop $ens 'legacyAnomaly')
        decisao_final    = Format-Bool (Get-Prop $rt 'finalAnomaly')
        # Nao capturados pelo Lab: tipo da tag, confianca e roteamento (P20); entrada invalida (P19).
        tipo_tag = ''; confianca_tipo = ''; roteamento_por_tipo = ''
        # Deadband do baseline (nao o deadband efetivo de BaselineProtectionProfile usado no replay).
        deadband           = Format-Num (Get-Prop $base 'deadband')
        origem_deadband    = $(if ($null -ne $base) { 'baseline_lab' } else { '' })
        confianca_deadband = Format-Num (Get-Prop $base 'deadbandConfidence')
        origem_baseline    = $(if ($null -ne $base) { Get-EnumName (Get-Prop $base 'kind') $EnumBaseline } else { 'desconhecida' })
        media              = Format-Num (Get-Prop $base 'mean')
        desvio             = Format-Num (Get-Prop $base 'standardDeviation')
        pontos_historico   = Format-Num (Get-Prop $hq 'validPointCount')
        entrada_invalida = ''; motivo_entrada_invalida = ''
        erro             = $(if ($null -ne $fail) { [string](Get-Prop $fail 'code') } else { '' })
        conferencia      = 'ia_real_lab'
        valor_regime     = Format-Num (Get-Prop $base 'regimeValue')
        motivo_baseline  = Get-Prop $base 'reason'
        hora_leitura_kind = Get-Prop $d 'readingTimestampKind'
        hora_leitura_utc  = Format-Instante (Get-Prop $d 'readingTimestampUtc')
        recebido_utc      = Format-Instante (Get-Prop $ctx 'receivedAtUtc')
        concluido_utc     = Format-Instante (Get-Prop $d 'completedAtUtc')
        ms_total          = Format-Ms (Get-Prop (Get-Prop $d 'timing') 'total')
        fonte_decisao     = Get-Prop $rt 'source'
        legado_anomalia   = Format-Bool (Get-Prop $rt 'legacyAnomaly')
        motivos_runtime   = (@(Get-Prop $rt 'reasons') | Where-Object { $_ }) -join '|'
        amostras_baseline = Format-Num (Get-Prop $base 'sampleCount')
        completude        = Get-EnumName (Get-Prop $comp 'status') $EnumCompleto
        secoes_ausentes   = (@(Get-Prop $comp 'missingSections') | Where-Object { $_ }) -join '|'
        mv_grupos     = ($mv | ForEach-Object { Get-Prop $_ 'groupName' }) -join '|'
        mv_minuto     = ($mv | ForEach-Object { Format-Num (Get-Prop $_ 'minuteIndex') }) -join '|'
        mv_idade_min  = ($mv | ForEach-Object { Format-Num (Get-Prop $_ 'ageMinutes') }) -join '|'
        mv_aplicavel  = ($mv | ForEach-Object { Format-Bool (Get-Prop $_ 'applicable') }) -join '|'
        mv_anomalia   = ($mv | ForEach-Object { Format-Bool (Get-Prop $_ 'anomaly') }) -join '|'
        mv_nota       = ($mv | ForEach-Object { Format-Num (Get-Prop $_ 'normalizedScore') }) -join '|'
        mv_limiar     = ($mv | ForEach-Object { Format-Num (Get-Prop $_ 'threshold') }) -join '|'
        mv_motivo     = ($mv | ForEach-Object { Get-Prop $_ 'reason' }) -join '|'
        diagnostic_id  = Get-Prop $ctx 'diagnosticId'
        correlation_id = Get-Prop $ctx 'correlationId'
    }

    # Linhas por detector: preferir ensemble.contributions (tem aplicavel/participou/motivo);
    # sem elas, usar detectors (status Executed = aplicavel; participou desconhecido).
    $porDetector = @()
    if ($contribs.Count -gt 0) {
        foreach ($c in $contribs) {
            $nome = [string](Get-Prop $c 'detector')
            $det = $porNome[$nome]
            $nota = Get-Prop $c 'normalizedScore'
            if ($null -eq $nota) { $nota = Get-Prop $det 'normalizedScore' }
            $peso = Get-Prop $det 'weight'
            if ($null -eq $peso) { $peso = Get-Prop $c 'effectiveWeight' }
            $part = Get-Prop $c 'participatedInAggregation'
            $contrib = 0.0
            if ($part -eq $true -and $null -ne $nota -and $null -ne $peso -and $null -ne $somaPesos -and [double]$somaPesos -gt 0) {
                $contrib = [double]$nota * [double]$peso / [double]$somaPesos
            }
            $motivo = Get-Prop $c 'nonApplicabilityReasonCode'
            if (-not $motivo) { $motivo = Get-Prop $c 'executionReasonCode' }
            $pesoCfg = Get-Prop $c 'configuredWeight'
            if ($null -eq $pesoCfg) { $pesoCfg = $peso }
            $aplic = Get-Prop $c 'applicable'
            $porDetector += , @($nome, (Format-Bool $aplic), (Format-Bool $part),
                $(if ($aplic -eq $true) { '' } else { [string]$motivo }),
                (Format-Num $nota), (Format-Num $pesoCfg), (Format-Num $contrib))
        }
    }
    else {
        foreach ($x in $detectores) {
            $status = Get-EnumName (Get-Prop $x 'status') $EnumDetector
            $aplic = $status -eq 'Executed'
            $porDetector += , @([string](Get-Prop $x 'name'), (Format-Bool $aplic), '',
                $(if ($aplic) { '' } else { $status }),
                (Format-Num (Get-Prop $x 'normalizedScore')), (Format-Num (Get-Prop $x 'weight')), '')
        }
    }
    if ($porDetector.Count -eq 0) { $porDetector = , @('', '', '', '', '', '', '') }

    $linhas = @()
    foreach ($pd in $porDetector) {
        $campos = @(
            $comum.data_hora, $comum.cliente, $comum.tag, $comum.valor,
            $pd[0], $pd[1], $pd[2], $pd[3], $pd[4], $pd[5], $pd[6],
            $comum.nota_final, $comum.limiar, $comum.decisao_ensemble, $comum.decisao_final,
            $comum.tipo_tag, $comum.confianca_tipo, $comum.roteamento_por_tipo,
            $comum.deadband, $comum.origem_deadband, $comum.confianca_deadband, $comum.origem_baseline,
            $comum.media, $comum.desvio, $comum.pontos_historico,
            $comum.entrada_invalida, $comum.motivo_entrada_invalida, $comum.erro, $comum.conferencia,
            $comum.valor_regime, $comum.motivo_baseline)
        foreach ($e in $ColunasExtras) { $campos += $comum[$e] }
        $linhas += Join-Csv $campos
    }
    return $linhas
}

function Convert-ShadowParaLinha($s) {
    $leituras = @(Get-Prop $s 'readings' | Where-Object { $null -ne $_ } | ForEach-Object {
            '{0}={1}@{2}' -f (Get-Prop $_ 'tagName'), (Format-Num (Get-Prop $_ 'value')), (Format-Hora (Get-Prop $_ 'sourceTimestamp'))
        }) -join '|'
    return Join-Csv @(
        (Format-Instante (Get-Prop $s 'recordedAtUtc')), (Get-Prop $s 'clienteId'), (Get-Prop $s 'groupName'),
        (Get-Prop $s 'vectorId'), (Get-EnumName (Get-Prop $s 'eventKind') $EnumEvento),
        (Format-Num (Get-Prop $s 'minuteIndex')), (Format-Bool (Get-Prop $s 'vectorComplete')),
        (Format-Bool (Get-Prop $s 'mahalanobisExecuted')), (Format-Bool (Get-Prop $s 'anomaly')),
        (Format-Num (Get-Prop $s 'normalizedScore')), (Format-Num (Get-Prop $s 'threshold')),
        (Format-Num (Get-Prop $s 'mahalanobisSquared')), (Get-Prop $s 'reasonCode'),
        ((@(Get-Prop $s 'expectedTags') | Where-Object { $_ }) -join '|'), $leituras, (Get-Prop $s 'diagnosticId'))
}

function Write-Saidas([string]$prefixo, $meta, [System.Collections.IDictionary]$leituras, [System.Collections.IDictionary]$shadow) {
    $ordLeituras = @($leituras.Values | Sort-Object { Format-Hora (Get-Prop $_ 'readingTimestampOriginal') }, { [string](Get-Prop $_ 'tagName') })
    $ordShadow = @($shadow.Values | Sort-Object { Format-Instante (Get-Prop $_ 'recordedAtUtc') })

    $doc = [ordered]@{ meta = $meta; leituras = $ordLeituras; shadow = $ordShadow }
    Write-Utf8 ($prefixo + '.json') @(($doc | ConvertTo-Json -Depth 32))

    $csv = New-Object System.Collections.Generic.List[string]
    $csv.Add((($ColunasReplay + $ColunasExtras) -join ','))
    foreach ($d in $ordLeituras) { foreach ($l in (Convert-LeituraParaLinhas $d)) { $csv.Add($l) } }
    Write-Utf8 ($prefixo + '.csv') $csv.ToArray()

    $csvS = New-Object System.Collections.Generic.List[string]
    $csvS.Add(($ColunasShadow -join ','))
    foreach ($s in $ordShadow) { $csvS.Add((Convert-ShadowParaLinha $s)) }
    Write-Utf8 ($prefixo + '_shadow.csv') $csvS.ToArray()
}

# ---------------------------------------------------------------- modo offline (-ArquivoJson)

if ($ArquivoJson) {
    $json = [System.IO.File]::ReadAllText([System.IO.Path]::GetFullPath($ArquivoJson), [System.Text.Encoding]::UTF8)
    $doc = $json | ConvertFrom-Json
    $L = [ordered]@{}; $S = [ordered]@{}
    foreach ($d in @(Get-Prop $doc 'leituras' | Where-Object { $null -ne $_ })) { $L[[string](Get-Prop (Get-Prop $d 'context') 'diagnosticId')] = $d }
    foreach ($s in @(Get-Prop $doc 'shadow' | Where-Object { $null -ne $_ })) { $S[[string](Get-Prop $s 'diagnosticId')] = $s }
    $prefixo = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($ArquivoJson)),
        [System.IO.Path]::GetFileNameWithoutExtension($ArquivoJson))
    Write-Saidas $prefixo (Get-Prop $doc 'meta') $L $S
    Write-Host ("CSV regenerados a partir de {0}: {1} leituras, {2} eventos shadow." -f $ArquivoJson, $L.Count, $S.Count)
    return
}

# ---------------------------------------------------------------- modo API

$uri = [Uri]$BaseUrl
if (@('localhost', '127.0.0.1', '[::1]', '::1') -notcontains $uri.Host.ToLowerInvariant()) {
    throw "BaseUrl precisa ser loopback (localhost, 127.0.0.1 ou [::1]); recebido: $($uri.Host). O endpoint recusa outras origens."
}
$base = $BaseUrl.TrimEnd('/')

$chave = [Environment]::GetEnvironmentVariable($VariavelChave)
if ([string]::IsNullOrEmpty($chave) -and $PedirChave) {
    $sec = Read-Host -AsSecureString 'Chave do Intelligence Lab (nao sera exibida)'
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
    try { $chave = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}
if ([string]::IsNullOrEmpty($chave)) {
    throw "Chave ausente. Defina a variavel de ambiente $VariavelChave com o mesmo valor de IntelligenceLab:ReadAccessKey da IAApi, ou use -PedirChave."
}
$headers = @{ 'X-Intelligence-Lab-Key' = $chave }

function Invoke-Lab([string]$caminho, [hashtable]$query) {
    $qs = ''
    if ($query) {
        $qs = '?' + (($query.GetEnumerator() | Where-Object { $_.Value } | ForEach-Object {
                    '{0}={1}' -f $_.Key, [Uri]::EscapeDataString([string]$_.Value) }) -join '&')
    }
    try {
        # Atribuir antes de devolver: no PowerShell 5.1 um array JSON sai como UM objeto;
        # 'return $r' o desempacota em itens no pipeline.
        $r = Invoke-RestMethod -Method Get -Uri ($base + $caminho + $qs) -Headers $headers -TimeoutSec 30
        return $r
    }
    catch {
        $status = $null
        $resp = $_.Exception.PSObject.Properties['Response']
        if ($null -ne $resp -and $null -ne $resp.Value) { try { $status = [int]$resp.Value.StatusCode } catch { } }
        switch ($status) {
            401 { throw 'HTTP 401: chave recusada pela IAApi.' }
            404 { return $null }   # Lab desligado / fora de Development / ID ja descartado do buffer
            default { throw "Falha em GET $caminho (HTTP $status): $($_.Exception.Message)" }
        }
    }
}

$health0 = Invoke-Lab '/api/intelligence-lab/health' $null
if ($null -eq $health0) {
    throw ('HTTP 404 em /api/intelligence-lab/health. Verifique: ASPNETCORE_ENVIRONMENT=Development, ' +
        'IntelligenceLab:Enabled=true, IntelligenceLab:Version:* preenchidas, IntelligenceLab:ReadAccessKey >= 32 caracteres, ' +
        'e BaseUrl em loopback.')
}

if (-not (Test-Path -LiteralPath $Saida)) { New-Item -ItemType Directory -Path $Saida | Out-Null }
$inicio = Get-Date
$prefixo = Join-Path $Saida ('ia_real_' + $inicio.ToString('yyyy-MM-dd_HHmm', $Inv))

$leituras = [ordered]@{}
$shadow = [ordered]@{}
$tagsConhecidas = New-Object System.Collections.Generic.HashSet[string]
$gruposConhecidos = New-Object System.Collections.Generic.HashSet[string]
foreach ($t in @($Tags)) { if ($t) { [void]$tagsConhecidas.Add($t) } }
foreach ($g in @($GruposShadow)) { if ($g) { [void]$gruposConhecidos.Add($g) } }
$lacunas = New-Object System.Collections.Generic.List[string]
$detalhesPerdidos = 0
$passada = 0

function Get-Novos([string]$rota, [hashtable]$query, [System.Collections.IDictionary]$destino, [string]$rotulo, [string]$campoId, [string]$campoAgrupar, $conjunto) {
    $lista = @(Invoke-Lab $rota $query | Where-Object { $null -ne $_ })
    $novos = @($lista | Where-Object { -not $destino.Contains([string](Get-Prop $_ $campoId)) })
    foreach ($r in $lista) { $v = [string](Get-Prop $r $campoAgrupar); if ($v -and $null -ne $conjunto) { [void]$conjunto.Add($v) } }
    # 100 resultados e nenhum ja conhecido (apos a 1a passada) => pode haver registros mais antigos inalcancaveis.
    if ($script:passada -gt 1 -and $lista.Count -ge 100 -and $novos.Count -eq $lista.Count) {
        $script:lacunas.Add(('{0} passada {1}: {2}' -f (Get-Date).ToString('HH:mm:ss', $Inv), $script:passada, $rotulo))
    }
    foreach ($r in $novos) {
        $id = [string](Get-Prop $r $campoId)
        $det = Invoke-Lab ($rota + '/' + $id) $null
        if ($null -eq $det) { $script:detalhesPerdidos++; continue }   # descartado do buffer entre a lista e o detalhe
        $destino[$id] = $det
    }
    return $novos.Count
}

$fim = if ($DuracaoMinutos -gt 0) { $inicio.AddMinutes($DuracaoMinutos) } else { $null }
$health1 = $health0
try {
    while ($true) {
        $passada++
        $n = 0
        $q = @{ limit = 100; clienteId = $ClienteId }
        $n += Get-Novos '/api/intelligence-lab/diagnostics' $q $leituras 'leituras (sem filtro de tag)' 'diagnosticId' 'tagName' $tagsConhecidas
        foreach ($t in @($tagsConhecidas)) {
            $n += Get-Novos '/api/intelligence-lab/diagnostics' @{ limit = 100; clienteId = $ClienteId; tagName = $t } $leituras "tag $t" 'diagnosticId' 'tagName' $null
        }
        $n += Get-Novos '/api/intelligence-lab/diagnostics/multivariate-shadow' $q $shadow 'shadow (sem filtro de grupo)' 'diagnosticId' 'groupName' $gruposConhecidos
        foreach ($g in @($gruposConhecidos)) {
            $n += Get-Novos '/api/intelligence-lab/diagnostics/multivariate-shadow' @{ limit = 100; clienteId = $ClienteId; groupName = $g } $shadow "grupo $g" 'diagnosticId' 'groupName' $null
        }

        $h = Invoke-Lab '/api/intelligence-lab/health' $null
        if ($null -ne $h) { $health1 = $h }
        $meta = [ordered]@{
            gerado_em          = (Get-Date).ToString('o', $Inv)
            inicio_coleta      = $inicio.ToString('o', $Inv)
            base_url           = $base
            cliente            = $ClienteId
            passadas           = $passada
            intervalo_segundos = $IntervaloSegundos
            tags               = @($tagsConhecidas)
            grupos_shadow      = @($gruposConhecidos)
            leituras           = $leituras.Count
            eventos_shadow     = $shadow.Count
            detalhes_perdidos  = $detalhesPerdidos
            possiveis_lacunas  = $lacunas.ToArray()
            health_inicio      = $health0
            health_fim         = $health1
            aviso              = 'Contem valores industriais: nao versionar nem compartilhar fora da equipe autorizada.'
        }
        Write-Saidas $prefixo $meta $leituras $shadow
        Write-Host ('[{0}] passada {1}: +{2} novos | leituras {3} | shadow {4} | tags {5} | lacunas {6}' -f
            (Get-Date).ToString('HH:mm:ss', $Inv), $passada, $n, $leituras.Count, $shadow.Count, $tagsConhecidas.Count, $lacunas.Count)

        if ($IntervaloSegundos -le 0) { break }
        if ($null -ne $fim -and (Get-Date).AddSeconds($IntervaloSegundos) -gt $fim) { break }
        Start-Sleep -Seconds $IntervaloSegundos
    }
}
finally {
    $chave = $null; $headers = $null
}

# Resumo de perdas: quantos registros o buffer gravou durante a coleta x quantos foram lidos.
$st0 = Get-Prop $health0 'storedTotal'; $st1 = Get-Prop $health1 'storedTotal'
$ev0 = Get-Prop $health0 'evictedTotal'; $ev1 = Get-Prop $health1 'evictedTotal'
Write-Host ''
Write-Host ("Arquivos: {0}.json / .csv / _shadow.csv" -f $prefixo)
if ($null -ne $st0 -and $null -ne $st1) {
    # StoredTotal/EvictedTotal somam as duas filas (univariado + shadow), cada uma com a capacidade BufferCapacity.
    Write-Host ("Buffer: capacidade {0} por fila; gravados durante a coleta {1} (univariado + shadow); lidos agora {2}; descartados pelo buffer {3}." -f
        (Get-Prop $health1 'bufferCapacity'), ([long]$st1 - [long]$st0), ($leituras.Count + $shadow.Count), ([long]$ev1 - [long]$ev0))
}
if ($lacunas.Count -gt 0 -or $detalhesPerdidos -gt 0) {
    Write-Warning ("Possivel perda: {0} filtros com 100 registros todos novos; {1} detalhes ja descartados. Reduza -IntervaloSegundos ou aumente IntelligenceLab:BufferCapacity." -f $lacunas.Count, $detalhesPerdidos)
}
