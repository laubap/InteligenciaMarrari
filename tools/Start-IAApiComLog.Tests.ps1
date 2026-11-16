<#
.SYNOPSIS
    Teste offline da recusa de endereco nao local do Start-IAApiComLog.ps1 (P34).

.DESCRIPTION
    NAO executa o Start-IAApiComLog.ps1 (ele compila e sobe a IAApi). Le o script
    pela arvore sintatica e:
      1. carrega so a funcao Test-EnderecoLocal e confere enderecos aceitos e
         recusados;
      2. confere que a checagem do -Url e a primeira acao do script: antes dela
         so podem vir Set-StrictMode, $ErrorActionPreference e a propria funcao.

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

if ($falhas -gt 0) {
    Write-Host "RESULTADO: $falhas falha(s)."
    exit 1
}
Write-Host 'RESULTADO: todos os testes passaram.'
exit 0
