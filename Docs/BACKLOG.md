# Backlog: detecção univariada

Atualizado na sessão 2 (2026-11-11), depois do cruzamento com `Docs/CENARIOS_CP300.md` (ver `AUDITORIA_STATUS.md` §5).

Ordem: primeiro medição e diagnóstico, depois correções. **Nenhuma tarefa está aprovada**: implementar só com autorização explícita (`CLAUDE.md`). As tarefas 1 a 6 não alteram a decisão de anomalia.

> **Atualização 2026-11-13 (IA real no preset 3; `PROBLEMAS_ENCONTRADOS.md` P26 e P27):** a tarefa **6 (latência) passou a ser a prioridade 1**. Entraram as tarefas de robustez para a demo **17 a 20**, nenhuma implementada. **Atualização 2026-11-13 (inventário do PK2, tarefa 2 concluída):** a nova tarefa **25 (P31, entrada inválida sistemática) passa a ser PRIORIDADE 1, antes de qualquer novo teste da IA real**. Em 12/11, 38 de 50 decisões não chegaram aos detectores; sem corrigir isso, um teste da IA real não mede detecção. A tarefa 6 continua necessária e vem logo depois. Entraram também a 26 (P30, escala da nota) e a 27 (P32, retreino do perfil). Depois, a 28 (P30 no LiveMonitor, para a demo; só planejada). Para os próximos testes foi aprovado só subir a IAApi com a saída gravada em arquivo com data e hora (tarefa 6, "Execução aprovada"); `dotnet-stack` ainda não.

> **Atualização 2026-11-16:** entrou a tarefa **29** (conferência, só leitura, dos 30 achados da auditoria B3.23). A **fase A (segurança) tem prioridade alta e vem antes de qualquer demo para fora da equipe**. Não altera a prioridade 1 (tarefa 25) para os testes internos. **Mais tarde em 16/11:** fase A da 29 concluída (P33 a P40). Pela tarefa **30**, foram corrigidos P33 e parte de P39, e P34 foi mitigado para a demo (sem commit). ~~Ficou pendente conferir e regenerar a referência `rc0_*` do Compare-Replay depois da exportação~~ **feito em 16/11: resultado idêntico; referência das `rc0_*` regenerada; 0 diferenças em 12 execuções** (tarefa 30, "Pendências"). **Ainda em 16/11:** pela tarefa **31**, foram corrigidos P35 a P38, sem commit. No P35, o `clienteId` é opcional, e sem ele a resposta é `400` só quando a tag existe em mais de um cliente (opção (a)). **Ainda em 16/11:** a tarefa 6 ganhou as subtarefas **6.0 a 6.5** (janela de histórico incremental e cadência de 1 min, ligadas às tarefas 22 e 25). O PERF_DIAG da 6.0 foi aprovado, e o `Start-IAApiComLog.ps1` ganhou `-PerfDiag` (sem commit). De 6.1 a 6.5, nada está aprovado. **Ainda em 16/11:** a tarefa **22** (P28) foi feita nos passos 1 e 2, sem commit. Os serviços de valor atual e de tags recebem o `HttpClient` pela DI, com `Bridge:BaseUrl`; os testes são offline, com `FakeBridgeHandler`; suíte com 1079/1079 e replay com 0 diferenças. Isso destrava os testes offline do laço (19, passo 2; 18; 6.3). Falta o aceite (e), com o Bridge.

**O que mudou nesta sessão**
- A antiga tarefa 1 (recuperar a tabela de cenários) foi **concluída**: `CENARIOS_CP300.md`. O que falta virou a nova tarefa 1 (exportar o histórico).
- O inventário do banco (antiga 5) **subiu para 2**: é a forma mais barata de confirmar três achados (rótulo como `TagRegime`, `TipoRepresentacao` e perfil colapsado em zero).
- O replay (antiga 2) passa a usar **dados reais do PSi4** em vez de séries sintéticas e precisa corrigir três diferenças do replay atual em relação ao PK2.
- Entram três correções candidatas (8, 9, 10), todas dependentes das evidências do replay.

---

### 1. Exportar o histórico dos testes CP300 (PSi4 → CSV)
- **Objetivo:** CSV com timestamp, tag e valor de TAG22–TAG51 (medições e estados) e TAG60–TAG81 (rótulos), para os intervalos dos presets 5, 3, 1, 2, 6 e 4, junto com a hora de início de cada preset, `HINI` e `SEED`.
- **Caminho recomendado:** `GET {Bridge}/historico?tagName=…&inicio=…&fim=…`, o mesmo usado por `BridgeHistoricoService.BuscarHistoricoPorPeriodo`. Ele entrega os valores **na mesma escala e com o mesmo timestamp que a IA recebe**. Executado pela equipe com autorização. Ver o plano na conversa da sessão 2.
- **Risco:** baixo (só leitura), mas é acesso a serviço industrial e exige autorização. Os dados são industriais e devem ser tratados como sensíveis.
- **Esforço:** P
- **Aceite:** um arquivo por preset; 1 linha/min/tag; TAG73 presente; fuso horário documentado.

### 2. Inventário do banco PK2 (somente leitura) — ✅ **CONCLUÍDA (2026-11-13)**
- **Resultado:** `Docs/INVENTARIO_PK2.md`, feito numa cópia autorizada do `ia_config.db` (com `-wal`), com a IAApi parada.
  - **Descartados:** rótulo ativo ou usado como `TagRegime` (só TAG22–33 ativas; nenhum regime cadastrado) e tipo explícito (`TagsContextoIa` vazia).
  - **Confirmados:** perfil colapsado (TAG28 e TAG32) e treino curto (30/10, ~3 h).
  - **Ausentes no PK2:** comportamento aprendido e modelo IF.
  - **Explicado** o "2,1 × 20–35": escala, entrada inválida e perfil. O replay com o perfil real reproduziu as 50 decisões.
  - Viraram os itens **25** (P31), **26** (P30) e **27** (P32).
- **Objetivo:** listar por tag: `IaAtiva`; `TagContextoIa.TipoRepresentacao`; `TagsRegimesOperacionaisIa` (`TagRegime`, ativo); `TagPerfilIa` (data do treino, média, desvio, deadband, total usado, outliers removidos, % zeros); existência de modelo IF.
- **Confirma ou descarta:** rótulo ativo ou usado como `TagRegime` (vazamento do gabarito); tipo explícito que bloqueie detectores; perfil colapsado (média 0 / desvio 0) nas tags de bomba; perfil treinado com ~2 h de dados.
- **Caminho:** GETs já existentes (`GET /api/tags`, contexto, perfis) ou cópia autorizada do `ia_config.db`.
- **Risco:** médio (acesso a banco/ambiente real; precisa de autorização). **Esforço:** P
- **Aceite:** tabela por tag com marcação "suspeito" e motivo.

### 3. Reprodução offline dos cenários CP300 com dados reais (antiga tarefa 2)
- **Objetivo:** rodar o pipeline de detecção sobre o CSV da tarefa 1 e gerar, por cenário e por tag, a nota de cada detector, o peso, a nota final, a classificação, o deadband e um resumo de acertos, faltas, alarmes falsos e atraso, usando TAG73 e os slots (TAG61–72) como gabarito.
- **Precisa corrigir em relação ao replay atual:** não forçar `TipoRepresentacao = "continuous"`; usar os pesos e limiares do `appsettings.json`; várias tags por execução; IF desligado por padrão (como no PK2), com opção de ligar.
- **Arquivos:** nova ferramenta em `tools/` e testes em `IAApi.Tests/`. Não toca no runtime.
- **Risco:** baixo (offline). Não reproduz a latência real (L1–L7 do mapa).
- **Esforço:** M
- **Aceite:** teste automatizado com CSV sintético pequeno passando; relatório gerado para pelo menos o preset 3 real.
- **Dividida em partes (aprovado em 2026-11-11):**
  - **Parte A, implementada em 2026-11-11.**
    - Exportação: `tools/Cp300Export/Export-Cp300Historico.ps1`. **Ainda não foi executado.**
    - Replay: `tools/Cp300OfflineReplay/`, com o treino "como em produção" (24 h antes de T) e o `PredictionService` real minuto a minuto, sobre SQLite em memória.
    - Pesos e limiares do `appsettings.json`, classificação real do tipo e IF desligado. Saída por leitura e detector, com coluna `conferencia`.
    - Testes: `IAApi.Tests/Cp300Replay/Cp300OfflineReplayTests.cs` cobre degrau, bomba reserva em 0 e valor congelado (7 testes; suíte completa com 790 aprovados).
    - Limitações em `tools/Cp300OfflineReplay/README.md`: sem regime/comportamento, multivariado, canário ou latência.
    - **Falta:** rodar a exportação real e o replay do preset 3.
  - **Parte B, pendente:** gabarito com TAG73 e slots, e resumo de acertos, faltas, alarmes falsos e atraso, por evento e minuto a minuto.
  - **Parte C, pendente:** modo de treino limpo (período escolhido, validando TAG73 = 0), para comparar com o treino "como em produção".

### 4. Relatório "por que não detectou" a partir do PERF_DIAG existente
- Sem mudança. Ferramenta offline que resume, por tag, os logs PERF_DIAG já capturados. **Esforço:** M. **Risco:** baixo.

### 5. Gravar o timestamp original da leitura no registro de detecção
- Sem mudança. Campo opcional `DataHoraLeitura` em `AnomaliaDetectada`, com migration. **Risco:** médio. **Esforço:** M.
- Enquanto não existir, o export das decisões reais (tarefa 16) usa a hora do processamento como `data_hora` (P18).

### 6. Medir a duração do ciclo e a cadência real por tag no ambiente de teste — **PRIORIDADE 2 (era 1; logo depois da 25 desde 2026-11-13)**
- **Subtarefas 6.0 a 6.5 (registradas em 2026-11-16):** avaliar cada tag a cada minuto sem buscar 24 h por tag a cada ciclo. Ficam no fim desta tarefa, ligadas às tarefas 22 e 25. A 6.0 (medição com PERF_DIAG) está aprovada; de 6.1 a 6.5, nada está aprovado.
- Sem mudança. `IAAPI_PERF_DIAGNOSTICS` numa sessão autorizada. **Risco:** médio. **Esforço:** P.
- **Bloqueia a RC-12** (decisão de 2026-11-16, `PLANO_REGIME_COMBINADO.md` §7.9): a ativação do regime combinado no PK2 só vem depois desta tarefa e da 27. Com o regime ligado, o provider faz 3 consultas de histórico por tag de bomba (18 por ciclo nas 6 tags, contra 6 hoje), e esse custo não foi medido no replay (RC-11, §7.7).
- **Por que subiu (P26, confirmado em log, IA real no preset 3, 12/11 14:21–15:09):**
  - Cada tag do PK2 foi avaliada só 3 a 5 vezes em ~48 min, cerca de 1 vez a cada ~10 min, contra 1 leitura por minuto do simulador.
  - O ciclo do PK2 se repetiu a cada 5 a 18 min. Com o Bridge livre, uma tag levou 2–10 s; sob carga, 60–110 s.
  - 9 consultas de histórico estouraram o timeout de 60 s e ficaram **sem decisão**. Às 15:13, a consulta em lote também estourou.
  - Depois disso, o monitor ficou ~11 h sem gravar nem logar nada (P27).
  - Sem esta medição, o placar da IA real mede uma amostra de ~1 em cada 10 minutos, não a detecção.
- **O que medir:** duração do ciclo; tempo por tag e por etapa (valor atual, histórico de 24 h, regime, multivariado em *shadow*, pós-decisão); idade da leitura na decisão; número de timeouts por ciclo.
- **Execução aprovada (2026-11-13), sem mudança de código:** subir a IAApi com a saída gravada em `dados/logs/iaapi_<data>.log`, com data e hora, sem depender da janela do console (P27, "Como confirmar", passos a, b, d). O PERF_DIAG continua precisando de aprovação própria; `dotnet-stack` ainda não foi aprovado.
- **Aceite:** tabela por ciclo (início, duração, tags avaliadas, timeouts) e por tag (tempo médio e máximo por etapa) num teste de pelo menos 2 h com o simulador rodando.
- **Observação (2026-11-11, exportação CP300 com o simulador rodando, relatado pela equipe):** o `GET /historico` do Bridge deu **timeout em muitos pedaços de 6 h** (timeout padrão do script na época: 60 s). Resultado: TAG24 e TAG27 com 357 de ~1552 leituras, TAG73 com 0. A IAApi consulta o mesmo endpoint a cada leitura (24 h, `BridgeHistoricoService`), então a lentidão do Bridge sob carga é candidata a fator de latência/falha na detecção [a validar]. Os tempos por pedaço das próximas exportações ficam em `<saída>.pedacos.csv`.

#### 6.x Avaliar cada tag a cada minuto sem buscar 24 h de histórico por tag a cada ciclo (registrado em 2026-11-16)

**Objetivo:** cada tag avaliada uma vez por minuto com a leitura recente. Para isso, guardar uma janela de 24 h por cliente e tag e completá-la só com o trecho novo do Bridge, sem mudar `IHistoricoService`, o `BridgeHistoricoService`, os detectores nem o contrato HTTP. **Ligações:** tarefa **25** (o recorte causal `HistoricoCausal` é pré-requisito de 6.0 a 6.3) e tarefa **22** (sem ela, o laço do monitor não é testável offline, o que bloqueia o teste offline da 6.3). Também: 20 (alerta de ciclo lento), RC-12 (`PLANO_REGIME_COMBINADO.md` §7.9) e P10, P11, P18, P26 e P31.

**Aprovado em 2026-11-16 pela responsável:**
- registro destas subtarefas;
- PERF_DIAG para a etapa **6.0**;
- parâmetro `-PerfDiag` no `tools/Start-IAApiComLog.ps1` (**feito**, ver 6.0);
- valores de projeto: **sobreposição de 10 min, recarga completa a cada 60 min, cadência de 1 min**;
- metas da tabela da 6.0, **a revisar depois da medição**.

**Não aprovado:** a implementação de 6.1 a 6.5. Cada uma exige aprovação própria.

**Onde está o tempo hoje** (números já registrados, sem medição nova):
- **Confirmado no código:**
  - o laço espera 1 min **depois** de cada ciclo (`MonitoramentoTagsService.cs:167`), então o intervalo é sempre maior que 1 min;
  - por tag, cada ciclo faz 1 `GET /historico?horas=24` na preparação e outro na tendência (`TendenciaValorService.cs:50`; o `PredictionHistoryReuse` só evita esse segundo em Development, com a variável);
  - com regime, mais 1 consulta por tag, ou 3 com o combinado;
  - o PK2 faz cerca de 24 consultas de 24 h por ciclo, síncronas, com timeout de 60 s.
- **Confirmado em log ou dados:**
  - pedaço de 2 h: 1–5 s com o Bridge livre e ~35 s sob carga (P10);
  - 2–10 s por tag com o Bridge livre e 60–110 s sob carga; ciclo do PK2 a cada 5–18 min;
  - 9 timeouts em 48 min (P26);
  - idade da leitura de 0,2–17,2 min (P18);
  - 38 de 50 decisões com entrada inválida; ≈ 2 % dos minutos-tag avaliados (P31).
- **Hipótese:** que a consulta de 24 h é a maior parte do tempo de cada tag. A 6.0 mede isso.

**Por que algo novo (regra do `CLAUDE.md`):**
- Nada no código guarda estado entre ciclos. O `PredictionRawHistorySnapshot` é, de propósito, local a uma predição (`Docs/prediction-history-reuse-experiment.md`; auditoria de concorrência).
- A 6.1 cria um **decorador** de `IHistoricoService`, paralelo e removível pela chave. O `BridgeHistoricoService` continua sendo a única fonte.
- **Plano de remoção:**
  - se a 6.2 for adotada e o Bridge ganhar uma consulta de histórico em lote ou incremental, o decorador é revisto ou removido;
  - se a 6.2 for rejeitada, o decorador sai, junto com a chave.
- **O que se reaproveita, sem duplicar:**
  - o lote de `/valores-atuais` (leitura recente);
  - `BuscarHistoricoPorPeriodo` (trecho novo, com o mesmo filtro, deduplicação e ordem);
  - `HistoricoCausal` (único ponto de recorte na hora da leitura);
  - `PredictionHistoryReuse` (tira a segunda consulta, a da tendência);
  - PERF_DIAG (`HistoricalRequests`, `Preparation.History`, `Monitoring.Reading.ReadingAgeAtStartMs`);
  - `HistoricoCausalOptions` (modelo da classe de opções).
- **Descartados:**
  - reduzir `Bridge:HorasHistorico` (muda MAD, EWMA e o regime, logo a decisão);
  - paralelizar o `foreach` (auditoria de concorrência, R01);
  - endpoint de histórico em lote no Bridge (exige mudar o Bridge; fica como evolução da 6.5).

##### 6.0 Medir sem mudar nada (etapa 0) — **aprovada (PERF_DIAG); aguardando sessão**
- **Como subir:** `.\tools\Start-IAApiComLog.ps1 -HistoricoCausal -PerfDiag`, com o simulador rodando, em Development, com as mesmas tags (PK2 12 e PK7 1), por pelo menos 2 h. O `HistoricoCausal` fica ligado porque, sem ele, o P31 confunde a medida.
- **Feito em 16/11 (sem commit):** parâmetro `-PerfDiag` no `tools/Start-IAApiComLog.ps1`.
  - Define `IAAPI_PERF_DIAGNOSTICS=1` só com o switch; sem ele, define `0`.
  - Recusa subir com `-PerfDiag` fora de `Development`, antes de criar qualquer arquivo.
  - Grava `PerfDiag` no `config.json`.
  - README em `tools/README.md`.
  - **Validação:** sintaxe sem erros; `tools/Start-IAApiComLog.Tests.ps1` **25 de 25**; `-PerfDiag -Ambiente Production` recusado sem criar arquivos.
  - **Rollback:** reverter o script e o README.
- **Medir:**
  - duração do ciclo;
  - tempo por tag e por etapa (`Preparation.History`, `Bridge.Transport`, tendência, regime, multivariado em *shadow*);
  - `HistoricalRequests` por predição;
  - idade da leitura (`ReadingAgeAtStartMs`);
  - timeouts;
  - nota 0 e entradas inválidas;
  - avaliações por tag por hora (`Export-AnomaliasIaReal.ps1`).
- **Metas aprovadas (a revisar depois desta medição), válidas para 6.2 e 6.3:**

  | Métrica | Fonte | Meta |
  |---|---|---|
  | Avaliações por tag por hora; minutos-tag avaliados ÷ possíveis | `Export-AnomaliasIaReal.ps1` | ≈ 60/h; ≥ 90 % |
  | Duração do ciclo e tiques pulados | log "Predições cliente …" e Warning da 6.3 | ≤ 60 s |
  | Idade da leitura (p50/p95/máx) | `Monitoring.Reading.ReadingAgeAtStartMs` | p95 < 2 min |
  | Tempo do histórico e consultas por predição | `Preparation.History`, `Bridge.Transport`, `HistoricalRequests` | queda medida |
  | Timeouts por hora | Event Viewer e log | 0 |
  | Nota 0 e entrada inválida | export e "HISTÓRICO RECORTADO" | ≈ 0 |
  | Diferença entre janela incremental e busca completa (6.1) | modo Comparar | só na borda da janela |

- **Aceite:** a tabela por ciclo e por tag da tarefa 6, mais a divisão do tempo entre as etapas. Esse resultado confirma ou descarta a hipótese de que o histórico de 24 h domina o tempo.
- **Risco:** médio (sessão com o simulador e o Bridge). A decisão não muda.

##### 6.1 Janela incremental em modo Comparar — **não aprovada**
- **Decorador de `IHistoricoService`** (novo arquivo em `Services/`), registrado em `BridgeServiceCollectionExtensions.cs` só com a chave. Sem a chave, a DI fica idêntica à atual.
- **Chave:** `Bridge:HistoricoIncremental:Modo = Desligado | Comparar | Usar`. Padrão `Desligado`; a chave fica fora do `appsettings`.
- **Valores aprovados:**
  - `SobreposicaoMinutos = 10`;
  - `RecargaCompletaMinutos = 60`;
  - intervalo mínimo entre trechos novos da mesma chave, para que a chamada da tendência não dispare outro trecho (valor a definir, sugestão de 30 s).
- **Funcionamento:**
  - primeira chamada de uma tag: as 24 h completas, como hoje;
  - chamadas seguintes: `BuscarHistoricoPorPeriodo` a partir do último ponto menos a sobreposição;
  - junção: um ponto por `DataHora`, o mais recente vence, ordem por data; descarta o que passou da janela;
  - retorno: sempre uma cópia;
  - recarga completa: periódica, quando o trecho falha ou quando aparece um buraco.
- **Escopo:** só o `BuscarHistorico` chamado pelo laço do monitor, marcado com `AsyncLocal` (o mesmo padrão do `PerformanceDiagnostics`). Treino, controllers e `POST /api/anomalias/tag` vão direto ao Bridge. Por ser registrado como o próprio `IHistoricoService`, o decorador preserva a identidade da fonte que o `PredictionHistoryReuse` exige.
- **Modo Comparar:**
  - devolve a busca completa, como hoje, e monta a janela incremental ao lado;
  - registra as diferenças (pontos a mais, a menos e com valor diferente; borda);
  - a decisão não muda; o Bridge recebe uma consulta pequena a mais por tag.
- **Isolamento por cliente:**
  - chave do cache = (cliente, tag, horas, URL do Bridge);
  - nada compartilhado entre clientes, mesmo com nome de tag igual: o `/historico` do Bridge não recebe cliente;
  - logs sempre com o cliente.
- **Concorrência:** o decorador é singleton, com trava por chave.
- **Memória:** estimativa de ~60 KB por tag, menos de 1 MB para 13 tags. Teto de entradas e descarte das tags inativas.
- **A confirmar:**
  - se o `/historico` devolve 1 ponto por minuto ou só as mudanças de valor;
  - trecho vazio: hoje o `BuscarHistoricoPorPeriodo` lança exceção; o decorador deve tratar como "sem novidade" sem engolir erro HTTP;
  - fim do trecho calculado pelo último ponto mais uma margem, sem supor fuso.
- **Testes offline** (com os dublês existentes `RecordingHistory`, `StubHistory` e `FixedHistory`):
  - primeira chamada completa e chamadas seguintes com trecho novo; junção, sobreposição com correção, corte da janela;
  - trecho vazio, falha com recarga completa, buraco;
  - mesma tag em dois clientes; duas chamadas ao mesmo tempo;
  - chave desligada: mesmo número de chamadas que hoje; fora do monitor: repassa direto ao Bridge;
  - **equivalência com fonte em movimento** (relógio simulado, 1 ponto por minuto): diferença zero ou só na borda;
  - replay: opção `--historico-incremental`; `Compare-Replay.ps1` com **0 diferenças em 12 execuções**;
  - regressão: `PredictionHistoryReuseTests`, `PerformanceDiagnosticsTests`, `PredictionServiceObservabilityEquivalenceTests`, `HistoricoCausalTests` e a suíte completa.
- **Aceite na sessão:** só diferenças na borda; consulta pequena medida.
- **Rollback:** `Modo = Desligado` (ou sem a chave) e reiniciar.
- **Risco:** baixo (a decisão não muda). **Esforço:** M.

##### 6.2 Janela incremental em modo Usar, com o `PredictionHistoryReuse` — **não aprovada; depende de 6.0 e 6.1**
- **O que muda:** `Modo = Usar`. Na sessão, o `IAAPI_EXPERIMENT_REUSE_PREDICTION_HISTORY=1` vem do ambiente; o reuso não está no `-PerfDiag`, e levá-lo para configuração fora de Development exige aprovação própria.
- **Impacto na detecção:** o que chega aos detectores pode diferir na borda da janela e nas correções tardias fora da sobreposição (até a recarga de 60 min).
- **Sessão:** alternar desligado → ligado → desligado, 1 h cada, com `HistoricoCausal` e PERF_DIAG ligados nas três fases. Metas da tabela da 6.0.
- **Rollback:** a chave.
- **Risco:** médio.

