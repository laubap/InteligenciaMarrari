# Replay offline CP300 (Parte A)

Reproduz, minuto a minuto, a detecção univariada de produção sobre o histórico exportado do Bridge. Gera um CSV com o detalhamento de cada detector. O subcomando `placar` (seção 6) compara essa saída com o gabarito do simulador por evento de falha.

**Isolamento:** SQLite em memória, histórico lido do CSV. Não acessa Bridge, PSi4, CLP nem `ia_config.db`, e não altera código de runtime.

## 1. Exportar o histórico (equipe, com autorização)

```powershell
.\tools\Cp300Export\Export-Cp300Historico.ps1 -BridgeUrl http://<bridge>:5001 `
    -Inicio '2026-11-10 06:00' -Fim '2026-11-11 14:00' -Cliente PK2 `
    -Saida .\dados\cp300.csv
```

- Por padrão exporta TAG22–TAG51 e TAG60–TAG81. Usa `GET /historico?tagName&inicio&fim`, o mesmo endpoint de `BridgeHistoricoService.BuscarHistoricoPorPeriodo`.
- Para o treino "como em produção", o período precisa começar **pelo menos 24 h antes** do instante de treino.
- Formato: `ClienteId,TagName,DataHora,Valor`. `DataHora` vem sem fuso, como o Bridge entrega.
- Consulta em pedaços de 2 h (`-PedacoHoras`), timeout de 180 s (`-TimeoutSeg`) e até 3 tentativas por pedaço. Cada tag é gravada assim que termina. Depois de timeouts ou Ctrl+C, repita o mesmo comando com `-Completar`: busca só os pedaços incompletos e acrescenta sem duplicar. Se o arquivo já existir, o script exige `-Completar` ou `-Sobrescrever`. Os tempos de cada pedaço ficam em `<saída>.pedacos.csv`.
- No Bridge as tags têm o prefixo do cliente (`PK2.TAG22`). O script consulta com prefixo e grava `TagName` **sem** ele (`TAG22`); o cliente fica em `ClienteId`. Antes de exportar, rode com `-Teste -BridgeUrl … -Cliente PK2`: busca `PK2.TAG22` das últimas 2 h e mostra a URL e os 3 primeiros pontos.

## 2. Rodar o replay

```powershell
dotnet run --project tools/Cp300OfflineReplay -- `
    --entrada .\dados\cp300.csv --saida .\dados\replay_preset3.csv `
    --treino-ate 2026-11-11T06:00:00 --fim 2026-11-11T13:30:00

# Treino por período escolhido (ex.: preset 5), replay depois:
dotnet run --project tools/Cp300OfflineReplay -- `
    --entrada .\dados\cp300.csv --saida .\dados\replay_preset3.csv `
    --treino-inicio 2026-11-10T06:00:00 --treino-fim 2026-11-10T11:30:00 `
    --inicio 2026-11-11T06:00:00 --fim 2026-11-11T13:30:00
