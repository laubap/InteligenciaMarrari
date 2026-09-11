# IAApi - Detecção de Anomalias para Tags Industriais

## Visão Geral

A IAApi é uma API ASP.NET Core que monitora tags industriais, treina perfis comportamentais a partir de histórico de operação e detecta anomalias em tempo real.

O sistema consome dados de histórico de um serviço Bridge conectado ao PowerServer, processa os registros com regras estatísticas e armazena perfis em SQLite para consulta e detecção contínua.

## Objetivos do Projeto

* Capturar histórico de tags industriais do PowerServer via Bridge.
* Limpar leituras inválidas e filtrar outliers com método IQR.
* Treinar perfis por cliente/tag usando estatísticas de série temporal.
* Detectar anomalias com um ensemble de detectores de diferentes técnicas.
* Interpretar e armazenar ocorrências de anomalia junto com contexto operacional.

## Arquitetura Geral

1. **BridgeHistoricoService** obtém histórico do Bridge.
2. **HistoricoLimpezaService** remove valores inválidos e outliers (IQR).
3. **PerfilTagService** calcula estatísticas e treina o perfil da tag.
4. **AppDbContext** persiste configurações e perfis em SQLite.
5. **PredictionService** executa detecção de anomalia usando um ensemble de detectores.
6. **AnomaliaController** expõe endpoints para análise e consulta de anomalias.

## Tecnologias utilizadas

* .NET 10 / ASP.NET Core
* Entity Framework Core
* SQLite
* Swagger / OpenAPI
* OpenAI / LLM (integração opcional)
* `Marrari.PowerServer.dll` para integração proprietária

## Estrutura de Pastas

* `Controllers/` - endpoints da API REST.
* `Services/` - lógica de negócios central.
* `Services/AnomalyDetection/` - detectores e ensemble de anomalias.
* `Models/` - classes de dados e DTOs.
* `Data/` - contexto EF e migrações.
* `Benchmark/` - infraestrutura de benchmark e testes de performance.

## Configuração e execução

### Requisitos

* .NET 10 SDK
* Acesso ao Bridge e PowerServer
* Biblioteca `Lib\Marrari.PowerServer.dll`

### Como executar

1. Abra o projeto em `c:\Users\Laura Marrari\Documents\IA MARRARI\IAApi`.
2. Execute:
   ```bash
   dotnet build
   dotnet run
   ```
3. A aplicação cria ou usa o banco SQLite `ia_config.db` na pasta de execução.
4. Se o Swagger estiver habilitado, acesse `/swagger` para explorar os endpoints.

### AppSettings principais

#### `Bridge`

* `BaseUrl` - URL base do serviço Bridge que retorna histórico de tags.
* `HorasHistorico` - quantidade de horas de histórico padrão para consulta.

#### `AnomalyDetection`

* `EnsembleThreshold` - limiar do ensemble para classificar anomalia.
* `MinHistoryPoints` - mínimo de pontos históricos exigidos.
* Pesos de detectores:
  * `ZScoreWeight`
  * `MadWeight`
  * `EwmaWeight`
  * `RateOfChangeWeight`
  * `PersistenceWeight`
  * `LevelShiftWeight`
* Parâmetros de cada detector, por exemplo:
  * `ZScoreThreshold`
  * `MadThreshold`
  * `EwmaLambda`
  * `EwmaSigmaLimit`
  * `RateOfChangeThreshold`
  * `PersistenceMinimumPoints`
  * `LevelShiftPreviousWindow`
  * `LevelShiftRecentWindow`
  * `LevelShiftThreshold`
  * `LevelShiftMinimumConsistency`

#### `AnomalyDetection:IsolationForest`

* `Weight` - peso do detector Isolation Forest no ensemble.
* `TreeCount` - número de árvores.
* `SampleSize` - tamanho da amostra de treinamento.
* `MinimumHistoryPoints` - mínimo de registros para executar.
* `FeatureWindowSize` - janela de extração de features.
* `AnomalyThreshold` - limiar para classificar anomalia.
* `RandomSeed` - semente determinística.
* `NormalizationSlope` - fator de normalização.

#### `Llm`

