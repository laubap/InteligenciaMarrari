# Plano de produto: área de Configuração na interface

*Criado em 2026-11-17. Feito só com leitura: a IAApi estava em sessão de medição. Nada foi compilado, nenhum teste ou replay foi rodado, e a API não foi chamada. Nenhum código foi alterado.*

**Nível de evidência usado:**
- **[código]**: confirmado no código nesta sessão (HEAD `f4020ec` da IAApi; HEAD `21dc80d` do IAFront);
- **[doc]**: vem de outro documento do repositório e não foi reconferido aqui;
- **[a conferir]**: hipótese ou ponto não verificado.

Nada aqui foi testado. As rotas "quebradas" do IAFront são leitura de código, não erro observado.

Tarefa no backlog: **37** (`Docs/BACKLOG.md`). A tarefa 27 teve a ordem revista por este plano.

> **Decisões da responsável (2026-11-17), sobre o §6** (detalhe no §8):
> 1. Configuração no IAFront; o LiveMonitor continua só de leitura.
> 2. IAFront e LiveMonitor juntos com proxy no IAFront, **sem mudar a API**.
> 3. A gravação usa **exatamente o perfil simulado**, sem recalcular, com um identificador da simulação.
> 4. A tabela de versões do perfil **entra na demo**, com backup antes da migration.
> 5. Limites dos avisos: proposta calibrada nos dados do CP300 no §3.2. ~~aguardando aprovação~~ **Aprovados na segunda rodada (abaixo).**
> 6. Regime na demo: configurar e simular pela tela; **ativar só se a RC-12 passar num ensaio antes da demo**.
>
> **Segunda rodada (2026-11-17; detalhe no §9):**
> - limites do §3.2 **aprovados**, configuráveis por cliente;
> - nomes de rotas, tabela e configuração escolhidos por Claude, a pedido da responsável, seguindo o padrão do projeto (§9.2);
> - validade da simulação: **15 min**;
> - leitura do §3.3 **confirmada**: com período, gravar exige o `simulacaoId`, e o `Gravar = true` que recalculava sai do desenho;
> - script de calibração trazido para `tools/CalibracaoAvisosTreino/`, com README;
> - **tarefa 6:** na demo, fica o reuso do histórico como está (Development, ~2,2 min por tag); 6.1 a 6.3 **pausadas** até investigar o custo fixo de ~4 s por requisição do Bridge;
> - **RC-12:** deixa de depender da 6.1–6.3. Pré-requisito passa a ser a **6.0b (feita)** e o **`HistoricoCausal` ligado**.
>
> Nada implementado.

---

## 0. Resumo

1. **A Configuração deve ficar no IAFront**, que é a interface do produto. O LiveMonitor continua sendo uma ferramenta de teste **só de leitura**. Pôr escrita no LiveMonitor quebraria a garantia "só GET" que ele tem hoje (lista fechada, testada) e criaria uma segunda interface de configuração em paralelo.
2. **Muito do que é preciso já existe na API**, mas sem tela ou sem as garantias certas:
   - tags monitoradas: `TagsIaConfig` e `POST /api/tags/configurar`;
   - treino: `treinar-perfil` (só as últimas 24 h, grava na hora, sem simulação);
   - grupos multivariados: CRUD completo com validações;
   - equipamento/processo: só criação, sem edição nem exclusão.
3. **O regime operacional (simples ou combinado) não tem nenhum endpoint.** As tabelas `TagsRegimesOperacionaisIa` e `TagsRegimesComponentesIa` só são lidas pelo provider, pelo replay e pelos testes. Para configurar o regime pela tela, é preciso criar a API.
4. **Risco que a tela tem de evitar:** gravar uma linha de regime com `Ativo = true` muda a detecção na leitura seguinte, **mesmo com o regime combinado desligado**. Nesse caso o provider usa só o componente 1, sem mapa, e esse regime não foi validado no replay.
5. **Retreino ao vivo:** a 27.3 vira o "back-end" de uma tela no IAFront (simular → prévia → gravar → voltar). Para "voltar ao anterior" é preciso guardar a versão anterior do perfil. Hoje `PerfisIa` tem uma linha por tag, que é sobrescrita a cada treino. A proposta é uma tabela aditiva de versões, que já é a base do "histórico de treinos" do produto.
6. **Demo do fim do mês: o prazo é apertado.** A ordem proposta põe primeiro a tela de retreino e depois a configuração do regime. ~~A ativação do regime (RC-12) continua dependendo das tarefas 6, 25 e 35.2 e de aprovação.~~ **[17/11, §9]** A ativação do regime (RC-12) depende da 6.0b (feita), do `HistoricoCausal` ligado (25), da 27, da 35 (feita) e de aprovação; não mais da 6.1–6.3. Na demo, o regime pode ser **configurado e simulado** pela tela sem ser ativado.

---

## 1. O que é o IAFront e como se compara ao LiveMonitor

| | **IAFront** | **tools/IAApi.LiveMonitor** |
|---|---|---|
| Onde | `..\IAFront` (pasta irmã, **fora** do repositório da IAApi, com git próprio) [código] | `tools/IAApi.LiveMonitor`, dentro do repositório da IAApi [código] |
| Histórico | 5 commits, de 30/06 a 28/07/2026; árvore limpa [código] | ativo; tarefas 19, 28 e 34 [doc] |
| Tecnologia | React 19, Vite 8, TypeScript, MUI 9, Recharts, axios, react-router [código] | React, TypeScript e Vite, com um servidor Node (`server/proxy.ts`) que faz de proxy [código] |
| Como fala com a API | O navegador chama direto `http://localhost:5145/api` (`src/services/api.ts`) [código] | O navegador chama o proxy local, que só repassa GETs de uma lista fechada; qualquer outro método recebe `405` (`server/proxy.ts:25`, `:92`) [código] |
| Cliente | Fixo `"PK7"` em `src/contexts/ClienteContext.tsx`, com o comentário "futuramente esse valor virá do login" [código] | `MONITOR_CLIENT` no `.env`, padrão `PK2` [código] |
| Para quem | Interface do produto: dashboard, equipamentos, processos, tags, anomalias, feedback, biblioteca de conhecimento, "Configurações da IA". Os textos se dirigem ao usuário do cliente ("Cadastre uma tag para ser monitorada…"). **Uso real por clientes: [a conferir]** | Equipe, para acompanhar testes CP300 do PK2. O README proíbe publicar o monitor ou colocá-lo atrás de proxy externo (os diagnósticos não têm isolamento por cliente) [código] |
| Escreve? | Sim: `POST /tags/configurar`, `/tags/treinar-perfil`, `/anomalias/tag/valor-atual`, `/processos`, `/tags/contexto`, feedback, motivos, ações e categorias; `PUT /configuracao-ia`; `DELETE` de motivos e categorias [código] | Não, por desenho e por teste (`tests/proxy.test.ts`) [código] |
| Porta | Padrão do Vite, 5173 (`vite.config.ts` não define porta) [código] | 127.0.0.1:5173 com `strictPort` [código] |

**Rotas da IAApi que o IAFront chama** [código]:
- `GET`:
  - `/tags/bridge/{cliente}`, `/tags/visao-cliente`, `/tags/tendencia/{tag}?horas=6`, `/tags/historico?tagName=&horas=`;
  - `/anomalias`, `/anomalias/{id}/feedback`, `/notificacoes`, `/dashboard-geral`;
  - `/equipamentos`, `/equipamentos-dashboard/{id}`, `/processos/{cliente}`, `/processos-dashboard/{id}`;
  - `/conhecimento-ia`, `/configuracao-ia/{cliente}`, `/categorias-falha-ia/{cliente}`, `/motivos-ia/{cliente}`.