```

| Opção | Efeito |
|---|---|
| `--treino-ate T` | Treina o perfil com `[T − HorasHistorico, T)` usando o `PerfilTagService` real, como em produção. O replay começa em T. |
| `--treino-inicio A --treino-fim B` | **Alternativa a `--treino-ate`** (não combinar os dois). Treina o perfil com `[A, B)`, também com o `PerfilTagService` real. Permite escolher um período limpo (por exemplo, o preset 5) em vez das 24 h anteriores. |
| `--inicio R` | Só com `--treino-inicio/--treino-fim`: início do replay. Padrão: `--treino-fim`. Não pode ser anterior a `--treino-fim`. |
| `--permitir-falha-no-treino` | Força a execução quando a trava de TAG73 encontra falha rotulada (seção 2.1). Sai um aviso no log. |
| `--fim` | Fim do replay, inclusivo. Padrão: última leitura do CSV. |
| `--tags` | Lista separada por vírgula. Padrão: todas do CSV, menos TAG60–TAG81. Rótulos são sempre recusados. |
| `--cliente` | Necessário se o CSV não tiver `ClienteId` ou tiver mais de um. |
| `--appsettings` | Padrão: `appsettings.json` da pasta atual. Fornece pesos, limiares e `Bridge:HorasHistorico`. |
| `--horas-historico N` | Substitui `Bridge:HorasHistorico`, tanto no treino quanto na detecção. |
| `--historico-inclui-atual` | Inclui a própria leitura t no histórico servido a ela. Padrão: não inclui. |
| `--regime TAGx=TAGy` | Baseline por regime: TAGy (tag de estado) vira a `TagRegime` de TAGx. Pode ser repetida, ou receber pares separados por vírgula. Cria a linha em `TagsRegimesOperacionaisIa` (SQLite em memória) com os padrões do modelo: lag 0, tolerância de 2 min, `ToleranciaEstado` 0,001 e mínimo de 30 amostras. **Recusa TAG60–TAG81** (rótulos), uma tag como regime de si mesma, TAGx fora das tags reproduzidas e TAGy ausente do CSV. TAGy precisa estar no CSV, mas não precisa ser reproduzida. |
| `--verbose` | Mostra os `Console.WriteLine` do runtime. Por padrão ficam ocultos. |

Código de saída: 0 = ok; 1 = erro, divergência na conferência ou treino recusado pela trava de TAG73; 2 = uso incorreto (inclusive combinação inválida de opções de treino).

### 2.1 Trava de TAG73 (falha rotulada no treino)

A TAG73 é o gabarito do simulador (1 = alguma falha ativa). Ela **nunca** é entrada da IA, mas a ferramenta a usa para não treinar sobre falha:

| Onde verifica | Intervalo | Se houver TAG73 = 1 |
|---|---|---|
| Treino (os dois modos) | `[início, fim)` do treino | Recusa (`Treino recusado: N min com TAG73 = 1 ...`), salvo com `--permitir-falha-no-treino` |
| Baseline por regime (só com `--regime`) | Parte anterior ao replay das janelas móveis: `[R − HorasHistorico, R)`, onde R é o início do replay | Idem |
| Dentro do replay | `[R, fim]` | **Não bloqueia**: é o cenário em teste. O log informa quantos minutos vão entrar nas janelas móveis das leituras seguintes |

- Se o CSV não tiver TAG73 (por exemplo, dados de planta real), a trava avisa `TAG73 ausente no CSV, nao verificada` e segue.
- A trava também vale com `--treino-ate`. Um replay antigo cujo treino continha falha (por exemplo, o preset 6 com treino até 15:06, que inclui a obstrução do preset 1) agora precisa de `--permitir-falha-no-treino` para reproduzir o mesmo resultado.

### 2.2 Baseline por regime e período de treino

O `--regime` **mantém o comportamento de produção**: a cada leitura t, o `OperationalRegimeBaselineProvider` recalcula o baseline sobre o histórico móvel `[t − HorasHistorico, t)`. Ele **não** usa o período de treino: o provider recebe o histórico pronto de `PrepararAsync` e não aceita outra janela sem mudar o runtime (ver `Docs/PROPOSTA_BASELINE_ESTAVEL.md`). O perfil treinado só é usado quando o regime não se aplica (fallback global).

### 2.3 Relatório no início da execução

```
Treino (periodo escolhido): [2026-11-10T15:06:00..., 2026-11-11T15:06:00...) | Replay: [...]
Trava TAG73 (treino): 0 min com TAG73 = 1 em [...)
Regime: baseline movel de 24 h (producao), independente do periodo de treino. Primeira janela [...), ultima [...); dados anteriores ao replay usados: [...)
Trava TAG73 (janela movel do regime (antes do replay)): ...
Perfil TAG22: pontos_treino=1434 media=... desvio=... deadband=... usados=... outliers=...
Regime TAG28 <- TAG43, pontos por estado no treino: 0=... 2=... 3=...
Regime TAG28 <- TAG43, pontos por estado na primeira janela movel: ...
```

- `pontos_treino` = pontos da tag no período de treino, **antes** da limpeza IQR (`usados` e `outliers` são depois).
- A contagem por estado usa um alinhamento simplificado: o último estado até 2 min antes, com lag 0 (os padrões do modelo). A seleção real é feita pelo `OperationalRegimeHistoryResolver`; a contagem serve para ver se o período cobre os regimes.
- O resumo da API (`Cp300ReplaySummary`) traz os mesmos dados: `TreinoInicio`, `TreinoFim`, `InicioReplay`, `PontosTreino`, `MinutosFalhaNoTreino` e `MinutosFalhaNaJanelaDoRegime`.

### 2.4 Regressão byte a byte (`Compare-Replay.ps1`, RC-2)

```powershell
.\tools\Cp300OfflineReplay\Compare-Replay.ps1 -Gerar   # uma vez: referência em dados\rc2_referencia\
.\tools\Cp300OfflineReplay\Compare-Replay.ps1          # depois de cada mudança: 0 diferenças = código 0
```

- `-Gerar` roda `sem` e `rec` de preset 1, madrugada e preset 6 (`Docs/PLANO_REGIME_COMBINADO.md` §5.2) e grava o manifesto (comandos e SHA-256 da entrada, do `appsettings.json` e de cada `.csv`). Recusa sobrescrever sem `-Sobrescrever`.
- O modo padrão roda os comandos do manifesto em `dados\rc2_rodadas\<data_hora>` e compara cada `.csv` por SHA-256 e cada `.log` linha a linha, ignorando só a linha `Saida:` (caminho). Para se a entrada mudou; avisa se o `appsettings.json` mudou.
- Opções: `-Somente preset6_rec`, `-Pasta`, `-SemBuild`, `-Configuracao` (padrão `Debug`, a mesma da etapa 0).
- Teste golden sem dados industriais: `Cp300OfflineReplayTests.Golden_BombasComRegime_SaidaIdenticaAReferencia`, referência em `IAApi.Tests/Cp300Replay/Golden/`. Regravar de propósito com `IAAPI_ATUALIZAR_GOLDEN=1`.

## 3. O que é igual à produção

- `PredictionService.Detectar` real, com `PredictionContextLoader`, preparação, ensemble, decisão, pós-decisão e gravação em `AnomaliaDetectada` (no SQLite em memória).
- 6 detectores na ordem da injeção de dependências. Pesos e limiares vêm do `appsettings.json`, validados por `AnomalyDetectionOptionsValidator`.
- Classificação real do tipo da tag: sem `TagContextoIa`, o tipo é `"perfil-comportamental"`, como em produção quando não há contexto cadastrado.
- Histórico de detecção = últimas `HorasHistorico` horas antes da leitura, que é o equivalente a `GET /historico?horas=N` no instante da leitura.

## 4. O que não é reproduzido

- **Isolation Forest:** desligado. O PK2 não tem modelo no repositório.
- **Multivariado** (shadow e fusão): desligado.
- **Decisão runtime/canário:** desligada, então a decisão é a legada do ensemble. Isso vale para o PK2, que está fora do canário.
- **Baseline por regime:** só com `--regime`. Nesse caso usa o `OperationalRegimeBaselineProvider` real, nas duas instâncias da preparação. Sem a opção, a coluna `origem_baseline` sai sempre `Global`, como antes.
- **Baseline por comportamento:** não instanciado. O banco do PK2 não foi inventariado (BACKLOG, tarefa 2).
- **Latência, ciclo de 60 s e processamento sequencial:** não reproduzidos. Cada leitura do CSV é avaliada no próprio minuto.
- **Contexto da tag** (`TipoRepresentacao`) do banco real: não usado.

## 5. Saída

Uma linha por leitura e detector (6 por leitura). Se a leitura falhar antes do ensemble, sai uma única linha com `erro`. Formato: vírgula, ponto decimal e booleanos como 1/0.

| Coluna | Origem |
|---|---|
| `detector`, `aplicavel`, `participou`, `motivo_nao_aplicavel`, `nota_detector`, `peso` | `DetalhesDetectores` do ensemble |
| `contribuicao` | `nota × peso / soma dos pesos aplicáveis`. Somadas, dão `nota_final`. |
| `nota_final`, `limiar`, `decisao_ensemble` | Resultado do ensemble |
| `decisao_final` | `AnomaliaResponse.EhAnomalia` do `PredictionService` |
| `tipo_tag`, `confianca_tipo`, `roteamento_por_tipo` | `TagBehaviorProfileResolver` sobre o mesmo `DetectorInput` |
| `deadband`, `origem_deadband`, `confianca_deadband` | `BaselineProtectionProfile.Build`: `profile`, `client`, `history-p95` ou `relative-floor` |
| `origem_baseline`, `media`, `desvio`, `pontos_historico` | `DetectorInput` e `BaselineEfetivo` da preparação |
| `entrada_invalida`, `motivo_entrada_invalida` | `DetectionInputValidator.Validate` |
| `erro` | Exceção do `PredictionService` ou do detalhamento. Exemplos: histórico com menos de 10 pontos, perfil não treinado. |
| `conferencia` | `ok`, ou `divergente:<itens>` |
| `valor_regime`, `motivo_baseline` | `BaselineEfetivo.RegimeValue` (vazio se o regime não foi aplicado) e `BaselineEfetivo.Reason` |

**Como o detalhamento é obtido.** A resposta de produção não expõe tipo, origem do deadband nem motivo de entrada inválida. Por isso, para cada leitura, a ferramenta pede o mesmo `DetectorInput` ao `PredictionDetectionPreparationService` real e roda nele uma **segunda instância** do mesmo `EnsembleDetectionService`. A coluna `conferencia` compara a decisão e a nota dessa execução com as do `PredictionService` e confere se as contribuições somam a nota final.

O gabarito não entra na saída do replay; a comparação com ele é feita pelo subcomando `placar` (seção 6). Da Parte C já existem o treino por período e a trava de TAG73 (seção 2); o modo experimental de referência estável por regime (`Docs/PROPOSTA_BASELINE_ESTAVEL.md`, F0) ainda não.

## 6. Placar por evento (`placar`)

Compara a saída de um replay com o gabarito do simulador **por evento de falha**, sem contar coincidências como acerto (`Docs/PROBLEMAS_ENCONTRADOS.md`, P21). Só lê os dois CSVs e o mapa JSON: não roda o replay, não acessa Bridge, PSi4, CLP nem banco.

```powershell
dotnet run --project tools/Cp300OfflineReplay -- placar `
    --replay .\dados\e0_preset6_sem.csv --gabarito .\dados\cp300_10a11nov.csv `
    --saida .\dados\placar_preset6_sem