##### 6.3 Cadência fixa de 1 minuto no laço — **não aprovada; o teste offline dependia da tarefa 22 (feita em 16/11, passos 1 e 2)**
- **O que muda:** `PeriodicTimer` de 1 min (valor aprovado) no lugar do `Task.Delay` depois do ciclo. Um ciclo que passa do minuto **pula o tique**, sem acumular, e registra Warning (junta com a tarefa 20).
- **Chave:** `Monitoramento:CadenciaFixa` (padrão `false`).
- **Arquivos:** `MonitoramentoTagsService.cs`.
- **Teste offline:** Bridge falso lento, para forçar o tique pulado. Isso só é possível depois da tarefa 22 (P28). **16/11:** a 22 foi feita. O `FakeBridgeHandler` entra pela DI (ver tarefa 22, "Destrava").
- **Rollback:** a chave.
- **Risco:** médio. Muda a cadência, não a lógica da decisão.

##### 6.4 Janela incremental para o histórico das tags de regime — **não aprovada; antes da RC-12**
- **O que muda:** a mesma lógica da 6.1 para os `BuscarHistoricoPorPeriodo` do `OperationalRegimeBaselineProvider`, chaveada por (cliente, tag de regime). Começa em modo Comparar.
- **Por que antes da RC-12:** com o regime combinado ligado, seriam 18 consultas por ciclo só nas tags de bomba.
- **Chave:** `Bridge:HistoricoIncremental:IncluirRegime`.
- **Aceite:** replay com o regime combinado com 0 diferenças.

##### 6.5 (Futuro) Completar a janela com a leitura do lote — **não aprovada**
- **O que muda:** acrescentar à janela o valor do `/valores-atuais` de cada ciclo, reduzindo ou eliminando a consulta por tag.
- **Condições:** só vale se o ciclo ficar em 1 min (6.3) **e** se o valor do lote for igual ao do histórico no mesmo minuto. Isso precisa ser medido em modo Comparar.
- **Alternativa:** endpoint de histórico incremental em lote no Bridge (fora deste repositório).
- **Chave:** própria.

### 7. (Correção) Ajustar a agregação do ensemble ou os pesos
- Sem mudança, mas agora depende da tarefa **3**. Lembrete: sem IF, Z+MAD+EWMA saturados já chegam exatamente a 60. **Risco:** alto. **Esforço:** G.
- **[2026-11-16]** Para congelamento e ruído, a tarefa **33** propõe evitar a diluição **sem mexer nos pesos**. Os dois detectores ficam como evidência independente, fora da média, combinada por "OU". Um detector a mais na média nunca chegaria a 60 sozinho (máx. 57,1 com peso 1) e diluiria as detecções atuais.

### 8. (Correção, nova) Treino do perfil por período
- **Objetivo:** permitir que `POST /api/tags/treinar-perfil` aceite `inicio`/`fim` opcionais ou use as `JanelasTreinamentoTagsIa`, como o IF já faz (`DadosTreinamentoTagService`). Assim dá para treinar com o preset 5 (validando TAG73 = 0).
- **Arquivos:** `Services/PerfilTagService.cs`, `Controllers/PerfilTagController.cs` (campo opcional, contrato compatível).
- **Risco:** médio. Não muda a lógica de decisão, mas o baseline resultante muda a detecção. Treinar ou promover exige aprovação.
- **Esforço:** P
- **Aceite:** sem parâmetros, comportamento idêntico ao atual (teste); com período, usa só aquele intervalo.

### 9. (Correção, nova) Tags de bomba reserva e perfil colapsado em zero
- **Opções a avaliar no replay (tarefa 3) antes de escolher:** (a) usar o **regime operacional já existente**, com a tag de estado da bomba (ex.: TAG44, que é medição de estado, **não** rótulo) como `TagRegime`: só configuração; (b) proteger o cálculo IQR para séries com maioria de zeros.
- **Risco:** médio a alto (altera baseline e decisão). **Esforço:** P (a) / M (b).
- **Aceite:** no replay do preset 5, alarmes falsos no rodízio e na partida da auxiliar reduzidos, sem perder as falhas 26/27.

### 10. (Correção, nova) Detecção de valor congelado e falha de comunicação
- **Objetivo:** hoje nenhum detector reage a valor parado (falhas 20, 30, 32, 33). Avaliar um detector de "sem variação além do normal" e/ou o uso do heartbeat (TAG40).
- **Risco:** alto (novo detector muda a decisão e o volume de alarmes). **Esforço:** M–G.
- **Aceite:** definido depois da tarefa 3.
- **[2026-11-16] Proposta detalhada na tarefa 33** (33.C valor congelado; 33.R ruído excessivo), com reaproveitamento do `FrozenSensorEvidenceCalculator` existente. Ele hoje só serve ao canário PK7 e não dispararia na TAG24, que o resolver classifica como `Unknown`. O heartbeat (TAG40) e o "sem dados" (tarefa 18) ficam fora da 33.

---

*Itens 11–13 acrescentados em 2026-11-12, depois do teste do baseline por regime no replay (`RESULTADO_PRESET1.md` §4). O replay ganhou a opção `--regime TAGx=TAGy`, que já recusa TAG60–TAG81 (só na ferramenta).*

### 11. (Proteção) Bloquear no código rótulos TAG60–TAG81 como tag de regime
- **Problema:** `OperationalRegimeBaselineProvider.Resolve` aceita qualquer `TagRegime` cadastrada. Um rótulo do simulador (por exemplo, TAG73) escolheria o baseline da medição pelo gabarito, vazando a resposta para a decisão (`AUDITORIA_STATUS.md` §5a). Hoje a proteção é só de procedimento.
- **Proposta:**
  - recusar no provedor (resultado "não aplicado", com motivo claro) toda `TagRegime` que seja rótulo, com a mesma regra do `Cp300Replay.IsLabelTag`;
  - de preferência, validar também no cadastro, se ele vier a existir.
  - A regra precisa ser **por cliente**: TAG60–TAG81 só são rótulos no CP300/PK2. Ela pode ser configurável (lista de tags proibidas por cliente) para não bloquear tags legítimas de outros clientes.
- **Arquivos:** `Services/AnomalyDetection/OperationalRegimeBaselineProvider.cs` e a configuração por cliente; testes em `IAApi.Tests/Services/AnomalyDetection/OperationalRegimeBaselineProviderTests.cs`.
- **Risco:** baixo. Só impede uma configuração inválida; se não houver rótulo cadastrado, o comportamento não muda.
- **Esforço:** P
- **Aceite:**
  - com `TagRegime = TAG73` no PK2, o provedor não aplica e registra o motivo;
  - com uma tag de estado válida, o resultado é idêntico ao atual.

### 12. (Correção) Regime combinado para as bombas (B-01 sozinha × em paralelo × set-point)
- **Problema (confirmado no replay, §4):** uma só tag de regime não separa os casos.
  - Com TAG42, "B-01 operando" mistura a bomba sozinha (TAG27 ~240) e em paralelo com a B-02 (~179). O IQR dentro do regime corta a operação sozinha.
  - Resultado: TAG27/29/31 alarmam **toda vez que a B-02 desliga** (52–56 minutos por tag no preset 1, contra 3–15 sem regime).
  - Na madrugada sobram alarmes com a bomba **sozinha e com o set-point noturno** (TAG31: 60 min; TAG32: 10 min logo após o rodízio).
  - O índice único (`ClienteId`, `TagName`) só permite **uma** `TagRegime` por tag.
- **Opções a avaliar no replay antes de escolher:**
  - (a) aceitar duas ou mais tags de regime, com o estado definido pela combinação dos valores;
  - (b) usar como tag de regime uma tag que já combine os estados (TAG41, estado da estação, separa "1 bomba" de "2 bombas", mas não diz qual bomba nem o set-point);
  - (c) uma tag derivada calculada pela IAApi.
- **Arquivos:** `Models/TagRegimeOperationalIa.cs` (migração), `OperationalRegimeHistoryResolver`, `OperationalRegimeBaselineProvider`, testes e a opção `--regime` do replay.
- **Risco:** médio a alto. Muda o esquema e o baseline; com mais combinações, cada estado tem menos amostras e cai mais vezes no perfil global.
- **Esforço:** M
- **Aceite:** no replay do preset 1 e da madrugada, TAG27/29/31 sem alarmes na operação sozinha normal, mantendo os ganhos da B-02, da TAG24 e da TAG26. Rodar também nos presets 3, 5 e 6.
- **Observação:** mesmo com o regime certo, a obstrução do preset 1 ficou dentro do normal da B-01 sozinha na estimativa (`RESULTADO_PRESET1.md` §3.3). Este item reduz os alarmes falsos, mas **não promete** detectar essa falha.
- **Atualização (2026-11-12):** a etapa 0 (`PLANO_REGIME_COMBINADO.md` §5) escolheu a opção (a) com 3 componentes (própria bomba, outra bomba e TAG46). A implementação foi quebrada nas tarefas **RC-0 a RC-12**, no fim deste backlog.

### 13. (Análise) Erro de controle TAG24 − TAG46 como alternativa ao regime da pressão
- **Motivação:** TAG24 (pressão de recalque) e TAG46 (set-point em uso) têm a mesma unidade (bar × 100). A diferença entre as duas fica centrada em ~0 em qualquer set-point, sem transições nem formação de regime.
  - O regime por TAG46 eliminou os 300 alarmes noturnos (§4).
  - Mas, sem uma noite anterior nas 24 h, cai no perfil global nos primeiros 30 min; o mesmo acontece com qualquer set-point novo.
- **Primeiro passo (offline, sem mudar o runtime):** calcular a série TAG24 − TAG46 no CSV e verificar:
  - a distribuição de dia e de noite;
  - o comportamento nas trocas de set-point;
  - se as falhas que afetam a pressão (presets 3, 5 e 6) ficam mais visíveis do que na TAG24 pura.
- **Se valer a pena:** decidir entre uma tag derivada no Bridge/CLP ou um suporte a "tag de referência" na IAApi. Esta segunda opção é mudança de código e exige aprovação.
- **Risco:** baixo na análise; médio a alto se virar mudança de runtime.
- **Esforço:** P (análise) / M (implementação)
- **Aceite da análise:** tabela com média, desvio e alarmes estimados para TAG24 pura, TAG24 com regime TAG46 e TAG24 − TAG46, na madrugada e no preset 1.

---

*Itens 14 e 15 acrescentados em 2026-11-12, como itens futuros saídos da RC-1 (`PLANO_REGIME_COMBINADO.md` §5.6.1 e §5.6.2). Não bloqueiam o regime combinado.*

### 14. (Futuro, correção) Sensibilidade dos detectores dentro do regime (antiga opção (b) da RC-1)
- **Problema (confirmado no replay e no código, §5.6.1):**
  - Com o baseline do regime, a TAG29 no preset 6 confirma às 15:37 em vez de 15:34.
  - A Persistência só conta se o valor atual estiver "fora" pelo mesmo critério do MAD (z modificado ≥ 3,5 sobre mediana e MAD do histórico). Com o regime, esse limite sobe de 232 para 285–293, e ela fica em 0 até 15:39.
  - Há também uma diferença de fontes de escala: Z-Score e EWMA usam média e desvio **depois** da limpeza IQR; Mediana e MAD e Persistência usam o histórico do regime **antes** dela (escala 27 × 35).
- **A avaliar no replay, antes de propor mudança:**
  - critério próprio para a Persistência (por exemplo, "fora" relativo ao deadband ou a um limiar menor que o do MAD);
  - mesma fonte de escala para todos os detectores;
  - relação com o item 7 (agregação e pesos do ensemble).
- **Risco:** alto. Muda a decisão de anomalia e pode aumentar alarmes falsos. Mudar limiar, peso ou gate exige aprovação.
- **Esforço:** M–G
- **Aceite (a detalhar):** TAG29 confirmada até 15:34 no preset 6 com o regime combinado, **sem** perder os ganhos de §5.3 (alarmes totais, falsos fora da falha e failover).

### 15. (Futuro, análise) Heterogeneidade do regime "B-01 sozinha"
- **Observação [hipótese] (§5.6.1):** no regime 1 (B-01 sozinha), a TAG29 tem mediana ~197 em 11/11 12:55–14:25 e ~152 no resto das 24 h. Pode faltar uma dimensão no regime (demanda, posição de válvula ou outra variável de processo).
- **Primeiro passo (offline, sem mudar o runtime):** no CSV, cruzar a TAG29 dentro do regime 1 com as variáveis de processo candidatas (exceto TAG60–TAG81) e ver se alguma explica os dois patamares. Repetir no CSV de 48 h da RC-0.
- **Se explicar:** avaliar um 4º componente na Opção A (o desenho já suporta N componentes), com teste no replay como na etapa 0.
- **Risco:** baixo na análise; médio se virar configuração nova.
- **Esforço:** P (análise)
- **Aceite da análise:** tabela de mediana e MAD por patamar e pela variável candidata, com a conclusão marcada como confirmada ou descartada.

### 16. (Medição) Placar das decisões reais da IAApi local
- **Implementado em 2026-11-12, ainda não executado:** `tools/Cp300Export/Export-AnomaliasIaReal.ps1`. Lê `GET /api/anomalias` (o mesmo endpoint do LiveMonitor), filtra por cliente (padrão PK2) e período (`-Inicio`/`-Fim`), e grava em `dados/` um CSV que o `placar` lê (`data_hora`, `tag`, `decisao_final`, `valor`, `nota_final`, `erro` vazio, `data_processamento`, `base_data_hora`, `id`, `tipo_tag`, `criticidade`, `classificacao_risco`). Só leitura; por padrão recusa URL fora do loopback.
- **Limitação (P18):** `data_hora` é o minuto do **processamento** (`DataDeteccao = DateTime.Now`), não o da leitura; a hora original fica em `data_processamento`. O atraso do placar inclui a latência. `-DefasagemMinutos` só com defasagem medida (tarefa 6); a correção de fundo é a tarefa 5.
- **Validação feita:** sintaxe (parser do Windows PowerShell 5.1, 0 erros). **2026-11-13:** corrigido um erro no 5.1 (`Contains` em `Dictionary[string,object]` → `ContainsKey`) e executado contra a IAApi local para o preset 3 (12/11 14:00 → 13/11 02:05): 2.911 registros recebidos, 50 do PK2 no período (14:21–15:09), 0 anomalias. O resultado expôs P26 e P27.
- **Próximo passo (com autorização, IAApi local ligada a um teste do simulador):** exportar um preset e rodar o placar; comparar com o replay do mesmo preset. Só faz sentido depois da tarefa 6 e das tarefas 17 a 20: com a cadência atual, o placar mede ~1 minuto em cada 10.
- **Risco:** baixo (GET local). O endpoint devolve todos os clientes sem paginação; o filtro é feito no script antes de gravar. **Esforço:** P

---

*Itens 17–20 acrescentados em 2026-11-13: robustez do monitor para a demo, saídos de P26 e P27 (`PROBLEMAS_ENCONTRADOS.md`). Nenhum está aprovado. **Regra comum:** nenhum deles pode mudar a decisão de anomalia, os pesos, os limiares ou os contratos HTTP existentes; endpoints e campos novos só aditivos. Ordem sugerida: 19 → 20 → 18 → 17. O 19 e o 20 tornam visível um monitor parado ou lento; o 17 só ataca uma das hipóteses do P27.*

### 17. (Robustez) Trocar `Console.WriteLine` por `ILogger` no caminho do monitor
- **Problema (P27, hipótese 1):** `BridgeHistoricoService` escreve no console de forma síncrona, na thread do monitor (21 chamadas; bloco de 8 linhas por consulta). Essas linhas não têm data e hora, não têm nível e não podem ser filtradas. Se a janela do console parar de consumir a saída, a thread trava sem exceção.
- **Achado no levantamento:** não é só esse arquivo. No caminho de cada leitura há ~140 `Console.WriteLine` (`PredictionDetectionPreparationService` 25, `PredictionFinalizationService` 14, `EnsembleDetectionService` 10, `PredictionMultivariateDecisionService` 7, `IsolationForestDetector` 3, além de `HistoricoLimpezaService`, `PerfilTagService` e outros). Trocar só o `BridgeHistoricoService` **não elimina** o risco.
- **Limite importante:** o `ILogger` de console também espera quando a fila enche. A proteção real contra janela bloqueada é rodar com a saída em arquivo (execução aprovada na tarefa 6). Esta tarefa dá data e hora, nível e filtro, e reduz o volume de saída.
- **Proposta:**
  - **Etapa 1:** `BridgeHistoricoService` com `ILogger<BridgeHistoricoService>` injetado. O bloco "BUSCANDO HISTÓRICO" vira uma linha `Debug` com cliente, tag e horas. "Histórico recebido/convertido" vira `Debug`. "AVISO: prefixo" vira `Warning`.
  - **Etapa 2 (opcional):** os demais serviços do caminho da leitura, um por vez.
- **Arquivos:** `Services/BridgeHistoricoService.cs` (construtor, DI em `Services/DependencyInjection/IAApiApplicationServiceCollectionExtensions.cs`); testes que instanciam o serviço.
- **Risco:** baixo. Não toca na decisão. Mudar a assinatura do construtor exige ajustar os testes e ferramentas que criam o serviço.
- **Esforço:** P (etapa 1) / M (etapa 2)
- **Aceite:** replay (RC-2) com 0 diferenças nas decisões; nenhum `Console.Write` em `BridgeHistoricoService`; com nível `Information`, o log por consulta de histórico cai de 8 linhas para 0.

### 18. (Robustez) Leitura com timeout ou falha deve virar registro visível ("sem dados")
- **Problema (P26):** quando o Bridge estoura o timeout (por tag ou no lote), a leitura **some**. Não há decisão gravada, o LiveMonitor não mostra nada e o placar não sabe que o minuto não foi avaliado. Em 48 min do preset 3, 9 tentativas sumiram assim, e o lote das 15:13 levou as 12 tags juntas. Hoje a falha só aparece como Warning no Event Viewer.
- **Proposta:**
  - Gravar um registro de **monitoramento** (não de decisão) por tag que falhou, com cliente, tag, hora, tipo (`TimeoutBridgeHistorico`, `TimeoutBridgeLote`, `ValorNaoEncontrado`, `Erro`), duração e mensagem curta.
  - **Não** gravar em `AnomaliaDetectada` como `EhAnomalia = false`, porque contaria como "sem anomalia" no placar, no dashboard e no feedback. O destino preferido é uma tabela nova (migration) ou um buffer em memória exposto pela tarefa 19. A decisão entre os dois fica para a aprovação.
  - O LiveMonitor mostra a lacuna como "sem dados" na linha do tempo da tag.
  - Export e placar (tarefa 16) passam a contar minutos "sem dados" separados de "sem anomalia".
- **Arquivos:** `Services/MonitoramentoTagsService.cs` (blocos `catch` por lote e por tag); nova entidade e migration, ou serviço em memória; endpoint aditivo; `tools/IAApi.LiveMonitor/` (proxy e tela); `tools/Cp300Export/Export-AnomaliasIaReal.ps1`.
- **Risco:** médio. Com tabela nova: migration e volume, porque com o Bridge fora são 13 registros por ciclo. Precisa de retenção ou agregação. Não muda a decisão.
- **Esforço:** M
- **Aceite:** com o Bridge simulado fora em teste automatizado, cada tag do lote gera um registro "sem dados" e nenhum registro em `AnomaliaDetectada`; o LiveMonitor exibe a lacuna; `GET /api/anomalias` fica idêntico.

### 19. (Robustez) "Sinal de vida" do monitor: endpoint e LiveMonitor
- **Problema (P27):** o monitor ficou ~11 h parado sem nenhum sinal, com a API respondendo. Hoje não existe forma de perguntar à IAApi se o monitor está rodando nem quando foi o último ciclo.
- **Situação:** proposta detalhada em 2026-11-13 (abaixo). **Não aprovada; nada implementado.** Cada passo exige aprovação própria.

#### 19.1 O que já existe (levantamento de 2026-11-13, regra "reaproveitar antes de criar")

Tudo nesta tabela foi **confirmado no código**. Os efeitos em teste real estão marcados como inferência.

| Mecanismo | O que já entrega | Por que não carrega o sinal de vida sozinho | O que se reaproveita |
|---|---|---|---|
| LiveMonitor, painel "Saúde do monitoramento agora" (`src/App.tsx`, card "2 · Análise operacional mais recente"), alimentado por `GET /api/anomalias` | Hora da última decisão das tags selecionadas. Fica vermelho acima de `staleMs` (padrão de 180 s, `MONITOR_STALE_MS`). | Não separa monitor parado, ciclo lento, timeout do Bridge e tags sem `IaAtiva`. Não mostra a etapa. Não vê leituras que falharam (P26: só aparecem no Event Viewer). Inferência: teria ficado vermelho no P27, mas só com a tela aberta e sem dizer o motivo. | **O lugar na tela:** o indicador novo entra nesse painel. Não se cria tela nova. O card 2 continua como plano B. |
| `GET /api/anomaly-runtime/metrics` (`RuntimeDecisionMetricsController`; `AnomalyRuntimeMetrics:Enabled = true` no `appsettings.json`) | `TotalDecisions` e `LastDecisionAtUtc` em memória. Atualizados a cada decisão em `PredictionRuntimeDecisionService.Resolve`. | É global: soma todos os clientes e também as chamadas manuais a `POST /api/anomalias/tag`. Marca a hora da resolução, antes de gravar. Não tem ciclo, etapa nem falhas. Liga-se a uma chave do rollout multivariado. | **Já serve hoje, sem código** (passo 0). É também o **modelo de implementação**: singleton com `Interlocked` e um `record` de snapshot. |
| PERF_DIAG (`Services/Diagnostics/PerformanceDiagnostics.cs`) | Tempo por etapa (`Bridge.*`, `Prediction.*`, `Decision.*`) e `Monitoring.Reading` por tag, com idade da leitura. | Só escreve no log. Só liga com `Development` + `IAAPI_PERF_DIAGNOSTICS=1`. Escreve pelo `ILogger`, o mesmo console suspeito na hipótese 1 do P27. Não guarda estado que se possa consultar. | Os **nomes das etapas** (passo 5, opcional) e o padrão "nunca lançar exceção" (`catch { }` com comentário). |
| Diagnóstico do Intelligence Lab (`BoundedDiagnosticBuffer`, `IntelligenceLabHealthMetrics`, `GET /api/intelligence-lab/health` e `/diagnostics`) | Diagnóstico **por leitura**, capturado dentro de `PredictionService`, e contadores de captura. | Vem desligado (`IntelligenceLab:Enabled = false`). Exige `Development`, loopback, chave com 32 caracteres ou mais e metadados de versão. Não vê o laço, o lote nem as falhas antes de `Detectar`. Pôr o sinal de vida aí o deixaria preso ao modo laboratório. | O padrão do controller (`Cache-Control: no-store`) e o estilo de teste de controller sem servidor (`IntelligenceLabDiagnosticsEndpointTests`). |
| `MonitoramentoTagsService.ExecuteAsync` | Já mede o tempo do lote (`tempoColeta`) e o de cada tag (`tempoTag`), o status da tag (`Concluída`, `Falha`, `Cancelada`) e conta as `analisadas`. Tudo isso só vai para o log. | Os dados se perdem no log e não podem ser consultados. | **Os pontos de atualização:** o status é preenchido nesses mesmos lugares, com os valores já calculados. |
| Patches soltos na raiz (`*.patch`) | Os 5 estão inteiramente incorporados ao código desde `c5f575d`: `bridge_em_lote` e `tempo_por_tag` com acréscimos do `PERF_DIAG`; `clareza_cenario1` só parecia divergir porque o `sessoes_de_teste` foi aplicado por cima (ver 21.1). Movidos para fora do repositório em 16/11. | Nenhum guarda estado do ciclo; nada a reaproveitar além do que já está no código. | — |

