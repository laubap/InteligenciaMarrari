<#
.SYNOPSIS
    Exporta o historico do Bridge (PSi4) dos testes CP300 para CSV.

.DESCRIPTION
    SOMENTE LEITURA. Chama, para cada tag e em pedacos de horas, o mesmo
    endpoint usado por BridgeHistoricoService.BuscarHistoricoPorPeriodo:

        GET {BridgeUrl}/historico?tagName=<Cliente>.<TAGnn>&inicio=<...>&fim=<...>

    A resposta e lida como a IAApi le (BridgeHistoricoService.ConverterHistorico):
    uma LISTA JSON de { dataHora, tagName, valor }, nomes sem diferenciar
    maiusculas. Valor numerico ou texto (invariante, depois pt-BR); NaN,
    Infinity e texto nao numerico sao descartados. Horario duplicado: fica o ultimo.

    Robustez:
      - pedacos de -PedacoHoras (padrao 2 h), timeout de -TimeoutSeg (padrao 180 s);
      - ate -Tentativas (padrao 3) por pedaco, com pausa crescente entre elas
        (-PausaSeg, 2x -PausaSeg, ...). HTTP 404 nao e repetido (sem dados);
      - cada tag e gravada no CSV assim que termina. Se o script for cancelado,
        as tags ja concluidas ficam no arquivo; use -Completar depois;
      - -Completar le o CSV existente, busca so os pedacos que faltam e
        acrescenta sem duplicar (tag + horario).

    Saida (UTF-8, virgula, ponto decimal):
        ClienteId,TagName,DataHora,Valor
    TagName SEM o prefixo do cliente (TAG22); o cliente vai em ClienteId.
    DataHora no mesmo relogio que a IAApi recebe (ver Convert-DataBridge),
    formato yyyy-MM-ddTHH:mm:ss, sem fuso: e o que o replay offline espera.
    A ordem das linhas nao importa: o replay ordena por tag e horario.

    Ao lado do CSV e gravado <Saida>.pedacos.csv, com uma linha por pedaco
    (tag, intervalo, tentativas, status, segundos, pontos). Serve de evidencia
    do tempo de resposta do Bridge.

    Execute somente com autorizacao: e acesso a servico industrial e os dados
    sao sensiveis. Nada e escrito no Bridge, no PSi4 ou no CLP.