- `POST`: `/tags/configurar`, `/tags/treinar-perfil`, `/anomalias/tag/valor-atual`, `/tags/contexto`, `/processos`, `/anomalias/{id}/feedback`, `/acoes-ia`, `/motivos-ia`, `/categorias-falha-ia`.
- `PUT`: `/configuracao-ia/{cliente}`.
- `DELETE`: `/motivos-ia/{id}`, `/categorias-falha-ia/{id}`.

**O IAFront está defasado em relação à API (4 meses sem commit).** Achados na leitura do código, não testados:
1. **Duas rotas que provavelmente não existem mais nesse formato:**
   - `GET /tags/tendencia/{tag}`: a API espera `/tags/tendencia/{clienteId}/{tagName}`. Com só um segmento, a URL cai em `GET /api/tags/{clienteId}/{tagName}` do `TagConfigController`, com `clienteId = "tendencia"` → `404`;
   - `GET /tags/historico?tagName=…`: a API espera `/tags/historico/{clienteId}/{tagName}`.
2. **"Configurações da IA" grava valores que nada lê.** `ConfiguracoesIA` (`HorasHistorico`, `Sensibilidade`, `ConfiancaMinima`, `AprendizadoAtivo`…) só é usada pelo `ConfiguracaoIAController`. Nenhum serviço da detecção a lê (busca em todo o código C#). Na demo, essa tela mostraria controles sem efeito.
3. **"Cadastrar Tag" faz 3 coisas em sequência:**
   - configura com `iaAtiva: true`;
   - treina o perfil com as últimas 24 h, gravando na hora;
   - roda uma análise.
   - Não há escolha de período, simulação nem volta. É exatamente o fluxo que o retreino ao vivo precisa substituir.
4. **Listas sem filtro de cliente:** `GET /tags/visao-cliente` (`PerfilTagController.cs:173`) lista os perfis de todos os clientes. Isso entra no P34.
5. **CORS:** a API só aceita a origem `http://localhost:5173` (`Program.cs:10-17`). O LiveMonitor usa a mesma porta com `strictPort`. **Os dois não sobem juntos como estão.** O LiveMonitor não depende de CORS (usa proxy); o IAFront depende.

### Recomendação: a Configuração fica no IAFront

- **Motivos:**
  - é a interface do produto: tem contexto de cliente pronto para o login, cadastro de tag e processo e uma área "Configurações da IA";
  - já tem a stack necessária para a prévia (MUI para formulários, Recharts para o gráfico perfil atual × novo).
- **O LiveMonitor não deve receber escrita:**
  - a garantia "só GET, lista fechada" é o que permite rodá-lo durante as sessões de medição sem risco;
  - ele não tem isolamento por cliente e não pode ser publicado;
  - pôr escrita nele criaria uma segunda área de configuração em paralelo, o que a regra do `CLAUDE.md` proíbe.
  - No máximo, ele ganha **um link** para a tela do IAFront (produto, opcional).
- **Consequências (registradas como passos):**
  - o IAFront precisa voltar a funcionar com a API atual e com o PK2 (cliente configurável, CORS/porta);
  - "Cadastrar Tag" e "Configurações da IA" têm de convergir para a nova área, **sem** conviver com ela (§5).
  - O IAFront é **outro repositório**: mudanças nele têm commits próprios, também só com autorização.

---

## 2. O que já existe e o que falta

### 2a. Quais tags são monitoradas

**Já existe:**
- Tabela `TagsIaConfig` (`ClienteId`, `TagName`, `IaAtiva`, `DataConfiguracao`) [código].
- Endpoints em `TagConfigController` [código]:
  - `POST /api/tags/configurar`: cria ou atualiza; na atualização, só muda `IaAtiva`;
  - `GET /api/tags`: todos os clientes;
  - `GET` e `DELETE /api/tags/{clienteId}/{tagName}`.
- O monitor relê `TagsIaConfig.Where(IaAtiva)` **a cada ciclo** (`Services/MonitoramentoTagsService.cs:39-41`), e a predição confere `IaAtiva` (`PredictionContextLoader.cs:24`) [código]. **Ativar ou desativar vale no ciclo seguinte, sem reiniciar.**
- O catálogo do Bridge vem de `GET /api/tags/bridge/{clienteId}`, já usado pelo IAFront e pelo LiveMonitor [código].
- RC-7: lista de tags proibidas como **regime** por cliente, em `AnomalyDetection:RegimeOperacional:TagsProibidasPorCliente` [código, doc].

**Falta:**
- **P15:** `IaAtiva = true` por padrão. Uma tag cadastrada por engano entra no monitoramento [código, doc].
- **Validações:** nenhuma no `configurar` [código]:
  - não confere se a tag existe no catálogo do Bridge do cliente;
  - não recusa rótulos (TAG60–TAG81 no PK2; a RC-7 só bloqueia rótulo como *regime*, e a lista fica no `appsettings`, lida por `IOptions`, ou seja, só muda com reinício);
  - não atualiza `DataConfiguracao` quando muda `IaAtiva`.
- **Lista por cliente:** `GET /api/tags` devolve todos os clientes (P34).
- **O que acontece com uma tag ativa sem perfil treinado:** [a conferir] em `PredictionContextLoader.BuscarPerfil`. A tela precisa avisar isso.
- **Quem e quando mudou:** não há registro (entra com a autenticação, P34).

### 2b. Janela de treino por tag, com simulação antes de gravar

**Já existe:**
- `POST /api/tags/treinar-perfil` e `retreinar-perfil` (`PerfilTagController.cs:22`, `:143`). Os dois chamam `PerfilTagService.TreinarPerfil` (`:34`), que [código]:
  - busca as últimas `Bridge:HorasHistorico` horas (`:55`);
  - calcula;
  - grava na hora (`:117`, `:194`).
  - **Não há período nem simulação.**
- `PerfisIa` tem uma linha por `(ClienteId, TagName)`, sobrescrita a cada treino, sem período e sem versão [doc, tarefa 27].
- A predição lê o perfil do banco a cada leitura, sem cache [doc, tarefa 27]: um perfil gravado vale na leitura seguinte.
- `GET /api/tags/perfis`: todos os clientes e **sem** os 4 campos que a detecção usa (`VariacaoMinimaRelevante`, `ConfiancaDeadband`, além de `Media` e `DesvioPadrao`, que ele mostra) [código]. Não serve para a prévia nem para backup.
- `IHistoricoService.BuscarHistoricoPorPeriodo` (Bridge `/historico?inicio&fim`, fim inclusivo) [doc, tarefa 27].
- **Janelas de treino** `JanelasTreinamentoTagsIa` com CRUD em `/api/janelas-treinamento`: valida período e sobreposição e tem ativar/desativar (`JanelasTreinamentoController.cs`) [código]. **Hoje só o Isolation Forest usa** [doc].
- **27.3 planejada** (BACKLOG 27, Q1) [doc]: `Inicio`/`Fim`/`Gravar` opcionais no `treinar-perfil`, recorte `[inicio, fim)`, simulação por padrão, validações (`fim > inicio`, `fim <= agora`, no máximo 7 dias, ao menos 720 pontos) e resposta com o perfil anterior completo. Absorve a tarefa 8.

**Falta, para a tela:**
1. **A própria 27.3** (nada implementado).
2. **Dados da prévia na resposta da simulação:**
   - perfil atual e candidato, **todos** os campos, lado a lado;
   - a série do período (1 ponto por minuto, já buscada para o treino) para o gráfico, evitando uma segunda consulta ao Bridge.
3. **Avisos sobre o período** (§3.2). Hoje não existe nenhum.
4. **"Voltar ao anterior":** hoje não há para onde voltar. O rollback da tarefa 27 (Q3) é um script SQL com a API parada, que não serve para a tela.
5. **Garantir que "Gravar" grava o que foi visto:** se gravar recalcular com nova busca ao Bridge, o resultado pode diferir da prévia (buracos, atraso) (§3.3).
6. **Decidir o papel das `JanelasTreinamentoTagsIa`:**
   - a 27 escolheu o período na requisição, e não a janela cadastrada;
   - a tela deve **guardar o período usado junto com a versão do perfil**, e não criar janelas;
   - unificar o IF com isso fica para o produto (§5).

### 2c. Associações entre tags

#### Regime com vários componentes e mapas

**Já existe:**
- Tabelas [código]:
  - `TagsRegimesOperacionaisIa`: `TagName`, `TagRegime` (componente 1), `Ativo` (**padrão `true`**), `LagMinutos`, `ToleranciaAlinhamentoMinutos`, `ToleranciaEstado`, `MapaEstados`, `MinimoAmostrasPorRegime` (30);
  - `TagsRegimesComponentesIa`: `ConfiguracaoId`, `Ordem`, `TagRegime`, `MapaEstados` e `ToleranciaEstado` (componentes 2..N, RC-5).
- Lógica pronta e testada [doc]:
  - chave e mapa (RC-4), resolver com N componentes (RC-6);
  - provider atrás da chave (RC-8), `RegimeKey` no diagnóstico (RC-9);
  - replay com `--regime` e `--regime-mapa` (RC-10).
  - O replay já recusa componente repetido, igual à tag analisada, ausente do CSV ou da lista de rótulos, e mapa inválido.
- Configuração alvo do PK2 decidida (RC-11, §7.9): TAG27/29/31 = TAG42 + TAG43 + TAG46; TAG28/30/32 = TAG43 + TAG42 + TAG46; mapas `2→2; *→0` e `380→380; *→450` [doc].
- Liga/desliga global `AnomalyDetection:RegimeCombinado:Habilitado` e lista da RC-7. As duas ficam no `appsettings` e são lidas por `IOptions` (`OperationalRegimeBaselineProvider.cs:39-49`), ou seja, **só mudam com reinício** [código].
- Banco local com a migration RC-5 (35.1). Falta a 35.2. Nenhum cliente tem regime cadastrado [doc].

**Falta:**
- **Nenhum endpoint lê ou grava regime.** Só o provider, o replay e os testes usam essas tabelas (busca em todo o código C#) [código]. Hoje o cadastro da RC-12 seria feito direto no banco.
- **Validação no runtime para o cadastro.** As regras existem no replay e, em parte, no provider (recusa só no momento da predição, como "não aplicado"). É preciso reaproveitar o parser de mapa da RC-4 e o `IsForbidden` da RC-7, **não** reescrevê-los.
- **Prévia de cobertura:** quantos pontos por chave de regime na janela de 24 h, qual é a chave atual, quais classes ficam abaixo de `MinimoAmostrasPorRegime` (→ fallback global) e quais valores estão fora do mapa. Hoje só o replay mostra isso, no log.
- **Proteção contra ativação perigosa** [código, `OperationalRegimeBaselineProvider.cs:106-135`, `:180`]:
  - o provider aplica qualquer linha com `Ativo = true`;
  - com o regime combinado **desligado**, ignora os componentes 2..N e o `MapaEstados`: usa só o `TagRegime` principal;
  - cadastrar a configuração alvo com `Ativo = true` e a chave desligada aplicaria **um regime de 1 componente sem mapa** (só TAG42 ou TAG43), que não é o validado na RC-11;
  - como `Ativo` é `true` por padrão no modelo, um cadastro ingênuo já ativa.

#### Grupo multivariado

**Já existe** [código]:
- `GruposAnaliseIa` e `GrupoAnaliseTagsIa` (`Role`), com CRUD em `/api/grupos-analise`.
- Validações em `GrupoAnaliseCadastroService`:
  - nome único por cliente;
  - ao menos 2 tags distintas, sem repetição;
  - `MinimumAvailableTags` entre 2 e o número de tags;
  - `MaxLagMinutes` ≥ 0, `Sensibilidade` entre 0 e 1, `PesoEnsemble` ≥ 0.
- Treino por período (`POST {id}/treinar`, com `Inicio`/`Fim`), backtest, revisão industrial, qualificação e prontidão.

**Falta:**
- Tela.
- Conferir que as tags do grupo são do mesmo cliente e estão monitoradas: não vi essa checagem no validador [a conferir].
- **Regra do `CLAUDE.md`:** habilitar grupos e alterar gates exige aprovação explícita. A tela pode criar e editar, mas ativar ou promover tem de continuar atrás das salvaguardas multivariadas existentes.
- **Fica para o produto** (não é necessário para a demo).

#### Equipamento e processo

**Já existe** [código]:
- `EquipamentosIa` e `EquipamentoTagsIa` (`PapelDaTag`, `Escopo`), `ProcessosIa`, `ProcessoTagsIa` e `ProcessoEquipamentosIa`.
- Endpoints de criação:
  - `POST /api/equipamentos`;
  - `POST /api/equipamentos/{id}/tags` (confere o cliente);
  - `POST /api/processos`;
  - `POST /api/processo-equipamentos`.
- Listagens e dashboards.
- No IAFront: "Cadastrar Processo" e lista de equipamentos.
- `TagDependenciasIa` (`POST`/`GET /api/tags/dependencias`).

**Falta:**
- Editar e excluir (não há `PUT` nem `DELETE`).
- Tela de equipamento.
- Listas filtradas por cliente (`GET /api/equipamentos` devolve todos, P34).
- Saber se alguma decisão de anomalia lê essas tabelas [a conferir; a tarefa 27 cita só telas e serviços auxiliares].
- **Fica para o produto.**

---

## 3. Retreino do PK2 ao vivo pela tela (a 27.3 como parte da tela)

### 3.1 Fluxo na tela (IAFront → Configuração → Treino do perfil)

1. Lista as tags monitoradas do cliente com o perfil atual: data do treino, pontos usados e os 4 campos da detecção.
2. Por tag, o usuário escolhe o período (início e fim, com atalhos "últimas 24 h" e "período validado") e clica em **Simular**.
   - Nada é gravado.
   - Uma tag por vez: cada simulação é uma consulta ao Bridge, que já é o gargalo do ciclo (91 % do tempo em buscas de 24 h, MEDICAO_ETAPA0) [doc].
3. **Prévia:**
   - tabela perfil atual × candidato (média, desvio, deadband, confiança, mín./máx., % de zeros, pontos recebidos, usados e removidos);
   - gráfico da série do período com as faixas `média ± k·desvio` dos dois perfis;
   - avisos (§3.2).
4. **Gravar**, com confirmação explícita ("isto muda a detecção desta tag a partir da próxima leitura"). Registra a hora.
5. **Voltar ao anterior:** restaura a versão anterior da tag, com a mesma confirmação. O próprio "voltar" é reversível (§3.4).

### 3.2 Avisos sobre o período (calculados na simulação; não mudam a decisão)

**Situação: ✅ aprovada em 2026-11-17 (§9), com os limites configuráveis por cliente.** Os valores abaixo são o padrão global da seção `TreinoPerfil` (§9.2).

Os avisos servem só para a tela. Os bloqueios só impedem a simulação ou a gravação. Nenhum dos dois entra na decisão de anomalia.

#### Como os limites foram calibrados (2026-11-17)

- **Dados:** `dados/cp300_09a13nov.csv` (simulador, PK2, 1 ponto por minuto, de 09/11 12:30 a 13/11 22:59), tags TAG22–TAG33.
- **Janelas:**
  - o período validado da tarefa 27, `[12/11 15:00, 13/11 15:00)`;
  - todas as janelas de **24 h** e de **3 h**, com passo de 1 h.
  - Cada janela foi separada em **limpa** (TAG73 = 0 em toda a janela) ou **com falha** (TAG73 = 1 em algum minuto). A TAG73 foi usada só aqui, offline, para calibrar; ela não entra no runtime.
- **Métricas:** iguais às do treino.
  - A limpeza é IQR 1,5× com percentil linear (`Services/HistoricoLimpezaService.cs:33-53`, `:65-81`) [código].
  - **Conferência do script:** no período validado ele reproduz exatamente o `dados/p3b_sem.stdout`: 1.437 pontos por tag, as mesmas médias e desvios e 302 outliers na TAG24.
- **Execução:** só leitura do CSV, com um script Python (`avisos_cp300.py`). Não houve compilação, teste, replay nem chamada à API. **[17/11] Trazido para `tools/CalibracaoAvisosTreino/`, sem mudança, com README;** a saída com `dados/cp300_09a13nov.csv` foi conferida idêntica à da calibração.
- **Limite da evidência:** dados do **simulador**, e não de planta. Ruído, quantização e paradas reais podem ser diferentes. Os limites devem ficar **por cliente, em configuração**, com estes valores como padrão, e ser recalibrados com dados da planta.

**O que os dados mostram** (TAG22–TAG33):

| Métrica | 24 h limpas (48 janelas) | 24 h com falha (36) | 3 h limpas (90) | Período validado | Perfil atual do PK2 (30/10, INV §2) |
|---|---|---|---|---|---|
| Pontos | 1.403 a 1.440 | 1.433 a 1.440 | 149 a 180 | 1.437 em todas | 161 a 232 |
| Maior buraco (min) | p95 1; **máx. 30** (uma vez) | máx. 1 | p95 1; máx. 30 | 1 | não guardado |
| % de zeros | 0 nas tags contínuas; **13 a 39 % nas vazões das bombas** (TAG26/27/28/31/32: rodízio) | igual | até **100 %** (bomba parada) | 0 a 31 % | **TAG28 77 %, TAG32 97 %** (colapsadas) |
| Maior trecho constante **não nulo** (min) | **máx. 5** | **até 30** (TAG24 30, TAG23 20: sensor congelado no preset 3) | máx. 5 | máx. 4 | — |
| % removido pelo IQR | **TAG24 21 % em toda janela** (noite, P01); 0 nas contínuas; até 35 % nas bombas em algumas janelas | igual | até 31 % | TAG24 21,0 %; o resto ≤ 0,1 % | 0 a 24 % |

**Leitura:**
- **Zeros e outliers não distinguem uma janela limpa de uma com falha.** As distribuições são praticamente iguais. Contaminação por falha só aparece por dois sinais:
  - **valor congelado** (trecho constante não nulo);
  - **anomalias registradas no período**.
  - Por isso o aviso de anomalias registradas é necessário, e a trava da TAG73 continua sendo a conferência offline.
- **Zeros nas vazões das bombas são normais (rodízio).** Um limite baixo avisaria sempre nas tags das bombas. O que distingue o perfil ruim de 30/10 é a **maioria** de zeros (77 % e 97 %).

#### Bloqueios (recusam a simulação ou a gravação)

| Bloqueio | Limite proposto | Base nos dados |
|---|---|---|
| Período invertido ou no futuro | `fim <= inicio` ou `fim > agora` | (lógico) |
| Duração máxima | **7 dias** | carga no Bridge (P10, P42); os dados não informam |
| Mínimo de pontos | **720** (12 h a 1 ponto por minuto) | 24 h limpas têm ≥ 1.403; o treino de 30/10 (161–232) e as janelas de 3 h (≤ 180) **seriam recusados** |

#### Avisos (não impedem gravar; a tela mostra e pede confirmação)

| Aviso | Limite proposto | No período validado | No perfil de 30/10 | Base |
|---|---|---|---|---|
| **Período curto** | duração < **24 h** | não | **sim** (~3 h) | P01: só 24 h cobrem a noite |
| **Buraco** | maior intervalo sem leitura ≥ **15 min**, ou cobertura < **95 %** | não (máx. 1 min; 99,8 %) | — | limpas: p95 = 1 min; o único buraco de 30 min aparece [causa não investigada] |
| **Maioria de zeros** | % de zeros > **50 %** | não (máx. 31 %) | **sim** (TAG28 77 %, TAG32 97 %) | rodízio normal: até 39 % em 24 h |
| *Informação:* "tag com estados (ligada/parada)" | 10 % a 50 % de zeros | sim: TAG26/27/28/31/32 | — | só um texto: o perfil global mistura estados; ver o regime |
| **Valor congelado** | trecho constante **não nulo** ≥ **15 min** | não (máx. 4) | — | limpas: máx. 5; congelamentos do preset 3: 20 e 30 |
| **Muitos outliers** | IQR remove ≥ **10 %** | **sim, TAG24 (21 %)** | — | contínuas: ~0 %; avisa o P01 ("o IQR pode estar removendo um estado normal, por exemplo a noite") |
| **Anomalias registradas no período** | ≥ **1** em `AnomaliasDetectadas` da tag no período | [a conferir no banco do ambiente] | — | é o único sinal de contaminação no runtime |
| **Mudança grande em relação ao perfil atual** | \|Δmédia\| > **2** desvios do perfil atual, ou razão entre o desvio novo e o atual fora de **[0,5; 2]** | **sim** em TAG24 (razão 0,08), TAG27 (2,1), TAG28, TAG30, TAG31 e TAG32; **não** em TAG22, 23, 25, 26, 29 e 33 | — | texto: "mudança grande; é o esperado se o perfil atual estava ruim; confira a prévia" |
| *Informação:* "tag com regime ativo" | configuração de regime ativa | não (nenhum regime no PK2) | — | o global vale só como fallback |

- **Mudança calculada contra o perfil do banco** (INV §2, cópia de 13/11). Δmédia em desvios do atual e razão dos desvios:

  | Tag | Δmédia (em desvios do atual) | Razão dos desvios |
  |---|---|---|
  | TAG22 | −0,03 | 0,93 |
  | TAG23 | 0,01 | 0,79 |
  | TAG24 | 0,69 | **0,08** |
  | TAG25 | 0,53 | 0,79 |
  | TAG26 | 0,23 | 0,95 |
  | TAG27 | −1,67 | **2,12** |
  | TAG28 | **192** | **120** |
  | TAG29 | −1,56 | 1,87 |
  | TAG30 | **7,6** | **4,5** |
  | TAG31 | **−2,9** | **4,0** |
  | TAG32 | **1.746** | **1.063** |
  | TAG33 | 0,49 | 0,77 |

- **Na demo, avisos esperados no período validado:**
  - "muitos outliers" na TAG24;
  - "mudança grande" nas 6 tags acima;
  - a informação de tag com estados nas 5 vazões das bombas.
  - Nenhum bloqueio.
  - O ensaio (27.5) confere que a tela mostra exatamente isso.
- **Fora do runtime:** a trava da TAG73 (gabarito do simulador) continua só no replay. Rótulo nunca entra no runtime (tarefas 11 e 12). Na demo, o período usado é o validado offline na 27.2.
- **Onde ficam os limites:** na seção **`TreinoPerfil`** (§9.2; por cliente, com padrão global). Ela fica **fora** de `AnomalyDetection` e não é lida pela detecção. Mudar um limite muda só os avisos e bloqueios da tela.

### 3.3 "Gravar" grava exatamente o que foi simulado

**Decidido (17/11, decisão 3): gravar usa exatamente o perfil simulado, sem recalcular.**
- **Simulação:** devolve um `simulacaoId` e guarda o candidato em memória por pouco tempo (**15 min, aprovado em 17/11**, `TreinoPerfil:SimulacaoValidadeMinutos`). Guarda também o período e a versão do perfil atual em que a prévia se baseou.
- **Gravação com o id:** grava **esse** perfil, sem nova busca ao Bridge. Isso também evita uma segunda carga no Bridge durante a demo.
- **Id vencido ou desconhecido** (por exemplo, a API reiniciou) → `409`, e a tela pede nova simulação.
- **Perfil atual mudou entre a simulação e a gravação** (outra gravação ou um "voltar" no meio) → `409`. Assim, a prévia "atual × novo" que o usuário viu continua verdadeira no momento de gravar.
- **Consequência para o contrato da 27.3 (✅ interpretação confirmada em 17/11; contrato no §9.2):**
  - **com período**, gravar **exige** o `simulacaoId`;
  - o `Gravar = true` que recalculava sai do desenho;
  - **sem período**, o `treinar-perfil` continua idêntico ao de hoje (24 h, grava na hora), por compatibilidade;
  - descartado: recalcular ao gravar e comparar com a prévia.

### 3.4 Voltar ao anterior: versões do perfil

**Decidido (17/11, decisão 4): entra na demo, com backup antes da migration** (cópia do `ia_config.db` com `-wal` e `-shm` e SHA-256, com a API parada, no padrão da 35.1; conferência depois, no padrão da 35.2).

- **Desenho:** tabela aditiva **`PerfisVersoesIa`** (entidade `TagPerfilVersaoIa`; nome decidido em 17/11, §9.2):
  - cópia **completa** da linha de `PerfisIa` (todas as colunas);
  - período usado (nulo quando foi o treino de 24 h), origem (`gravacao`, `restauracao`) e data.
- **Gravação:**
  - antes de sobrescrever `PerfisIa`, o caminho novo copia a linha atual para `PerfisVersoesIa`, na mesma transação;
  - "voltar" copia a versão anterior de volta para `PerfisIa` e guarda também a que estava em uso. Assim, voltar pode ser desfeito.
- **Impacto na detecção:** nenhum por si só. A predição continua lendo só `PerfisIa`, e um teste deve provar que a `PredictionContextLoader` não muda. O que muda a detecção é o conteúdo restaurado, como em qualquer gravação.
- **Precisa de migration.** Aplicar no banco do ambiente da demo com backup, no padrão da 35.1, e conferir no padrão da 35.2. Lição do P43: uma migration pendente faz a predição falhar ao ler tabelas alteradas. Esta tabela é nova e a predição não a lê, mas a conferência continua obrigatória.
- **Não substitui o backup da tarefa 27 (Q3)**, que continua obrigatório antes da demo (cópia do `.db` e JSON das linhas com SHA-256).
- **Não é trabalho descartável:** o "histórico de treinos" do produto é uma tela sobre a mesma tabela (§4, P2).
- **Alternativa descartada:** arquivos JSON de backup gravados pela API. Evita a migration, mas fica fora do banco, não serve para o produto e seria jogada fora (regra de não manter em paralelo).

### 3.5 Nova ordem da tarefa 27

O que muda: a 27.3 passa a incluir a tela e o "voltar". O código (27.3) não espera mais a 27.2 e pode andar em paralelo com ela. A gravação (27.6) passa a ser feita **ao vivo pela tela**, depois de um ensaio.

| Passo | O que | Muda a decisão? | Depende de |
|---|---|---|---|
| 27.0 | Plano (feito em 16/11) | Não | — |
| 27.1 | Replay `--perfis <json>` | Não | — |
| 27.2 | Medição offline banco × candidato. **Decide quais tags e qual período serão escolhidos na demo** | Não | 27.1 |
| 27.3a | API: período e simulação no `treinar-perfil` (Q1), resposta com perfil atual e candidato completos, série para o gráfico, avisos (§3.2) e `simulacaoId` (§3.3) | Não (sem os campos novos, igual a hoje) | — (em paralelo com 27.1 e 27.2) |
| 27.3b | API: `PerfisVersoesIa` + "voltar ao anterior" (§3.4) + `GET` do perfil completo de uma tag, com o cliente | Não (tabela que a predição não lê) | 27.3a; migration aplicada com backup |
| 27.3c | IAFront: tela Treino do perfil (§3.1) | Não (a tela só chama a API) | 27.3a, 27.3b; D1 |
| 27.4 | (Opcional, **depois da demo**) o replay usa o mesmo método | Não | 27.3a |
| 27.5 | **Ensaio autorizado** com o Bridge: backup Q3; simular pela tela o período escolhido (aceite: 1.437 pontos por tag e estatísticas iguais às do `p3b_sem.stdout`); gravar e voltar numa tag de ensaio, conferindo `PerfisIa` igual ao backup depois do "voltar" | Sim, por minutos, numa tag de ensaio; volta no mesmo ensaio | 27.2, 27.3c, aprovação |
| 27.6 | **Demo:** gravar pela tela só as tags aprovadas na 27.2; anotar a hora | **Sim** | 27.5, aprovação explícita |
| 27.7 | Observação com o simulador e o `HistoricoCausal` ligado (25); se precisar, voltar pela tela (o script Q3 fica de reserva) | — | 25 |

- **Atenção:** a decisão da 27 (Q4) continua valendo. Se nenhum perfil global for aceitável nas tags da B-02 sem regime, o retreino delas acompanha a RC-12. Na demo, isso significa retreinar primeiro e configurar o regime depois, na mesma sessão.
- **Risco de demo:** o Bridge precisa ainda devolver o histórico de 12–13/11 no dia. O ensaio (27.5) confirma. Plano B: o período validado mais recente com as mesmas condições, medido de novo na 27.2.

---

## 4. Plano em passos pequenos

Cada passo tem aprovação própria. Nenhum passo de código muda a decisão sem gravação explícita. Todos têm testes offline, com `IHistoricoService` e Bridge falsos e banco em memória, e `Compare-Replay.ps1` com 0 diferenças quando tocam a API.

### 4.1 Entra na demo do fim do mês

Ordem: primeiro tudo o que o retreino precisa, depois o regime. Se o prazo apertar, corta-se do fim (D7 → D6), e o retreino continua de pé.

| # | O que | Onde | Muda a decisão? | Aceite | Rollback | Esforço |
|---|---|---|---|---|---|---|
| D0 | Decisões (§6): IAFront como casa da Configuração; porta e CORS; nomes de rotas e tabela; limiares dos avisos | doc | Não | Registradas no backlog | — | P (✅ **feita em 17/11**: §8 e §9) |
| D1 | IAFront volta a funcionar no ambiente da demo: cliente por variável (`VITE_CLIENTE_ID`, padrão `PK7`, como hoje); proxy do Vite para `/api` (o navegador deixa de depender do CORS, e o IAFront pode subir em outra porta junto do LiveMonitor, **sem mudar a API**); nova entrada de menu "Configuração"; o cartão de `ConfiguracoesIA` fica fora da demo ou marcado "sem efeito na detecção" | IAFront | Não | IAFront e LiveMonitor abertos juntos; o IAFront lista as tags do PK2 | Reverter o commit do IAFront | P |
| D2 | **27.3a** (§3.5) | `PerfilTagController`, `PerfilTagService` (método novo; o atual não muda), DTOs | Não | Sem período, chamada e resposta iguais às de hoje (teste); com período, só `[inicio, fim)`, nunca grava (só simula); gravar só com `simulacaoId` + `gravar = true` (§9.2), avisos e bloqueios calculados com a seção `TreinoPerfil`, `409` com id vencido (15 min) ou perfil atual mudado | Reverter o commit | M |
| D3 | **27.3b** (§3.4): migration aditiva, versão antes de gravar, voltar | modelo, migration, serviço, controller | Não | Gravar → versão criada na mesma transação; voltar → `PerfisIa` igual à versão (todas as colunas); voltar duas vezes → estado original; `PredictionContextLoader` intocada | `Down` da migration + reverter o commit; dados: backup Q3 | M |
| D4 | **27.3c**: tela Treino do perfil (§3.1) | IAFront | Não | Simular, prévia, avisos, gravar e voltar contra a API com fakes (ou a API local com o Bridge falso da tarefa 22) | Reverter o commit do IAFront | M |
| D5 | **API de regime**, só cadastro (§4.3) | controller e serviço novos; reaproveita o parser da RC-4 e o `IsForbidden` da RC-7 | **Não** (salva sempre inativo) | Validações; nada é ativado; criar com `Ativo = false` mesmo que o modelo tenha `true` como padrão | Reverter o commit | M |
| D6 | **Prévia de cobertura do regime** (`simular`, só leitura) | mesmo serviço; reaproveita o resolver da RC-6 e as consultas do provider | Não | Para a configuração alvo, cobertura por chave igual à do replay da RC-10/11 no mesmo período (tolerância de alinhamento) | Reverter o commit | M |
| D7 | **Tela Regime**: componentes em ordem (tag do catálogo + mapa), parâmetros, prévia, salvar inativo, ativar e desativar (protegidos, §4.3); "copiar para as tags da mesma bomba" | IAFront | Não (a ativação é a RC-12) | Cadastra a configuração alvo de §7.9 sem ativar | Reverter o commit; apagar as linhas inativas | M |
| D8 | **Ensaio geral autorizado** (Bridge e simulador): 27.5; avisos da tela iguais aos esperados no §3.2; regime cadastrado inativo e prévia conferida; **ensaio da RC-12** (decisão 6): ativar pela tela com a chave ligada, **com `-HistoricoCausal` e `-ReusarHistorico` (Development)**, rodar os presets 1 e 6 e aplicar o aceite da RC-12, **medindo o tempo de ciclo com as consultas do regime (§9.3)**; depois **desativar** até a demo | sessão | Sim, só durante o ensaio e com volta | Checklist; resultado da RC-12 registrado (passou ou não, com o motivo) | Voltar pela tela; backup Q3; regime com `Ativo = false` e chave em `false` | M |
| D9 | **Demo:** 27.6 ao vivo; regime configurado e simulado pela tela; **ativação só se a RC-12 tiver passado no ensaio da D8** (decisão 6) e com a aprovação explícita da sessão | sessão | Sim | Hora de cada gravação; `RegimeKey` no diagnóstico, se ativado | Voltar pela tela; chave do regime em `false`; `Ativo = false` | — |

**Pré-requisitos já no backlog que valem para a demo:** 34 (aceite do LiveMonitor), 35.2 (✅ feita), 25 (para a 27.7), 28 (escala da nota no LiveMonitor) e a mitigação do P34 (API só em loopback, pelo `Start-IAApiComLog.ps1`). **[17/11, §9.1]** A IAApi da demo sobe em **Development com `-HistoricoCausal -ReusarHistorico`** (o reuso só liga em Development; ~2,2 min por tag na 6.0b). **Nada da demo pode expor a API fora da máquina:** o IAFront escreve sem autenticação.

### 4.2 Fica para o produto

| # | O que | Observação |
|---|---|---|
| P1 | **Autenticação e política por cliente** (P34 definitiva) na API; login no IAFront no lugar do cliente fixo | Muda o contrato HTTP de todos os consumidores; o LiveMonitor continua só em loopback. Esforço G |
| P2 | **Histórico de treinos:** listar versões por tag (período, pontos, quem, quando, motivo), comparar duas quaisquer e restaurar qualquer uma | Tela sobre a `PerfisVersoesIa` do D3; "quem" depende do P1. Base para a F1 (referência por regime) |
| P3 | **Sugestão automática de associações:** componentes de regime e grupos candidatos | Reaproveitar `POST /api/diagnosticos-multivariados/correlacoes` e o `behavior-memory/discover` antes de criar algo; **sempre como sugestão**, nunca aplicada sozinha |
| P4 | Tags monitoradas pela tela: ativar/desativar com validação (existe no catálogo do Bridge; não é rótulo; aviso de tag sem perfil); `IaAtiva` explícito (P15); lista por cliente | Pequeno; pode entrar antes, se a demo sobrar tempo |
| P5 | Grupo multivariado pela tela (o CRUD já existe) | Ativar ou promover continua atrás das salvaguardas e de aprovação |
| P6 | Equipamento e processo: editar, excluir e tela de equipamento; listas por cliente | — |
| P7 | Lista da RC-7 e liga/desliga do regime combinado **por cliente, no banco**, mudando sem reinício | Hoje ficam no `appsettings`, global e só com reinício |
| P8 | Consolidação (§5) | — |

### 4.3 Proteções da API de regime (D5–D7)

- **Criar e editar nunca ativa:** linha nova com `Ativo = false`, explicitamente. Editar uma configuração **ativa** é recusado (`409`: "desative antes de editar").
- **Ativar** (`PATCH /api/regimes-operacionais/{id}/ativar`, §9.2; padrão já usado em `JanelasTreinamentoController`):
  - recusa (`409`) configuração com mais de 1 componente ou com mapa quando `RegimeCombinado:Habilitado = false` nesta instância. Evita aplicar o regime de 1 componente sem mapa (§2c);
  - o `GET` mostra o estado da chave, só para leitura;
  - ativar é a RC-12 e exige aprovação.
- **Validações:**
  - a tag analisada está monitorada;
  - componentes não vazios, distintos e diferentes da tag analisada;
  - nenhum componente na lista da RC-7;
  - componentes existentes no catálogo do Bridge (se o Bridge não responder, vira aviso, não bloqueio);
  - mapa válido pelo parser da RC-4;
  - `Ordem` contínua a partir de 1;
  - `LagMinutos` e tolerâncias ≥ 0; `MinimoAmostrasPorRegime` ≥ 1.
- **Gravação:** a linha principal e os componentes são gravados numa transação (substitui os componentes).
- **Simular:** usa a mesma janela e as mesmas consultas do provider (1 por componente + a tag): **4 consultas de 24 h por tag** de bomba. Uma tag por vez, por causa da carga no Bridge (P10, P42).

---

## 5. Coisas em paralelo e plano de remoção (regra do `CLAUDE.md`)

| Hoje | Com este plano | O que fazer com o antigo |
|---|---|---|
| "Cadastrar Tag" do IAFront (configura, treina 24 h e grava, analisa) | Configuração → Tags monitoradas (P4) + Treino do perfil (D4) | Depois da demo, "Cadastrar Tag" passa a só configurar e a abrir a tela de treino com simulação; o treino automático de 24 h sai. Até lá, fora do roteiro da demo |
| "Configurações da IA" (`ConfiguracoesIA`, que ninguém lê) | Área Configuração | Decidir (P8): ligar de verdade (exige plano próprio: mexe em limiares) ou remover os controles. Motivos e categorias continuam onde estão |
| `treinar-perfil` sem período (24 h, grava na hora) e `retreinar-perfil` (idêntico) | `treinar-perfil` com período e simulação | Manter os dois por compatibilidade. No produto, o caminho sem período também passa a gravar versão; `retreinar-perfil` vira alias documentado ou é removido depois de conferir os consumidores |
| `JanelasTreinamentoTagsIa` (IF) × período na requisição (perfil) | Período guardado na versão do perfil | Decidir no produto se o IF passa a usar o mesmo fluxo (simular → gravar → versão) e se as janelas viram só "períodos salvos" na tela |
| Rollback por script SQL com a API parada (27 Q3) | "Voltar" pela tela | O script continua como reserva até o P2 estar maduro |
| Cadastro do regime direto no banco (RC-12 original: "nenhum código") | API e tela de regime (D5–D7) | A RC-12 passa a cadastrar pela tela; não haverá script paralelo de cadastro |

---

## 6. Decisões da responsável

Itens 1 a 6 decididos em 17/11 (ver §8).

~~**Ainda pendentes:**~~ **[17/11] Todos resolvidos na segunda rodada (§9):**
- ~~aprovar os limites do §3.2~~ aprovados, configuráveis por cliente;
- ~~os nomes das rotas, da tabela de versões e da seção de configuração dos avisos~~ escolhidos no §9.2;
- ~~o tempo de vida da simulação (15 min?)~~ 15 min;
- ~~a interpretação do §3.3~~ confirmada;
- ~~os pré-requisitos da RC-12 no ensaio~~ 6.0b (feita) e `HistoricoCausal` ligado, no lugar da 6.1–6.3 (§9.1).

**Nada pendente de decisão para começar a D1–D3**, mas cada passo de implementação continua exigindo aprovação própria.

## 7. Limitações desta análise

- Nada foi executado: as rotas quebradas do IAFront, o efeito de uma tag ativa sem perfil e a checagem de cliente nos grupos são leitura de código ou ponto a conferir.
- O IAFront foi lido nos pontos de chamada à API, nas rotas, no contexto de cliente e nas telas Cadastrar Tag e Configurações da IA; as demais telas não foram revisadas.
- O esforço (P, M, G) é estimativa; o prazo até o fim do mês é apertado para D0–D9, e o corte previsto é D7 → D6.

## 8. Decisões registradas (2026-11-17, responsável)

| # | Decisão | Efeito no plano |
|---|---|---|
| 1 | Configuração no IAFront; o LiveMonitor continua só de leitura | §1, recomendação confirmada. O LiveMonitor não ganha escrita, nem para a demo |
| 2 | IAFront e LiveMonitor juntos, **com proxy no IAFront, sem mudar a API** | D1: proxy do Vite para `/api` e o IAFront em outra porta. O CORS (`Program.cs:10-17`) **não muda**. Aceite da D1: os dois abertos ao mesmo tempo |
| 3 | Gravar usa **exatamente o perfil simulado**, sem recalcular, com identificador da simulação | §3.3: `simulacaoId` obrigatório para gravar com período; `409` se o id venceu ou se o perfil atual mudou desde a simulação. Entra na 27.3a (D2) |
| 4 | Tabela de versões do perfil **na demo**, com backup antes da migration | §3.4 e 27.3b (D3) na demo. Backup no padrão da 35.1 e conferência no padrão da 35.2, antes da D8 |
| 5 | Limites dos avisos propostos a partir dos dados do CP300, **para aprovação** | §3.2: proposta calibrada em janelas limpas e com falha. ~~Não aprovada ainda~~ **Aprovada em 17/11 (§9)** |
| 6 | Regime na demo: configurar e simular pela tela; **ativar só se a RC-12 passar num ensaio antes da demo** | D8 ganha o ensaio da RC-12; D9 ativa só se ele passar e com a aprovação explícita da sessão |

**Pontos em aberto da decisão 6** (~~em aberto~~ **resolvidos em 17/11 pela segunda rodada, §9.1**; texto original mantido abaixo):
- O ensaio da RC-12 **é** a RC-12 executada no ambiente de teste do PK2: muda a detecção durante o ensaio e exige aprovação explícita no dia.
- **Pré-requisitos que o plano da RC-12 ainda lista** (`PLANO_REGIME_COMBINADO.md` §7.9):
  - tarefa **6** (cadência do ciclo);
  - tarefa **27** (aqui, a 27.6 feita antes do ensaio);
  - **35.2**;
  - a chave `HistoricoCausal` (tarefa **25**) ligada na mesma sessão.
- **A tarefa 6 não está feita** (6.1 a 6.5 não aprovadas). Sem ela, o diagnóstico vê cerca de 1 minuto em cada 10 (P26), e o aceite da RC-12 ("`RegimeKey` no diagnóstico") fica mais fraco.
- **Cabe à responsável escolher**, antes da D8, entre:
  - (a) manter a 6 como pré-requisito, e então a ativação provavelmente não entra na demo;
  - (b) dispensá-la só para o ensaio e a demo, registrando o motivo.
- Este plano não toma essa decisão. **[17/11] Decidido (§9.1):** variante de (b) — a 6.1–6.3 deixa de ser pré-requisito; o pré-requisito da tarefa 6 passa a ser a **6.0b**, já feita. Com o reuso, cada tag é avaliada a cada ~2,2 min (46 % dos minutos-tag), e não mais ~1 em cada 10 min como no P26.

## 9. Segunda rodada de decisões (2026-11-17, responsável)

*Registrado só em documentação. Nada implementado: cada passo D1–D9 continua com aprovação própria.*

### 9.1 Decisões

| # | Decisão | Efeito no plano |
|---|---|---|
| 7 | **Limites dos avisos do §3.2 aprovados**, configuráveis por cliente | §3.2 vira o padrão global da seção `TreinoPerfil` (§9.2). Os bloqueios (7 dias, 720 pontos) também ficam configuráveis; as checagens lógicas (`fim > inicio`, `fim <= agora`) não |
| 8 | **Nomes de rotas, tabela e configuração:** Claude escolhe, seguindo o padrão do projeto | §9.2 |
| 9 | **Validade da simulação: 15 min** | `TreinoPerfil:SimulacaoValidadeMinutos = 15` |
| 10 | **Leitura do §3.3 confirmada:** com período, gravar exige o `simulacaoId`; o `Gravar = true` que recalculava sai do desenho; sem período, o `treinar-perfil` fica como hoje | Contrato no §9.2 |
| 11 | **Script de calibração em `tools/`, com README** | ✅ Feito em 17/11: `tools/CalibracaoAvisosTreino/` (script sem mudança; saída conferida idêntica à da calibração) |
| 12 | **Tarefa 6 na demo:** fica o reuso do histórico como está (Development, `-ReusarHistorico`, ~2,2 min por tag na 6.0b). **6.1 a 6.3 pausadas** até investigar o custo fixo de ~4 s por requisição do Bridge | BACKLOG 6. A IAApi da demo e do ensaio (D8) sobe em Development com `-HistoricoCausal -ReusarHistorico` |
| 13 | **RC-12 deixa de depender da 6.1–6.3.** O pré-requisito da tarefa 6 passa a ser a **6.0b (feita)**, com o **`HistoricoCausal` ligado** | Resolve o ponto em aberto da decisão 6 (§8). Continuam: 27 (27.6 antes do ensaio), 35 (feita), RC-11 e aprovação no dia |

- **6.4 (✅ confirmado pela responsável em 17/11):** a 6.4 (janela incremental das tags de regime) estava marcada "antes da RC-12". Ela usa a mesma lógica da 6.1 e fica **pausada** junto com ela. **Não é mais pré-requisito** da RC-12.
- **`PLANO_REGIME_COMBINADO.md` §7.9:** ✅ atualizado em 17/11 com os novos pré-requisitos da RC-12 (§7.9.1).

### 9.2 Nomes escolhidos (padrão do projeto)

**Padrão observado [código]:**
- rotas `api/<nome-no-plural>` em português e kebab-case (`api/janelas-treinamento`, `api/grupos-analise`, `api/categorias-falha-ia`); ações como verbo (`treinar-perfil`, `configurar`, `{id:int}/ativar`, `{id:int}/desativar`);
- tabelas no plural com sufixo `Ia` (`TagsRegimesOperacionaisIa`, `JanelasTreinamentoTagsIa`, `TagsComportamentosSnapshotsIa`); entidades no singular com `Ia` (`TagPerfilIa`, `TagRegimeComponenteIa`);
- migrations com prefixo da tarefa (`RC5_AddTagRegimeComponente`, `MV08_4D1_AddMultivariateIndustrialEventReviews`);
- opções com `SectionName` e valores padrão na própria classe, sem mudar o `appsettings.json` (`HistoricoCausalOptions`, RC-4, RC-7); listas por cliente como dicionário `…PorCliente` (RC-7).

#### Treino do perfil (D2, D3; `PerfilTagController`, rota `api/tags`, já existente)

| Rota | Corpo | O que faz |
|---|---|---|
| `POST /api/tags/treinar-perfil` | `{ clienteId, tagName }` | **Igual a hoje** (últimas 24 h, grava na hora). `gravar` é ignorado |
| `POST /api/tags/treinar-perfil` | `{ clienteId, tagName, inicio, fim }` | **Simula; nunca grava.** Devolve `simulacaoId`, `validaAte`, perfil atual e candidato completos, série do período, avisos, informações e bloqueios |
| `POST /api/tags/treinar-perfil` | `{ clienteId, tagName, simulacaoId, gravar: true }` | Grava **o candidato guardado**, sem nova busca ao Bridge, e cria a versão (D3) na mesma transação. `409`: id vencido ou desconhecido, ou perfil atual diferente do que a simulação viu. `400`: id de outra tag ou cliente; `inicio`/`fim` junto com `simulacaoId`; `gravar = true` com período e sem id |
| `GET /api/tags/perfis/{clienteId}/{tagName}` | — | Perfil completo de uma tag (todas as colunas de `PerfisIa`) e o `carimbo` do perfil atual |
| `POST /api/tags/perfis/{clienteId}/{tagName}/restaurar-anterior` | `{ carimboAtual }` | "Voltar ao anterior": copia a versão anterior para `PerfisIa` e guarda a que estava em uso como nova versão (origem `restauracao`). `409` se o carimbo não bate; `404` sem versão anterior |
| *(produto, P2)* `GET /api/tags/perfis/{clienteId}/{tagName}/versoes` | — | Histórico de treinos. Nome reservado; fora da demo |

- **Carimbo:** hash das colunas do `PerfisIa` da tag, guardado com a simulação e devolvido no `GET`. Serve para os `409` sem acrescentar coluna ao `PerfisIa`, que a predição lê (lição do P43). [a conferir na implementação]
- **Rotas:** `api/tags/perfis/{c}/{t}` tem um segmento a mais que `api/tags/{clienteId}/{tagName}` (`TagConfigController`) e não deve colidir. Um teste de rota confirma. [a conferir na implementação]
- `retreinar-perfil` não ganha os campos novos (§5).

#### Tabela de versões (D3)

- **Tabela `PerfisVersoesIa`**, entidade `TagPerfilVersaoIa`, `DbSet<TagPerfilVersaoIa> PerfisVersoesIa`.
- **Colunas:** `Id`; cópia de **todas** as colunas de `PerfisIa` (inclusive `ClienteId`, `TagName` e `DataTreinamento`); `PeriodoInicio` e `PeriodoFim` (nulos no treino de 24 h); `Origem` (`gravacao` ou `restauracao`); `SimulacaoId` (nulo quando não houver); `DataVersao`.
- **Índice** `(ClienteId, TagName, DataVersao)`.
- **Migration** `<timestamp>_T37_AddTagPerfilVersao`, só aditiva.

#### Regime (D5–D7; controller e serviço novos)

- `RegimesOperacionaisController`, rota **`api/regimes-operacionais`** (mesmo nome da tabela `TagsRegimesOperacionaisIa`); serviço `RegimeOperacionalCadastroService` (padrão do `GrupoAnaliseCadastroService`).

| Rota | O que faz |
|---|---|
| `GET /api/regimes-operacionais/{clienteId}` | Configurações do cliente e o estado da chave `RegimeCombinado:Habilitado` nesta instância (só leitura) |
| `GET /api/regimes-operacionais/{clienteId}/{tagName}` | Configuração da tag, com os componentes |
| `POST /api/regimes-operacionais` | Cria **sempre com `Ativo = false`** |
| `PUT /api/regimes-operacionais/{id:int}` | Edita; `409` se ativa |
| `POST /api/regimes-operacionais/simular` | Prévia de cobertura (D6), com o mesmo corpo do `POST`, sem gravar |
| `PATCH /api/regimes-operacionais/{id:int}/ativar` | É a RC-12; recusas do §4.3 |
| `PATCH /api/regimes-operacionais/{id:int}/desativar` | Rollback |
| `DELETE /api/regimes-operacionais/{id:int}` | Só configuração inativa; `409` se ativa |

#### Configuração dos limites (D2)

- **Seção `TreinoPerfil`**, no nível de cima, **fora de `AnomalyDetection`**, para deixar claro que a detecção não a lê. Classe `TreinoPerfilOptions` (`SectionName = "TreinoPerfil"`).
- Padrões na classe; o `appsettings.json` **não muda**. Um cliente só aparece na configuração quando precisar de valor diferente.
- `TreinoPerfil:LimitesPorCliente:{clienteId}` sobrescreve **campo a campo** os de `TreinoPerfil:Limites`.
- Lida por `IOptions`: mudar um limite exige reinício, como a lista da RC-7. Mudar sem reinício fica com o P7.
- **Aceite extra:** um teste mostra que nenhum serviço da predição depende de `TreinoPerfilOptions`.

```json
"TreinoPerfil": {
  "SimulacaoValidadeMinutos": 15,
  "Limites": { "...": "padrão abaixo" },
  "LimitesPorCliente": { "PK2": { "PontosMinimos": 720 } }
}
```
*(exemplo de formato; nada será gravado no `appsettings.json`)*

| Campo | Padrão | Tipo | Dispara quando |
|---|---|---|---|
| `DuracaoMaximaDias` | 7 | bloqueio | duração > 7 dias |
| `PontosMinimos` | 720 | bloqueio | pontos < 720 |
| `DuracaoMinimaHoras` | 24 | aviso | duração < 24 h |
| `BuracoMaximoMinutos` | 15 | aviso | maior intervalo sem leitura ≥ 15 min |
| `CoberturaMinimaPercentual` | 95 | aviso | cobertura < 95 % |
| `ZerosMaximoPercentual` | 50 | aviso | zeros > 50 % |
| `ZerosInformativoPercentual` | 10 | informação | zeros entre 10 % e 50 % ("tag com estados") |
| `ValorCongeladoMinutos` | 15 | aviso | trecho constante não nulo ≥ 15 min |
| `OutliersMaximoPercentual` | 10 | aviso | IQR remove ≥ 10 % |
| `AnomaliasNoPeriodoMinimo` | 1 | aviso | ≥ 1 anomalia da tag registrada no período |
| `MudancaMediaDesvios` | 2 | aviso | \|Δmédia\| > 2 desvios do perfil atual |
| `RazaoDesvioMinima` / `RazaoDesvioMaxima` | 0,5 / 2 | aviso | razão desvio novo ÷ atual fora de [0,5; 2] |

### 9.3 Atenção para o ensaio da RC-12 (inferência; não muda a decisão 13)

- **[código]** Com o regime combinado ligado, o provider faz **3 consultas de histórico por tag de bomba**, síncronas e em sequência (`OperationalRegimeBaselineProvider.cs:335-341` e `:389-409`). Nas 6 tags de bomba do PK2, são **+18 requisições por ciclo**.
- **[inferência, não medido]** Com o custo fixo de ~4 s por requisição visto na 6.0b, isso somaria cerca de **+70 a +80 s** ao ciclo do PK2: de ~61 s para ~140 s. O intervalo por tag voltaria de ~2,2 para **~3,5 min**, o nível da 6.0.
- O aceite da RC-12 já mede o "tempo de ciclo com 3 consultas por tag de bomba". O ensaio da D8 deve registrar esse número e a idade da leitura (p95) antes de decidir a ativação na demo.
- A investigação do custo fixo do Bridge (decisão 12) passa a interessar também à RC-12.
- **[17/11] Preparação registrada: tarefa 38 do backlog.** As 6 tags de bomba usam as mesmas 3 tags de estado (TAG42, TAG43 e TAG46). Buscar cada uma **uma vez por ciclo e por cliente**, reaproveitando entre as tags, leva de 18 para 3 requisições. Ganho estimado: ciclo do PK2 com regime de ~140 s para ~75 s, ~2,4 min por tag [inferência]. Primeiro em modo `Comparar` e depois em `Usar`, dentro do ensaio da D8.
