# IAApi.LiveMonitor

Monitor local React + TypeScript + Vite para acompanhar testes CP300 do cliente **PK2**. Usa apenas GETs previamente permitidos. Não inicia a IAApi, não dispara detecção, não treina modelos e não escreve no banco ou nas configurações operacionais.

## Executar no Windows / PowerShell

Requisito: Node.js 22.12+ (validado com 22.22.0) e npm.

```powershell
cd 'C:\Users\Laura Marrari\Documents\IA MARRARI\IAApi_RECUPERADO\tools\IAApi.LiveMonitor'
npm ci
Copy-Item .env.example .env
npm run dev
```

Abra **http://127.0.0.1:5173/**. A API padrão é `http://localhost:5145`, conforme `Properties/launchSettings.json`. Se já tiver `.env`, preserve-o em vez de repetir a cópia. Dependências já foram instaladas neste checkout.

O monitor pode iniciar com a IAApi desligada: exibirá INDISPONÍVEL e tentará reconectar. Inicie a IAApi pelo seu procedimento habitual de teste. Não foi iniciada automaticamente nesta entrega, pois seu hosted service executa análises e grava resultados.

```powershell
# Verificação e versão compilada (pare o servidor anterior com Ctrl+C)
npm test
npm run build
npm run preview
```

`preview` também fornece o proxy de leitura. Abrir `dist/index.html` diretamente ou hospedar só os arquivos estáticos não fornece o proxy.

## Configuração

Edite `.env` e reinicie o monitor:

| Variável | Padrão | Finalidade |
| --- | --- | --- |
| `IAAPI_URL` | `http://localhost:5145` | Origem HTTP(S) da IAApi; sem usuário/senha na URL |
| `MONITOR_CLIENT` | `PK2` | Cliente consultado |
| `MONITOR_POLL_MS` | `2000` | Intervalo entre o fim de uma consulta de decisões e o início da próxima; mínimo 1 s |
| `MONITOR_HISTORY_MS` | `15000` | Histórico do Bridge, mais caro; mínimo 5 s |
| `MONITOR_STALE_MS` | `180000` | Limite para considerar leitura ou análise antiga |
| `MONITOR_SOURCE_OFFSET` | `-03:00` | Offset das datas da API/Bridge que chegam sem fuso |
| `IAAPI_DIAGNOSTICS_KEY` | vazio | Opcional, chave **já existente** no Intelligence Lab |

As variáveis são lidas pelo servidor Node. A chave não recebe prefixo `VITE_`, não integra o bundle/configuração pública e não é enviada pelo navegador. `.env` é ignorado pelo Git. Não desative validação TLS para usar HTTPS.

O Lab só funciona se a IAApi **já** estiver em Development, com Lab e buffer habilitados e chave válida (mínimo 32 caracteres). O monitor não altera essas condições. Para usar a chave, a URL da API precisa ser loopback. Sem Lab, as decisões normais/anômalas persistidas continuam visíveis, mas alguns campos ficam indisponíveis. HTTP 401 indica chave recusada; 404 pode indicar Lab desabilitado, buffer ausente, configuração insuficiente ou ID removido do buffer.

Servidor e preview escutam em `127.0.0.1`, porta 5173. O middleware verifica endereço local, Host e Origin e recusa métodos diferentes de GET. Não publique nem coloque o monitor atrás de proxy externo: os diagnósticos têm acesso global à instância, sem isolamento por usuário/tenant. Não é necessário mudar CORS da IAApi.

## Como acompanhar um teste CP300

1. Execute a IAApi e o Bridge pelo fluxo habitual, com as tags do ensaio já configuradas e treinadas. O dashboard não faz essas alterações.
2. Abra o monitor, aguarde o catálogo e selecione as tags na lateral. A seleção é salva somente no armazenamento local do navegador. O catálogo é relido a cada 60 s; descrições do Bridge são consultadas ao abrir a página.
3. Execute seu cenário no simulador. Observe **Última decisão operacional**, o valor analisado e **Registro da análise** (ou **Análise concluída**, quando o Lab fornece esse horário).
4. Uma nova linha aparece automaticamente quando a API persistir uma análise. Anomalias novas após a primeira consulta bem-sucedida geram destaque vermelho. “Dispensar destaque” limpa apenas o destaque local.
5. Consulte “Tempos, detectores e multivariada” para timestamps, diferenças de tempo e resultados intermediários. Valores ausentes são exibidos como “não informado” ou “—”.

