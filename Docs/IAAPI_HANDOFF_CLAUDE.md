# Transferência de contexto técnico — IAApi / Intelligence Lab / CP300

**Preparado:** 11/11/2026  
**Último snapshot técnico detalhado disponível:** 05/11/2026  
**Origem:** histórico de trabalho relatado em conversas; **nenhuma auditoria direta do repositório foi feita para este handoff**. Validar toda afirmação contra o código, testes e logs disponíveis na máquina.

## 1. Objetivo real e estado do trabalho

A IAApi recebe leituras industriais por meio do PSi4/Bridge/PowerServer e realiza análise e detecção de anomalias. Está sendo testada com um simulador de CLP CP300, e **os resultados observados pela usuária nesses cenários estão ruins**. Ainda **não há neste handoff uma relação preenchida dos cenários e das falhas concretas**; coletá-la antes de atribuir causas.

**Prioridade principal:** (1) rastrear os arquivos, classes e métodos da **detecção univariada**, desde a chegada da leitura de tag até a anomalia aparecer no LiveMonitor; (2) ligar cada falha observada nos cenários CP300 aos possíveis pontos responsáveis no código e às medições necessárias para confirmar a causa. Em seguida, investigar qualidade do treino/baseline; **latência é uma possível causa das falhas, não um objetivo independente**. Multivariado shadow e PERF_DIAG são contexto secundário.

## 2. Como classificar evidências

Manter estas categorias explícitas no relatório e em `docs/MAPA_DETECCAO.md`:

| Categoria | Critério | Situação inicial deste handoff |
|---|---|---|
| **Confirmado no código** | Arquivo, classe, método ou configuração inspecionado no checkout atual, com referência precisa | **[PREENCHER]** após a auditoria; nenhum ponto foi conferido diretamente nesta transferência |
| **Confirmado em teste/log** | Execução ou registro observado, com comando, data/ambiente, resultado e evidência | Há resultados **relatados historicamente** abaixo; revalidar antes de tratá-los como confirmação atual |
| **Histórico a validar** | Informação de conversas, exemplos de configuração, tempos antigos, nomes/caminhos ou hipótese | Todo o contexto histórico abaixo, até conferência independente |

Nunca promover uma hipótese a fato sem evidência. Diferenciar o resultado da IAApi, a resposta da API e o que efetivamente apareceu no LiveMonitor.

## 3. Arquitetura e fluxo reportados — histórico a validar

- Backend .NET com APIs REST; confirmar SDK, solution, controllers, DI, persistência, agendadores e ponto de entrada atual.
- Biblioteca extraída `src/IAApi.Engine/Detection`: tipos/contratos citados `IAnomalyDetector`, `DetectorFamily`, `DetectorResult`, `EvidenceStrength`, `ConfidenceEngineOptions` e opções de detecção. Refatoração anterior citada na branch `refactor/b326-architecture-cleanup`.
- Detecção/orquestração: `PredictionService`, `PredictionContextLoader`, `PredictionDecisionOrchestrationService`, `PredictionTrendAnalysisService`, `TendenciaValorService`; podem existir outros componentes decisivos. Confirmar métodos e ordem reais de chamada.
- Origem de dados: `BridgeTagsService`, `BridgeHistoricoService`, `BridgeValorAtualService`, controllers de histórico/tendência. Localizar onde entram leituras atuais e históricas, seus timestamps e sua seleção para inferência.
- Saída/monitoramento: persistência ou emissão da anomalia, endpoints consultados, eventuais filtros e polling no `tools/IAApi.LiveMonitor`; o monitor local foi usado em `http://127.0.0.1:5173`, mas confirmar scripts/porta e evitar iniciar conexões não autorizadas.
- Multivariado: Mahalanobis, grupos, buffer e execução shadow; detalhes secundários na seção 9.
- Branch relatada anteriormente: `feat/intelligence-lab-e2e-validation`; **não presumir que seja a branch atual**.

**Mapa a construir:** leitura recebida → validação/normalização/timestamp → obtenção do histórico → seleção de baseline/modelo → detectores → ensemble/evidência/confiança → orquestração/decisão → publicação/persistência/API → consulta/polling e apresentação no LiveMonitor. **Essa sequência é apenas um roteiro de investigação, não um call graph já comprovado.** Encontrar pontos reais de entrada, bifurcações, filtros, flags, parâmetros e tratamento de erros.

## 4. Cenários de teste CP300 e resultados observados

