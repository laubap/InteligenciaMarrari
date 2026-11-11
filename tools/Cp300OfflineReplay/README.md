# Replay offline CP300 (Parte A)

Reproduz, minuto a minuto, a detecção univariada de produção sobre o histórico exportado do Bridge. Gera um CSV com o detalhamento de cada detector.

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
```

| Opção | Efeito |
|---|---|
| `--treino-ate T` | Treina o perfil com `[T − HorasHistorico, T)` usando o `PerfilTagService` real. O replay começa em T. |
| `--fim` | Fim do replay, inclusivo. Padrão: última leitura do CSV. |
| `--tags` | Lista separada por vírgula. Padrão: todas do CSV, menos TAG60–TAG81. Rótulos são sempre recusados. |
| `--cliente` | Necessário se o CSV não tiver `ClienteId` ou tiver mais de um. |
| `--appsettings` | Padrão: `appsettings.json` da pasta atual. Fornece pesos, limiares e `Bridge:HorasHistorico`. |
| `--horas-historico N` | Substitui `Bridge:HorasHistorico`, tanto no treino quanto na detecção. |
| `--historico-inclui-atual` | Inclui a própria leitura t no histórico servido a ela. Padrão: não inclui. |
| `--verbose` | Mostra os `Console.WriteLine` do runtime. Por padrão ficam ocultos. |

Código de saída: 0 = ok; 1 = erro ou divergência na conferência; 2 = uso incorreto.

## 3. O que é igual à produção

- `PredictionService.Detectar` real, com `PredictionContextLoader`, preparação, ensemble, decisão, pós-decisão e gravação em `AnomaliaDetectada` (no SQLite em memória).
- 6 detectores na ordem da injeção de dependências. Pesos e limiares vêm do `appsettings.json`, validados por `AnomalyDetectionOptionsValidator`.
- Classificação real do tipo da tag: sem `TagContextoIa`, o tipo é `"perfil-comportamental"`, como em produção quando não há contexto cadastrado.
- Histórico de detecção = últimas `HorasHistorico` horas antes da leitura, que é o equivalente a `GET /historico?horas=N` no instante da leitura.

## 4. O que não é reproduzido

- **Isolation Forest:** desligado. O PK2 não tem modelo no repositório.
- **Multivariado** (shadow e fusão): desligado.
- **Decisão runtime/canário:** desligada, então a decisão é a legada do ensemble. Isso vale para o PK2, que está fora do canário.
- **Baseline por regime e por comportamento:** não instanciados. O banco do PK2 não foi inventariado (BACKLOG, tarefa 2). A coluna `origem_baseline` sai sempre `Global`.
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

**Como o detalhamento é obtido.** A resposta de produção não expõe tipo, origem do deadband nem motivo de entrada inválida. Por isso, para cada leitura, a ferramenta pede o mesmo `DetectorInput` ao `PredictionDetectionPreparationService` real e roda nele uma **segunda instância** do mesmo `EnsembleDetectionService`. A coluna `conferencia` compara a decisão e a nota dessa execução com as do `PredictionService` e confere se as contribuições somam a nota final.

Fora do escopo desta parte: gabarito (TAG73 e slots), resumo de métricas e modo de treino limpo (Partes B e C).