### Estados e correlação

O estado no topo do card refere-se à **leitura mais recente disponível**, não à ausência de registros de anomalia:

- **NORMAL / ANOMALIA:** decisão final concluída com timestamp **e valor** da leitura correspondentes. Na implementação atual isso exige diagnóstico detalhado válido.
- **AGUARDANDO:** ainda não há decisão correlacionável à leitura atual. Sem timestamp da leitura no registro público, uma decisão persistida pode aparecer como NORMAL/ANOMALIA na faixa “Última decisão”, enquanto o estado da leitura permanece AGUARDANDO.
- **DESATUALIZADO:** leitura ou análise ultrapassou `MONITOR_STALE_MS`. O timestamp, e não a chegada de um HTTP 200, determina a idade.
- **INDISPONÍVEL:** falha na obtenção das fontes necessárias. Dados anteriores permanecem visíveis, identificados como indisponíveis.

Os totais “última análise normal/anômala” contam a última decisão recebida de cada tag, inclusive antiga; não representam saúde atual. A conexão refere-se à consulta pública de decisões. Falhas de histórico/Lab são exibidas separadamente.

Não se infere normalidade de score baixo, lista vazia, voto de detector ou resultado shadow. `runtimeDecision.finalAnomaly` pode divergir de `ensemble.legacyAnomaly`. Diagnóstico `Partial` representa instrumentação incompleta; uma decisão final booleana com `completedAtUtc` ainda é válida. Sem esses campos não há confirmação.

## Inspeção da IAApi: fontes e finalidade

| Endpoint | Uso e significado confirmado no código |
| --- | --- |
| `GET /api/tags` | Usado para catálogo e `iaAtiva`, filtrado por cliente no proxy. `TagConfigController.Listar` retorna configuração, sem valores ou decisões. |
| `GET /api/tags/bridge/PK2` | Usado para descrição/unidade; metadados do Bridge, sem detecção. |
| `GET /api/anomalias` | Fonte pública principal: **todos** os registros persistidos, normais e anômalos. `AnomaliaController.Listar` expõe ID, cliente, tag, `valor`, `score`, `ehAnomalia`, `dataDeteccao` e contexto de risco. Não possui paginação/filtro por cliente e calcula feedback por registro. O proxy filtra o cliente antes de responder ao navegador. |
| `GET /api/tags/historico/PK2/{tag}?horas=2` | Usado para gráfico e última leitura disponível. `TagHistoricoController` lê via `IHistoricoService` / `BridgeHistoricoService` e retorna `{ historico: [{ clienteId, tagName, dataHora, valor }] }`. Se a janela estiver vazia, pode devolver as últimas 20 leituras antigas. Isso não é resultado de IA. |
| `GET /api/intelligence-lab/diagnostics?clienteId=PK2&tagName=...&limit=100` | Opcional e protegido. Lista IDs, timestamp da leitura, completude, score normalizado e `finalAnomaly`. Resumo não contém valor nem horário de conclusão. |
| `GET /api/intelligence-lab/diagnostics/{id}` | Opcional: detalhe com `readingValue`, `readingTimestampUtc`, `completedAtUtc`, `runtimeDecision.finalAnomaly/source`, `ensemble.normalizedScore/threshold` e detectores. Fonte para correlação e latência. IDs são consultados uma vez por sessão de seleção e mantidos em cache limitado. |
| `GET /api/intelligence-lab/diagnostics/multivariate-shadow?clienteId=PK2&limit=100` e `/{id}` | Opcionais: eventos de montagem/finalização/execução por grupo/vetor. O detalhe fornece `expectedTags`, score e threshold; permite associar o grupo à tag. A tela mostra o evento mais recente por grupo. **Não é decisão operacional.** |
| `GET /api/anomalias-historico/PK2` | Investigado, não usado: filtra `EhAnomalia == true`. Não revela análises normais nem permite concluir normalidade por ausência de evento. |
| `GET /api/tags/visao-cliente` | Alternativa investigada: última decisão, valor e horário por perfil treinado, além de descrição. Não contém ID/score/threshold/timestamp da leitura; flags `monitoramentoIa` e `perfilTreinado` são constantes true na projeção. Não adotado como catálogo operacional. |
| `GET /api/anomalias-dashboard/resumo/PK2`, `/api/dashboard-geral` | Agregados históricos, insuficientes para confirmar a última leitura de cada tag. |
| `GET /api/anomaly-runtime/metrics`, `/status`, `/readiness`, `/observation` | Observabilidade agregada do runtime/rollout, não substitui decisão individual por leitura. Não consultados. |
| `POST /api/anomalias/tag`, `/api/anomalias/tag/valor-atual` | Executam detecção. **Nunca chamados.** Endpoints de teste, correlação, backtest e treinamento também não são chamados. |