**Conclusão:** nenhum mecanismo atual guarda **ciclo, etapa atual e contagens por cliente** num lugar que se possa consultar. O serviço de status é novo, mas reaproveita o modelo do `RuntimeDecisionMetrics`, os pontos de medição que o laço já tem e o painel de saúde do LiveMonitor.

**Por que um controller próprio e não uma rota no `RuntimeDecisionMetricsController`:** aquele controller pertence ao rollout multivariado e só responde com `AnomalyRuntimeMetrics:Enabled`. Isso amarraria o sinal de vida a uma chave de outra finalidade. Nada fica em paralelo: o card 2 do LiveMonitor e o `/api/anomaly-runtime/metrics` continuam com a mesma função de hoje.

#### 19.2 Passos (pequenos e reversíveis; nenhum muda a decisão)

**Passo 0 — Sem código, já possível na sessão de teste autorizada (tarefa 6).**
- Vigiar `GET /api/anomaly-runtime/metrics` a cada 5 min, junto com a última `DataDeteccao` de `/api/anomalias` (P27, "Como confirmar", passo b).
- Se `TotalDecisions` parar de subir enquanto a API responde, o laço parou. Registrar a hora e seguir o passo c do P27.
- Limite: o contador é global. Uma chamada manual a `POST /api/anomalias/tag` também o faz subir.

**Passo 1 — Serviço de status em memória, sem ligação com o laço.** Esforço: P. Risco: nenhum, porque nada existente muda.
- Novo `Services/Monitoring/MonitoramentoStatusService.cs`, ao lado do `MonitoramentoTagsBatchPlanner`.
  - Singleton com `TimeProvider`, que é nativo do .NET (sem pacote novo).
  - Usa lock curto. Não faz I/O nem log, e nenhum método lança exceção (corpo em `try/catch` vazio, como no PERF_DIAG).
  - Métodos: `IniciarCiclo`, `IniciarEtapa(etapa, clienteId?, tag?)`, `RegistrarTagsAtivas(porCliente)`, `RegistrarResultadoTag(clienteId, tag, status, duracaoMs, ehTimeout)`, `RegistrarFalhaLote(clienteId, qtdTags, ehTimeout)`, `RegistrarErroGeral(tipoExcecao)`, `ConcluirCiclo` e `Snapshot()`.
- Etapas: `Iniciando`, `ConsultandoTagsAtivas`, `ConsultandoLote`, `AvaliandoTag`, `Aguardando` e `Encerrado`.
- Contrato `MonitoramentoStatusSnapshot` (record, só aditivo):
  - `GeradoEmUtc` e `ServicoIniciadoEmUtc`;
  - `CicloAtual { Numero, IniciadoEmUtc }`;
  - `UltimoCicloConcluido { Numero, IniciadoEmUtc, ConcluidoEmUtc, DuracaoMs }`;
  - `ProximoCicloPrevistoEmUtc`;
  - `EtapaAtual { Etapa, ClienteId, TagName, DesdeUtc }`;
  - `Clientes[] { ClienteId, TagsAtivas, AvaliadasUltimoCiclo, FalhasUltimoCiclo, TimeoutsUltimoCiclo, LoteFalhouUltimoCiclo, UltimaAvaliacaoConcluidaUtc }`;
  - `UltimoErro { QuandoUtc, Origem (Lote | Tag | Geral), TipoExcecao, ClienteId, TagName }`.
- **Sem a mensagem da exceção.** Ela pode trazer URL ou host do Bridge. Basta o tipo e a origem.
- **Regra de timeout:** `TaskCanceledException` ou `TimeoutException` sem cancelamento do host. É a mesma distinção que o laço já faz com `stoppingToken`.
- **Testes** (`IAApi.Tests/Monitoring/MonitoramentoStatusServiceTests.cs`, offline, com um `TimeProvider` falso de poucas linhas escrito no próprio teste):
  - ciclo completo (números, duração e contagens);
  - tag com timeout (conta como falha e como timeout);
  - lote com timeout (todas as tags do cliente ficam não avaliadas e `LoteFalhouUltimoCiclo = true`);
  - sem novas chamadas, a etapa e o `DesdeUtc` ficam parados enquanto o relógio avança (simula o laço travado);
  - o snapshot é uma cópia (não muda depois de gerado);
  - argumentos nulos ou vazios não lançam exceção;
  - chamadas de várias threads não corrompem as contagens.

**Passo 2 — Ligar o serviço no laço.** Esforço: P. Risco: baixo.
- `MonitoramentoTagsService` recebe `MonitoramentoStatusService? status = null` no construtor. É o mesmo padrão do `PerformanceDiagnostics? = null`, então o construtor atual continua válido.
- Só se acrescentam chamadas, nos pontos que já existem:
  - começo do `while` → `IniciarCiclo`;
  - antes do `ToListAsync` → etapa `ConsultandoTagsAtivas`; depois → `RegistrarTagsAtivas`;
  - antes de `BuscarValoresAtuais` → etapa `ConsultandoLote`;
  - `catch` do lote → `RegistrarFalhaLote`;
  - antes de `DetectarComValorAtual` → etapa `AvaliandoTag`;
  - `finally` da tag → `RegistrarResultadoTag`, com o `status` e o `tempoTag` já calculados;
  - fim dos lotes → `ConcluirCiclo`;
  - `catch` geral → `RegistrarErroGeral`;
  - antes do `Task.Delay` → etapa `Aguardando`, com o próximo ciclo previsto (agora + 1 min);
  - saída do laço → etapa `Encerrado`.
- `UltimaAvaliacaoConcluidaUtc` corresponde ao status `Concluída`. Pelo código, isso significa que `Detectar` gravou a decisão (P27: toda leitura que chega a `Detectar` grava ou lança). `PredictionService` não muda.
- DI em `Services/DependencyInjection/IAApiApplicationServiceCollectionExtensions.cs`, ao lado do `AddHostedService<MonitoramentoTagsService>()`: `AddSingleton<MonitoramentoStatusService>()` e `TryAddSingleton(TimeProvider.System)`.
- **Testes:**
  - teste do laço com SQLite em memória **sem nenhuma tag ativa**: o ciclo é concluído, `Aguardando` aparece e, ao cancelar, `Encerrado`;
  - o mesmo teste com um contexto que falha: `UltimoErro.Origem = Geral`;
  - replay RC-2 com 0 diferenças. O replay chama `Detectar` direto, sem passar pelo laço (confirmado no código). Isso só confirma que o caminho da decisão não mudou.
- **Limite de teste (confirmado no código em 13/11; superado pela tarefa 22 em 16/11):** `BridgeValorAtualService` cria o próprio `HttpClient`, aponta fixo para `http://localhost:5001` e não tem interface nem métodos virtuais.
  - Um teste automático do laço **com tags** chamaria o Bridge real, se estivesse ligado. Isso viola o isolamento do `CLAUDE.md`.
  - Os caminhos de lote e de tag ficam cobertos pelos testes do passo 1 e pela conferência do passo 5.
  - O ponto de substituição no Bridge é a **tarefa 22** (P28), **feita em 16/11 (passos 1 e 2)**. O laço já pode ser testado com tags e com o `FakeBridgeHandler` pela DI. Sem ela, a alternativa seria trocar o `_httpClient` por reflexão, como os testes do histórico já fazem. Não é recomendada: o contorno é frágil.

**Passo 3 — Endpoint de leitura.** Esforço: P. Risco: baixo.
- Novo `Controllers/MonitoramentoStatusController.cs`: `GET /api/monitoramento/status?clienteId=` com `Cache-Control: no-store`. Rota nova; nenhum contrato existente muda.
- O controller calcula, **na hora da consulta**, `SegundosDesdeUltimoCicloConcluido` e `SegundosNaEtapaAtual`. Esse cálculo não depende da thread do laço, que pode estar travada. É a base do verificador independente da tarefa 20.
- **Isolamento:** com `clienteId`, devolve só aquele cliente. Na etapa atual e no último erro, nome de tag de outro cliente vira `"(outro cliente)"`.
  - Não expõe nada além do que `/api/anomalias` e `/api/tags` já expõem (que hoje não têm filtro): nada de valores, mensagens ou URLs.
  - Sem o serviço registrado, responde 404, como os endpoints do Intelligence Lab.
- **Testes** (`IAApi.Tests/Monitoring/MonitoramentoStatusControllerTests.cs`, no estilo de `RuntimeDecisionMetricsControllerTests`):
  - filtro por cliente;
  - mascaramento da tag de outro cliente;
  - cálculo das idades com o relógio falso;
  - cabeçalho `no-store`;
  - 404 sem o serviço.

**Passo 4 — LiveMonitor.** Esforço: P–M. Risco: baixo (ferramenta local, só leitura).
- **Proxy** (`server/proxy.ts`): `case '/monitor-api/monitor-status'` → `/api/monitoramento/status?clienteId=<client>`. Rota sem chave, como `decisions`. Entra na lista fechada que já existe.
- **Código:** `src/api.ts` (`monitorStatus`), `src/types.ts` (`MonitorStatus`) e `src/useMonitor.ts` (um polling próprio, no mesmo intervalo das decisões, com o erro guardado à parte).
- **Classificação** em `src/domain.ts`, função pura `monitorHealth(status, now, staleMs)`. Usa o `staleMs` que o monitor já tem (180 s), sem número novo. O limite definitivo é decisão da tarefa 20.

  | Estado | Regra |
  |---|---|
  | **ATIVO** (verde) | Último ciclo concluído há até `staleMs`. |
  | **LENTO** (amarelo) | Sem ciclo concluído há mais de `staleMs`, mas a etapa atual começou há até `staleMs` (o laço está andando, como nos ciclos de 8–15 min do P26). |
  | **PARADO** (vermelho) | A etapa atual não muda há mais de `staleMs`, ou está `Encerrado`. |
  | **SEM STATUS** (cinza) | A rota respondeu 404 (IAApi sem o passo 3) ou deu erro. O card 2 atual continua valendo. |

- **Como aparece na tela:**
  - **Barra do topo:** ao lado de "IAApi conectada", um segundo indicador: "Monitor da IA: ATIVO / LENTO / PARADO / sem status". Isso separa "a API responde" de "o laço roda", que é a confusão do P27.
  - **Painel "Saúde do monitoramento agora":** um card novo, "0 · Ciclo do monitor da IA", antes dos quatro atuais. Exemplo:
    - linha principal: estado e "último ciclo concluído há 2 min 10 s · durou 1 min 40 s";
    - detalhe: "12/12 tags avaliadas · 0 falhas · 0 timeouts";
    - etapa: "Agora: avaliando PK2.TAG28 há 45 s".
    - Com lote em timeout: "Lote do Bridge falhou (timeout) no último ciclo".
  - **Aviso amarelo** já existente (`readiness-warning`): passa a aparecer também com LENTO ou PARADO. No PARADO, o texto diz a etapa e desde quando, por exemplo "parado em 'avaliando PK2.TAG33' desde 15:09:13". É isso que mostra **onde** travou, sem `dotnet-stack`.
  - O texto fixo do aviso "A IA tem seu próprio ritmo" ganha a duração real do último ciclo, quando houver.
- **Testes** (vitest, offline; `fetch` é substituído por `vi.fn` como já se faz):
  - `tests/proxy.test.ts`: rota nova, cliente passado adiante e nenhum cabeçalho de chave;
  - `tests/domain.test.ts`: os quatro estados de `monitorHealth`, incluindo os limites exatos de `staleMs` e relógio adiantado ou atrasado (a mesma tolerância de −5 s já usada).

**Passo 5 — Conferência no ambiente de teste e etapa fina (opcional).** Exige aprovação própria, porque roda a IAApi ligada ao Bridge e ao simulador.
- **a.** Num teste do simulador de pelo menos 2 h (o mesmo da tarefa 6), comparar o card novo com o log gravado em arquivo. A duração dos ciclos, as tags avaliadas e os timeouts devem bater com as linhas `Bridge lote…` e `Predição finalizada…`.
- **b. Opcional:** levar a "etapa atual" para dentro da tag (`Bridge.HttpBuffered`, `Prediction.Preparation`, `Decision.Ensemble`…), reaproveitando as chamadas `PerformanceDiagnostics.Measure` que já delimitam essas etapas.
  - Para isso, `Measure` precisaria avisar o status mesmo com o PERF_DIAG desligado, o que muda o caminho rápido de hoje (`if (!Enabled) return operation()`).
  - Fica para depois do passo 4 e só se a etapa da tag não bastar para achar o travamento.

#### 19.3 Resumo de arquivos, risco e aceite
- **Arquivos novos:**
  - `Services/Monitoring/MonitoramentoStatusService.cs` (com o record do snapshot);
  - `Controllers/MonitoramentoStatusController.cs`;
  - `IAApi.Tests/Monitoring/MonitoramentoStatusServiceTests.cs` e `MonitoramentoStatusControllerTests.cs`.
- **Arquivos alterados:**
  - `Services/MonitoramentoTagsService.cs` (só chamadas acrescentadas);
  - `Services/DependencyInjection/IAApiApplicationServiceCollectionExtensions.cs`;
  - LiveMonitor: `tools/IAApi.LiveMonitor/server/proxy.ts`, `src/api.ts`, `src/types.ts`, `src/useMonitor.ts`, `src/domain.ts`, `src/App.tsx`, `src/style.css`, `tests/proxy.test.ts` e `tests/domain.test.ts`.
- **Não muda:** `PredictionService`, detectores, pesos, limiares, `AnomaliaDetectada`, `GET /api/anomalias`, `/api/anomaly-runtime/*`, `/api/intelligence-lab/*`, `appsettings.json`. Não há migration nem pacote novo.
- **Risco:** baixo. O ponto sensível é o passo 2: uma chamada ao status que lançasse exceção desviaria o laço. Por isso o serviço é "nunca lança", e isso é testado no passo 1.
- **Rollback:**
  - passo 4: reverter o commit do LiveMonitor. O card 2 continua funcionando sozinho;
  - passos 2 e 3: reverter o commit, ou só tirar o registro na DI. O laço aceita `status = null`;
  - passo 1: apagar o arquivo.
- **Aceite:**
  - **(a)** `dotnet test` (filtro `Monitoramento` e a suíte completa) e `npm test` no LiveMonitor, ambos verdes e offline. Antes de rodar, conferir o isolamento, como pede o `CLAUDE.md`.
  - **(b)** Com o relógio falso e sem chamadas novas, a etapa e o `DesdeUtc` ficam parados, e `monitorHealth` devolve PARADO depois de `staleMs`.
  - **(c)** Replay RC-2 com 0 diferenças.
  - **(d)** Na conferência do passo 5a, o card bate com o log por ciclo.
  - Sem (d), não afirmar que o indicador funciona no teste real.
- **Relação com outras tarefas:**
  - O cálculo de idade do passo 3 é o "verificador independente do laço" da **tarefa 20**. Ela só acrescenta o limite configurável e o Warning.
  - A **tarefa 18** pode usar as contagens de falha e timeout deste status como o "buffer em memória" que ela cita. A decisão entre buffer e tabela continua na 18.
- **Esforço total:** M (passos 1–4).

### 20. (Robustez) Alerta quando o ciclo passa de X minutos
- **Problema (P26, P27):** um ciclo de 15 min e um monitor parado por 11 h passaram despercebidos.
- **Proposta:**
  - Limite configurável `Monitoramento:AlertaCicloMinutos` (valor de X a definir; sugestão para a demo: 5 min, já que os dados são de 1/min).
  - **Ciclo lento:** o próprio laço registra Warning ao fechar um ciclo acima de X.
  - **Monitor parado:** um verificador **independente do laço** (timer próprio ou cálculo na leitura do status da tarefa 19) marca alerta quando não há ciclo concluído há mais de X. Esse alerta não pode depender da thread que travou.
  - O alerta aparece no LiveMonitor (indicador da tarefa 19) e no Event Viewer.
  - Opcional: notificação pelo mecanismo de notificações existente, com aprovação, para não poluir as notificações de anomalia.
- **Arquivos:** os da tarefa 19, mais a configuração (`appsettings.json`, chave nova com padrão seguro).
- **Risco:** baixo. Só alerta, sem efeito na decisão. Definir X é decisão operacional e exige aprovação.
- **Esforço:** P (depois da tarefa 19)
- **Aceite:** com ciclo artificialmente lento, Warning e indicador amarelo; com o laço travado, alerta vermelho em até X + 1 min, sem depender do laço.

---

*Item 21 acrescentado em 2026-11-13, a partir da regra do `CLAUDE.md` "procurar o que já existe antes de criar". Não aprovado; nada implementado.*

### 21. (Consolidação) Inventário das ferramentas e dos mecanismos de diagnóstico
- **Problema:** as ferramentas de `tools/` e os mecanismos de diagnóstico cresceram em paralelo, cada um para uma etapa do trabalho. Algumas funções parecem se repetir. Exemplos, todos **hipóteses a conferir**: dois replays (Intelligence Lab × CP300); várias formas de comparar saídas (Comparer, Evaluator, `Compare-Replay.ps1`); vários caminhos de diagnóstico (`PERF_DIAG`, diagnóstico do Intelligence Lab, `Dump-Diagnostics.ps1`). Sem um inventário, cada tarefa nova tende a criar mais uma ferramenta em vez de estender uma existente.
- **Escopo do inventário:**
  - **Ferramentas em `tools/`:** `IntelligenceLabReplay`, `IntelligenceLabEvaluator`, `IntelligenceLabComparer`, `IntelligenceLabScenarioGenerator`, `Cp300OfflineReplay` (inclui `Compare-Replay.ps1` e o placar), `Cp300Export` (`Export-Cp300Historico.ps1`, `Export-AnomaliasIaReal.ps1`) e `IntelligenceLabDump` (`Dump-Diagnostics.ps1`). Também estão em `tools/` e entram no inventário, mesmo fora da lista original: `IAApi.Benchmarks` e `IAApi.LiveMonitor`.
  - **Mecanismos de diagnóstico no runtime:** `PERF_DIAG` (`Services/Diagnostics/PerformanceDiagnostics.cs`) e o diagnóstico do Intelligence Lab (`src/IAApi.Engine/Observability/`, por exemplo `DiagnosticContracts.cs` e `ReadingAcquisitionDiagnostic.cs`, e os endpoints e exportadores que o expõem, como `ReplayDiagnosticJsonlExporter`).
- **Para cada item, levantar** (confirmado no código, sem executar nada conectado):
  - finalidade e em que etapa foi criado (documento ou commit de origem);
  - entrada, saída e como se executa;
  - dependências externas: acessa Bridge, PSi4, banco real ou API em execução? Pode rodar offline?
  - quem usa: testes (`IAApi.Tests.csproj` referencia vários desses projetos), scripts, documentos, tarefas abertas (por exemplo, a RC-2 a RC-11 dependem do `Cp300OfflineReplay` e do `Compare-Replay.ps1`; a RC-12 depende do `Dump-Diagnostics.ps1`);
  - sobreposição com outros itens da lista;
  - situação: em uso, parado ou quebrado (só pelo código e pelos testes existentes).
- **Proposta por item:** **manter**, **unir com outro** (dizer qual e o que sobra), **adaptar** (o que mudar) ou **remover**, sempre com o motivo. Quando a proposta for unir ou remover, incluir também:
  - o que precisa migrar antes (testes, documentos, tarefas abertas);
  - o risco;
  - o rollback (a remoção é por commit, reversível).
- **Entrega:** uma tabela neste item ou num documento próprio em `Docs/`, a decidir na aprovação. **Nenhuma ferramenta é unida, alterada ou removida nesta tarefa:** cada mudança vira tarefa própria, com aprovação.
- **Restrições:**
  - nada que mude a decisão de anomalia;
  - o `Cp300OfflineReplay` e o `Compare-Replay.ps1` ficam intactos até a RC-11, porque são a referência da regressão byte a byte (RC-2);
  - contratos HTTP e o formato dos diagnósticos já consumidos (LiveMonitor, `Dump-Diagnostics.ps1`) só mudam de forma aditiva.
- **Risco:** baixo (só leitura e documento). **Esforço:** M.
- **Aceite:** todos os itens do escopo estão na tabela, com finalidade, dependências, usos, sobreposições e proposta com motivo, e com cada afirmação marcada como confirmada no código ou hipótese. Nenhum arquivo de código ou de ferramenta foi alterado.