.PARAMETER BridgeUrl
    URL base do Bridge (ex.: http://<host>:5001).

.PARAMETER Cliente
    Identificador do cliente (ex.: PK2). Vira o prefixo do nome no Bridge.

.PARAMETER Inicio
    Inicio do periodo, no relogio do Bridge (hora local), ex.: '2026-11-10 06:00'.

.PARAMETER Fim
    Fim do periodo, no relogio do Bridge.

.PARAMETER Tags
    Nomes sem prefixo. Padrao: TAG22-TAG51 (medicoes e estados) e TAG60-TAG81 (rotulos).

.PARAMETER Saida
    Caminho do CSV. Se ja existir, exige -Completar ou -Sobrescrever.

.PARAMETER PedacoHoras
    Tamanho de cada consulta, em horas (padrao 2; aceita fracao, ex.: 0.5).

.PARAMETER TimeoutSeg
    Timeout de cada consulta, em segundos (padrao 180).

.PARAMETER Tentativas
    Tentativas por pedaco (padrao 3).

.PARAMETER PausaSeg
    Pausa base entre tentativas, em segundos (padrao 10; cresce a cada tentativa).

.PARAMETER LeiturasPorMinuto
    Cadencia esperada do Bridge (padrao 1). Usada para "esperadas" na tabela
    final e, no -Completar, para decidir se um pedaco esta incompleto.

.PARAMETER Completar
    Le o CSV existente em -Saida e busca so os pedacos com menos leituras que o
    esperado. Acrescenta apenas pares (tag, horario) que ainda nao estao no arquivo.

.PARAMETER Sobrescrever
    Apaga o CSV existente e exporta do zero.

.PARAMETER SemPrefixoCliente
    Envia o nome da tag ao Bridge sem o prefixo "<Cliente>.".

.PARAMETER Teste
    Verificacao: busca so <Cliente>.TAG22 nas ultimas 2 horas e mostra a URL,
    a quantidade de pontos, os 3 primeiros pares hora/valor, os primeiros 300
    caracteres do JSON bruto, a indicacao de fuso e o tempo de resposta.
    Nao grava arquivo.

.EXAMPLE
    .\tools\Cp300Export\Export-Cp300Historico.ps1 -Teste -BridgeUrl http://bridge:5001 -Cliente PK2

.EXAMPLE
    .\tools\Cp300Export\Export-Cp300Historico.ps1 -BridgeUrl http://bridge:5001 -Cliente PK2 `
        -Inicio '2026-11-10 06:00' -Fim '2026-11-11 06:00' -Saida .\dados\preset1.csv

.EXAMPLE
    # Depois de timeouts ou Ctrl+C: busca so o que falta, mesmo periodo e mesmas tags.
    .\tools\Cp300Export\Export-Cp300Historico.ps1 -BridgeUrl http://bridge:5001 -Cliente PK2 `
        -Inicio '2026-11-10 06:00' -Fim '2026-11-11 06:00' -Saida .\dados\preset1.csv -Completar
#>
[CmdletBinding(DefaultParameterSetName = 'Exportar')]
param(
    [Parameter(Mandatory = $true)] [string] $BridgeUrl,
    [Parameter(Mandatory = $true)] [string] $Cliente,
    [Parameter(Mandatory = $true, ParameterSetName = 'Exportar')] [datetime] $Inicio,
    [Parameter(Mandatory = $true, ParameterSetName = 'Exportar')] [datetime] $Fim,
    [Parameter(Mandatory = $true, ParameterSetName = 'Exportar')] [string] $Saida,
    [Parameter(ParameterSetName = 'Exportar')]
    [string[]] $Tags = (@(22..51) + @(60..81) | ForEach-Object { "TAG$_" }),
    [Parameter(ParameterSetName = 'Exportar')] [Alias('BlocoHoras')]
    [ValidateRange(0.1, 168)] [double] $PedacoHoras = 2,
    [Parameter(ParameterSetName = 'Exportar')] [ValidateRange(1, 10)] [int] $Tentativas = 3,
    [Parameter(ParameterSetName = 'Exportar')] [ValidateRange(0, 600)] [int] $PausaSeg = 10,
    [Parameter(ParameterSetName = 'Exportar')] [ValidateRange(0.01, 600)] [double] $LeiturasPorMinuto = 1,
    [Parameter(ParameterSetName = 'Exportar')] [switch] $Completar,
    [Parameter(ParameterSetName = 'Exportar')] [switch] $Sobrescrever,
    [Parameter(Mandatory = $true, ParameterSetName = 'Teste')] [switch] $Teste,
    [switch] $SemPrefixoCliente,
    [Alias('TimeoutSegundos')] [ValidateRange(5, 3600)] [int] $TimeoutSeg = 180
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$inv = [System.Globalization.CultureInfo]::InvariantCulture
$ptBr = [System.Globalization.CultureInfo]::GetCultureInfo('pt-BR')
$utf8 = New-Object System.Text.UTF8Encoding($false)
$baseUrl = $BridgeUrl.TrimEnd('/')
$Cliente = $Cliente.Trim()
$prefixo = "$Cliente."
$formatoData = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"
# PowerShell 7.0-7.4 converte texto ISO em DateTime no ConvertFrom-Json; 7.5+
# permite desligar com -DateKind String. O Windows PowerShell 5.1 nao converte.
$temDateKind = (Get-Command ConvertFrom-Json).Parameters.ContainsKey('DateKind')
# Fusos vistos nas respostas (sem fuso / Z / +hh:mm), para o aviso final.
$script:fusosVistos = @{}

# Nome curto (TAG22), sem prefixo de cliente.
function Get-NomeCurto([string] $tag) {
    if ($tag.StartsWith($prefixo, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $tag.Substring($prefixo.Length)
    }
    return $tag
}

# Nome usado no Bridge (PK2.TAG22).
function Get-NomeBridge([string] $tag) {
    $curto = Get-NomeCurto $tag
    if ($SemPrefixoCliente) { return $curto }
    return "$prefixo$curto"
}

function Format-DataBridge([datetime] $d) {
    # Mesmo formato "O" de BridgeHistoricoService (sem fuso quando Kind=Unspecified).
    return ([datetime]::SpecifyKind($d, [System.DateTimeKind]::Unspecified)).ToString('O', $inv)
}

function Get-UrlHistorico([string] $nomeBridge, [datetime] $de, [datetime] $ate) {
    return "$baseUrl/historico?tagName=$([uri]::EscapeDataString($nomeBridge))" +
           "&inicio=$([uri]::EscapeDataString((Format-DataBridge $de)))" +
           "&fim=$([uri]::EscapeDataString((Format-DataBridge $ate)))"
}

function Get-Propriedade($objeto, [string] $nome) {
    # PSObject.Properties[...] nao diferencia maiusculas (como PropertyNameCaseInsensitive).
    $p = $objeto.PSObject.Properties[$nome]
    if ($null -eq $p) { return $null }
    return $p.Value
}

# Mesmo resultado de System.Text.Json para DateTime:
#   sem fuso -> Unspecified, relogio mantido;  "Z" -> Utc, relogio UTC mantido;
#   +hh:mm   -> convertido para a hora local da maquina (Kind Local).
# DateTimeStyles.RoundtripKind reproduz exatamente essas tres regras.
function Convert-DataBridge($valor) {
    if ($valor -is [datetime]) {
        # PowerShell 7.0-7.4 ja converteu; o texto original se perdeu.
        $script:fusosVistos["convertido pelo PowerShell (Kind=$($valor.Kind))"] = $true
        return $valor.ToString($formatoData, $inv)
    }
    $texto = ([string]$valor).Trim()
    if ($texto -match '(Z|[+-]\d{2}:?\d{2})$') { $script:fusosVistos[$Matches[1]] = $true }
    else { $script:fusosVistos['sem fuso'] = $true }
    $d = [datetime]::MinValue
    if (-not [datetime]::TryParse($texto, $inv, [System.Globalization.DateTimeStyles]::RoundtripKind, [ref]$d)) {
        return $null
    }
    return $d.ToString($formatoData, $inv)
}

# Mesmas regras de BridgeHistoricoService.ConverterValor. $null = descartado.
function Convert-ValorBridge($valor) {
    if ($null -eq $valor -or $valor -is [bool]) { return $null }
    if ($valor -is [string]) {
        $t = $valor.Trim()
        if ($t -eq '' -or $t -match '^(NaN|-?Infinity)$') { return $null }
        $d = 0.0
        if ([double]::TryParse($t, [System.Globalization.NumberStyles]::Float, $inv, [ref]$d)) { return $d }
        if ([double]::TryParse($t, [System.Globalization.NumberStyles]::Float, $ptBr, [ref]$d)) { return $d }
        return $null
    }
    if ($valor -is [System.ValueType]) {
        $d = [double]$valor
        if ([double]::IsNaN($d) -or [double]::IsInfinity($d)) { return $null }
        return $d
    }
    return $null
}

# Busca e decodifica uma resposta do /historico. Devolve um hashtable (nao a
# lista direto) para o PowerShell nao "desenrolar" ou "embrulhar" o array.
function Get-HistoricoBridge([string] $url) {
    $resposta = Invoke-WebRequest -Method Get -Uri $url -TimeoutSec $TimeoutSeg -UseBasicParsing
    $bruto = $resposta.Content
    if ($bruto -is [byte[]]) { $bruto = [System.Text.Encoding]::UTF8.GetString($bruto) }
    $bruto = [string]$bruto

    $json = if ($temDateKind) { ConvertFrom-Json -InputObject $bruto -DateKind String }
            else { ConvertFrom-Json -InputObject $bruto }

    $pontos = New-Object System.Collections.Generic.List[object]
    $descartados = 0
    # 'foreach' sobre a variavel percorre os elementos do array JSON.
    foreach ($item in $json) {
        if ($null -eq $item) { continue }
        $dataBruta = Get-Propriedade $item 'dataHora'
        $valorBruto = Get-Propriedade $item 'valor'
        if ($dataBruta -is [System.Array] -or $valorBruto -is [System.Array]) {
            throw "Formato inesperado: a resposta nao e uma lista de {dataHora, valor}. Inicio do JSON: " +
                  $bruto.Substring(0, [Math]::Min(300, $bruto.Length))
        }
        $data = if ($null -eq $dataBruta) { $null } else { Convert-DataBridge $dataBruta }
        $valor = Convert-ValorBridge $valorBruto
        if ($null -eq $data -or $null -eq $valor) { $descartados++; continue }
        $pontos.Add([pscustomobject]@{ DataHora = $data; Valor = $valor })
    }
    return @{ Bruto = $bruto; Pontos = $pontos; Descartados = $descartados }
}

# Codigo HTTP de uma excecao do Invoke-WebRequest (5.1: WebException; 7: HttpResponseException).
function Get-StatusHttp($erro) {
    $ex = $erro.Exception
    while ($null -ne $ex) {
        $resp = $null
        if ($ex.PSObject.Properties['Response']) { $resp = $ex.Response }
        if ($null -ne $resp -and $resp.PSObject.Properties['StatusCode']) {
            try { return [int]$resp.StatusCode } catch { return $null }
        }
        $ex = $ex.InnerException
    }
    return $null
}

# Um pedaco com ate $Tentativas tentativas. Nunca lanca excecao.
function Invoke-Pedaco([string] $nomeBridge, [datetime] $de, [datetime] $ate) {
    $url = Get-UrlHistorico $nomeBridge $de $ate
    $ultimoErro = ''
    $status = $null
    for ($t = 1; $t -le $Tentativas; $t++) {
        $relogio = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $r = Get-HistoricoBridge $url
            $relogio.Stop()
            return @{ Ok = $true; Tentativas = $t; Segundos = $relogio.Elapsed.TotalSeconds
                      Pontos = $r.Pontos; Descartados = $r.Descartados; Erro = ''; Status = 200 }
        }
        catch {
            $relogio.Stop()
            $status = Get-StatusHttp $_
            $ultimoErro = $_.Exception.Message
            $segundosFalha = $relogio.Elapsed.TotalSeconds
            # 404 = Bridge sem dados no intervalo: repetir nao adianta.
            if ($status -eq 404) { break }
            if ($t -lt $Tentativas) {
                $pausa = $PausaSeg * $t
                Write-Host ("    {0} [{1} .. {2}] tentativa {3}/{4} falhou em {5:N1} s ({6}); nova tentativa em {7} s" -f
                    $nomeBridge, $de.ToString('dd/MM HH:mm'), $ate.ToString('dd/MM HH:mm'), $t, $Tentativas,
                    $segundosFalha, $ultimoErro, $pausa) -ForegroundColor Yellow
                if ($pausa -gt 0) { Start-Sleep -Seconds $pausa }
            }
        }
    }
    return @{ Ok = $false; Tentativas = [Math]::Min($t, $Tentativas); Segundos = $segundosFalha
              Pontos = @(); Descartados = 0; Erro = $ultimoErro; Status = $status }
}

function Format-ValorCsv([double] $v) { return $v.ToString('R', $inv) }

function Escape-Csv([string] $s) {
    if ($s -match '[",\r\n]') { return '"' + $s.Replace('"', '""') + '"' }
    return $s
}

# Divide uma linha CSV simples (campos podem vir entre aspas).
function Split-LinhaCsv([string] $linha) {
    $campos = New-Object System.Collections.Generic.List[string]
    $atual = New-Object System.Text.StringBuilder
    $aspas = $false
    for ($i = 0; $i -lt $linha.Length; $i++) {
        $c = $linha[$i]
        if ($aspas) {
            if ($c -eq '"' -and $i + 1 -lt $linha.Length -and $linha[$i + 1] -eq '"') { [void]$atual.Append('"'); $i++ }
            elseif ($c -eq '"') { $aspas = $false }
            else { [void]$atual.Append($c) }
        }
        elseif ($c -eq '"') { $aspas = $true }
        elseif ($c -eq ',') { $campos.Add($atual.ToString()); [void]$atual.Clear() }
        else { [void]$atual.Append($c) }
    }
    $campos.Add($atual.ToString())
    return ,$campos
}

# Chave de horario normalizada (mesmo formato gravado no CSV).
function Get-ChaveHorario([string] $texto) {
    $d = [datetime]::MinValue
    if ([datetime]::TryParse($texto.Trim(), $inv, [System.Globalization.DateTimeStyles]::RoundtripKind, [ref]$d)) {
        return $d.ToString($formatoData, $inv)
    }
    return $null
}

# ---------------------------------------------------------------- modo teste
if ($PSCmdlet.ParameterSetName -eq 'Teste') {
    $ate = Get-Date
    $de = $ate.AddHours(-2)
    $nome = Get-NomeBridge 'TAG22'
    $url = Get-UrlHistorico $nome $de $ate
    Write-Host "Tag no Bridge: $nome"
    Write-Host "URL: $url"
    $relogio = [System.Diagnostics.Stopwatch]::StartNew()
    $r = Get-HistoricoBridge $url
    $relogio.Stop()
    Write-Host ("Tempo de resposta: {0:N1} s (timeout {1} s)" -f $relogio.Elapsed.TotalSeconds, $TimeoutSeg)
    Write-Host "Pontos recebidos: $($r.Pontos.Count) (descartados: $($r.Descartados))"
    Write-Host 'Primeiros pontos (hora / valor):'
    $r.Pontos | Select-Object -First 3 | ForEach-Object {
        Write-Host ("  {0}  {1}" -f $_.DataHora, (Format-ValorCsv $_.Valor))
    }
    if ($r.Pontos.Count -gt 0) {
        Write-Host ("Ultimo ponto: {0}" -f $r.Pontos[$r.Pontos.Count - 1].DataHora)
    }
    Write-Host "JSON bruto (300 primeiros caracteres):"
    Write-Host ('  ' + $r.Bruto.Substring(0, [Math]::Min(300, $r.Bruto.Length)))
    Write-Host ("Fuso no texto de dataHora: {0}" -f (($script:fusosVistos.Keys | Sort-Object) -join ', '))
    Write-Host ("Agora nesta maquina: local {0} | UTC {1}" -f
        $ate.ToString("yyyy-MM-dd'T'HH:mm:ss", $inv), $ate.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss", $inv))
    Write-Host 'Se o ultimo ponto estiver perto da hora LOCAL, o Bridge entrega hora local (esperado pelo replay).'
    return
}

# ------------------------------------------------------------ modo exportar
if ($Fim -le $Inicio) { throw 'Fim deve ser maior que Inicio.' }
if ($Completar -and $Sobrescrever) { throw 'Use -Completar ou -Sobrescrever, nao os dois.' }

$caminho = [System.IO.Path]::GetFullPath($Saida)
$caminhoPedacos = "$caminho.pedacos.csv"
$cabecalho = 'ClienteId,TagName,DataHora,Valor'
$dir = Split-Path -Parent $caminho
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

# Pedacos [de, ate) que cobrem o periodo.
$pedacos = New-Object System.Collections.Generic.List[object]
$p0 = $Inicio
while ($p0 -lt $Fim) {
    $p1 = $p0.AddHours($PedacoHoras)
    if ($p1 -gt $Fim) { $p1 = $Fim }
    $pedacos.Add(@{ De = $p0; Ate = $p1; Esperadas = [Math]::Floor(($p1 - $p0).TotalMinutes * $LeiturasPorMinuto) })
    $p0 = $p1
}
$esperadasPorTag = [Math]::Floor(($Fim - $Inicio).TotalMinutes * $LeiturasPorMinuto)

# Horarios ja gravados, por tag curta: HashSet de chaves normalizadas.
$existentes = @{}
if (Test-Path $caminho) {
    if ($Sobrescrever) {
        Remove-Item -LiteralPath $caminho
        if (Test-Path $caminhoPedacos) { Remove-Item -LiteralPath $caminhoPedacos }
    }
    elseif (-not $Completar) {
        throw "O arquivo '$caminho' ja existe. Use -Completar para buscar so o que falta ou -Sobrescrever para exportar do zero."
    }
    else {
        $leitor = New-Object System.IO.StreamReader($caminho, $utf8)
        try {
            $primeira = $leitor.ReadLine()
            if ($null -eq $primeira -or $primeira.Trim().TrimStart([char]0xFEFF) -ne $cabecalho) {
                throw "Cabecalho inesperado em '$caminho' (esperado: $cabecalho)."
            }
            $nLinha = 1
            while ($null -ne ($linha = $leitor.ReadLine())) {
                $nLinha++
                if ($linha.Trim() -eq '') { continue }
                $c = Split-LinhaCsv $linha
                if ($c.Count -lt 4) { throw "Linha $nLinha de '$caminho' com menos de 4 campos." }
                if ($c[0] -ne $Cliente) { throw "Linha $nLinha de '$caminho' e do cliente '$($c[0])', nao '$Cliente'." }
                $chave = Get-ChaveHorario $c[2]
                if ($null -eq $chave) { continue }
                if (-not $existentes.ContainsKey($c[1])) {
                    $existentes[$c[1]] = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
                }
                [void]$existentes[$c[1]].Add($chave)
            }
        }
        finally { $leitor.Dispose() }
        Write-Host "Completando '$caminho': $($existentes.Count) tag(s) ja presentes."
    }
}
if (-not (Test-Path $caminho)) {
    [System.IO.File]::WriteAllText($caminho, $cabecalho + "`r`n", $utf8)
}
if (-not (Test-Path $caminhoPedacos)) {
    [System.IO.File]::WriteAllText($caminhoPedacos,
        "Tag,De,Ate,Tentativas,Status,Segundos,Pontos,Erro`r`n", $utf8)
}

$resumo = New-Object System.Collections.Generic.List[object]
$totalNovas = 0
$totalDescartados = 0
$falhas = New-Object System.Collections.Generic.List[string]
$inicioExecucao = Get-Date

foreach ($tagInformada in $Tags) {
    $curto = Get-NomeCurto $tagInformada
    $nomeBridge = Get-NomeBridge $tagInformada
    # Atribuicao direta (um 'if' como expressao desenrolaria o HashSet).
    if (-not $existentes.ContainsKey($curto)) {
        $existentes[$curto] = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    }
    $jaTem = $existentes[$curto]

    # Horarios novos desta tag (duplicado entre pedacos ou repetido pelo Bridge: fica o ultimo).
    $novos = New-Object 'System.Collections.Generic.SortedDictionary[string,double]' ([System.StringComparer]::Ordinal)
    $tempos = New-Object System.Collections.Generic.List[double]
    $pedacosFalhos = 0
    $pedacosBuscados = 0

    foreach ($pd in $pedacos) {
        if ($Completar) {
            # Pedaco completo no CSV: nao busca de novo.
            $deTxt = $pd.De.ToString($formatoData, $inv)
            $ateTxt = $pd.Ate.ToString($formatoData, $inv)
            $noPedaco = 0
            foreach ($k in $jaTem) {
                # Chaves no mesmo formato ordenavel: comparacao de texto equivale a de horario.
                if ([string]::CompareOrdinal($k, $deTxt) -ge 0 -and [string]::CompareOrdinal($k, $ateTxt) -lt 0) { $noPedaco++ }
            }
            if ($noPedaco -ge $pd.Esperadas) { continue }
        }

        $pedacosBuscados++
        $res = Invoke-Pedaco $nomeBridge $pd.De $pd.Ate
        $statusTxt = if ($res.Ok) { 'ok' } elseif ($res.Status -eq 404) { 'sem-dados-404' } else { 'falhou' }
        if ($res.Ok) {
            $tempos.Add($res.Segundos)
            $totalDescartados += $res.Descartados
            foreach ($p in $res.Pontos) {
                if (-not $jaTem.Contains($p.DataHora)) { $novos[$p.DataHora] = $p.Valor }
            }
        }
        else {
            $pedacosFalhos++
            $falhas.Add("$nomeBridge [$((Format-DataBridge $pd.De)) .. $((Format-DataBridge $pd.Ate))]: $statusTxt - $($res.Erro)")
        }
        [System.IO.File]::AppendAllText($caminhoPedacos, ((
            (Escape-Csv $curto), (Format-DataBridge $pd.De), (Format-DataBridge $pd.Ate), $res.Tentativas,
            $statusTxt, $res.Segundos.ToString('0.###', $inv), $res.Pontos.Count, (Escape-Csv $res.Erro)
        ) -join ',') + "`r`n", $utf8)
    }

    # Grava a tag assim que termina: um Ctrl+C depois daqui nao perde esta tag.
    if ($novos.Count -gt 0) {
        $linhas = New-Object System.Text.StringBuilder
        foreach ($par in $novos.GetEnumerator()) {
            [void]$linhas.Append(((Escape-Csv $Cliente), (Escape-Csv $curto), (Escape-Csv $par.Key),
                (Format-ValorCsv $par.Value)) -join ',').Append("`r`n")
        }
        [System.IO.File]::AppendAllText($caminho, $linhas.ToString(), $utf8)
    }
    $totalNovas += $novos.Count

    # Leituras obtidas no periodo (ja existentes + novas).
    $inicioTxt = $Inicio.ToString($formatoData, $inv)
    $fimTxt = $Fim.ToString($formatoData, $inv)
    $obtidas = 0
    foreach ($k in $jaTem) {
        if ([string]::CompareOrdinal($k, $inicioTxt) -ge 0 -and [string]::CompareOrdinal($k, $fimTxt) -lt 0) { $obtidas++ }
    }
    foreach ($k in $novos.Keys) {
        if ([string]::CompareOrdinal($k, $inicioTxt) -ge 0 -and [string]::CompareOrdinal($k, $fimTxt) -lt 0) { $obtidas++ }
    }
    $media = if ($tempos.Count -gt 0) { ($tempos | Measure-Object -Average).Average } else { $null }
    $maximo = if ($tempos.Count -gt 0) { ($tempos | Measure-Object -Maximum).Maximum } else { $null }

    $linhaResumo = [pscustomobject]@{
        Tag        = $curto
        Obtidas    = $obtidas
        Esperadas  = $esperadasPorTag
        Pct        = if ($esperadasPorTag -gt 0) { [Math]::Round(100.0 * $obtidas / $esperadasPorTag, 1) } else { $null }
        Novas      = $novos.Count
        Buscados   = $pedacosBuscados
        Falhos     = $pedacosFalhos
        MediaSeg   = if ($null -ne $media) { [Math]::Round($media, 1) } else { $null }
        MaxSeg     = if ($null -ne $maximo) { [Math]::Round($maximo, 1) } else { $null }
    }
    $resumo.Add($linhaResumo)
    Write-Host ("{0,-14} {1,6}/{2,-6} leituras  (+{3} novas; pedacos buscados {4}, falhos {5}; media {6} s)" -f
        $nomeBridge, $obtidas, $esperadasPorTag, $novos.Count, $pedacosBuscados, $pedacosFalhos,
        $(if ($null -ne $linhaResumo.MediaSeg) { $linhaResumo.MediaSeg } else { '-' }))
}

# ------------------------------------------------------------------- resumo
Write-Host ''
Write-Host ("Pedaco: {0} h | timeout: {1} s | tentativas: {2} | esperadas = {3} leitura(s)/min x periodo" -f
    $PedacoHoras.ToString($inv), $TimeoutSeg, $Tentativas, $LeiturasPorMinuto.ToString($inv))
$resumo | Format-Table Tag,
    @{ Label = 'Obtidas'; Expression = { $_.Obtidas }; Align = 'Right' },
    @{ Label = 'Esperadas'; Expression = { $_.Esperadas }; Align = 'Right' },
    @{ Label = '%'; Expression = { $_.Pct }; Align = 'Right' },
    @{ Label = 'Novas'; Expression = { $_.Novas }; Align = 'Right' },
    @{ Label = 'Pedacos'; Expression = { $_.Buscados }; Align = 'Right' },
    @{ Label = 'Falhos'; Expression = { $_.Falhos }; Align = 'Right' },
    @{ Label = 'Media s/pedaco'; Expression = { $_.MediaSeg }; Align = 'Right' },
    @{ Label = 'Max s'; Expression = { $_.MaxSeg }; Align = 'Right' } -AutoSize | Out-Host

$todosTempos = @($resumo | Where-Object { $null -ne $_.MediaSeg })
if ($todosTempos.Count -gt 0) {
    $tempoOk = Import-Csv -LiteralPath $caminhoPedacos | Where-Object { $_.Status -eq 'ok' } |
        ForEach-Object { [double]::Parse($_.Segundos, $inv) }
    if (@($tempoOk).Count -gt 0) {
        $m = $tempoOk | Measure-Object -Average -Maximum
        Write-Host ("Tempo de resposta do Bridge (pedacos ok, todas as execucoes): media {0:N1} s, max {1:N1} s, {2} pedacos" -f
            $m.Average, $m.Maximum, $m.Count)
    }
}
Write-Host ("Duracao: {0:N0} min. Novas leituras: {1} ({2} descartadas) -> {3}" -f
    ((Get-Date) - $inicioExecucao).TotalMinutes, $totalNovas, $totalDescartados, $caminho)
Write-Host "Registro por pedaco: $caminhoPedacos"

if ($falhas.Count -gt 0) {
    Write-Warning "$($falhas.Count) pedaco(s) sem dados ou com erro. Rode de novo com -Completar para buscar so o que falta:"
    $falhas | ForEach-Object { Write-Warning $_ }
}
$incompletas = @($resumo | Where-Object { $_.Obtidas -lt $_.Esperadas })
if ($incompletas.Count -gt 0) {
    Write-Warning ("{0} tag(s) abaixo do esperado: {1}" -f $incompletas.Count, (($incompletas | ForEach-Object { $_.Tag }) -join ', '))
}
$fusos = ($script:fusosVistos.Keys | Sort-Object) -join ', '
if ($fusos -ne '') { Write-Host "Fuso no texto de dataHora: $fusos" }
if ($fusos -ne 'sem fuso' -and $fusos -ne '') {
    Write-Warning 'O Bridge enviou horarios com fuso. O CSV traz o relogio que a IAApi obteria (Z = UTC; +hh:mm = hora local desta maquina). Confira antes de usar no replay.'
}
