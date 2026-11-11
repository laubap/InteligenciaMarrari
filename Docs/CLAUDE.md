# IAApi — instruções permanentes para Claude Code

> Projeto corporativo Marrari. Usar Claude Code e compartilhar código com a Anthropic somente se houver autorização da empresa.

## Fonte de verdade e evidências

- O repositório atual é a fonte de verdade para implementação; testes e logs **efetivamente executados/verificados** são a fonte para comportamento observado. `docs/IAAPI_HANDOFF_CLAUDE.md` contém contexto histórico, não uma auditoria do estado atual.
- Em toda análise, diferenciar explicitamente: **confirmado no código**, **confirmado em teste/log** e **histórico a validar**. Não tratar suspeitas como causa demonstrada.
- Começar por `git status`, branch, commits recentes, projetos, configuração de DI, fluxo relevante e testes; não presumir caminhos ou responsabilidades sem verificar.

## Segurança e permissões

- **Permitido sem nova aprovação:** leitura/análise do repositório e execução de testes automatizados **comprovadamente offline e isolados** (por exemplo, `dotnet test` e testes do LiveMonitor). Antes de rodá-los, inspecionar projetos, scripts, fixtures, variáveis de ambiente e dependências para garantir que **não** acessem Bridge, PSi4, PowerServer, CLP, bancos reais ou serviços industriais. Se não for possível garantir isolamento, **não executar**; informar o risco e pedir autorização. Não iniciar API/monitor conectado a ambiente real.
- **Exige aprovação explícita:** editar código, instalar dependências, mudar configuração, executar integrações industriais, acessar bancos reais ou dados de clientes, treinar/retreinar/promover modelos, habilitar grupos ou alterar gates, thresholds, baselines e decisões operacionais.
- Nunca escrever em CLP/Bridge/PSi4, fazer commit, push, merge, deploy, trocar branch, descartar mudanças ou executar comandos destrutivos sem autorização específica.
- Não ler, reproduzir ou transmitir segredos, tokens, `.env`, credenciais, strings de conexão, certificados, dumps ou dados industriais sensíveis; solicitar exemplos redigidos quando necessário. Respeitar políticas e isolamento por cliente.
- Preservar contratos HTTP, comportamento operacional legado, salvaguardas multivariadas e isolamento; instrumentação/observabilidade não pode modificar a decisão de anomalia. Não confundir simulador com validação em planta real.

## Prioridade de investigação

1. **Mapear a detecção univariada ponta a ponta:** da chegada da leitura até o registro de anomalia e sua exibição no LiveMonitor; identificar arquivos, classes, métodos, decisões, configurações e thresholds.
2. **Cruzar o mapa com os cenários CP300 que falharam:** localizar possíveis causas no código e definir evidências para confirmar ou descartar cada hipótese.
3. **Examinar qualidade do treino e do baseline:** janela, origem, volume, outliers, possível contaminação e diferenças entre dados sintéticos e simulador.
4. **Investigar latência e alinhamento temporal entre tags:** apenas como possíveis fatores que explicam falhas de detecção, incluindo coleta, histórico, processamento sequencial e timestamps.

## Forma de trabalho

- Antes de propor mudanças, explicar evidências, hipóteses, arquivos envolvidos, impacto sobre a detecção, riscos, testes de aceite e rollback. Aguardar aprovação para implementar; preferir alterações pequenas e reversíveis.
- Relatar comandos executados, testes aprovados/falhos/não executados e limitações. Não afirmar que um cenário CP300 foi validado sem dados observados.
- Na **primeira sessão**, seguir as entregas e a exceção documental restrita descritas em `docs/IAAPI_HANDOFF_CLAUDE.md`: somente `docs/MAPA_DETECCAO.md` pode ser criado; nenhum código deve ser alterado.