#### 21.1 Os 5 `.patch` soltos na raiz (P23) — ✅ **resolvido (2026-11-16)**
- **Decisão da responsável (16/11):** **mover sem apagar**. Os 5 arquivos estão em `C:\Users\Laura Marrari\Documents\IA MARRARI\arquivo_patches_2026-11\`, fora do repositório. O SHA-256 de cada arquivo é o mesmo antes e depois, e a raiz não tem mais arquivos não rastreados. P23 marcado como resolvido.
- A análise que fundamentou a decisão segue abaixo.
- **Método** (só leitura; nenhum patch aplicado no repositório):
  - `git apply --check` (aplicar) e `git apply -R --check --ignore-whitespace` (desfazer) contra o HEAD `05c64c3`;
  - para os patches empilhados, desfazer em ordem numa cópia no scratchpad, fora do repositório;
  - busca dos símbolos, textos e testes de cada patch no código atual.
- **Resultado:** os 5 estão **inteiramente incorporados**. Todo o conteúdo entrou no commit `c5f575d` (11/11, "checkpoint IAApi before CP300 detection fixes"). Nenhum aplica mais, e nenhum traz algo que falte no código.

  | Patch (data do arquivo) | O que faz | Incorporado? | Evidência (confirmado no código) | Recomendação |
  |---|---|---|---|---|
  | `IAApi_monitoramento_bridge_em_lote` (05/11) | Coleta no Bridge **em lote por cliente** (`MonitoramentoTagsBatchPlanner.CriarLotes` + `BridgeValorAtualService.BuscarValoresAtuais`), `DetectarComValorAtual`, tempo de coleta e de predição por lote no log, cancelamento limpo | **Total**, com acréscimos posteriores | Planner e testes já existem (`already exists`); `MonitoramentoTagsService.cs:45-88,148-153`. O reverso só falha porque o `PERF_DIAG` (`using IAApi.Services.Diagnostics`, `MonitoringReading`) foi intercalado depois | **Descartar** |
  | `IAApi_tempo_por_tag` (05/11) | Por cima do anterior: log "Predição iniciada/finalizada" por tag, com status (`Concluída`/`Falha`/`Cancelada`) e duração | **Total**, com acréscimos | `MonitoramentoTagsService.cs:97-104,120-144`; o `finally` hoje também alimenta o `PERF_DIAG` | **Descartar** |
  | `IAApi_observabilidade_temporal` (04/11) | Diagnóstico do Intelligence Lab guarda o horário industrial **sem inventar UTC**: `ReadingTimestampOriginal`, `ReadingTimestampKind`, `ReadingTimestampUtc` anulável; resumo do endpoint com esses campos; 5 testes | **Total** | `DiagnosticContracts.cs:843-866`; `IntelligenceLabDiagnosticsController.cs:80-81,202-203`; os 4 testes nomeados estão em `IAApi.Tests/IntelligenceLab/`. O reverso só falha no contexto de `DiagnosticContractsTests.cs`, que tem os mesmos testes. Teste aprovado só em log histórico (`TestResults/*.trx` de 05/11); **não executado nesta análise** | **Descartar** |
  | `IAApi_LiveMonitor_clareza_cenario1` (05/11) | LiveMonitor: painel "O que está funcionando agora?" (API × análise recente × histórico × decisões recentes), contagens só de decisões **recentes**, idade (`ageLabel`) da leitura e da análise no cartão, aviso "decisão pode ser antiga", `readingAtBasis` (fuso configurado × UTC conhecido), botão "Exportar decisões (CSV)" | **Total** (corrige a nota da tarefa 28, que dizia "parcial") | Desfazendo primeiro o `sessoes_de_teste` (aplicado por cima), o reverso deste passa **limpo** na cópia. Todos os símbolos estão em `App.tsx`, `TagCard.tsx`, `domain.ts`, `types.ts`, `style.css` e `tests/domain.test.ts` | **Descartar** |
  | `IAApi_LiveMonitor_sessoes_de_teste` (05/11) | LiveMonitor: "sessões de teste" (intervalo nomeado que filtra as decisões; não dispara nada no CLP), `src/testRuns.ts` + 5 testes | **Total** | `git apply -R --check` passa limpo no HEAD; `testRuns.ts` e `tests/testRuns.test.ts` versionados desde `c5f575d` | **Descartar** |

- **Por que descartar e não guardar nem refazer:**
  - nenhum tem conteúdo pendente, então não há o que refazer como tarefa;
  - o histórico do git (`c5f575d`) já guarda tudo. Manter os `.patch` seria uma cópia paralela e desatualizada do código, contra a regra do `CLAUDE.md`. Ela já induziu erro: as notas da tarefa 19.1 e da 28 diziam que o `clareza_cenario1` "diverge" ou foi "parcialmente incorporado".
- **Tarefa 28:** pode ser implementada direto sobre o código atual, **sem conflito**. A exportação CSV que ela move para `domain.ts` (`App.tsx`, `exportDecisions`) é justamente a que veio do `clareza_cenario1`. Nenhum dos patches mexe na escala da nota.
- **Ação executada:**
  - movidos para fora do repositório (nada apagado);
  - P23 atualizado em `PROBLEMAS_ENCONTRADOS.md`;
  - linha "Patches soltos" da tabela 19.1 corrigida.
  - **Rollback:** nenhum código mudou. Os arquivos estão na pasta acima, e o conteúdo também está em `c5f575d`.

#### 21.2 Os 3 `Docs/B3.23_*` removidos — ✅ **decidido (2026-11-16)**
- **Decisão da responsável:** **não restaurar**. A remoção em `15f1ca9` fica como está. O registro de achados será conferido na **tarefa 29**, que é só de leitura e começa pela segurança.
- A análise que fundamentou a decisão segue abaixo.
- **Situação no git** (confirmado): a remoção **já está commitada** em `15f1ca9` (12/11). Ela entrou junto com "Replay: opção --regime…", sem justificativa no commit. O `git status` atual não mostra os arquivos como apagados, então **não há `git rm` a fazer**.
- **O que eram** (versão `15f1ca9^`), todos da fase B3.23 (set/2026, base de 147 testes):
  - `B3.23_CONTEXT_AND_GUARDRAILS.md` (627 linhas): contexto e regras para a auditoria. Avisa que `Benchmark/` contém runtime real, define a auditoria como só leitura e as regras de evidência.
  - `B3.23_CODEX_AUDIT_PROMPT.md` (763 linhas): o prompt dado ao Codex para essa auditoria.
  - `B3.23_CODEX_AUDIT_RESULT.md` (995 linhas): o relatório da auditoria, com mapa do fluxo, dependências Benchmark ↔ runtime, multi-cliente, configuração, LLM, segurança e dependências, o **registro de 30 achados (B323-01 a B323-30)** e o plano B3.24.
- **Ainda é útil?**
  - CONTEXT e PROMPT: **não**. São instruções a outro agente para uma fase encerrada, e o `CLAUDE.md` e o `IAAPI_HANDOFF_CLAUDE.md` substituem essas regras.
  - RESULT: **em parte**. O retrato do código é de setembro, e parte dos achados já foi resolvida ou absorvida (histórico a validar). Mas o registro de achados tem itens que **não aparecem em nenhum documento atual**. Conferido por amostra no código de hoje:
    - **B323-07** (campos de deadband perdidos após gap crítico): **corrigido**. `CreateContiguousRuntimeInput` copia os 3 campos, e há teste em `RuntimeFunctionalDecisionContextProviderTests.cs`.
    - **B323-08** (envelope do Increasing Noise calculado e não usado na supressão do ROC): **ainda presente**. Em `RegimeEvidenceAdapter.cs:186-200`, `currentWithinIncreasingNoiseEnvelope` só vai para o diagnóstico (linha 737). Isso afeta a detecção, prioridade 2 do `CLAUDE.md`. Não está provado que tenha relação com as falhas do CP300.
    - **B323-01** (`POST /api/dev/reset` sem autenticação): o endpoint continua em `DevController.cs:14`, e `MapControllers` não está condicionado ao ambiente (`Program.cs:44`). Se isso é proteção suficiente fica **a validar**.
    - Sem registro atual também: B323-02 a B323-06 (multi-cliente e autenticação), B323-17 (vazamento pelo LLM e stack trace) e B323-23 (dependências com alertas). B323-09, B323-21 e B323-29 têm correspondência nas tarefas 5, 22 e 6/19.
- **Recomendação:**
  - **manter a remoção** (não restaurar os 3 arquivos em `Docs/`): restaurar devolveria uma auditoria antiga em paralelo à documentação atual, contra a regra do `CLAUDE.md`;
  - **aproveitar o registro de achados**: abrir uma tarefa de triagem que confira cada B323-xx no código atual, marque o que foi resolvido e mande os vivos para o BACKLOG ou para o `PROBLEMAS_ENCONTRADOS.md`, citando a fonte por `git show 15f1ca9^:Docs/B3.23_CODEX_AUDIT_RESULT.md`. Prioridade: B323-08 (detecção) e B323-01/02 (segurança). A tarefa só lê e documenta; cada correção exige aprovação própria.
  - **Rollback:** nada muda no repositório. Se a responsável preferir restaurar, basta `git checkout 15f1ca9^ -- Docs/B3.23_*.md`.

---

*Item 22 acrescentado em 2026-11-13, a partir do P28 (levantamento da tarefa 19). Passos 1 e 2 feitos em 2026-11-16, a pedido da responsável, sem commit; passo 3 não aprovado.*

### 22. (Robustez/testes) Endereço do Bridge pela configuração e `HttpClient` substituível nos serviços do Bridge — ✅ **PASSOS 1 E 2 CONCLUÍDOS (2026-11-16, sem commit)**; passo 3 não feito; aceite (e) pendente
- **Resultado (16/11):**
  - **Conferência antes de mudar** (sem expor o valor): `Bridge:BaseUrl` só está definida no `appsettings.json` e é **igual** a `http://localhost:5001`, também sem `/` no fim. Não aparece no `appsettings.Development.json` nem no `Properties/launchSettings.json`, e a variável `Bridge__BaseUrl` não está definida nesta máquina (processo, usuário e máquina). Com a configuração atual, as URLs não mudam.
  - **Produção:**
    - `Services/DependencyInjection/BridgeServiceCollectionExtensions.cs`: `AddHttpClient<BridgeTagsService>` e `AddHttpClient<BridgeValorAtualService>` no lugar dos `AddScoped`, com a mesma assinatura de `AddBridgeServices`. `BaseAddress` vem de `ObterBaseAddress(IConfiguration)`: `Bridge:BaseUrl` normalizada com `/` no fim; chave ausente ou vazia usa `BaseUrlPadrao`; endereço que não seja http(s) absoluto gera `InvalidOperationException`, sem o valor na mensagem. Timeouts `TimeoutValorAtual` = 60 s e `TimeoutTags` = 100 s.
    - **`RemoveAllLoggers()` nos dois clientes** (decisão desta implementação, não estava na proposta): com `LogLevel:Default = Information`, o `IHttpClientFactory` gravaria 4 linhas por requisição no log do monitor. Sem elas, o log fica igual ao de antes.
    - `BridgeValorAtualService` e `BridgeTagsService`: construtor com `HttpClient` e caminhos relativos `valores-atuais` e `tags?cliente={clienteId}`, com a mesma codificação. Corpo, conversão e tratamento de erro não mudaram.
    - `BridgeHistoricoService.ObterBaseUrl`: o literal virou `BridgeServiceCollectionExtensions.BaseUrlPadrao`, com o mesmo valor, para cumprir o aceite (d). Nada mais mudou no histórico.
    - **Vida útil:** `Transient`. Os consumidores (`CurrentValuePredictionService`, `AnaliseRelacionadasService`, `ProcessoInteligenciaService`, `EquipamentoInteligenciaService`, controllers e o escopo do `MonitoramentoTagsService`) são `Scoped` ou resolvidos dentro de escopo (confirmado no código).
  - **Testes:**
    - **Handler comum** `IAApi.Tests/Bridge/FakeBridgeHandler.cs` (conta e registra as chamadas, com `Json(...)` e `FailIfCalled()`). Ele substitui os dois `BridgeHandler` privados de `PerformanceDiagnosticsTests` e `PredictionHistoryReuseTests`, que viraram fábricas com as mesmas asserções e respostas. A reflexão no `_httpClient` do histórico continua, porque é o passo 3.
    - **`IAApi.Tests/Bridge/BridgeServicesTests.cs` (29 testes):**
      - URL final com e sem `/` no fim, com caminho, sem a chave e com a chave vazia;
      - URL igual à do endereço fixo antigo, inclusive para `clienteId` com espaço, `&`, acento, `/` e `%`;
      - corpo e `Content-Type` do `POST`, conversão da resposta (número, texto, `NaN`, vazio, `null`, inválido, ausente) e erro HTTP;
      - `BaseAddress` e `Timeout` pelo `IHttpClientFactory`, vida útil (`Transient`/`Singleton`) e endereço inválido;
      - nenhum log do factory, com um cliente de controle que prova que a captura funciona.
    - `IntelligenceLabBehaviorBaselineEndToEndTests`: `BridgeValorAtualService` com `FakeBridgeHandler.FailIfCalled()` e `Calls = 0` conferido depois de cada detecção.
    - **Mutações** (temporárias, com o arquivo restaurado e conferido por `cmp`): sem `RemoveAllLoggers`, 1 teste falha; sem a `/` no fim, 3 falham; com o endereço fixo de volta, 9 falham.
  - **Aceite:**
    - **(a)** ✅ 29/29 novos, offline, e relacionados 124/124 (Bridge, PERF_DIAG, reaproveitamento do histórico, E2E do IntelligenceLab, `HistoricoCausal`, `Monitoring`);
    - **(b)** ✅ suíte completa **1079/1079**. O isolamento foi conferido antes: bancos só temporários ou em memória, e nenhum teste com endereço do Bridge real;
    - **(c)** ✅ `Compare-Replay.ps1`: **0 diferenças em 12 execuções**, sem e com `-HistoricoCausal` (`dados/rc2_rodadas/20261116_144938` e `20261116_145514`);
    - **(d)** ✅ `localhost:5001` aparece em um único lugar do código de produção (`BaseUrlPadrao`);
    - **(e)** ⏳ pendente: conferir as linhas `Bridge lote…` na próxima sessão autorizada com o Bridge. Até lá, não afirmar que o comportamento no ambiente de teste ficou igual.
  - **Destrava:** o teste offline do laço do monitor com tags (tarefa 19, passo 2), o aceite da tarefa 18 e o teste offline da 6.3. Basta `services.AddHttpClient(nameof(BridgeValorAtualService)).ConfigurePrimaryHttpMessageHandler(...)` com o `FakeBridgeHandler`, como em `BridgeServicesTests`.
  - **Rollback:** reverter os 4 arquivos de produção e os 3 de teste, e apagar `IAApi.Tests/Bridge/`. Não há configuração nova nem migration.
- **Problema (P28, confirmado no código):**
  - `BridgeValorAtualService` e `BridgeTagsService` têm `http://localhost:5001` fixo no código e criam o próprio `HttpClient`. Só o `BridgeHistoricoService` lê `Bridge:BaseUrl`.
  - Mudar a chave muda só o histórico, sem aviso.
  - Os testes não têm como substituir o transporte, a não ser por reflexão em campo privado. Isso bloqueia testes offline do laço do monitor com tags (tarefa 19, passo 2) e o aceite da tarefa 18.
- **O que se reaproveita** (confirmado no código; não se cria configuração nova):
  - **A chave `Bridge:BaseUrl`**, que já existe no `appsettings.json` e já é lida por `BridgeHistoricoService.ObterBaseUrl`, com o mesmo padrão `http://localhost:5001`. O valor configurado hoje é **igual** ao endereço fixo, conferido sem expor o valor. Com a configuração atual, as URLs não mudam.
  - **O padrão de cliente tipado:** `AddHttpClient<ILlmProvider, LmStudioLlmProvider>` em `DependencyInjection/Llm/LlmServiceCollectionExtensions.cs` já define `BaseAddress` e `Timeout` pela configuração. O `AddHttpClient` já compila no projeto, então não há pacote novo.
  - **O registro atual:** `Services/DependencyInjection/BridgeServiceCollectionExtensions.cs` (`AddBridgeServices`), sem mudar a assinatura. A configuração é lida pelo `IServiceProvider` dentro do registro, como faz o LLM.
  - **O handler falso dos testes** (`BridgeHandler` em `IAApi.Tests/PerformanceDiagnosticsTests.cs`, com `http://bridge.test`): reaproveitar ou mover para um auxiliar de teste comum, em vez de escrever outro.
- **Proposta em passos** (cada um com aprovação própria; nenhum muda a decisão):
  - **Passo 1. `BridgeValorAtualService` e `BridgeTagsService` como clientes tipados.**
    - Em `AddBridgeServices`, trocar `AddScoped<BridgeValorAtualService>()` e `AddScoped<BridgeTagsService>()` por `AddHttpClient<…>`.
    - `BaseAddress` vem de `Bridge:BaseUrl`, com o mesmo padrão `http://localhost:5001`, normalizado com uma `/` no fim. Sem essa barra, um endereço com caminho perderia o último trecho ao juntar com o caminho relativo.
    - **Timeouts iguais aos de hoje:** 60 s no valor atual. Nos tags, os 100 s do padrão do .NET, escritos explicitamente para documentar.
    - Os construtores passam a receber `HttpClient`. As chamadas usam caminhos relativos: `valores-atuais` e `tags?cliente=…`, com o mesmo `clienteId` de hoje, sem mudar a codificação.
    - **Vida útil:** de `Scoped` passa a `Transient`, que é o padrão do cliente tipado. Os dois serviços não guardam estado além do `HttpClient` (confirmado no código), então a troca não muda o comportamento. Ela também elimina o `HttpClient` novo e não descartado a cada escopo (P28, hipótese de esgotar conexões).
    - **Fora do escopo:** chave nova de timeout. Mudar o timeout é decisão operacional.
  - **Passo 2. Testes e ajuste dos usos diretos.**
    - Novo `IAApi.Tests/Bridge/BridgeServicesTests.cs`, com handler falso e sem rede:
      - URL final igual a `{BaseUrl}/valores-atuais` e `{BaseUrl}/tags?cliente=…`, com e sem barra no fim e com caminho no `BaseUrl`;
      - corpo JSON do `POST` idêntico ao atual;
      - a conversão da resposta não muda;
      - registro na DI: resolver pelo `ServiceCollection` com configuração em memória e conferir o `Timeout` e o `BaseAddress` do cliente nomeado (`IHttpClientFactory.CreateClient(nameof(…))`);
      - sem `Bridge:BaseUrl`, cai em `http://localhost:5001`, só verificando a URL montada pelo handler falso, sem conexão.
    - **Ajuste** em `IAApi.Tests/IntelligenceLab/IntelligenceLabBehaviorBaselineEndToEndTests.cs:1105`: passar um `HttpClient` com handler que **falha o teste se for chamado**. A garantia que hoje só está no comentário passa a ser verificada.
    - `tools/Cp300OfflineReplay` (`null!`) não muda.
  - **Passo 3 (opcional). `BridgeHistoricoService` no mesmo padrão.**
    - Receber `HttpClient` pelo construtor, mantendo o `IConfiguration` para `Bridge:HorasHistorico`.
    - Isso apaga o `ObterBaseUrl` duplicado. Os testes de `PerformanceDiagnosticsTests` e `PredictionHistoryReuseTests` deixam de usar reflexão.
    - **Cuidado:** o serviço é `Singleton`, registrado como `IHistoricoService`. Um cliente tipado dentro de um singleton fica preso para sempre. Usar `IHttpClientFactory` ou manter um único `HttpClient` criado no registro.
    - Fica separado porque mexe no histórico de toda leitura e nos testes do PERF_DIAG e do reaproveitamento do histórico.
- **Arquivos:** `Services/BridgeValorAtualService.cs`; `Services/BridgeTagsService.cs`; `Services/DependencyInjection/BridgeServiceCollectionExtensions.cs`; `IAApi.Tests/Bridge/BridgeServicesTests.cs` (novo); `IAApi.Tests/IntelligenceLab/IntelligenceLabBehaviorBaselineEndToEndTests.cs`. No passo 3, também `Services/BridgeHistoricoService.cs`, `IAApi.Tests/PerformanceDiagnosticsTests.cs` e `IAApi.Tests/PredictionHistoryReuseTests.cs`.
- **Não muda:**
  - URLs, corpo, timeouts e tratamento de erro com a configuração atual;
  - decisão de anomalia, contratos HTTP da IAApi e `appsettings.json` (a chave já existe).
- **Risco:** baixo a médio. É o caminho de aquisição de toda leitura do monitor, mas só a forma de obter o cliente e a URL. O comportamento muda apenas onde `Bridge:BaseUrl` for diferente de `http://localhost:5001`: aí o valor atual e os tags passam a seguir a chave, como o histórico já faz.
  - Antes de implementar, conferir sem expor valores se alguma máquina de teste define a chave diferente, por `appsettings.*.json` ou pela variável de ambiente `Bridge__BaseUrl`.
  - Hoje o `appsettings.Development.json` não define a chave.
- **Rollback:** reverter o commit. Não há migration nem configuração nova.
- **Esforço:** P (passos 1 e 2) / P (passo 3).
- **Aceite:**
  - **(a)** Testes novos verdes, offline, sem nenhuma conexão real. O handler falso conta as chamadas.
  - **(b)** Suíte completa de `dotnet test` verde. Antes de rodar, conferir o isolamento, como pede o `CLAUDE.md`.
  - **(c)** Replay RC-2 com 0 diferenças. Só como sanidade: o replay não usa estes serviços (confirmado no código).
  - **(d)** `localhost:5001` aparece no código de produção em um único lugar, o valor padrão da configuração.
  - **(e)** Na próxima sessão autorizada com o Bridge, as linhas `Bridge lote…` do log mostram tempos e contagens normais. Sem essa conferência, não afirmar que o comportamento no ambiente de teste ficou igual.
- **Ordem:** antes do passo 2 da tarefa 19 e dos testes das tarefas 18 e 20. Não depende de nenhuma outra tarefa.

### 23. (Ferramenta) Código de saída do `Compare-Replay.ps1` nos erros de uso — ✅ **CONCLUÍDA (2026-11-13)**
- **Resultado:** função `ErroDeUso` no script (`Write-Error … -ErrorAction Continue` seguido de `exit 2`), usada nos **seis** caminhos de erro de uso. Os quatro listados abaixo e mais um: `-Gerar` com `-HistoricoCausal`, que tinha o mesmo problema. A entrada ausente conta em dois lugares, no `-Gerar` e na comparação. Só o script mudou.
  - **Testado** com `-SemBuild` e cópias do manifesto no scratchpad. `dados/rc2_referencia/` não foi tocado. A entrada alterada foi simulada com `entrada_sha256` trocado na cópia. Comparação: entrada ausente, entrada alterada e manifesto ausente. `-Gerar`: manifesto existente, `-HistoricoCausal` e entrada ausente. Os seis davam código **1** no script do `HEAD` e agora dão **2**, com a mesma mensagem. Nenhum rodou o replay.
  - **Comparação normal:** `Compare-Replay.ps1 -Configuracao Release`, **0 diferenças em 12 execuções**, código **0** (`dados/rc2_rodadas/t23_codigo_saida`).
- **Problema** (confirmado em execução na RC-0, 2026-11-13; causa confirmada no código):
  - Com a entrada alterada (SHA-256 diferente do manifesto), o script para antes de rodar qualquer execução, como deve. Mas sai com código **1**, e não com o **2** documentado no cabeçalho ("2 = uso incorreto").
  - **Causa:** `$ErrorActionPreference = 'Stop'` faz o `Write-Error` encerrar o script antes do `exit 2`.
  - Vale para todos os `Write-Error …; exit 2`: manifesto já existente sem `-Sobrescrever`, manifesto ausente, entrada ausente e entrada alterada.
  - O problema já existia antes da mudança da RC-0.
- **Impacto:** quem chama o script não distingue "há diferenças" (1) de "a comparação não vale" (2). O resultado da comparação não muda (0 = 0 diferenças).
- **Proposta:** nesses pontos, escrever a mensagem sem encerrar o script e depois chamar `exit 2`. Por exemplo, `Write-Error … -ErrorAction Continue` ou `[Console]::Error.WriteLine(…)`. Só o script muda; nenhum código de runtime.
- **Risco:** baixo (ferramenta offline). **Esforço:** P
- **Aceite:**
  - os quatro caminhos de erro de uso saem com código 2;
  - os testes usam cópias do manifesto no scratchpad, sem tocar em `dados/rc2_referencia/`;
  - a comparação normal continua com 0 diferenças e código 0.
- **Ordem:** independente. Pode entrar junto com qualquer tarefa de código que já rode a RC-2.

### 24. (Futuro, correção) Fallback do regime por poucos pontos: resolver com a referência estável
- **Problema** (confirmado no replay, RC-0; `PLANO_REGIME_COMBINADO.md` §5.7.3):
  - Com o baseline por regime em 24 h móveis, uma classe rara cai no perfil global quando tem menos de `MinimoAmostrasPorRegime` (30) pontos na janela.
  - Na RC-0, mesmo com a noite anterior no histórico, a classe "B-01 parada + 380" teve só 27–29 pontos às 00:43–00:45 de 11/11, porque as bombas se alternam entre as noites. Resultado: 9 minutos-tag de alarme da B-01 com `origem_baseline = Global`.
  - É o P08 e o risco 4 de `PROPOSTA_BASELINE_ESTAVEL.md` §3 ("estados ausentes na janela"). Quanto mais componentes o regime tem (item 12), mais classes raras aparecem.
  - **RC-11 (16/11, Opção A real, 3 componentes; `PLANO_REGIME_COMBINADO.md` §7.5):** em 5 cenários, foram **174 minutos-tag em `Global` nas bombas, 72 com alarme**, todos com a classe abaixo de 30 pontos.
    - Madrugada de 11/11: 18 minutos-tag (9 com alarme, B-01).
    - 13/11 00:47–01:12, depois do rodízio de 00:47: 156 minutos-tag (63 com alarme, B-02). A classe "B-02 parada + 380" foi de 14 a 29 pontos.
    - Sem o set-point (2 componentes), os fallbacks somem, mas a classe "sozinha" mistura dia e noite e alarma mais (§7.6).
    - **Sugestão para o aceite (a aprovar):** incluir também a madrugada de 13/11 (replay das 18 h normais de 12/11) sem `Global` nas bombas.
- **Solução futura:** a referência estável de `PROPOSTA_BASELINE_ESTAVEL.md` §4.
  - **Peça A:** treino por período, com perfil e contagem de pontos por regime.
  - **Peça B:** ordem de seleção: perfil treinado do regime atual → perfil global treinado → regime móvel.
  - Uma referência treinada num período que contenha as duas alternâncias noturnas não depende do que sobrou na janela de 24 h.
  - A chave do regime combinado (RC-4, `TAG42=2|TAG43=0|TAG46=380`) já foi pensada como a chave desses perfis (PROP §4-A; `PLANO_REGIME_COMBINADO.md` §4).
- **O que não fazer agora:** baixar `MinimoAmostrasPorRegime` ou alongar a janela. As duas mudam o baseline, exigem aprovação e não estão propostas.
- **Relação:**
  - depende das fases F1–F3 da proposta e do item 8 (treino por período);
  - o risco da proposta (§6) vale aqui também: se o período de treino não tiver todos os estados, a referência gera falso alarme. Por isso, exigir o relatório de pontos por regime no treino.
- **Primeiro passo, quando for priorizado** (offline; depende do modo experimental F0 do replay, que ainda não existe):
  - treinar a referência por regime num período com as duas alternâncias noturnas (CSV de 09–13/11);
  - medir a madrugada de 11/11: se os 9 fallbacks somem e quantos alarmes a B-01 tem.
- **Risco:** médio a alto, porque muda o baseline (fase F3). **Esforço:** G para a proposta inteira; P para o primeiro passo.
- **Aceite (a detalhar):** madrugada de 11/11 com `combs` sem `origem_baseline = Global` nas tags das bombas, sem piorar o preset 1 nem o preset 6.

### 25. (Correção) Entrada inválida sistemática no monitor (P31) — **PRIORIDADE 1: antes de qualquer novo teste da IA real**
- **Problema** (confirmado no código; reproduzido offline 50 de 50; `INVENTARIO_PK2.md` §4.2b e §4.3):
  - `MonitoramentoTagsService` busca os valores atuais **em lote** no início do ciclo, e cada tag busca as 24 h de histórico só na sua vez.
  - Se o histórico já tem um ponto posterior à leitura, `DetectionInputValidator` recusa a entrada (leitura anterior ao último ponto). A IA grava **nota 0 e "sem anomalia"**, sem aviso (P19).
  - Em 12/11, **38 de 50** decisões foram assim. Somado ao P26, só ≈ 2 % dos minutos-tag foram avaliados.
  - Desde 05/11, 67–91 % das decisões do PK2 têm nota 0 [hipótese: mesmo mecanismo].
- **Por que é prioridade 1:**
  - enquanto isso acontecer, nenhum teste da IA real mede detecção nem alarme falso;
  - o LiveMonitor e o placar mostram essas leituras como "normal";
  - quanto mais lento o ciclo (tarefa 6), pior.
- **Opções a avaliar** (proposta antes de implementar: evidências, arquivos, impacto, riscos, aceite, rollback):
  - **(a) Só observabilidade, não muda a decisão:** contar e expor por ciclo as entradas inválidas (log estruturado, PERF_DIAG ou LiveMonitor) e marcar a decisão como "não avaliada". Primeiro passo recomendado, porque dá a medida.
  - **(b) Buscar o valor atual por tag, logo antes da predição.** Reduz a idade da leitura (P18), mas aumenta as chamadas ao Bridge (P10).
  - **(c) Recortar o histórico até a hora da leitura**, como o replay faz. Mantém o lote e corresponde ao que o replay já valida. Muda o que chega aos detectores.
  - (b) e (c) mudam o que os detectores recebem e exigem aprovação explícita. Não relaxar a validação (proteção contra vazamento temporal).
- **Arquivos prováveis:** `Services/MonitoramentoTagsService.cs`, `Services/CurrentValuePredictionService.cs`, `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs`, `Services/AnomalyDetection/DetectionInputValidator.cs` (só leitura), testes em `IAApi.Tests/`.
- **Antes de mexer:** checar com a tarefa 21 se já existe contador de entrada inválida reaproveitável (`EntradaInvalida` no detalhamento do replay; `PerformanceDiagnostics`).
- **Risco:** médio para (a), alto para (b) e (c). **Esforço:** P para (a), M para (b) e (c).
- **Aceite:**
  - teste offline que simule lote + histórico mais novo e mostre a entrada inválida contada ou evitada;
  - `Compare-Replay.ps1` com 0 diferenças (o replay não passa por esse caminho);
  - num teste autorizado com o simulador, fração de entradas inválidas ≈ 0 (ou medida e visível, na opção (a)).
- **Rollback:** reverter o commit; na opção (a), nada muda na decisão.
- **Status (13/11): opção (c) implementada atrás da chave `AnomalyDetection:HistoricoCausal`, DESLIGADA por padrão; não commitada.** Detalhes no P31.
  - Decisões aprovadas:
    - recorte com `<`, que exclui o ponto do próprio minuto;
    - tolerância de 30 min;
    - opção `--historico-causal` no replay.
  - Arquivos:
    - `Services/AnomalyDetection/HistoricoCausalOptions.cs` (novo; segue o padrão de `RegimeCombinadoOptions`, porque nenhuma opção existente servia);
    - `PredictionDetectionPreparationService.cs` (`RecortarAteLeitura`);
    - `AnomalyDetectionServiceCollectionExtensions.cs`;
    - replay (`Cp300Replay.cs`, `Program.cs`, `Compare-Replay.ps1`, README);
    - testes (`HistoricoCausalTests.cs`; golden com a chave desligada e ligada).
  - **Aceite offline atingido (13/11):**
    - `HistoricoCausalTests` 18/18 e golden 2/2; 284 testes relacionados;
    - `Compare-Replay.ps1`: **0 diferenças em 12 execuções**, tanto sem `-HistoricoCausal` quanto com ele;
    - suíte completa: **967 aprovados, 0 falhas**.
  - **Pendente:**
    - [ ] ligar a chave numa sessão autorizada com o simulador e medir a fração de entradas inválidas e de notas 0 no PK2 (esperado ≈ 0);
    - [ ] opção (a): tornar visível a entrada inválida que restar (acima da tolerância);
    - [ ] avaliar os alarmes com a B-02 ligada antes de confiar neles (P32, tarefa 27).

### 26. (Ferramenta/documentação) Escala da nota nas análises da IA real (P30)
- **Problema** (confirmado no código e nos dados; `INVENTARIO_PK2.md` §4.2a):
  - `AnomaliaDetectada.Score`, e `score` em `GET /api/anomalias`, é a nota do ensemble **÷ 20** (`EnsembleDetectionService.cs:406-410`).
  - `Export-AnomaliasIaReal.ps1` grava esse valor como `nota_final`, a mesma coluna em que o replay grava a nota 0–100.
  - A RES3 §2.4b comparou as duas escalas.
- **Proposta:**
  - a exportação grava também `nota_0_100 = score × 20` e documenta a escala no README §1.1;
  - corrigir a tabela da RES3 §2.4b;
  - verificar como o LiveMonitor exibe o `Score`.
  - **Não mudar** o contrato HTTP nem o valor gravado.
- **Situação (2026-11-13):** **parcialmente concluída** (detalhe em `PROBLEMAS_ENCONTRADOS.md` P30, "Feito em 13/11").
  - [x] `Export-AnomaliasIaReal.ps1` grava `nota_0_100` (`score × 20`, no fim da linha; `nota_final` continua 0–5). Escala de cada coluna no cabeçalho do script e no README §1.1. Sintaxe validada no PowerShell 5.1 e teste offline com JSON sintético.
  - [x] `dados/ia_real_preset3.csv` com `nota_0_100`, **calculada do próprio `nota_final`** (não havia JSON da API guardado; a API não foi chamada). Aceite: 50 de 50 iguais a `nota_real_x20` (dif. máx. 0,004); 13 colunas originais idênticas, então o placar não muda.
  - [x] RES3 §2.4b refeita na escala 0–100 (e o item 8 do resumo).
  - [x] Verificar o LiveMonitor (leitura do código): mostra o `score` persistido cru (0–5) e, na fonte de diagnóstico, o `normalizedScore` (0–100) ao lado do threshold. Não verificado na tela.
  - [ ] Corrigir a exibição no LiveMonitor: virou a **tarefa 28**. **Exige aprovação**.
  - [ ] Opcional: a exportação poder gravar a resposta JSON bruta (e reler dela), para regenerar o CSV sem chamar a API. Não implementado.
- **Arquivos:** `tools/Cp300Export/Export-AnomaliasIaReal.ps1`, `tools/Cp300OfflineReplay/README.md`, `Docs/RESULTADO_PRESET3.md`.
- **Risco:** baixo (não toca no runtime). **Esforço:** P.
- **Aceite:** export das 50 decisões de 12/11 com `nota_0_100` igual a `nota_real_x20` de `dados/inventario_pk2/perfil_real_50_decisoes_12nov.csv`; placar inalterado.

### 27. (Correção, configuração) Retreinar o perfil do PK2 com período limpo (P32)
- **Problema** (confirmado nos dados; `INVENTARIO_PK2.md` §2):
  - o `PerfisIa` do PK2 é de **30/10**, com 161–232 pontos (~3 h) e nunca foi retreinado;
  - foi treinado com a B-02 parada: TAG28 (0,35 ± 0,72, deadband 2) e TAG32 (0,03 ± 0,17, deadband 0,04) estão colapsadas;
  - TAG25 e TAG26 têm desvio inflado (mistura de estados), e a TAG24 tem desvio 12× o do treino limpo;
  - com a B-02 ligada, o alarme falso é provável [hipótese].
- **Proposta:**
  - escolher um período limpo e longo (TAG73 = 0; pelo menos um dia inteiro com noite, rodízio e B-02 ligada e parada);
  - medir antes no replay: perfil do banco × perfil novo, com `--treino-inicio/--treino-fim`;
  - só então retreinar em produção.
  - O endpoint atual treina só as últimas 24 h (P13). Para escolher o período em produção, depende do item **8**; sem ele, só dá para chamar o treino logo após 24 h limpas.
- **Atenção:** P01 continua valendo. O IQR corta a noite da TAG24 mesmo com treino limpo, e o retreino sozinho não resolve a reserva parada (P02); ver itens 9 e 12.
- **Antes de executar:** guardar cópia das linhas atuais de `PerfisIa` do PK2 (rollback = restaurar os valores).
- **Risco:** alto (treinar altera a detecção; exige aprovação explícita). **Esforço:** P a M.
- **Aceite:**
  - replay do período de 12–13/11 com o perfil candidato sem piorar alarmes falsos nem a detecção nos presets 1 e 6;
  - relatório de pontos por regime no treino;
  - aprovação da responsável antes do retreino real.
- **Depende de:** 25 (para medir o efeito na IA real) e 8.
- **Bloqueia a RC-12** (decisão de 2026-11-16, `PLANO_REGIME_COMBINADO.md` §7.9): a ativação do regime combinado no PK2 só vem depois desta tarefa e da 6. Quando a classe do regime tem menos de 30 pontos na janela de 24 h, a tag cai no perfil global. Isso aconteceu em 174 minutos-tag das bombas no replay da RC-11 (§7.5), e o perfil global hoje é o de 30/10, com a B-02 colapsada.

### 28. (LiveMonitor, demo) Toda nota exibida na escala 0–100, ao lado do limiar na mesma escala (P30)
*Planejada em 2026-11-13; **não implementada, exige aprovação**. Liga-se ao P30 (pendência da tarefa 26) e à demo (mesma regra das tarefas 17–20: não muda decisão, pesos, limiares nem contratos HTTP).*

- **Problema** (confirmado no código; não visto na tela):
  - O mesmo campo `Decision.score` chega em duas escalas, conforme a fonte:

    | Fonte | Onde entra | Valor | Escala |
    |---|---|---|---|
    | Persistida (`GET /api/anomalias`) | `src/domain.ts:16`, `parseDecisions` | `score` = `AnomaliaDetectada.Score` = nota ÷ 20 | **0–5** |
    | Diagnóstico (Intelligence Lab) | `src/domain.ts:45-46`, `fromDiagnostic` | `ensemble.normalizedScore` e `ensemble.threshold` | **0–100** |

  - Telas afetadas:
    - `TagCard.tsx:59`: cartão da tag. Mostra "Score · persistido" (0–5) ou "Score normalizado · ensemble" (0–100) **ao lado de "Threshold · ensemble"**. Nas decisões persistidas, o threshold aparece "—", porque a API pública não o informa.
    - `TagCard.tsx:80`: tabela de decisões, coluna "Score persistido".
    - `App.tsx:94-95`: exportação CSV, coluna `score`, com as duas escalas misturadas no mesmo arquivo (a coluna `fonte` diz qual é qual).
  - Na demo, um "2,1" perto de um limiar 60 sugere "muito longe de alarmar", quando a nota real é 42.
- **Fatos do backend que o plano usa** (confirmados no código):
  - `Services/AnomalyDetection/EnsembleDetectionService.cs:406-410`: `Score = scoreFinalNormalizado / 20.0`, `ScoreNormalizado = scoreFinalNormalizado`. Nas saídas antecipadas (entrada inválida, detectores bloqueados), `Score = 0`.
  - `PredictionService` grava `resultadoDetector.Score`; `AnomaliaHistoricoService.Registrar` é o único que cria `AnomaliaDetectada`.
  - O `/ 20.0` existe desde o primeiro commit do arquivo (`76852f9`, 28/07/2026), então todas as linhas persistidas estão na mesma escala. Os dados batem: as 131 anomalias do PK2 têm 3,0–3,73.
  - Diagnóstico (`PredictionDiagnosticCapture.cs:330-341`): `originalScore` = `Score` (÷ 20), `normalizedScore` = `ScoreNormalizado` (0–100) e `threshold` = métrica `limiteEnsemble`. O `EnsembleThreshold` é validado em (0, 100] (`AnomalyDetectionOptionsValidators.cs:20`; 60 no `appsettings.json`).
  - As notas por detector (`detectors[].normalizedScore`) também são 0–100.
- **Onde converter: no domínio (`src/domain.ts`)**, reaproveitando as duas funções que já são a única porta de entrada de cada fonte.
  - **Não no proxy** (`server/proxy.ts`): hoje ele só repassa e filtra o cliente (`proxy.ts:78-80`), e `proxy.test.ts` cobre isso. Converter ali mudaria o JSON que o navegador recebe em relação à API e misturaria responsabilidades.
  - **Não no componente:** a conversão ficaria espalhada em três lugares (cartão, tabela, CSV), sem teste. O projeto não tem testes de componente (não há Testing Library nem jsdom), e criá-los exige instalar dependência.
- **Proposta (pequena e reversível):**
  1. `src/domain.ts`: criar a constante `PERSISTED_SCORE_FACTOR = 20`, com comentário apontando `EnsembleDetectionService.cs:406-410` e o P30.
     - Em `parseDecisions`: `score = x.score × 20` (0–100) e um campo novo `storedScore = x.score`, o valor cru, só para auditoria e CSV.
     - Score ausente ou não finito continua `undefined`, **nunca 0**, para não inventar nota.
  2. `fromDiagnostic` continua usando `normalizedScore` e `threshold` sem conversão. Ela passa a guardar também `storedScore = ensemble.originalScore`.
     - **Conferência em tempo de execução:** se `|originalScore × 20 − normalizedScore| > 0,01`, marca a decisão (`scaleMismatch`). O cartão mostra um aviso discreto em vez de esconder a diferença.
     - Isso protege contra uma mudança futura do fator no backend, e o próprio diagnóstico vira a evidência de que o fator vale.
  3. `src/types.ts`: em `Decision`, deixar documentado que `score` e `threshold` estão em 0–100. Acrescentar `storedScore?` e `scaleMismatch?`.
  4. `src/TagCard.tsx`, só textos (os valores já chegam convertidos):
     - cartão: "Nota do ensemble (0–100)" e "Limiar do ensemble (0–100)", com uma linha "fonte: persistida / diagnóstico";
     - tabela: cabeçalho "Nota (0–100)".
     - **Limiar das decisões persistidas:** continua "—", com o texto "não informado por esta fonte". O monitor não inventa o 60. O limiar só aparece quando a fonte o devolve, e aí já está em 0–100.
     - **Não mexer** no bloco *shadow* multivariado (`TagCard.tsx:72`): é outra métrica, com limiar próprio, e já está rotulado como shadow.
  5. Exportação CSV: tirar a montagem das linhas de `App.tsx` para uma função pura em `domain.ts` (por exemplo, `decisionCsvRows`), para poder testar.
     - A coluna `score` vira `nota_0_100`, e entra `score_api_0_5` com o valor cru (vazio quando não houver).
     - **Isso muda o cabeçalho do CSV do monitor.** Não há leitor conhecido no repositório: o placar lê a exportação de `tools/Cp300Export`, não esta.
  6. `README.md` do LiveMonitor: uma linha em "Estados e correlação" e outra em "Limitações conhecidas" sobre a escala, apontando o P30.
- **Testes** (linha de base em 13/11: `npx vitest run`, 28 de 28 aprovados em 4 arquivos, todos offline; o `fetch` é simulado):

  | Arquivo | Situação | Mudança |
  |---|---|---|
  | `tests/domain.test.ts` (14) | **mudar 2** | "decisão persistida sem timestamp…" (`score: 0`) passa a verificar `score` 0 **e** `storedScore` 0. "usa a decisão final…" tem valores irreais (`normalizedScore: 0.9, threshold: 0.5`) e passa a usar valores do backend (`originalScore: 2.7, normalizedScore: 54, threshold: 60`) |
  | `tests/domain.test.ts` | **criar ~5** | (a) persistida 2,1 → 42 e 3,0 → 60, com `storedScore` preservado; (b) `score` nulo, NaN ou texto → `undefined`, não 0; (c) diagnóstico não é multiplicado (54 continua 54, limiar 60); (d) **a mesma leitura pelas duas fontes dá a mesma nota** (persistida 2,1 ≡ diagnóstico 42); (e) `scaleMismatch` só quando `originalScore × 20` diverge de `normalizedScore` |
  | `tests/domain.test.ts` ou novo `tests/csv.test.ts` | **criar 1–2** | cabeçalho com `nota_0_100` e `score_api_0_5`; linha persistida e linha de diagnóstico na mesma escala |
  | `tests/proxy.test.ts` (7) | **manter**; opcional +1 | Garantir que o proxy repassa `score` sem alterar (já implícito em "transmite apenas GET, filtra cliente…") |
  | `tests/polling.test.ts` (2), `tests/testRuns.test.ts` (5) | manter | Não usam `score` |
  | Componentes | não criar | Sem infraestrutura de teste de componente; os textos do `TagCard` são conferidos no `npm run build` (o `tsc` checa os tipos) e na tela |

- **Arquivos:** `tools/IAApi.LiveMonitor/src/domain.ts`, `src/types.ts`, `src/TagCard.tsx`, `src/App.tsx`, `tests/domain.test.ts` (e talvez `tests/csv.test.ts`), `README.md`. **Não toca** em `server/proxy.ts`, na IAApi, no `appsettings.json` nem na exportação `tools/Cp300Export`.
- **Atenção, patch pendente:** `IAApi_LiveMonitor_clareza_cenario1.patch` (raiz, não versionado) mexe em `App.tsx`, `TagCard.tsx`, `domain.ts`, `types.ts` e `tests/domain.test.ts`. Ele **não está aplicado e não aplica mais** (`git apply --check` falha nos três primeiros); parte dele já parece incorporada (`readingAtBasis`). Ele não muda a escala da nota. Decidir antes se ele será descartado ou refeito, para não implementar a 28 em cima de um conflito. **Atualização de 2026-11-16 (tarefa 21.1):** o patch está **inteiramente incorporado**. Ele só falhava no reverso porque o `sessoes_de_teste` foi aplicado por cima. A recomendação é descartá-lo, e a 28 não tem conflito com ele.
- **Risco:** baixo. É uma ferramenta local de leitura e não muda decisão, contrato nem dado gravado. O risco residual é o backend mudar o fator 20 no futuro; a conferência do item 2 e o teste (d) cobrem isso.
- **Esforço:** **P**, cerca de 2 a 3 h com os testes (~40–60 linhas de código e ~60 de teste).
- **Aceite:**
  - `npx vitest run` com 28 + os novos, todos passando; `npm run build` (com `tsc --noEmit`) sem erro;
  - na tela, com autorização para subir o monitor ligado à IAApi local: uma decisão persistida e o diagnóstico da mesma leitura mostram a **mesma nota 0–100**; nenhuma nota aparece em 0–5 perto do limiar 60;
  - no CSV exportado, `nota_0_100` coerente entre as fontes.
- **Rollback:** reverter o commit. Não há migração, dado nem configuração a desfazer.
- **Depende de:** nada no backend. Para a demo, vem depois das 19 e 20 (sinal de vida), mas é barata o bastante para entrar no mesmo pacote do LiveMonitor.

---

*Item 29 acrescentado em 2026-11-16, por decisão da responsável a partir da tarefa 21.2. Os `B3.23_*` não serão restaurados; o registro de achados é lido pelo git. Tarefa só de leitura. **Fase A concluída em 2026-11-16; fases B e C pendentes.***

### 29. (Conferência, só leitura) Os 30 achados da auditoria B3.23 contra o código atual — **segurança (fase A) = PRIORIDADE ALTA: antes de qualquer demo para fora da equipe**
- **Andamento:** 🟡 **fase A (segurança) ✅ concluída em 16/11; fases B e C ⏳ pendentes.**
  - **Resumo da fase A:** os 8 achados **continuam presentes** (o B323-23 em parte). Viraram **P33 a P40** no `PROBLEMAS_ENCONTRADOS.md`, cada um com a correção mínima proposta e o esforço. **Antes de qualquer demo para fora da equipe, tratar pelo menos P33 (`/api/dev/reset`) e P34 (sem autenticação)**; a mitigação de P34 para a demo é manter a API só em `localhost` (esforço P). **Atualização:** P33 corrigido, P34 mitigado e P39 corrigido em parte pela **tarefa 30** (16/11, sem commit). ~~P35 a P38~~, P40 e o resto de P34/P39 continuam abertos. **Atualização:** P35 a P38 corrigidos pela **tarefa 31** (16/11, sem commit).
  - **Como foi feito:** leitura do código no HEAD `05c64c3`; a API não foi iniciada e nenhum endpoint foi chamado. Para o B323-23, a responsável **autorizou** `dotnet list package --vulnerable --include-transitive` (13 projetos) e `npm audit` no LiveMonitor; ambos só consultam, nada foi instalado nem alterado.

  | Achado | Situação | Evidência (HEAD `05c64c3`) | Nível | Destino |
  |---|---|---|---|---|
  | B323-01 | **Ainda presente** | `Controllers/DevController.cs:14-35`; `Program.cs:44` (sem condição de ambiente) | Confirmado no código | **P33** |
  | B323-02 | **Ainda presente** | `Program.cs:7-44` sem autenticação; ex.: `AnomaliaController.cs:81-86`, `EquipamentoController.cs:51-61`, `MotivosIAController.cs:67-100`. Só o Intelligence Lab tem guarda (`IntelligenceLabLocalReadAccess.cs:35-58`) | Confirmado no código | **P34** |
  | B323-03 | **Ainda presente** | `Controllers/TagRelacoesController.cs:23-43` | Confirmado no código | **P35** |
  | B323-04 | **Ainda presente** | `Controllers/EquipamentoController.cs:72-73`, `:103` | Confirmado no código | **P36** |
  | B323-05 | **Ainda presente** | `Controllers/ProcessoEquipamentoController.cs:24-44` | Confirmado no código | **P37** |
  | B323-06 | **Ainda presente** | `Controllers/FeedbackAnomaliaController.cs:34-35` | Confirmado no código | **P38** |
  | B323-17 | **Ainda presente** | `LocalLlmAnomalyInterpretationService.cs:75-82`; `LmStudioLlmProvider.cs:96-101`; `AnomaliaController.cs:33-59` (stack trace na resposta) | Confirmado no código | **P39** |
  | B323-23 | **Ainda presente, em parte** | SQLite 2.1.11 (alta) na API e em 5 projetos; Xml/Pkcs 10.0.0 (alta) só em testes e 2 ferramentas (**na API, corrigido** pela poda do pacote); `tinypool` (crítica) e `vitest` (moderada) só em desenvolvimento no LiveMonitor | Confirmado nas consultas de vulnerabilidade | **P40** |
  | B323-07 a 16, 18 a 22, 24 a 30 | ⏳ fases B e C | — | — | — |
- **Problema:** a auditoria B3.23 (set/2026, 147 testes) registrou 30 achados (B323-01 a B323-30). Os arquivos foram removidos em `15f1ca9`, e vários achados não aparecem em nenhum documento atual. Pela amostra de 16/11 (tarefa 21.2):
  - o B323-07 já foi corrigido;
  - o B323-08 continua presente (`RegimeEvidenceAdapter.cs:186-200`);
  - o B323-01 (`POST /api/dev/reset`, `DevController.cs:14`) parece continuar exposto, a validar.

  Sem a conferência, a demo pode mostrar uma API com falhas de segurança conhecidas, e um defeito de detecção fica sem registro.
- **Fonte:** `git show 15f1ca9^:Docs/B3.23_CODEX_AUDIT_RESULT.md`. A tabela está em §18 ("Findings Register"); o detalhe de cada achado, nas seções 5 a 17. Os links do relatório apontam para outra pasta (`IA MARRARI/IAApi/`) e números de linha antigos: **localizar pelo símbolo**, não pela linha.
- **Ordem obrigatória:**

  | Fase | Achados | Tema (como descrito na B3.23) |
  |---|---|---|
  | **A: segurança (prioridade alta)** | B323-01 | Sem autenticação; `POST /api/dev/reset` global |
  | | B323-02 | Endpoints globais sem autenticação (exposição ou modificação entre clientes) |
  | | B323-03 | `TagRelacoesController` filtra só por `TagName` (leitura de dados de outro cliente) |
  | | B323-04 | Vínculo equipamento ↔ tag não compara clientes |
  | | B323-05 | Processo ↔ equipamento vinculados só por IDs |
  | | B323-06 | Motivo de feedback validado só por ID |
  | | B323-17 | Resposta bruta do LLM, corpo de erro e stack trace devolvidos ou logados (vazamento de dado industrial) |
  | | B323-23 | Dependências com alertas de severidade alta |
  | **B: detecção** | B323-08 | Envelope do Increasing Noise calculado e não usado na supressão do ROC (um spike real pode ser escondido) |
  | **C: demais** | B323-07, 09–16, 18–22, 24–30 | Bug pós-gap (corrigido na amostra; confirmar), persistência e contrato da API, multivariado fora do fluxo, configuração, observabilidade, encoding, código morto. Correspondências já conhecidas: **B323-09 ↔ tarefa 5**, **B323-21 ↔ tarefa 22 (P28)**, **B323-29 ↔ tarefas 6 e 19**, **B323-20 ↔ P31/tarefa 25 (a confirmar)** |

- **Para cada achado, registrar:**
  - **situação:** **ainda presente**, **corrigido**, **não se aplica** (o código mudou de forma que o achado perdeu sentido) ou **inconclusivo** (só por leitura não dá para decidir; dizer o que faltaria);
  - **evidência:** `arquivo:linha` no HEAD conferido, com o commit anotado. No caso de "corrigido", também o teste que cobre a correção, se houver;
  - **nível da evidência:** confirmado no código, confirmado em teste/log ou histórico a validar (regra do `CLAUDE.md`);
  - **destino:**
    - P novo, para os presentes que ainda não têm registro;
    - P ou tarefa existente, quando já houver (citar);
    - nenhum, para corrigidos ou que não se aplicam.
- **Saída:**
  1. Tabela com os 30 achados neste item, ou em `Docs/CONFERENCIA_B323.md` (decidir na aprovação). Essa tabela substitui a lista da B3.23 como referência viva; o relatório antigo fica só no git.
  2. **Cada achado confirmado como presente vira problema no `PROBLEMAS_ENCONTRADOS.md`** (a partir do P33), no formato atual:
     - o que acontece e evidência;
     - impacto;
     - origem (`B323-xx`);
     - "BACKLOG: não há item", até haver proposta aprovada.
  3. Resumo da fase A **antes** de começar a B. Se houver achado de segurança presente, avisar a responsável na hora, sem esperar o fim da tarefa.
- **Restrições:**
  - **só leitura;** nenhum código, configuração ou dependência alterado. Nenhuma correção nesta tarefa; cada uma vira tarefa própria, com aprovação;
  - **não executar nada para provar falha de segurança:** não subir a API, não chamar `/api/dev/reset` nem outros endpoints, não montar requisição entre clientes. A conferência é por leitura do código, do `Program.cs` (pipeline, `MapControllers`, `UseAuthentication`/`UseAuthorization`, condições de ambiente) e da configuração;
  - **B323-23:** `dotnet list package --vulnerable` consulta o nuget.org, ou seja, manda nomes e versões de pacotes para fora, e **exige autorização antes de rodar**. Sem ela, conferir só por leitura dos `.csproj` e do `obj/project.assets.json` e marcar como "inconclusivo: falta consulta de vulnerabilidades";
  - **B323-17:** ao citar trechos de log ou de resposta, não reproduzir dados industriais nem segredos;
  - testes automatizados só se forem comprovadamente offline (regra do `CLAUDE.md`), e só para mostrar que um teste de correção existe e passa.
- **Risco:** baixo (leitura e documento). **Esforço:** M (fase A: P; fase B: P; fase C: M).
- **Aceite:**
  - os 30 achados na tabela, na ordem A → B → C, cada um com situação, `arquivo:linha`, nível da evidência e destino;
  - todo achado presente tem P no `PROBLEMAS_ENCONTRADOS.md` ou aponta para um registro existente;
  - nenhum arquivo de código, configuração ou dependência alterado (`git status` só com `Docs/`).
- **Rollback:** reverter o commit de documentação.
- **Depende de:** nada. **Prioridade:**
  - a **fase A** vem antes de qualquer demo para fora da equipe (tarefas 17–20 e 28);
  - as fases B e C seguem a ordem normal. O B323-08 entra junto com a investigação de detecção (prioridade 2 do `CLAUDE.md`), sem passar à frente da tarefa 25.

### 30. (Segurança, demo) Correções mínimas de P33, P39 e mitigação de P34 — ✅ **CONCLUÍDA (2026-11-16, sem commit)**
- **Origem:** tarefa 29, fase A. **Aprovada pela responsável em 16/11:** P33 (reset só em Development e local, com a mesma proteção do Intelligence Lab); P39 (erro genérico sem stack trace, resposta bruta da LLM só em Debug, erro da LLM só com o código HTTP); P34 (o `Start-IAApiComLog.ps1` recusa endereço que não seja local). Sem mudar nenhuma decisão de anomalia; sem commit.
- **Arquivos alterados:**
  - **P33:** `Controllers/DevController.cs`; `Controllers/LocalDevelopmentRequestGuard.cs` (**novo**); `Controllers/IntelligenceLabLocalReadAccess.cs`.
    - Por que um arquivo novo: é a regra "Development + loopback" **extraída** do `IntelligenceLabLocalReadAccess`, que passa a chamá-la. Não é uma proteção paralela: existe uma só regra, usada pelas duas guardas.
  - **P39:** `Controllers/AnomaliaController.cs`; `Services/AnomalyDetection/Interpretation/LocalLlmAnomalyInterpretationService.cs`; `src/IAApi.Infrastructure/Llm/Providers/LmStudioLlmProvider.cs` e `OllamaLlmProvider.cs`. O Ollama não está registrado na DI, mas tinha o mesmo vazamento; o ajuste evita que ele volte se o provedor for ligado.
  - **P34:** `tools/Start-IAApiComLog.ps1` (função `Test-EnderecoLocal` e checagem como primeira ação).
  - **Testes novos:**
    - `IAApi.Tests/Security/DevControllerResetGuardTests.cs` (8);
    - `IAApi.Tests/Security/SensitiveDataExposureTests.cs` (6);
    - `tools/Start-IAApiComLog.Tests.ps1` (25 verificações). É o primeiro teste de script do repositório, porque não havia infraestrutura para isso (o Pester 3.4 do Windows não é usado). Ele lê o script pela árvore sintática **sem executá-lo**.
- **Efeito na detecção:** nenhum. A interpretação por LLM é chamada depois da decisão final (`PredictionDecisionOrchestrationService.cs:117-147`), e só mudou o que vai para o log e para o corpo de erro HTTP. O replay confirma (abaixo).
- **Mudanças de contrato:**
  - o corpo de erro `400` de `POST /api/anomalias/tag` perdeu o campo `stack`, e `erro` passou a ser uma mensagem fixa. Nada no repositório lia esses campos;
  - `POST /api/dev/reset` responde `404` fora de Development e local.
- **Execução (16/11, tudo offline; a API não foi iniciada e o Bridge não foi acessado):**
  - **Testes relacionados:** `dotnet test --filter` com `IAApi.Tests.Security` e os testes de endpoint do Intelligence Lab: **53 aprovados, 0 falhas**. São 14 novos e 39 do Intelligence Lab, que provam que a guarda extraída não mudou o comportamento.
  - **Script:** `tools/Start-IAApiComLog.Tests.ps1`: **25 de 25**. Na primeira rodada, o teste pegou um defeito: o .NET Framework expande `[::1]` (corrigido comparando como endereço IP). O script real com `-Url http://0.0.0.0:5145` foi recusado sem criar arquivo.
  - **Compare-Replay**, só as 6 execuções originais, sobre `dados/cp300_10a11nov_derivado.csv` (as `rc0_*` ficaram de fora; ver "Pendências"):
    - **0 diferenças em 6 execuções** (CSV e log iguais, referência do commit `d89eb0c`);
    - saídas em `dados/rc2_rodadas/20261116_111201`;
    - a rodada `20261116_111137` não vale: 0 execuções, ver "Pendências".
  - **Suíte completa:** **1024 aprovados, 0 falhas**.
  - Sem avisos de compilação novos nos arquivos alterados. Continuam os avisos NU1902/NU1903 de pacotes (P40).
- **Ficou de fora** (registrado em P34 e P39):
  - autenticação de verdade (P34, esforço G);
  - o `Console.WriteLine` com pedido e resposta inteiros em `AnomaliaController.cs:33-47`;
  - `ex.Message` em 24 pontos dos controllers;
  - P35 a P38 e P40.
- **Rollback:** reverter os arquivos listados e apagar os 3 arquivos de teste e o `LocalDevelopmentRequestGuard.cs`. Nada foi commitado.
- **Pendências abertas nesta tarefa:**
  - ✅ **Referência `rc0_*` do `Compare-Replay.ps1` a conferir e regenerar.** **Feito em 16/11, depois do fim da exportação (detalhe no fim deste item).** Em 16/11, a exportação estava acrescentando dados ao `dados/cp300_09a13nov.csv`, a origem do `dados/cp300_09a13nov_derivado.csv` usado pelas 6 execuções `rc0_*`. Por isso, a tarefa 30 rodou o Compare-Replay **só com as 6 execuções originais** (`dados/cp300_10a11nov_derivado.csv`). **Quando a exportação terminar:**
    1. regenerar o derivado;
    2. conferir que as `rc0_*` dão resultado **idêntico** no trecho que usam. O arquivo de entrada muda (o SHA-256 do manifesto deixa de bater e o script sai com código 2), mas o trecho 09/11 12:30 → 13/11 07:59 deve ser o mesmo;
    3. regenerar a referência dessas 6 execuções (`-Gerar -Sobrescrever`, que hoje regrava as 12; registrar o commit). Sem isso, as `rc0_*` não servem de regressão.
    - **Resultado (16/11, pedido da responsável; tudo offline, nenhum código alterado):**
      - **CSV novo** (`dados/cp300_09a13nov.csv`):
        - vai de 09/11 12:30 a **13/11 22:59**, com 43 tags, 273.996 linhas e 6.372 minutos; os 2.924 pedaços estão `ok`;
        - as 235.382 linhas do CSV antigo (recuperadas das linhas originais do derivado) estão **todas no novo, idênticas**;
        - não há linha nova dentro do trecho antigo, nem duplicata (tag, minuto). A exportação só acrescentou 38.614 linhas **depois** de 13/11 07:59.
      - **Derivado:**
        - o `rc0_derivar.py` (scratchpad da sessão da RC-0, fora do repositório) foi validado antes de uso: rodado sobre o CSV antigo recuperado, reproduziu **byte a byte** o derivado anterior (SHA-256 `41e47407…`);
        - o derivado novo (`8736ce8e…`) tem as 268.226 linhas até 13/11 07:59 **idênticas e na mesma ordem**, mais 44.002 depois.
      - **Conferência antes de regenerar:**
        - as 6 `rc0_*` rodaram sobre o derivado novo, a partir do scratchpad e sem tocar em `dados/` nem no manifesto, com os argumentos do manifesto e a mesma comparação do script;
        - resultado: **0 diferenças em 6** (CSV por SHA-256, log linha a linha).
      - **Regeneração só das `rc0_*`:**
        - o `-Gerar -Somente` grava um manifesto **só com as execuções selecionadas**. Um `-Gerar -Sobrescrever -Somente rc0_…` no `manifesto.json` apagaria as 6 originais;
        - por isso, o script gerou com `-Manifesto dados\rc2_referencia\manifesto_rc0.json`, no commit **`9cd4983`** e sem mudanças locais. Assim, só os arquivos `rc0_*` da pasta da referência foram regravados, e as 6 entradas foram juntadas ao `manifesto.json`. O manifesto temporário foi apagado;
        - nas 6 entradas `rc0_*`, só mudou `entrada_sha256` (`41e47407…` → `8736ce8e…`). Os 12 arquivos `rc0_*.csv`/`.log` regravados são byte a byte iguais aos anteriores;
        - cada entrada `rc0_*` ganhou `referencia_commit`, `referencia_gerada_em` e `referencia_mudancas_locais`, campos que o script ignora. O topo do manifesto e as 6 entradas originais ficaram intactos: o topo continua com o commit `d89eb0c`, que é o das originais.
      - **Aceite:** `Compare-Replay.ps1` normal, com as **12** execuções: **0 diferenças em 12**, código 0 (`dados/rc2_rodadas/20261116_130537`). As `rc0_*` voltam a servir de regressão.
      - **Backup e rollback:** `dados/rc2_referencia_backup_20261116/` guarda o manifesto anterior, os 12 arquivos `rc0_*` e o derivado anterior (`cp300_09a13nov_derivado.ate_20261113_0759.csv`). Para voltar, basta copiá-los de volta para `dados/rc2_referencia/` e `dados/cp300_09a13nov_derivado.csv`.
      - **Atenção:** o derivado agora cobre até 13/11 22:59. Os números de minutos e de estados da RC-0 (`PLANO_REGIME_COMBINADO.md` §5.7) descrevem o trecho até 07:59.
  - ✅ **`Compare-Replay.ps1` aceitava 0 execuções como sucesso. Corrigido em 16/11 (aprovado pela responsável, sem commit).**
    - **O problema:** `powershell -File … -Somente a,b,c` passava a lista como um único texto. Nenhuma execução casava, e o script terminava com "0 diferenças em 0 execuções" e **código 0**. A rodada foi refeita com `-Command` e uma lista de verdade.
    - **A correção**, só em `tools/Cp300OfflineReplay/Compare-Replay.ps1`:
      - o `-Somente` aceita array **ou** texto separado por vírgula (inclusive misturados, com espaços);
      - a função `Selecionar` aplica o filtro nos dois modos (comparar e `-Gerar`) e sai com **código 2**, com a mensagem "Nenhuma execucao selecionada; nada foi rodado", o que foi pedido e as execuções disponíveis, quando não sobra nenhuma;
      - um `-Somente` só com vírgulas ou espaços também sai com código 2;
      - a ajuda (`.EXAMPLE` e códigos de saída) foi atualizada.
    - **Testes de uso** (o script chamado de verdade; nenhum roda execução):

      | Caso | Chamada | Código |
      |---|---|---|
      | A | `-File -Somente nao_existe` | 2 |
      | B | `-File -Somente "nao_existe,outro"` | 2 |
      | C | `-Command -Somente @('nao_existe','outro')` | 2 |
      | D | `-Command -Somente nao_existe,outro` | 2 |
      | F | `-File -Somente " , "` | 2 |
      | G | `-Command -Gerar -Manifesto <scratchpad> -Somente @('nao_existe')` | 2 |

      - No caso G, nenhuma pasta foi criada.
      - Em todos os casos, `dados/rc2_referencia/manifesto.json` ficou intacto (SHA-256 igual) e nenhuma pasta nova apareceu em `dados/rc2_rodadas`.
      - **Caso E**, `-File -Somente ""`: código **1**. O Windows PowerShell 5.1 descarta o `""` ao montar a linha de comando, e o próprio PowerShell recusa ("Missing an argument for parameter 'Somente'") antes de o script começar. Nada roda, e o erro não se passa por aprovação. Não dá para tratar dentro do script.
    - **Rodadas completas das 6 execuções originais**, com a referência do commit `d89eb0c`; as `rc0_*` ficaram de fora pela pendência acima:
      - `-File -Somente "preset1_sem,preset1_rec,madrugada_sem,madrugada_rec,preset6_sem,preset6_rec"`: **0 diferenças em 6 execuções**, código 0 (`dados/rc2_rodadas/t30_file`);
      - `-Command -Somente @('preset1_sem', …, 'preset6_rec')`: **0 diferenças em 6 execuções**, código 0 (`dados/rc2_rodadas/t30_command`).
    - **Rollback:** reverter `tools/Cp300OfflineReplay/Compare-Replay.ps1`.
    - ✅ **Complemento aprovado e feito em 16/11: nome desconhecido no `-Somente`.**
      - **Antes:** um erro de digitação junto com nomes certos rodava só as execuções que casavam.
      - **Agora:** qualquer nome que não exista no manifesto (ou na lista padrão, no `-Gerar`) faz o script sair com **código 2**, sem rodar nada. A mensagem diz "Nome(s) desconhecido(s) no -Somente: …; nada foi rodado. Execucoes disponiveis: …", com cada nome desconhecido uma vez só.
      - A comparação de nomes não diferencia maiúsculas de minúsculas, como antes.
      - **Testes de uso:**

        | Caso | Chamada | Código |
        |---|---|---|
        | H | `-File -Somente "preset1_sem,preset6_se"` | 2 (lista `preset6_se` e as 12 disponíveis) |
        | I | `-Command -Somente @('preset1_sem','preset6_se','xpto','xpto')` | 2 (lista `preset6_se, xpto`) |
        | J | `-Command -Gerar -Manifesto <scratchpad> -Somente @('preset1_sem','nao_existe')` | 2, sem criar pasta |

        - Os casos A, C, D e F repetidos dão 2. A, C e D agora caem na mensagem de nome desconhecido.
        - O manifesto da referência ficou intacto, e nenhuma pasta nova apareceu em `dados/rc2_rodadas`.
      - **6 execuções originais de novo, referência `d89eb0c`:**
        - `-File`, lista como texto: **0 diferenças em 6**, código 0 (`dados/rc2_rodadas/t30b_file`);
        - `-Command`, lista como array: **0 diferenças em 6**, código 0 (`dados/rc2_rodadas/t30b_command`).

### 31. (Segurança, isolamento entre clientes) Correções mínimas de P35 a P38 — ✅ **CONCLUÍDA (2026-11-16, sem commit; P35 completado pela opção (a))**
- **Origem:** tarefa 29, fase A. **Pedida pela responsável em 16/11:** a correção mínima proposta em cada um, com testes que falham antes e passam depois. No P35, a mudança no contrato HTTP tinha de ser aditiva ou compatível. Sem mudar nenhuma decisão de anomalia; sem commit.
- **Consumidores conferidos antes:** nada no repositório (LiveMonitor, `tools/`, `IAApi.http`, serviços) chama os 4 endpoints. Os serviços do runtime (`ProcessoAnaliseService`, `FeedbackAnomaliaAnaliseService`) só **leem** essas tabelas, e essas leituras não mudaram.
- **Arquivos alterados:**
  - **P35:** `Controllers/TagRelacoesController.cs`: parâmetro **opcional** `?clienteId=`. Com ele, filtra `ClienteId` nas duas consultas. Sem ele, a resposta é a de antes, com o mesmo corpo e os mesmos campos, **exceto** quando a tag existe em mais de um cliente: aí responde `400` (opção (a), ver o complemento abaixo).
  - **P36:** `Controllers/EquipamentoController.cs` (`VincularTag`): `400` quando `request.ClienteId` difere do cliente do equipamento, ao criar e ao atualizar.
  - **P37:** `Controllers/ProcessoEquipamentoController.cs`: `400` quando o processo e o equipamento são de clientes diferentes.
  - **P38:** `Controllers/FeedbackAnomaliaController.cs`: o motivo tem de ser do cliente da anomalia e estar ativo. A resposta de recusa é igual à de motivo inexistente.
  - **Testes:**
    - `IAApi.Tests/Security/ClienteIsolamentoTests.cs` (**novo**, 13 casos de P35 a P37). Não havia teste desses 3 controllers.
    - `IAApi.Tests/FeedbackAnomaliaConsistencyTests.cs` (**estendido**, +4 casos de P38; já testava esse controller).
- **Efeito na detecção:** nenhum. Nenhum caminho de leitura do runtime mudou; só passaram a ser recusadas gravações inconsistentes, e os dados já gravados não foram tocados. O feedback (P38) é posterior à decisão. O replay confirma (abaixo).
- **Mudanças de contrato:**
  - `GET /api/tags/{tagName}/relacoes`: parâmetro opcional (aditivo), mais o `400` da opção (a), só para tag sem `clienteId` em mais de um cliente;
  - `POST /api/equipamentos/{id}/tags`: `400` novo para `ClienteId` vazio ou diferente do cliente do equipamento, que antes era gravado;
  - `POST /api/processo-equipamentos`: `400` novo para clientes diferentes;
  - `POST /api/anomalias/{id}/feedback`: o `400` que já existia agora também vale para motivo de outro cliente ou inativo, com a mesma mensagem.
- **Execução (16/11, tudo offline; a API não foi iniciada e o Bridge não foi acessado). Antes, conferi que os testes não usam banco real, rede nem `WebApplicationFactory`:**
  - **Antes da correção:**
    - com os controllers do HEAD, os testes do P35 **não compilavam** (CS1501: o parâmetro não existia);
    - com só a assinatura do P35 acrescentada (parâmetro ignorado) e P36 a P38 sem correção, **9 falhas e 9 aprovados** de 18. Falharam exatamente os 9 testes do defeito: 3 de P35, 3 de P36, 1 de P37 e 2 de P38. Passaram os 9 de compatibilidade.
    - Numa primeira rodada, houve uma 10ª falha, culpa do teste e não do código: o JSON escapava o "ã". Corrigido lendo o campo `erro`.
  - **Depois da correção:**
    - os 18 testes passam;
    - relacionados (`ClienteIsolamentoTests`, `FeedbackAnomaliaConsistencyTests`, `IAApi.Tests.Security` e Intelligence Lab): **246 aprovados, 0 falhas**.
  - **Compare-Replay**, só as 6 execuções originais, sobre `dados/cp300_10a11nov_derivado.csv`, com a referência do commit `d89eb0c`: `-File -Somente "preset1_sem,preset1_rec,madrugada_sem,madrugada_rec,preset6_sem,preset6_rec"` deu **0 diferenças em 6 execuções**, código 0 (`dados/rc2_rodadas/20261116_114822`). As `rc0_*` ficaram de fora pela pendência da tarefa 30: a exportação ainda grava em `dados/cp300_09a13nov.csv`.
  - **Suíte completa:** **1041 aprovados, 0 falhas** (1024 + 17 novos).
- ✅ **Complemento aprovado e feito em 16/11: P35, opção (a).**
  - **Antes:** sem `clienteId`, a resposta misturava as relações de todos os clientes.
  - **Agora:** sem `clienteId` e com a tag em **mais de um cliente**, contando `ProcessoTagsIa` e `EquipamentoTagsIa` juntas, a resposta é `400` com `"A tag existe em mais de um cliente. Informe o cliente em ?clienteId=."`. Ela não traz relações nem cita os clientes. Com um cliente só, ou sem relações, a resposta é igual à de hoje. Com `clienteId`, nada muda.
  - **Arquivo:** `Controllers/TagRelacoesController.cs`.
  - **Testes:** em `ClienteIsolamentoTests.cs`, o caso "sem `clienteId`, dois clientes, resposta igual" deu lugar a 2 casos de `400` (tag nos dois clientes; processo de um e equipamento de outro), e entraram 2 de compatibilidade (processo e equipamento do mesmo cliente; tag sem relações). Agora são 8 casos `P35_*`.
    - **Antes da mudança:** 2 falhas (exatamente os 2 de `400`, que recebiam `200` com a mistura) e 6 aprovados.
    - **Depois:** 8 de 8.
  - **Execução (offline; a API não foi iniciada):**
    - relacionados (`ClienteIsolamentoTests`, `FeedbackAnomaliaConsistencyTests`, `IAApi.Tests.Security` e Intelligence Lab): **250 aprovados, 0 falhas**;
    - **Compare-Replay**, só as 6 execuções originais, com a referência do commit `d89eb0c`: **0 diferenças em 6**, código 0 (`dados/rc2_rodadas/20261116_115609`);
    - suíte completa: **1045 aprovados, 0 falhas**.
  - **Rollback:** reverter o bloco de ambiguidade em `Controllers/TagRelacoesController.cs` volta à primeira etapa (filtro opcional); reverter o arquivo inteiro volta ao HEAD.
- **Ficou de fora:**
  - procurar no banco vínculos e feedbacks **já gravados** entre clientes diferentes (exige acesso autorizado ao banco);
  - autenticação (P34). Sem ela, o `clienteId` delimita o escopo, mas não é controle de acesso.
- **Rollback:** reverter os 4 controllers e `IAApi.Tests/FeedbackAnomaliaConsistencyTests.cs`, e apagar `IAApi.Tests/Security/ClienteIsolamentoTests.cs`. **Atualização:** a responsável commitou a tarefa em `9cd4983` (16/11); o rollback passa a ser reverter esse commit.

### 32. (Ferramenta) `Compare-Replay.ps1 -Gerar -Somente` apaga do manifesto as execuções não selecionadas — ⏳ **ABERTA (registrada em 2026-11-16; não implementar sem aprovação)**
- **Problema** (confirmado no código, `tools/Cp300OfflineReplay/Compare-Replay.ps1`): no `-Gerar`, o script seleciona as execuções (`Selecionar`, linha 199), roda só essas e grava o manifesto **inteiro** com elas (`WriteAllText`, linha 234).
  - Com `-Sobrescrever -Somente rc0_…` no `dados\rc2_referencia\manifesto.json`, as 6 execuções originais **sairiam do manifesto**. Os arquivos delas continuariam na pasta, mas a comparação deixaria de rodá-las, **sem aviso**.
  - Foi notado ao regenerar a referência `rc0_*` (tarefa 30, "Pendências", 16/11). Lá, o contorno foi gerar num manifesto separado (`-Manifesto dados\rc2_referencia\manifesto_rc0.json`) e juntar as entradas à mão.
- **Risco:** perder parte da regressão sem perceber. A rodada seguinte diria "0 diferenças em 6 execuções", e não 12.
- **Comportamento seguro proposto:**
  - **`-Gerar -Somente`, com manifesto existente** (exige `-Sobrescrever`, como hoje): ler o manifesto, regravar só os arquivos e as **entradas selecionadas** e manter as demais sem mudança, na mesma ordem.
    - Cada entrada atualizada ganha `referencia_commit`, `referencia_gerada_em` e `referencia_mudancas_locais`, os mesmos campos usados à mão em 16/11.
    - O topo do manifesto (`commit`, `gerado_em`, `entrada`, `entrada_sha256`, `appsettings_sha256`) fica como está.
    - Se o `appsettings.json` atual for diferente do registrado, avisar (como a comparação já faz), porque as entradas passariam a vir de configurações diferentes.
  - **`-Gerar` sem `-Somente`, ou manifesto inexistente:** sem mudança (gera tudo do zero).
  - O resumo deve dizer quantas entradas foram atualizadas e quantas mantidas.
- **Aceite:**
  - sobre uma **cópia** do manifesto no scratchpad: `-Gerar -Sobrescrever -Somente <2 nomes>` mantém as outras 10 entradas idênticas (JSON comparado entrada a entrada) e atualiza só as 2;
  - `-Gerar` sem `-Somente` sobre manifesto novo dá o mesmo formato de hoje;
  - os casos de uso da tarefa 30 (nome desconhecido, seleção vazia: código 2, sem criar nada) continuam iguais;
  - `Compare-Replay.ps1` com as 12 execuções dá 0 diferenças, e `dados/rc2_referencia/manifesto.json` fica intacto durante os testes (SHA-256 antes e depois).
- **Esforço:** P. **Efeito na detecção:** nenhum (só a ferramenta de regressão). **Rollback:** reverter `tools/Cp300OfflineReplay/Compare-Replay.ps1`.

### 33. (Correção, proposta) Detectores de qualidade do sinal: valor congelado (33.C) e ruído excessivo (33.R) — ⏳ **ABERTA (registrada em 2026-11-16)**; ✅ **passo 1 concluído (2026-11-16, sem commit)**; passos 2 a 6 não aprovados

Detalha a tarefa **10** (P12) e trata o P07 para esses dois casos **sem mexer nos pesos** (tarefa 7). A proposta saiu da leitura do código e de uma checagem offline no CSV do simulador. A IAApi não foi chamada.

> **Decisões da responsável (2026-11-16):** (1) quando o "OU" dispara, nota final = **máximo entre a média e a nota do detector** (opção (a) da 33.1); (2) orçamento de falsos da 33.R aprovado como sugerido (≤ 10 minutos-tag nas 18 h de 12/11 e ≤ 4 fora das falhas no preset 3); (3) P12 atualizado com os achados da 33.0; (4) **passo 1 aprovado e feito** (ver 33.P1).

#### 33.0 O que já existe (regra "reaproveitar antes de criar")

| O que | Onde | Situação |
|---|---|---|
| Lógica de congelamento | `src/IAApi.Engine/Runtime/Regime/Evidence/FrozenSensorEvidenceCalculator.cs` (B3.22.4). Cauda de valores idênticos com ≥ `FrozenMinimumPoints` (10) e ≥ `FrozenMinimumMinutes` (10). Antes dela, nos 30 pontos anteriores, a taxa de passos iguais tem de ser ≤ 0,60. Exige sinal `Continuous` | Confirmado no código. **Sem estado**: dá para chamar de dentro de um detector |
| Lógica de ruído | `.../Evidence/IncreasingNoiseEvidenceCalculator.cs` (B3.22.5). Compara a escala robusta (MAD) dos últimos 30 pontos com uma referência. Exige razão ≥ 3 e centro estável (≤ 2 escalas) | Confirmado no código. **Com estado**: a referência fica presa ao `ActiveRegimeId` do `RegimePolicyEngine`, que é um regime interno, não o regime operacional TAG42/43/46. Mede a dispersão dos **valores**, então também reage a variação lenta de demanda |
| Uso dessas lógicas | `RuntimeFunctionalDecisionContextProvider` → `RuntimeFunctionalDecisionCandidateFactory.Create` (`FrozenSensor` ⇒ `EmitAnomaly`; `IncreasingNoise` ⇒ `NewAnomaly`) | Confirmado no código. **Só o canário PK7** (`RuntimeDecision.AllowedClientIds`). No PK2 e no replay (`Enabled = false`), o contexto é calculado e descartado |
| Família | `DetectorFamily.DataQuality = 7` ("qualidade, ausência, congelamento…") | Existe e ninguém usa |
| Precedente de regra fora da média | `EnsembleDetectionService.Detectar`, `ExperimentalPersistenceTemporalConfirmationEnabled` (desligada por padrão, exclui resultados da soma com `AggregationExclusionReasonCode`) | Confirmado no código |

**Por que não basta ligar o canário runtime no PK2:** isso ligaria de uma vez a recuperação de falso negativo, a política de regime e o baseline duplo, o que não é um passo pequeno nem reversível. Além disso, **o calculador de congelamento não dispararia na falha 20**: no replay `dados/p3b_{sem,combs}.csv`, a TAG24 tem `tipo_tag = Unknown` (confiança 0) nos 480 minutos, inclusive nos 30 da falha, e o calculador devolve `signal-not-continuous` (confirmado no replay e no código; o adaptador recebe a mesma entrada quando não há gap crítico). Só TAG22 e TAG25 saem `Continuous`.

**Por que não basta um detector a mais na média (P07):** com os pesos do `appsettings.json` (soma 0,75 sem IF), um detector novo saturado (100) com todos os outros em 0, que é o caso do congelamento no set-point, dá no máximo `100·w/(0,75 + w)` = **57,1 com w = 1**, o máximo que o validador aceita. Nunca chega a 60. Pior: em leituras normais, ele entraria na média com nota 0 e **diluiria todas as detecções atuais**. Exemplo: Z + MAD + EWMA saturados, que hoje dão 60, cairiam para 50 com w = 0,15, e a TAG29 do preset 6 (60,9) deixaria de alarmar. Cálculo sobre o código; não rodado.

#### 33.1 Desenho comum aos dois (como entra no ensemble sem diluir)

- **Evidência independente, fora da média.** Novo valor `DetectorEvidenceRole.IndependentEvidence` e família `DataQuality`. No `EnsembleDetectionService`, resultados com esse papel:
  - **nunca** entram em `somaPesos`/`somaScoresPonderados`. Ficam em `DetalhesDetectores` com `AggregationExclusionReasonCode = ExcludedIndependentEvidence`. Assim a nota dos outros detectores não muda em nenhuma leitura;
  - quando um deles confirma (`EhAnomalia = true`), a decisão final é anomalia, um **"OU"** com a média. Fica `AggregationPolicy = "WeightedMeanOrIndependentEvidence"`, mais as métricas `evidenciaIndependente = 1` e o nome do detector;
  - **só podem acrescentar alarmes, nunca removê-los** (aceite: nenhum minuto passa de 1 para 0).
- **Nota final quando o "OU" dispara** (decisão da responsável): (a) `max(média, nota do detector)`, que mantém a regra "nota ≥ limiar ⇔ alarme" usada no LiveMonitor (tarefa 28). Ou (b) manter a média e expor a nota do detector só nos detalhes. **Recomendação: (a).**
- **Chaves (desligadas por padrão), no padrão do `RegimeCombinado`:** `AnomalyDetection:ValorCongelado:Habilitado = false` e `AnomalyDetection:RuidoExcessivo:Habilitado = false`, cada uma com `ClientesHabilitados = []` (vazia = nenhum cliente) e com os seus parâmetros, com validador.
  - Com a chave desligada, o detector **nem é registrado** na DI. Isso entra em `LegacyAnomalyDetectionServiceCollectionExtensions`, com uma sobrecarga que recebe `IConfiguration`, como `IsolationForestServiceCollectionExtensions` já faz. O ensemble fica idêntico: mesma lista de detectores, mesma saída e mesmo CSV do replay. Mudar a chave exige reiniciar a API.
  - Cliente fora da lista ⇒ `NaoAplicavel("cliente-fora-da-lista")`.
- **V2:** mapear os dois nomes para `DetectorFamily.DataQuality` em `LegacyDetectorResultV2Adapter.ResolveFamily` e em `EnsembleV2Simulator`. Hoje um nome desconhecido vira `Unknown`, e `DetectorResultV2` recusa `Unknown`.
- **Replay:** `BuildDetectors` (`tools/Cp300OfflineReplay/Cp300Replay.cs:840`) ganha as opções `--detector-congelado` e `--detector-ruido`. Sem elas, a lista atual fica igual.
- **Pré-requisitos para a IA real (não para o replay):** tarefa **25** (P31: hoje a maioria das leituras para na validação e não chega a nenhum detector) e tarefa **6** (cadência). Os dois detectores olham o histórico de 1 min vindo do Bridge, não a sequência de avaliações, então a cadência de ~10 min (P26) não os impede, só atrasa a decisão.

#### 33.C Valor congelado

- **Lógica.** Detector `ValorCongeladoDetector : IAnomalyDetector` que **chama o `FrozenSensorEvidenceCalculator` sem alterá-lo**, com duas diferenças, as duas dentro do detector:
  1. **Tipo do sinal:** aceita `Continuous` **e `Unknown`**. Bloqueia `Discrete`, `Counter` e `Setpoint` em qualquer confiança (estados, comandos, set-points). Isso resolve a TAG24 = `Unknown`.
  2. **Guarda "repouso conhecido"** (nova): não alarma se o valor parado já apareceu parado por ≥ `MinutosRepouso` (padrão 10) no histórico usado na decisão. Cobre bomba parada (0), válvula fechada e qualquer valor em que a tag legitimamente fica parada. Com regime, o histórico já é o do regime, e a guarda de "variação anterior" do próprio calculador também protege.
- **Parâmetros (padrões propostos):** `MinimoPontos = 10`, `MinimoMinutos = 10` e `JanelaAnteriorPontos = 30` (os mesmos de `RegimeShadowOptions`), `TaxaMaximaPassosIguaisAntes = 0,60`, `MinutosRepouso = 10`, tolerância de igualdade exata (1e-12; a V1 não trata micro-oscilação). Nota: `100 × min(1, duração / MinimoMinutos)`; confirma com `HasEvidence`.
- **Checagem offline** (`dados/cp300_09a13nov.csv`, sem a IAApi; confirmado nos dados, simulador). Períodos normais: 12/11 14:15 → 13/11 07:59 e 13/11 15:00–22:59, fora das falhas:
  - maior sequência de valor idêntico **≠ 0** nas TAG22–33: **2 a 4 min** (margem de 2,5× até 10 min);
  - sequências **= 0** (bomba parada ou válvula fechada): TAG32 **119 min**, TAG31 67, TAG28 24, TAG27 21, TAG26 9. Sem a guarda de repouso e sem regime, cada parada viraria alarme falso;
  - falha 20: TAG24 parada **30 min** ⇒ detecção prevista aos **10 min** (15:31), contra 14 min hoje só pela TAG32 no `combs` (P41) [previsão; medir no replay];
  - taxa de passos iguais da TAG24 em operação normal: 0,11 (bem abaixo de 0,60).
- **Riscos de alarme falso e como cada um é coberto:**
  - bomba parada e válvula fechada: guarda de repouso, histórico do regime e taxa anterior;
  - tags de estado e set-point (TAG41–46, TAG44 `Discrete`): bloqueio por tipo; a TAG46 constante por horas também cai em "variação anterior baixa";
  - sensor de resolução grossa que às vezes fica parado mais de 10 min: medir a sequência máxima por tag na operação normal antes de habilitar a tag;
  - limite físico legítimo (nível no máximo, válvula 100 %, saturação do controle): passa pela guarda de repouso se já tiver ocorrido no histórico; se não, alarma (é aceitável sinalizar);
  - histórico contaminado por um congelamento anterior (P13/P25): a guarda aprende o congelamento como "repouso" ⇒ **falso negativo**. Depende das tarefas 8 e 27.
- **Fora do escopo:** Bridge sem pontos novos (gap) ⇒ o calculador devolve `critical-gap`, não aplicável. Isso é da tarefa **18** ("sem dados"). Bridge repetindo o último valor ⇒ é detectado como congelado (desejado).
- **Testes (unitários, offline):** congelado num valor normal com variação antes ⇒ confirma no 10º min, não no 9º; `Unknown` aceito e `Discrete`/`Setpoint`/`Counter` bloqueados; zero longo já visto no histórico ⇒ não alarma; zero sem repouso no histórico ⇒ alarma; taxa anterior > 0,60 ⇒ não alarma; gap crítico ⇒ não aplicável; cliente fora da lista ⇒ não aplicável. Ensemble: papel independente fora da soma (nota idêntica com e sem o detector), "OU" com a chave ligada e nunca removendo alarme; DI com a chave desligada sem registro. Mutação: sem a guarda de repouso, o teste da bomba parada falha.

#### 33.R Ruído excessivo

- **Lógica.** Detector `RuidoExcessivoDetector : IAnomalyDetector`, **sem estado**, baseado na escala robusta das **diferenças de 1 min**. Diferenças não mudam com a variação lenta de demanda nem com o nível da tag, e pegam ruído de média zero, que o EWMA não vê (P07, falha 22).
  - `recente` = 1,4826·MAD das diferenças nos últimos `JanelaMinutos`;
  - `referência` = mesma medida no histórico de 24 h (o do regime, quando houver), sem a janela recente, descartando as diferenças que atravessam um gap maior que 2× a cadência (o histórico do regime é concatenado);
  - `razão = recente / max(referência, piso)`. O piso vem de `BaselineProtectionProfile.RelativeScaleFloor` ou da resolução do sensor; isso protege a bomba parada e sinais quantizados com MAD 0;
  - confirma se a razão ficar ≥ `RazaoMinima` em ≥ M das últimas P janelas (calculadas do próprio histórico, por isso sem estado), **e** se o centro recente estiver estável (≤ 2 escalas da referência, a mesma guarda do `IncreasingNoiseEvidenceCalculator`, para não confundir com degrau ou transição).
  - **Reaproveitamento:** usa `DetectorMath.Median`/`Mad` e as guardas do calculador existente. **Não** usa o próprio calculador, por três motivos: ele tem estado, usa o regime interno e mede os valores, não as diferenças. **Plano registrado (regra do CLAUDE.md):** se a 33.R for aprovada e validada, extrair o núcleo comum e avaliar trocar o `IncreasingNoiseEvidenceCalculator` (PK7) por ele, numa tarefa própria, com regressão do canário. Até lá, as duas lógicas convivem.
- **Parâmetros iniciais (a varrer):** `JanelaMinutos` 10, 15 ou 20; `RazaoMinima` 3, 4 ou 5; M-de-P = 3 de 5; mínimo de 10 diferenças válidas na janela e de 60 na referência.
- **Checagem offline (crua: sem M-de-P, sem guarda de centro, estado = TAG42/43/46 sem a TAG45; confirmado nos dados, simulador):**
  - falha 22 (TAG27, 800 ‰): razão máx. **5,7**, mediana **2,3**, **11 de 29 min ≥ 3**. O sinal existe, mas é **marginal**;
  - minutos-tag normais acima do limiar: **134 com 3**, 61 com 4, 22 com 5. A maior parte na TAG22 (58 com 3; nível) e na TAG26 (38 com 3; a válvula TAG45 não estava na chave de estado).
  - **Conclusão: viabilidade incerta.** Há um único exemplo positivo (falha 22) e uma única semente. O risco de escolher parâmetros que só servem a esses dados é alto.
- **Riscos de alarme falso:** transições de regime e partida ou parada de bomba (diferenças grandes; MAD robusto, guarda de centro, histórico do regime); malhas oscilando (nível TAG22 e válvula TAG26, vistos acima); quantização (piso); referência contaminada por ruído anterior nas 24 h (P25) ⇒ falso negativo; o ruído do simulador pode não ser como o da planta (sem validação em planta).
- **Testes (unitários, offline):** ruído de média zero com a razão acima do limiar em 3 de 5 janelas ⇒ confirma (e o EWMA continua em 0); degrau limpo ⇒ não confirma (guarda de centro); rampa lenta ⇒ não confirma (diferenças constantes); sinal quantizado com MAD 0 ⇒ piso, sem divisão por zero; diferenças que atravessam gap ⇒ descartadas; bomba parada (referência ≈ 0) ⇒ não alarma com variação de 1 unidade; poucas diferenças ⇒ não aplicável. Mutação: sem o piso, o teste da bomba parada falha.

#### 33.M Como medir no replay (antes de qualquer ativação)

- **Períodos** (todos com `--historico-causal` e treino limpo, como na RC-11): preset 3 de 13/11 (15:00–22:59), preset 6 e **18 h normais de 12/11** (12/11 14:15 → 13/11 07:59, com a madrugada). **Configurações:** `sem`, `rec` e `combr`, cada uma com o detector desligado (execuções já existentes) e ligado.
- **Métrica principal: a diferença entre ligado e desligado**, minuto a minuto (`decisao_final`):
  - **minutos ganhos** (0 → 1), classificados pelo placar: na falha e na tag-alvo, coincidência ou **falso fora das falhas**;
  - **minutos perdidos** (1 → 0): têm de ser **0**, por construção;
  - nota dos outros detectores e `nota_final` sem o "OU": **idênticas**;
  - contagem por motivo de não aplicável ou não confirmado (`signal-not-continuous`, `historically-low-variation`, repouso conhecido, piso…), para ver as guardas funcionando nas paradas de bomba.
- **Aceite proposto (para aprovação):**
  - **33.C:** falha 20 detectada **na TAG24** em ≤ 11 min nas 3 configurações; **0 minutos ganhos falsos** nas 18 h de 12/11 e no preset 6, nas 3 configurações; preset 6 com a TAG29 ainda às 15:32 (sem regime) e às 15:37 (`combr`).
  - **33.R:** falha 22 detectada **na TAG27** pela regra do placar (≥ 3 min seguidos) no `combr`; falsos ganhos ≤ orçamento a definir pela responsável (sugestão: ≤ 10 minutos-tag nas 18 h de 12/11 e ≤ 4 fora das falhas no preset 3, cerca de 10 % dos 37 atuais do `combr`). Se nenhuma combinação de parâmetros atender, **a 33.R para** e o resultado é documentado.
- **Regressão:** com as duas chaves desligadas, `Compare-Replay.ps1` com **0 diferenças nas 12 execuções** (sem e com `-HistoricoCausal`) e suíte completa verde.

#### 33.P Passos (cada um com aprovação própria; nenhum altera a decisão com as chaves desligadas)

| Passo | O que | Arquivos | Muda a decisão |
|---|---|---|---|
| C0 | ✅ Checagem offline do congelamento (feita em 16/11, acima). Script fora do repositório | — | não |
| 1 | ✅ **Feito em 16/11, sem commit (33.P1).** Infra: `IndependentEvidence`, exclusão da soma e "OU" atrás das chaves, opções e validadores, família `DataQuality` no V2. Testes com um detector falso | `DetectorEvidenceRole`, `EnsembleDetectionService.cs`, opções novas, `LegacyDetectorResultV2Adapter.cs`, `EnsembleV2Simulator.cs` | não (sem detector registrado) |
| 2 | `ValorCongeladoDetector` + registro condicional na DI + `--detector-congelado` no replay. Testes unitários | detector novo, `LegacyAnomalyDetectionServiceCollectionExtensions.cs`, `Cp300Replay.cs` | só com a chave |
| 3 | Medição 33.M do congelamento (9 execuções ligadas) e registro na RES3 e em `PROBLEMAS_ENCONTRADOS.md` (P12) | — | não |
| R0 | Varredura offline do ruído (Python, sem código da IAApi): janela × razão × M-de-P, com TAG45 na chave da TAG26. **Só segue se** alguma combinação detectar a falha 22 dentro do orçamento | script fora do repositório | não |
| 4 | `RuidoExcessivoDetector` + `--detector-ruido`. Testes | detector novo, DI, replay | só com a chave |
| 5 | Medição 33.M do ruído e registro (P07, P12) | — | não |
| 6 | Ativação no ambiente de teste do PK2 (`ClientesHabilitados = ["PK2"]`), numa sessão acompanhada, **depois das tarefas 25 e 6** | `appsettings` do ambiente de teste | **sim** |

- **Risco:** alto no passo 6 (muda a decisão e o volume de alarmes); baixo nos passos 1–5, com as chaves desligadas e a regressão byte a byte.
- **Esforço:** M (33.C) + M (33.R).

#### 33.P1 Passo 1: o que foi feito (2026-11-16, sem commit)

- **O que mudou** (nenhum detector real com o papel novo existe; com as chaves desligadas, a saída é a mesma):
  - `src/IAApi.Engine/Detection/Core/EvidenceSemantics.cs`: `DetectorEvidenceRole.IndependentEvidence = 2`.
  - `src/IAApi.Engine/Observability/DiagnosticContracts.cs`: código `ExcludedIndependentEvidence`.
  - `src/IAApi.Engine/Detection/EvidenciaIndependenteOptions.cs` (novo): base `EvidenciaIndependenteOptions` (`Habilitado = false`, `ClientesHabilitados = []`, `EstaHabilitadoPara(cliente)`, que ignora maiúsculas/minúsculas e espaços), mais `ValorCongeladoOptions` (`AnomalyDetection:ValorCongelado`) e `RuidoExcessivoOptions` (`AnomalyDetection:RuidoExcessivo`). Os parâmetros dos detectores entram nos passos 2 e 4. O `appsettings.json` **não mudou**.
  - `Services/AnomalyDetection/AnomalyDetectionOptionsValidators.cs`: `EvidenciaIndependenteOptionsValidator<T>`. Lista vazia é válida; lista nula ou item em branco, não.
  - `Services/AnomalyDetection/DependencyInjection/AnomalyDetectionServiceCollectionExtensions.cs`: `Configure<>` das duas seções em `AddAnomalyDetectionConfiguration` e validadores em `AddAnomalyDetectionValidation`. **Nada lê essas opções ainda**, então o validador não roda em produção até o passo 2.
  - `Services/AnomalyDetection/EnsembleDetectionService.cs`:
    - um resultado aplicável com papel `IndependentEvidence` **não entra** em `somaPesos`, `somaScoresPonderados`, `quantidadeDetectoresAplicaveis`, `quantidadeAlertas` nem no detector de referência dos limites. Fica nos detalhes com `ExcludedIndependentEvidence` e peso efetivo 0;
    - se algum confirma: `EhAnomalia = true` e nota final = `max(média, maior nota confirmada)`, com a nota limitada a [0, 100] e valor não finito tratado como 0. A política vira `WeightedMeanOrIndependentEvidence`, ou `<política experimental>+IndependentEvidenceOr` com a política temporal experimental ativa;
    - sem nenhum detector da média aplicável e com evidência confirmada: `IndependentEvidenceOnly`, aplicável e anomalia, sem a exceção de soma de pesos zero; sem evidência confirmada continua `NoApplicableDetectors`;
    - métricas novas (`quantidadeEvidenciasIndependentes`, `…Confirmadas`, `evidenciaIndependenteConfirmada`, `scoreEvidenciaIndependenteMaximo`, `scoreMediaPonderada`) **só quando há evidência independente aplicável**. Sem ela, o dicionário continua com as 12 chaves de antes;
    - no ramo experimental, o código de exclusão passou a ser `??=`, para não sobrescrever `ExcludedIndependentEvidence`. Antes desse passo ele era sempre nulo ali, então nada muda.
  - `EnsembleEvidenceParticipationPolicy.cs`: `IndependentEvidence` nunca participa.
  - `LegacyDetectorResultV2Adapter.cs` e `EnsembleV2Simulator.cs`: papel `IndependentEvidence` ⇒ família `DataQuality`, decidida pelo papel e não pelo nome. Os outros detectores seguem pelo nome, como antes.
  - A política de aplicabilidade (tag `Discrete`/`Counter`/`Setpoint` de alta confiança) bloqueia também a evidência independente, como bloqueia qualquer detector (há teste).
- **Testes:** `IAApi.Tests/Detection/EnsembleIndependentEvidenceTests.cs` (novo, **38 casos**, só detectores falsos). Cobrem: nota, decisão e somas idênticas com e sem evidência não confirmada (inclusive os 60 exatos do P07); detalhes fora da agregação; "OU" com `max`; média maior mantida; várias evidências; nota NaN, infinita ou fora da faixa; nunca remove alarme; combinação com a política temporal experimental; somente evidência (confirmada e não confirmada, com peso 0); sem evidência ⇒ 12 métricas; evidência não aplicável ignorada; bloqueio por tag discreta; política de participação; família V2; opções (padrão desligado, seção, filtro de cliente); validador direto e pela DI; `appsettings.json` sem as seções.
  - **Mutações** (temporárias, arquivo restaurado e conferido com `cmp`): evidência dentro da média ⇒ **14 falham**; "OU" desligado ⇒ **8 falham**.
- **Execução** (a IAApi seguiu rodando na sessão de medição, pelo `bin\Debug`; **tudo compilado em `-c Release`**, numa pasta de saída separada, sem tocar no `bin\Debug` em uso):
  - testes relacionados (ensemble, cenários, regime combinado, V2/confiança, persistência, Cp300): **228/228**;
  - suíte completa: **1117/1117** (1079 + 38). O isolamento offline foi conferido (os testes do Bridge usam só `FakeBridgeHandler`);
  - `Compare-Replay.ps1 -Configuracao Release -SemBuild`: **0 diferenças em 12 execuções** (`dados/rc2_rodadas/20261116_154049`); com `-HistoricoCausal`: **0 diferenças em 12** (`20261116_154524`).
- **Rollback:** reverter os 7 arquivos alterados e apagar os 2 novos (`EvidenciaIndependenteOptions.cs` e o arquivo de teste). Não há configuração nova no `appsettings.json` nem migration.
- **Rollback:** desligar a chave e reiniciar a API (o detector deixa de ser registrado); no código, reverter os commits de cada passo.

---

*Tarefas RC-0 a RC-12 acrescentadas em 2026-11-12: implementação do item 12 (Opção A, regime combinado com 3 componentes). O detalhe de cada uma (objetivo, arquivos, aceite) está em `PLANO_REGIME_COMBINADO.md` §6. Cada tarefa cabe numa sessão e exige aprovação própria.*

**Regra comum:** tudo fica atrás da chave `AnomalyDetection:RegimeCombinado:Habilitado`, **desligada por padrão**. Com ela desligada, o replay tem de sair **idêntico byte a byte** ao atual (verificado pela RC-2 a cada tarefa de código). Só a RC-12 muda a detecção. **Rollback:** desligar a chave; depois `Ativo = false` por tag.

| # | Tarefa | Arquivos principais | Aceite resumido | Depende de | Muda comportamento |
|---|---|---|---|---|---|
| RC-0 | ✅ **Concluída com ressalva (2026-11-13).** CSV `dados/cp300_09a13nov.csv` (09/11 12:30 → 13/11 07:59; trecho comum com o de 10–11/11 idêntico). **Tupla pura = `RB0xS` nos 5.474 minutos** (mesma partição, 0 minutos diferentes; (0,0,·) nunca ocorre) ⇒ tupla pura confirmada, sem ajuste de mapa. Replays (12): preset 1 idêntico à etapa 0; preset 6 idêntico + minuto 17:40 sem alarme; madrugada `combs`: **B-01 = 30** (meta ≤ 30), total 198 → 74, B-02 32 → 0. **Ressalva:** 9 minutos-tag da B-01 ainda caem no `Global` (00:43–00:45). Causa: a classe "B-01 parada + 380" tinha 27–29 pontos na janela de 24 h (mínimo 30), por causa da alternância das bombas entre as noites. Aceita pela responsável; recontar na RC-11; nenhum limiar alterado. Referência da RC-2 regenerada com as 6 execuções `rc0_*` (`rec`/`combs`): 12 execuções, **0 diferenças**. Ver `PLANO_REGIME_COMBINADO.md` §5.7 | nenhum do repo; exportação pela equipe, com autorização (Bridge). `Compare-Replay.ps1`: entrada por execução + execuções RC-0 (aprovado) | B-01 madrugada ≤ 30 sem fallback da 1ª noite; tupla = `RB0xS` minuto a minuto (ou diferenças listadas) | — | não |
| RC-1 | ✅ **Concluída (2026-11-12).** Análise do atraso da detecção confirmada da TAG29 (§5.6, itens 2 e 3). Causa: com o regime, mediana e MAD sobem (139/18 → 164–171/23,5), o limite de "fora" passa de 232 para 285–293 e a Persistência fica em 0 até 15:39; o MAD sai da saturação. O desenho da Opção A não muda. **Decisão (a), 2026-11-12:** atraso de 3 min aceito; o critério da RC-11 passou a ser "até 15:37" (§5.6.2). Ver `PLANO_REGIME_COMBINADO.md` §5.6.1 | só leitura de `dados/e0_preset6_*.csv` e dos detectores | tabela por detector e minuto `rec` × `combs`; hipótese confirmada ou descartada | — | não |
| RC-2 | ✅ **Concluída (2026-11-12).** Referência congelada e regressão byte a byte. `Compare-Replay.ps1 -Gerar` gravou `dados/rc2_referencia/` (manifesto + 6 execuções `sem`/`rec` de preset 1, madrugada e preset 6; idênticas às `e0_*`). Duas rodadas de comparação: **0 diferenças** (CSV por SHA-256, log por linha). Teste golden `Golden_BombasComRegime_SaidaIdenticaAReferencia` verde; testes da ferramenta 32/32. Execuções da RC-0 incluídas em 2026-11-13 (12 execuções; 0 diferenças). Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-2 | `tools/Cp300OfflineReplay/Compare-Replay.ps1` (novo), `Cp300OfflineReplayTests.cs`, `IAApi.Tests/Cp300Replay/Golden/` | 0 diferenças em duas rodadas no código atual; teste golden verde | — | não |
| RC-3 | ✅ **Concluída (2026-11-13).** Resolver com dois ponteiros: dentro do laço, `FindCausalRegimePoint` deu lugar a um ponteiro que só avança (O(n + m)); o ponto atual continua com a busca linear; testes de idade num helper comum. Equivalência com a implementação linear anterior (oráculo copiado no teste): 3000 cenários aleatórios com semente fixa + 5 casos de timestamps repetidos, mesmos `Applied`, `Reason`, contagens e mesmos objetos selecionados; teste com mutação (`<=`→`<`) falha. Testes relacionados 128/128; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Custo do preset 6 dentro do ruído (`rec` 17,7 s → 16,3–17,8 s). Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-3 | `OperationalRegimeHistoryResolver.cs` + testes | igual ao oráculo linear em séries aleatórias; RC-2 com 0 diferenças; custo medido | RC-2 | não (provado) |
| RC-4 | ✅ **Concluída (2026-11-13).** Decisões de §6.2 fechadas: (a) `TagRegime` + coluna `MapaEstados`; tupla pura com mapa configurável (conferir na RC-0); fora do mapa passa sem mudança e vai ao relatório; comparação e formato da chave conforme a recomendação. Classes novas e isoladas: `RegimeCombinadoOptions` (`AnomalyDetection:RegimeCombinado:Habilitado`, padrão `false`, registrada na DI, `appsettings.json` sem mudança), `OperationalRegimeStateMap` (parse `2:2;*:0`, tolerância, `*`, `OutOfMap`) e `OperationalRegimeKey` (`TAG42=2\|TAG43=0\|TAG46=380`, invariante). Nada as chama ainda. 65 testes novos; relacionados 205/205; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Ver `PLANO_REGIME_COMBINADO.md` §6.2 e §6.3, RC-4 | `RegimeCombinadoOptions.cs`, `OperationalRegimeStateMap.cs`, `OperationalRegimeKey.cs` (novos), DI | unitários de parse, chave e opções (padrão `false`); RC-2 com 0 diferenças | RC-0 | não |
| RC-5 | ✅ **Concluída (2026-11-13).** Entidade `TagRegimeComponenteIa` (componentes 2..N; tabela `TagsRegimesComponentesIa`, FK para `TagsRegimesOperacionaisIa` com cascata, índice único (`ConfiguracaoId`, `Ordem`), `CHECK Ordem >= 2`) e coluna nula `MapaEstados` em `TagsRegimesOperacionaisIa`; migration `20261113160248_RC5_AddTagRegimeComponente` + snapshot. Nada lê as tabelas novas. **Migration não aplicada em nenhum banco real** (nem `ia_config.db` local, hash conferido antes/depois, nem PK2). 7 testes novos em SQLite em memória (`Up` da cadeia inteira, restrições, cascata, `Down` preservando a configuração, `Up` de novo, `EnsureCreated`, modelo = snapshot); relacionados 212/212; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-5 | `Models/`, `Data/AppDbContext.cs`, `Migrations/` | `Up`/`Down` em SQLite vazio; testes atuais verdes; **não** aplicar no PK2 | RC-4 | não |
| RC-6 | ✅ **Concluída (2026-11-13).** Sobrecarga `Resolve(configuração, componentes 2..N, alvo, séries 1..N, instante)` com um ponteiro por componente, ponto descartado se faltar qualquer componente, estado atual exige todos; `CurrentRegimeKey` na resolução (nula no caminho atual). **Decisões de 13/11/2026:** (1) lag e tolerância de alinhamento da configuração valem para todos os componentes (aprovado); (2) comparação: os dois lados mapeados (entrada ou `*`) ⇒ exato; algum lado fora do mapa sem `*` (ou componente sem mapa) ⇒ `\|Δ\| ≤ ToleranciaEstado`. Assinatura atual sem mudança e sem delegação; nada no runtime chama a sobrecarga. Equivalência: 1 componente sem mapa = oráculo linear da RC-3 em 3000 cenários + timestamps repetidos; N componentes = oráculo de tupla em 3000 cenários; mutações (`<=`→`<` no ponteiro: 16 testes falham; fora do mapa exato: 3 falham). Relacionados 250/250; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Ver `PLANO_REGIME_COMBINADO.md` §6.2 e §6.3, RC-6 | `OperationalRegimeHistoryResolver.cs` + testes | unitários de §3.2; 1 componente = caminho atual; RC-2 com 0 diferenças | RC-3, RC-4 | não (provado) |
| RC-7 | ✅ **Concluída (2026-11-13).** Bloqueio de rótulos por cliente (item 11), válido para todos os componentes. Lista em `AnomalyDetection:RegimeOperacional:TagsProibidasPorCliente` (`OperationalRegimeForbiddenTagsOptions`, vazia por padrão; `appsettings.json` não mudou). O provider recusa a `TagRegime` da lista antes de consultar o histórico, com motivo e linha `BASELINE POR REGIME RECUSADO`. Testes do provider e relacionados 122/122; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-7 | `OperationalRegimeBaselineProvider.cs` + testes | TAG73 recusada com a lista; lista vazia = idêntico | — | não (lista vazia por padrão) |
| RC-8 | ✅ **Concluída (2026-11-13).** Service e provider com componentes, atrás da chave `AnomalyDetection:RegimeCombinado:Habilitado` (desligada). **Provider:** com a chave desligada, a tabela de componentes não é lida (teste com a tabela apagada) e `MapaEstados` é ignorado. Com a chave ligada e componentes cadastrados, ele carrega os componentes por `Ordem` e recusa, antes de qualquer consulta, componente da lista da RC-7 ou com `TagRegime` vazia. Depois faz uma consulta por componente na mesma janela; falha em qualquer consulta ⇒ fallback global. Com a chave ligada e sem componentes, segue o caminho atual. **Service:** sobrecarga com a lista de séries, que delega ao resolver da RC-6; a assinatura atual não muda (trecho comum extraído). **Resolução:** `OperationalRegimeBaselineResolution.CurrentRegimeKey`, nula no caminho atual. 20 testes novos (provider 14, service 6), incluindo injeção de dependências pela seção. Mutação da chave (sempre ligada / sempre desligada): 6 e 8 testes falham. Relacionados 314/314; `Compare-Replay.ps1`: **0 diferenças em 12 execuções**, sem e com `-HistoricoCausal`; suíte completa **987/987**. Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-8 | `OperationalRegimeBaselineProvider.cs`, `OperationalRegimeBaselineService.cs` + testes | chave desligada com componentes = sem componentes, sem consulta extra; ligada: 3 consultas e fallback em falha | RC-5, RC-6, RC-7 | só com a chave |
| RC-9 | ✅ **Concluída (2026-11-13).** `BaselineDiagnostic.RegimeKey` (parâmetro opcional no fim do construtor; espaços ⇒ nula) com `[JsonIgnore(WhenWritingNull)]` na propriedade. Assim ela fica fora do JSON quando nula nos dois pontos de serialização levantados: o exportador JSONL (`JsonIgnoreCondition.Never`) e o endpoint `GET /intelligence-lab/diagnostics/{id}` (padrão web do `AddControllers`). A cópia do `InMemoryDiagnosticCollector` só repassa o objeto. `PrepararAsync` preenche a chave só com o regime aplicado (vinda da RC-8); a linha `BASELINE POR REGIME APLICADO` ganha `Chave=…` só quando ela existe. Não entra em nenhuma decisão. 12 testes novos: JSON do baseline sem chave igual ao literal do contrato anterior, no JSONL e no padrão web; com chave, `regimeKey` no fim; preparação de ponta a ponta com a chave desligada (sem chave no diagnóstico, no JSON e no log) e ligada; regime não aplicado sem chave. Mutação (sem o `JsonIgnore`): 3 testes falham. Relacionados 505/505; `Compare-Replay.ps1`: **0 diferenças em 12 execuções**, sem e com `-HistoricoCausal`; suíte completa **999/999**. Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-9 | `DiagnosticContracts.cs`, `PredictionDetectionPreparationService.cs` | JSON e log iguais com a chave desligada; RC-2 com 0 diferenças | RC-8 | só com a chave (observabilidade) |
| RC-10 | ✅ **Concluída (2026-11-13).** Replay com `--regime-combinado` (liga `RegimeCombinado:Habilitado` só no provider do replay, com TAG60–TAG81 na lista de tags proibidas do cliente), `--regime TAG27=TAG42+TAG43+TAG46` (componente 1 na linha principal com `MapaEstados`; 2..N em `TagsRegimesComponentesIa`) e `--regime-mapa "TAG42=2:2;*:0"`. Sem a opção, `+` e `--regime-mapa` são recusados (código 2) e o provider é construído como antes. Recusa TAG60–TAG81 em qualquer componente, componente ausente do CSV, repetido ou igual à tag analisada, e mapa inválido ou de tag que não é componente. Coluna `chave_regime` (de `BaselineEfetivo.RegimeKey`) só com a opção; no log, cobertura por chave no treino e na primeira janela móvel, com valores fora do mapa. 11 testes novos (parse, rótulo nas posições 1–3, recusas, 1 componente = golden RC-2 + coluna vazia, golden novo `replay_bombas_regime_combinado.csv` sem/com `HistoricoCausal`, cobertura no log). Mutação (coluna sempre vazia): os 2 goldens novos falham. Ferramenta 44/44; `Compare-Replay.ps1`: **0 diferenças em 12 execuções**, sem e com `-HistoricoCausal`; suíte completa **1010/1010**. Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-10 | `tools/Cp300OfflineReplay/` + testes | `tools/Cp300OfflineReplay/` + testes | sem a opção, saída idêntica; TAG60–81 recusada em qualquer componente | RC-8, RC-9 | não (ferramenta) |
| RC-11 | ⚠️ **Aprovada com ressalvas (2026-11-16).** ✅ **Decisão da responsável (2026-11-16): 3 componentes** (estado da própria bomba, estado da outra bomba e TAG46), com os mapas de §6.2 (`PLANO_REGIME_COMBINADO.md` §7.9). Replay com `--regime-combinado --historico-causal` sobre `dados/cp300_09a13nov.csv` em 5 cenários (preset 1, madrugada de 11/11, preset 6, 18 h normais de 12/11 e preset 3 de 13/11), com 3 componentes (`combr`) e 2 componentes (`comb2r`): 10 execuções, 0 erros e 0 divergências.<br>**(1)** `Compare-Replay.ps1`: 0 diferenças em 12 execuções, sem e com `-HistoricoCausal`.<br>**(2)** Equivalência: 0 linhas diferentes em 8 comparações (`rc0_*_combs`/`comb`, `p3_combs`, `p3b_combs`).<br>**(3)** Metas de §3.1 atendidas: B-01 no preset 1 = 4; B-01 na madrugada = **30 (no limite)**; B-02 no failover = 0; TAG29 com 1º alarme às 15:32 e 0 antes da falha. **Fallbacks por poucos pontos: 174 minutos-tag nas bombas (72 com alarme)**, sempre na 1ª noite com 380 depois de um rodízio: 11/11 00:43–00:45 (9, B-01) e 13/11 00:47–01:12 (63, B-02; classe com 14–29 pontos). Mais 12 na TAG26 (transição da válvula). Foram para o item 24.<br>**(4)** TAG29 às 15:33/15:34: chave `TAG42=2\|TAG43=0\|TAG46=450`, média 169,9/170,1, desvio 27,4/27,7, 434/435 pontos, `OperationalRegime`; **confirmada às 15:37 (no limite)**.<br>**(5)** Custo do preset 6 em Release: 15,2 s com a chave desligada (`combs`) contra 16,4 s ligada (`combr`), +8 %, dentro da variação. I/O (3 consultas por tag) não medido.<br>**Com × sem set-point:** total 830 × 1.028; preset 3 com 5 × 4 de 7 falhas e 37 × 62 falsos; sem set-point falham B-01 no preset 1 (6) e B-02 no failover (40). Nas madrugadas, cada um ganha numa: 11/11 74 × 58; 13/11 113 × 110, com a B-01 16 × 78. Recomendação: 3 componentes. Ver `PLANO_REGIME_COMBINADO.md` §7.<br>**Escopo original:** Equivalência com a etapa 0, metas de §3.1, critério da TAG29 e custo. **Acrescentado em 2026-11-13 (saída da RC-0): antes de decidir os componentes, comparar o regime combinado com e sem set-point.** São 2 componentes (própria bomba e outra bomba, equivale a `comb`) contra 3 (mais TAG46, equivale a `combs`), de dia (preset 1 e preset 6) e de madrugada, nos CSVs de 10–11/11 e de 09–13/11. Medir: alarmes da B-01 e da B-02, falsos fora da falha, failover do preset 6, TAG29 e `origem_baseline = Global`. **Motivo:** com a noite anterior, `comb` teve menos alarmes na madrugada (58 contra 74; B-01 0 contra 30). Mas o set-point ainda foi necessário para a B-02 no failover do preset 6 (40 → 0) (`PLANO_REGIME_COMBINADO.md` §5.7.4). A escolha não exige código: a RC-10 aceita N componentes. Os fallbacks por poucos pontos que sobrarem vão para o item 24 | nenhum código; resultado em `PLANO_REGIME_COMBINADO.md` §7 | regressão byte a byte; mesmas decisões que `e0_*_combs` (3 componentes) e `e0_*_comb` (2 componentes); **TAG29 com detecção confirmada até 15:37 no preset 6** (decisão (a), §5.6.2); se depois, documentar a causa e parar; **tabela com e sem set-point (dia e madrugada) e decisão dos componentes registrada pela responsável antes da RC-12** | RC-0, RC-1, RC-10 | não |
| RC-12 | ⏳ **NÃO aprovada (2026-11-16).** Por decisão da responsável, fica para **depois da tarefa 6** (cadência do ciclo) e **da tarefa 27** (retreino do perfil do PK2). Será feita numa **sessão acompanhada**, junto com a ativação da chave `AnomalyDetection:HistoricoCausal` (tarefa 25). Componentes: os 3 decididos na RC-11 (`PLANO_REGIME_COMBINADO.md` §7.9).<br>Ativação no ambiente de teste do PK2 | banco e configuração do PK2 | `RegimeKey` no diagnóstico durante presets 1 e 6; alarmes na ordem do replay; tempo de ciclo com 3 consultas por tag de bomba; validação no simulador, não em planta | RC-11 + tarefas 6 e 27 + aprovação | **sim** |