**Tabela de trabalho — exemplos vazios, NÃO representam testes executados.** Preencher uma linha para cada execução/caso relevante; registrar tags reais, magnitude e duração da injeção, timestamps e evidências. Se possível, anexar IDs ou caminhos de logs/capturas redigidos, sem dados sensíveis.

| Cenário | Tag(s) | O que foi injetado (tipo e magnitude) | Início/fim | Esperado | Observado na IA | Observado no LiveMonitor | Tipo de falha |
|---|---|---|---|---|---|---|---|
| [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] |
| [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] |
| [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] | [PREENCHER] |

**Tipo de falha (escolher uma opção por registro):** `falso positivo` / `anomalia não detectada` / `detecção atrasada` / `tag errada` / `outro`. Quando houver múltiplas falhas na mesma execução, usar linhas separadas ou explicar em notas.

**Evidências complementares por cenário:** identificador do teste [PREENCHER]; versão/commit [PREENCHER]; origem dos timestamps [PREENCHER]; log da IAApi [PREENCHER]; leitura/histórico PSi4 [PREENCHER]; captura ou registro do LiveMonitor [PREENCHER]. Não preencher resultados por inferência.

**Materiais reportados:** `SIM_ANOMALIAS_CP300.c` (lógica do CLP), `Tags_CP300_SIM_ANOMALIAS.csv` (tags do PSi4), `docs/SIMULADOR_ANOMALIAS.html` (descrição de cenários); `MrSimuladorClp` foi usado para modificar valores. O PSi4 coletava aproximadamente **1 leitura/minuto**, com histórico confirmado em etapa anterior; parte das tags tinha **menos de 24 horas de histórico**. Conferir artefatos, execução e versões antes de tirar conclusões. Não comandar o simulador/CLP nem consultar ambiente industrial sem aprovação.

## 5. Testes e ensaio sintético — resultados históricos relatados

- Checkpoints de testes da IAApi: **418/418** e **9/9** específicos de BehaviorMemory na fase de refatoração; depois **699/699**, **704/704** e **741/741** aprovados (último relato em 05/11/2026, com instrumentação `PERF_DIAG` e **25 testes direcionados**).
- LiveMonitor: build e **21 testes** aprovados em checkpoint anterior.
- Esses resultados são relatos de execução passada, não garantias para o checkout atual, nem evidência de qualidade nos cenários CP300.

### M4.2A — drift gradual sintético

Treino relatado: **120 amostras normais**; avaliação: **120 amostras**; janela inicial de **30 minutos**. Regiões: 0–29 normal; 30–74 deterioração; 75–89 degradado; 90–104 recuperação não rotulada; 105–119 normal; incidente **[30,90)**.

Resultado reportado para **6/7 detectores**: **TP 54, FN 6, FP 0, TN 45; precisão 1,000; recall 0,900**. Confirmar rótulos/denominadores se usados como evidência formal. **Não extrapolar** precisão e recall sintéticos para o simulador CP300.

## 6. Hipóteses a investigar — nenhuma é causa confirmada

1. **HIPÓTESE — latência gerando dados antigos/desalinhados.** Predições relatadas de aproximadamente **25 a 40 segundos por tag**, executadas sequencialmente para **12 tags**, poderiam consumir **300–480 segundos (5–8 minutos)** só nessa etapa, enquanto o PSi4 coleta aproximadamente **1 valor/minuto**. É uma **estimativa ilustrativa, não medição de um ciclo completo**. Pode haver leitura defasada ou tags comparadas em momentos diferentes. **Verificar:** ordem real de execução, paralelismo, duração do ciclo, timestamp da amostra usada, janela histórica, timestamp da decisão e momento de exibição.
2. **HIPÓTESE — baseline curto ou contaminado.** Treino relatado de `PK2.TAG22`: **130 amostras** (~**2 h 10 min** a 1/min), **31 outliers**, **mínimo 73**, **média 605,31** (também: desvio **121,92**, máximo **798**, registro em **30/10/2026 14:10**); algumas tags tinham menos de **24 h** de histórico. Se houve injeção de anomalias durante o treino, o modelo pode ter aprendido padrões anômalos como normais. **Verificar:** intervalo e origem das amostras, marcas das injeções, descarte de outliers, filtros de qualidade, versão e persistência do baseline; não concluir contaminação apenas pelo número de outliers.
3. **HIPÓTESE — diferença entre experimento sintético e CP300.** O M4.2A relatou **precisão 1,0** e **recall 0,9**, mas os resultados no CP300 foram descritos como ruins, **sem tabela de casos preenchida neste handoff**. Ruído, amostragem, timestamps, histórico curto, dinâmica física simulada, thresholds ou caminhos de execução podem diferir. **Verificar:** comparação de distribuições, cadência, janelas, regimes, pré-processamento e condições de treino/inferência antes de responsabilizar um detector.
4. **HIPÓTESE — timestamps inconsistentes.** Horário de aquisição no CLP/PSi4 pode ser diferente do horário do histórico no Bridge, do horário de avaliação/registro na IAApi e do horário exibido no LiveMonitor. **Verificar:** origem, fuso, precisão, ordenação, atrasos, arredondamentos e conversões em cada etapa; comparar os mesmos identificadores de leitura ponta a ponta.