* `Habilitado` - habilita interpretação por LLM.
* `BaseUrl` - URL do servidor de inferência.
* `model` - modelo a ser utilizado.
* `Temperatura` - temperatura de geração.
* `TimeoutSegundos` - timeout da chamada.
* `KeepAlive` - tempo de keep-alive.

#### `OpenAI`

* `Modelo` - modelo OpenAI.
* `Habilitado` - habilita uso de OpenAI.

#### `Benchmark`

* `ClienteId` - cliente usado para benchmark.
* `CleanDatabasePath` - caminho do banco limpo.
* `AnomalyDatabasePath` - caminho do banco com anomalias.
* `ResultDirectory` - pasta de saída dos resultados.
* `StartDateUtc` - data de início do benchmark.
* `HistoryWindowSize` - tamanho da janela histórica em minutos.
* `MinimumHistoryPoints` - mínimo de pontos para análises.
* `EnableLlm` - habilita uso de LLM no benchmark.

## Banco de Dados

O projeto usa SQLite por padrão com `Data Source=ia_config.db`.

### Entidades principais

#### `TagIaConfig`

* Id
* ClienteId
* TagName
* IaAtiva
* DataConfiguracao

#### `TagPerfilIa`

* Id
* ClienteId
* TagName
* Media
* DesvioPadrao
* Minimo
* Maximo
* Amplitude
* PercentualZeros
* VariacaoMedia
* QuantidadePicos
* TotalRegistrosHistorico
* TotalRegistrosUsados
* TotalOutliersRemovidos
* VariacaoMinimaRelevante
* DeadbandSugerido
* ConfiancaDeadband
* VariacaoMinimaCalculadaAutomaticamente
* JustificativaDeadbandSugerido
* DataTreinamento

#### `AnomaliaDetectada`

* Id
* ClienteId
* TagName
* TipoTag
* ValorRecebido
* Score
* EhAnomalia
* Mensagem
* Criticidade
* ScoreRiscoProcesso
* ClassificacaoRisco
* TendenciaRisco
* TendenciaValor
* Status
* DataDeteccao

### Índices e relacionamentos

* `ClienteId + TagName` é único em `TagIaConfig` e `TagPerfilIa`.
* `TagDependenciaIa` é único por `ClienteId + TagName + TagDependente`.
* `ProcessoEquipamentoIa` vincula processos e equipamentos com relação única.
* `ModeloTagIaStatus` é único por cliente/tag/tipo de modelo.

## Fluxo de Treinamento

1. Requisição POST em `/api/tags/treinar-perfil` ou `/api/tags/retreinar-perfil`.
2. `PerfilTagService` chama `BridgeHistoricoService.BuscarHistorico()`.
3. `HistoricoLimpezaService.RemoverOutliersPorIqr()` filtra valores inválidos e outliers.
4. Calculam-se métricas estatísticas:
   * média, mínimo, máximo, desvio padrão, amplitude
   * percentual de zeros
   * variação média entre leituras
   * quantidade de picos fora de 3 desvios padrão
5. Sugere-se deadband com base no percentil das variações.
6. Perfil é salvo ou atualizado em `PerfisIa`.

## Fluxo de Detecção de Anomalias

1. Requisição POST em `/api/anomalias/tag` com `ClienteId`, `TagName`, `DataHora` e `Valor`.
2. Verifica se existe `TagIaConfig` ativo e perfil treinado.
3. Busca histórico recente via Bridge e normaliza leituras válidas.
4. Cria `DetectorInput` com histórico, perfil e valor atual.
5. O `EnsembleDetectionService` executa todos os detectores registrados.
6. Cada detector retorna score, limite e explicação.
7. O ensemble calcula score ponderado e decide anomalia com base em `EnsembleThreshold`.
8. A resposta inclui perfil comportamental, contexto, dependências, risco e interpretação.

## Endpoints principais

### Tags

* `POST /api/tags/treinar-perfil`
  * Request: `{ "ClienteId": "", "TagName": "" }`
  * Result: perfil treinado e métricas principais.

* `POST /api/tags/retreinar-perfil`
  * Mesma entrada de `treinar-perfil`, faz re-treinamento.