```

| Opção | Efeito |
|---|---|
| `--replay` | CSV gerado pelo replay (seção 5). Usa `data_hora`, `tag`, `decisao_final` e `erro`. |
| `--gabarito` | CSV de dados (`ClienteId,TagName,DataHora,Valor`) com os slots TAG61–TAG72 e a TAG73. Normalmente o mesmo `--entrada` do replay. |
| `--saida P` | Prefixo dos arquivos gerados: `P.md`, `P_eventos.csv`, `P_por_tag.csv` e `P_falsos_por_hora.csv`. |
| `--mapa` | Mapa falha → tags. Padrão: `placar_falhas_cp300.json` (copiado para a pasta do executável). |
| `--janela-antes N` | Minutos antes da falha em que a tag não pode ter alarmado (padrão 20). `0` desliga a verificação. |
| `--duracao-minima D` | Minutos **seguidos** de alarme na mesma tag elegível para contar como detecção (padrão 3). Sequências mais curtas saem como "sinal breve". `1` = qualquer alarme (regra sem duração). |
| `--tolerancia-buraco M` | Buraco máximo, em minutos, entre leituras do mesmo código de falha dentro de um evento (padrão 2). |
| `--cliente` | Necessário se o gabarito não tiver `ClienteId` ou tiver mais de um. |

Código de saída: 0 = ok; 1 = erro de leitura; 2 = uso incorreto.

### 6.1 Regras

- **Alarme** = minuto-tag com `decisao_final = 1` (as 6 linhas de detector de uma leitura contam uma vez). Leituras com `erro` não contam como avaliadas nem como alarme.
- **Evento** = minutos seguidos com o mesmo código ≠ 0 num slot (TAG61, TAG65, TAG69). Início e fim são o primeiro e o último minuto com o código. Slot ausente no gabarito gera um aviso (o CSV atual do PK2 não tem o slot C). Só entram eventos que se sobrepõem ao período do replay.
- **Tags afetadas** vêm do mapa (`placar_falhas_cp300.json`, editável). Tokens: `TAGnn`; `ALVO.<campo>` e `OUTRA.<campo>` (bomba atingida e a outra); `CANAL` (falhas de sensor: canal = alvo % 100); `*` (todas as tags do replay). Alvo 0 = bomba principal no último minuto antes da falha (TAG44). Rótulos TAG60–TAG81 são recusados no mapa.
- **Detecção (P21):** a primeira sequência de **≥ D minutos seguidos** de alarme (padrão 3), dentro do evento, numa tag **afetada** e que **não alarmou** nos N minutos antes do início. Se várias tags começam a sequência no mesmo minuto, todas aparecem.
  - **1º alarme** = minuto em que a sequência começa; **confirmada às** = início + D − 1 (o minuto em que já dá para saber que ela durou D). **Atraso** = início da sequência − início do evento.
  - A sequência é cortada no fim do evento: alarmes depois do último minuto da falha não contam.
  - **Severidade** e **fase** = valor do slot (TAG63/67/71 e TAG64/68/72) no início da sequência.
- **Sinal breve:** a primeira sequência com **menos de D minutos** numa tag elegível, com tag, minuto, duração, atraso, severidade e fase. Só aparece se vier **antes** da detecção (ou se não houver detecção), ao lado dela no relatório, para não perder a informação. Com `--duracao-minima 1` não há sinal breve.
  - Tags afetadas que não estão no replay (ex.: TAG36 na perda de lubrificação) aparecem como "fora do replay"; tags excluídas aparecem com o número de alarmes na janela anterior.
- **Classificação de todos os alarmes** (a soma dá o total):
  - **Atribuídos:** dentro de um evento, numa tag elegível desse evento (independente da duração mínima: um sinal breve também entra aqui).
  - **Coincidências:** com falha ativa (evento ou TAG73 = 1), mas não atribuídos. Um alarme atribuído a uma falha não conta como coincidência de outra falha sobreposta.
  - **Falsos fora:** minutos sem evento e sem TAG73 = 1. Saem por tag e por hora do dia, com a soma de madrugada (00h–05h) e dia.

### 6.2 Limitações

- A regra é mecânica. A duração mínima separa alarmes de 1–2 minutos (ex.: preset 1 com regime combinado, 13:37, 2 min → sinal breve), mas também empurra a detecção quando há um buraco curto no alarme (ex.: preset 6 com regime combinado: 15:32 vira sinal breve de 1 min e a detecção passa a 15:35). Por isso as duas leituras saem lado a lado.
- Alarmes atribuídos incluem tudo o que a tag elegível alarmou durante o evento, inclusive efeitos indiretos (ex.: TAG29 com a B-01 parada depois do failover do preset 6).
- Atraso e severidade têm resolução de 1 minuto (amostragem do histórico); o início exato da falha pode estar até 1 min antes.