Nenhum mecanismo SignalR, WebSocket ou SSE para as decisões foi encontrado na inspeção de controllers/registro de serviços. O MVP usa polling.

## Caminho da decisão e latência esperada

Referências relativas à raiz da IAApi:

- `Services/MonitoramentoTagsService.cs`: hosted service lista `TagsIaConfig.Where(IaAtiva)`, processa cada tag **sequencialmente** e depois aguarda `TimeSpan.FromMinutes(1)`.
- `Services/CurrentValuePredictionService.cs`: lê o valor atual no Bridge e passa valor + `DataHora` para `PredictionService.Detectar`.
- `Services/PredictionService.cs`: prepara histórico/contexto; chama `PredictionDecisionOrchestrationService` e finaliza a decisão antes da captura passiva de diagnóstico.
- `Services/AnomalyDetection/Orchestration/PredictionDecisionOrchestrationService.cs`: chama `EnsembleDetectionService.Detectar` e resolve a decisão de runtime, incluindo as regras existentes de promoção. O valor operacional é `runtimeDecision.EmitAnomaly`.
- `Services/AnomalyDetection/Orchestration/PredictionFinalizationService.cs` e `Services/AnomaliaHistoricoService.cs`: persistem **incondicionalmente** tanto normal quanto anomalia. `DataDeteccao = DateTime.Now` é horário de registro, não o timestamp de aquisição.
- `Services/AnomalyDetection/Observability/PredictionDiagnosticCapture.cs`: captura depois da finalização, sem recalcular; publica `CompletedAtUtc` e a decisão final. Pode rejeitar timestamp `Unspecified`, valor não finito e falhas de captura.
- `Controllers/IntelligenceLabLocalReadAccess.cs`: exige Development + Lab habilitado + buffer habilitado + origem loopback + chave válida.

**Não há garantia de uma análise exata por minuto.** Para cada tag, o intervalo do serviço é aproximadamente **60 s + tempo de processamento de todas as tags**, incluindo chamadas ao Bridge, histórico e pós-processamento. Uma mudança no CP300 pode esperar quase um ciclo completo antes de ser observada, além do tempo de aquisição/detecção. O polling acrescenta normalmente até cerca de **2 s + tempo de resposta HTTP** após a persistência, sem sobrepor consultas. Atrasos da aba em segundo plano/suspensão do computador também aumentam esse tempo.

O histórico é consultado em ciclo separado a cada 15 s + duração das consultas, com até 3 tags em paralelo; assim, histórico lento não bloqueia consulta de decisões. Seu endpoint busca inicialmente a janela configurada no Bridge (padrão 24 h no serviço), mesmo com `horas=2`, antes de filtrar a resposta. A consulta do monitor tem timeout de 12 s; a IAApi pode continuar trabalhando em uma chamada síncrona ao Bridge até o próprio timeout, pois esse caminho não propaga o cancelamento HTTP.