* `POST /api/tags/testar-perfil`
  * Request: `{ "ClienteId": "", "TagName": "", "Valor": 12.34 }`
  * Result: verifica se o valor está fora dos limites de 3 desvios padrão.

* `GET /api/tags/perfis`
  * Lista todos os perfis treinados.

* `GET /api/tags/visao-cliente`
  * Consulta visão consolidada de tags com última análise e contexto.

### Anomalias

* `POST /api/anomalias/tag`
  * Request: `{ "ClienteId": "", "TagName": "", "DataHora": "2026-08-05T12:00:00Z", "Valor": 123.45 }`
  * Result: objeto `AnomaliaResponse` com resultado de detecção e interpretação.

* `POST /api/anomalias/tag/valor-atual`
  * Request: `{ "ClienteId": "", "TagName": "" }`
  * Result: detecta usando o valor atual obtido do Bridge em vez de um valor explícito.

* `GET /api/anomalias`
  * Retorna lista de anomalias detectadas e feedback similar.

## Modelos de dados importantes

#### `AnomaliaRequest`

* `ClienteId`
* `TagName`
* `DataHora`
* `Valor`

#### `AnomaliaResponse`

* `ClienteId`, `TagName`, `TipoTag`
* `EhAnomalia`
* `Score`
* `Mensagem`
* `Resumo`
* `Perfil`
* `Contexto`
* `Interpretacao`
* `Dependencias`
* `Risco`
* `Tendencias`
* `Processo`
* `Detectores`

## Detectores de anomalia

O projeto registra vários detectores via DI e combina resultados:

* `StatisticalDetector` (Z-Score)
* `MadDetector`
* `EwmaDetector`
* `RateOfChangeDetector`
* `PersistenceDetector`
* `LevelShiftDetector`
* `IsolationForestDetector`

O `EnsembleDetectionService` faz a média ponderada dos scores aplicáveis e classifica a anomalia.

## Serviços principais

* `PerfilTagService` - treina e atualiza perfis de tags.
* `HistoricoLimpezaService` - remove registros inválidos e outliers.
* `BridgeHistoricoService` - busca histórico de tags do Bridge.
* `PredictionService` - orquestra a detecção de anomalia e interpretação.
* `MonitoramentoTagsService` - serviço hospedado para monitorar tags em segundo plano.

## Observações importantes

* O banco SQLite padrão é `ia_config.db`.
* O serviço `BridgeHistoricoService` depende de `Bridge:BaseUrl` e `Bridge:HorasHistorico`.
* A detecção de anomalias exige histórico suficiente; casos com menos de 10 pontos são rejeitados.
* O projeto inclui benchmarking e integração opcional com LLM para interpretação.

## Extensões possíveis

* Adicionar mais detectores ao ensemble.
* Implementar Swagger/OpenAPI completo para documentação automática.
* Criar tests unitários e de integração para endpoints e detecção.
* Parametrizar a persistência SQLite para múltiplos ambientes.

## Como contribuir

1. Atualize `appsettings.json` conforme o ambiente.
2. Use `dotnet build` e `dotnet run`.
3. Valide os endpoints com Swagger ou Postman.
4. Ajuste pesos e thresholds em `AnomalyDetection` conforme necessidade.


---

## Configuração do Bridge

Arquivo:

appsettings.json

```json
{
  "Bridge": {
    "BaseUrl": "http://localhost:5001",
    "HorasHistorico": 24
  }
}
```

---

## Execução

### Executar o Bridge

```bash
PowerServerCurrentTagsPropGet.exe
```

### Executar a API

```bash
dotnet run
```

### Swagger

```text
http://localhost:5145/swagger
```

---

## Melhorias Futuras

* Transformar o Bridge em Serviço Windows
* Retreinamento automático agendado
* Dashboard de monitoramento
* Notificações por e-mail
* Agrupamento automático de tags por comportamento
* Implementação de Machine Learning não supervisionado
* Histórico de anomalias detectadas
* Implementação com o software da Marrari
* Cliente obtido através do usuário autentificado no sistema

---

## Autor

Laura Baptisini

Projeto desenvolvido para aplicação de Inteligência Artificial em ambientes industriais utilizando PowerServer e .NET.