Para cada cenário preenchido na seção 4, ligar **falha observada → etapa do mapa → hipótese → evidência necessária → modo de reprodução offline**, distinguindo claramente ausência de evidência de evidência negativa.

## 7. Clientes, baselines e configurações — exemplos históricos

- Em checkpoint anterior, `GET /api/tags` exibiu **12 tags `PK2.TAG22` a `PK2.TAG33`** com `iaAtiva=true`; verificar configuração atual e sua influência no pipeline de detecção.
- Grupo multivariado `CP300-B01-Piloto` (id histórico **1**, algoritmo **Mahalanobis**, `minimumAvailableTags=3`; entradas `PK2.TAG27`, `PK2.TAG29`, `PK2.TAG31`). Janela de correlações aproximada **14:30–16:29**, com **119 leituras/tag**: TAG27–TAG31 **0,890**; TAG27–TAG29 **0,748**; TAG29–TAG31 **0,704**. Treino: **119 vetores finalizados**, modelo gerado no caminho histórico `Multivariate\PK2\CP300-B01-Piloto.json`.
- O grupo foi marcado `ativo=true` em momento posterior, **sem implicar promoção operacional**. Para PK2, univariado habilitado; shadow multivariado elegível; fusão/promoção e decisão multivariada runtime **bloqueadas** no último registro. Diagnóstico histórico de TAG27 citou `LegacyCanaryExcluded` e `StableKnownRegime`. **Nunca modificar gates ou interpretar shadow como decisão operacional sem verificar código e autorização.**

## 8. Latência e PERF_DIAG — contexto de apoio ao diagnóstico

**Tempos de logs reportados em 05/11/2026**, não revalidados no checkout atual:

| Operação | Tempo reportado |
|---|---:|
| Bridge lote PK7, 1 tag | **4,5 s** |
| Predição `PK7.TAG1` | **40,7 s** |
| Bridge lote PK2, 12 tags | **29,4 s** |
| Predição `PK2.TAG22` | **25,5 s** |
| Predição `PK2.TAG23` | **34,5 s** |
| Histórico `PK2.TAG27` | **12,29 s**; ~**126.729 bytes** |

Havia indicação de **predições sequenciais**. Investigar se isso causou janelas diferentes entre tags, amostras antigas ou perda de eventos — não assumir causalidade. Separar tempo de aquisição, histórico, cálculo, decisão, publicação, polling e renderização.

Instrumentação opt-in `IAAPI_PERF_DIAGNOSTICS=1`, com log esperado em `%TEMP%\IAApi-perf-diag.log`, abrangendo contexto, shadow, consultas históricas, baselines, ensemble, fusão/interpretação e finalização. Uma inspeção anterior **não encontrou linhas `PERF_DIAG`**; verificar habilitação/caminho antes de concluir que a coleta funciona. **Não ativar serviços nem buscar histórico industrial real nesta primeira sessão.**

## 9. Multivariado shadow — contexto secundário

Serviços mencionados: `MultivariateShadowRuntimeCoordinator`, `MultivariateRuntimeStateStore`, `BufferedGroupReading`. Observabilidade histórica distinguia vetor elegível/finalizado/descartado, execução Mahalanobis efetiva, score, threshold, distância, indisponibilidade de modelo e erros; coletava com retenção limitada, isolamento por cliente e comportamento fail-open. Endpoint `/api/intelligence-lab/diagnostics/multivariate-shadow` tinha restrições de Development + loopback + chave. A observabilidade não deveria alterar fusão nem decisão operacional. Validar no código somente na medida necessária para compreender interações com a detecção univariada.

## 10. LiveMonitor e endpoints — contexto secundário

Endpoints historicamente usados:

- `/api/tags`
- `/api/tags/bridge/PK2`
- `/api/anomalias`
- `/api/tags/historico/PK2/{tag}?horas=2`
- `/api/intelligence-lab/diagnostics`