Tempos apresentados: leitura → conclusão/registro; conclusão/registro → **primeira recepção da decisão** no dashboard; leitura → recepção. O primeiro recebimento é preservado na deduplicação. Em registros carregados ao abrir a tela, essa diferença inclui a idade histórica, não mede apenas latência de rede. Sem timestamp da leitura, essas diferenças ficam indisponíveis. Diferenças negativas indicam possível dessincronização de relógios. Datas sem offset usam a configuração explícita `MONITOR_SOURCE_OFFSET`.

## Limitações conhecidas / lacunas da API

1. `GET /api/anomalias` não expõe timestamp da leitura, threshold, detector/fonte de decisão nem o término exato da análise. O Lab preenche parte dessas lacunas se já estiver disponível. Não foi criado endpoint na IAApi.
2. Sem Lab, é possível acompanhar novas decisões NORMAL/ANOMALIA e seus horários de registro, mas **não confirmar que pertencem à última leitura mostrada no gráfico**. Por isso, os pontos vermelhos exigem correlação real do Lab; não são posicionados pelo horário de gravação.
3. Último valor disponível vem do histórico/diagnóstico; não há neste MVP consulta de aquisição instantânea no CP300/Bridge. Não equivale necessariamente ao valor físico neste exato instante.
4. O endpoint público é ilimitado e recalcula feedback. Em bases grandes, polling de 2 s pode ser caro; aumente o intervalo. Uma solução durável seria um GET paginado/incremental ou um GET de últimas decisões com timestamp de aquisição, **somente após aprovação para alterar a IAApi**.
5. Diagnósticos são temporários e limitados; só os últimos 100 resumos por tag/grupo são consultáveis por vez, sem paginação. IDs podem expirar entre lista e detalhe. Isso não garante auditoria sem perdas. Há cache de detalhes e lista visual limitada; recarregar a página redefine a memória local dos alertas.
6. Shadow pode fechar vetor depois da análise univariada. O monitor associa grupos via `expectedTags` e não assume que todo evento tem execução do Mahalanobis ou decisão final. Thresholds individuais de detectores não são capturados atualmente; threshold do ensemble/shadow só aparece quando realmente retornado.
7. No modo público, a lista cronológica e alertas usam IDs persistidos; detalhes Lab complementam os cards sem criar um segundo evento para a mesma análise.
8. O código de diagnóstico multivariado já tinha modificações locais antes desta tarefa. Elas foram preservadas; a instância executada pode não oferecer esses contratos até usar o build correspondente.

## Arquivos e validação

- `server/proxy.ts`, `vite.config.ts`: proxy fechado, GET-only, chave só no servidor, configuração de desenvolvimento/preview.
- `src/api.ts`, `src/types.ts`: cliente HTTP tipado e contratos.
- `src/domain.ts`: interpretação, timestamps, estados, correlação e deduplicação.
- `src/polling.ts`, `src/useMonitor.ts`: cancelamento, recuperação automática, ciclos sem sobreposição e cache.
- `src/App.tsx`, `src/TagCard.tsx`, `src/Chart.tsx`, `src/style.css`, `src/main.tsx`: interface e gráficos SVG sem biblioteca gráfica adicional.
- `tests/*.test.ts`: interpretação, cinco estados, decisões finais divergentes de ensemble, deduplicação, polling e proteção do proxy.
- `.env.example`, `.gitignore`, `package*.json`, `tsconfig.json`, `index.html`: configuração do projeto.

Criado dentro de `tools/`, já excluída do projeto .NET em `IAApi.csproj`. A única alteração fora do monitor foi uma exceção no `.gitignore` para permitir versionar esta ferramenta. Não houve commit, mudança de algoritmo, configuração operacional, banco, modelo ou simulador.

Validação nesta entrega: build TypeScript/Vite e 21 testes automatizados passaram. Servidor local respondeu HTTP 200; configuração pública foi verificada sem chave exposta; proxy retornou HTTP 502 explícito para a API desligada. A interface foi aberta e inspecionada no navegador, incluindo a mudança do intervalo de polling. A API real estava desligada (conexão recusada em `localhost:5145`), portanto a consulta real e o cenário físico CP300 **não foram validados**. Os testes usam dados sintéticos somente dentro de `tests/`; a aplicação não possui modo de demonstração nem resultados fictícios.