LiveMonitor em `tools/IAApi.LiveMonitor` tinha seleção de tags, polling configurável, cards, gráficos, histórico, destaque de novas anomalias e cinco estados. Confirmar **qual endpoint efetivamente alimenta o indicador de anomalia**, quais filtros/estados o ocultam, o papel do polling e a diferença entre o horário do evento e o horário da exibição. Houve conflito de porta 5173 no passado. Não presumir contratos atuais.

## 11. Próxima sessão — primeira tarefa do Claude Code

**Modo:** auditoria e documentação, sem alterar código, dados ou configurações. A única criação/edição de arquivo de projeto autorizada para a sessão é **`docs/MAPA_DETECCAO.md`**; não modificar este handoff nem `CLAUDE.md` sem solicitação. Artefatos transitórios inevitáveis de compilação/teste (`bin`, `obj`, caches) não devem ser versionados nem incluídos como entregas. Não modificar arquivos rastreados ou executar rotinas com efeitos externos.

**Entregas obrigatórias:**

**a. Git e estado local.** Confirmar `git status`, branch, commit atual e alterações recentes; preservar mudanças não commitadas. Registrar diferenças entre o código e este handoff.

**b. Testes offline.** Inspecionar antes os projetos, scripts e fixtures; executar os testes automatizados comprovadamente **offline/isolados**, incluindo `dotnet test` e testes do LiveMonitor **quando seguros**. Relatar comando, escopo, total, aprovados/falhos/ignorados, duração e o que **não** foi executado por risco de conexão. Não acessar Bridge, PSi4, PowerServer, CLP, banco real ou serviços industriais.

**c. `docs/MAPA_DETECCAO.md`.** Gerar um mapa **comprovado pelo código** da detecção **univariada ponta a ponta**, começando na chegada/obtenção da leitura e terminando na API e no LiveMonitor. Apresentar **arquivos, classes e métodos em ordem de chamada**, ramificações, dependências e o que cada etapa recebe, transforma, filtra e decide. Mostrar onde ficam **thresholds, baselines/modelos, flags, janelas, políticas e seus valores/origens de configuração** (código, `appsettings`, banco ou outro — verificar). Localizar pontos de persistência, exceção, supressão, timestamp e publicação da anomalia. Destacar o que não foi possível confirmar.

**d. Falhas CP300 ↔ mapa.** Para **cada cenário efetivamente preenchido** na tabela da seção 4, indicar os pontos do mapa que **podem** explicar a falha, que medição/log/teste offline diferenciaria as hipóteses e qual evidência ainda falta. Se todas as linhas estiverem `[PREENCHER]`, **não inventar casos nem conclusões**: fornecer uma matriz/modelo de diagnóstico e apontar objetivamente os dados necessários.

**e. Plano priorizado.** Sugerir **3 a 5 próximos passos**, ordenados por impacto na **qualidade da detecção CP300** (não por instrumentação isolada). Para cada um: evidência de suporte, benefício esperado, risco, esforço, dependências e **teste de aceite observável**. Tratar otimização de latência apenas quando relacionada a uma hipótese de falha.

**f. Limites da sessão.** **Não editar código**, não treinar/retreinar/promover modelos, não alterar gates, thresholds, baselines ou configurações, não executar integração nem se conectar ao ambiente industrial. Não instalar dependências, iniciar serviços, efetuar commits/push/deploys ou usar banco real sem autorização. O **único arquivo novo permitido** é `docs/MAPA_DETECCAO.md`. Aguardar aprovação antes de implementar correções.

## 12. Critérios de qualidade e comunicação

- **Rastreabilidade:** cada afirmação técnica deve indicar arquivo/método ou teste/log; manter as categorias **confirmado no código**, **confirmado em teste/log** e **histórico a validar**.
- **Qualidade da detecção:** priorizar correspondência entre injeção CP300, dados efetivamente avaliados, decisão da IA e apresentação no LiveMonitor. Não considerar uma predição correta apenas porque retornou HTTP 200 ou porque um teste unitário passou.
- **Segurança:** nenhuma mudança operacional inadvertida, sem credenciais nem dados de clientes nos documentos; não confundir simulação e planta real.
- **Reprodutibilidade:** registrar commit, comandos e pré-condições; separar fatos, hipóteses, testes que passaram, testes que falharam e testes não executados.
- **Comunicação:** entregar descrição técnica e explicação simples, compreensível por alguém que não participou do desenvolvimento.

**Regra final:** se código ou testes atuais contradisserem este handoff, documentar a divergência. O handoff não substitui o repositório nem os resultados observados.
