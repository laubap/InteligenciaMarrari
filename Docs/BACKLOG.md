# Backlog: detecção univariada

Atualizado na sessão 2 (2026-11-11), depois do cruzamento com `Docs/CENARIOS_CP300.md` (ver `AUDITORIA_STATUS.md` §5).

Ordem: primeiro medição e diagnóstico, depois correções. **Nenhuma tarefa está aprovada**: implementar só com autorização explícita (`CLAUDE.md`). As tarefas 1 a 6 não alteram a decisão de anomalia.

> **Atualização 2026-11-13 (IA real no preset 3; `PROBLEMAS_ENCONTRADOS.md` P26 e P27):** a tarefa **6 (latência) passou a ser a prioridade 1**. Entraram as tarefas de robustez para a demo **17 a 20**, nenhuma implementada. **Atualização 2026-11-13 (inventário do PK2, tarefa 2 concluída):** a nova tarefa **25 (P31, entrada inválida sistemática) passa a ser PRIORIDADE 1, antes de qualquer novo teste da IA real**. Em 12/11, 38 de 50 decisões não chegaram aos detectores; sem corrigir isso, um teste da IA real não mede detecção. A tarefa 6 continua necessária e vem logo depois. Entraram também a 26 (P30, escala da nota) e a 27 (P32, retreino do perfil). Para os próximos testes foi aprovado só subir a IAApi com a saída gravada em arquivo com data e hora (tarefa 6, "Execução aprovada"); `dotnet-stack` ainda não.

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
- Sem mudança. `IAAPI_PERF_DIAGNOSTICS` numa sessão autorizada. **Risco:** médio. **Esforço:** P.
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

### 7. (Correção) Ajustar a agregação do ensemble ou os pesos
- Sem mudança, mas agora depende da tarefa **3**. Lembrete: sem IF, Z+MAD+EWMA saturados já chegam exatamente a 60. **Risco:** alto. **Esforço:** G.

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
| Patches soltos na raiz (`*.patch`) | `tempo_por_tag` e `bridge_em_lote` já estão incorporados ao código (com acréscimos posteriores). `sessoes_de_teste` e `observabilidade_temporal` já foram aplicados. | Nenhum guarda estado do ciclo. `clareza_cenario1` diverge do código atual e não foi analisado a fundo. | — |

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
- **Limite de teste (confirmado no código):** `BridgeValorAtualService` cria o próprio `HttpClient`, aponta fixo para `http://localhost:5001` e não tem interface nem métodos virtuais.
  - Um teste automático do laço **com tags** chamaria o Bridge real, se estivesse ligado. Isso viola o isolamento do `CLAUDE.md`.
  - Os caminhos de lote e de tag ficam cobertos pelos testes do passo 1 e pela conferência do passo 5.
  - O ponto de substituição no Bridge é a **tarefa 22** (P28). Fazê-la antes deste passo permite testar o laço com tags e um handler falso. Sem ela, a alternativa seria trocar o `_httpClient` por reflexão, como os testes do histórico já fazem. Não é recomendada: o contorno é frágil.

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

---

*Item 22 acrescentado em 2026-11-13, a partir do P28 (levantamento da tarefa 19). Não aprovado; nada implementado.*

### 22. (Robustez/testes) Endereço do Bridge pela configuração e `HttpClient` substituível nos serviços do Bridge
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

### 23. (Ferramenta) Código de saída do `Compare-Replay.ps1` nos erros de uso
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
| RC-8 | Service e provider com componentes, atrás da chave | `OperationalRegimeBaselineProvider.cs`, `OperationalRegimeBaselineService.cs` + testes | chave desligada com componentes = sem componentes, sem consulta extra; ligada: 3 consultas e fallback em falha | RC-5, RC-6, RC-7 | só com a chave |
| RC-9 | `RegimeKey` no diagnóstico e no log (aditivo, omitido quando nulo) | `DiagnosticContracts.cs`, `PredictionDetectionPreparationService.cs` | JSON e log iguais com a chave desligada; RC-2 com 0 diferenças | RC-8 | só com a chave (observabilidade) |
| RC-10 | Replay: `--regime-combinado`, `TAG27=TAG42+TAG43+TAG46`, `--regime-mapa`, coluna `chave_regime` | `tools/Cp300OfflineReplay/` + testes | sem a opção, saída idêntica; TAG60–81 recusada em qualquer componente | RC-8, RC-9 | não (ferramenta) |
| RC-11 | Equivalência com a etapa 0, metas de §3.1, critério da TAG29 e custo. **Acrescentado em 2026-11-13 (saída da RC-0): antes de decidir os componentes, comparar o regime combinado com e sem set-point.** São 2 componentes (própria bomba e outra bomba, equivale a `comb`) contra 3 (mais TAG46, equivale a `combs`), de dia (preset 1 e preset 6) e de madrugada, nos CSVs de 10–11/11 e de 09–13/11. Medir: alarmes da B-01 e da B-02, falsos fora da falha, failover do preset 6, TAG29 e `origem_baseline = Global`. **Motivo:** com a noite anterior, `comb` teve menos alarmes na madrugada (58 contra 74; B-01 0 contra 30). Mas o set-point ainda foi necessário para a B-02 no failover do preset 6 (40 → 0) (`PLANO_REGIME_COMBINADO.md` §5.7.4). A escolha não exige código: a RC-10 aceita N componentes. Os fallbacks por poucos pontos que sobrarem vão para o item 24 | nenhum código; resultado em `PLANO_REGIME_COMBINADO.md` §7 | regressão byte a byte; mesmas decisões que `e0_*_combs` (3 componentes) e `e0_*_comb` (2 componentes); **TAG29 com detecção confirmada até 15:37 no preset 6** (decisão (a), §5.6.2); se depois, documentar a causa e parar; **tabela com e sem set-point (dia e madrugada) e decisão dos componentes registrada pela responsável antes da RC-12** | RC-0, RC-1, RC-10 | não |
| RC-12 | Ativação no ambiente de teste do PK2 | banco e configuração do PK2 | `RegimeKey` no diagnóstico durante presets 1 e 6; alarmes na ordem do replay; validação no simulador, não em planta | RC-11 + aprovação | **sim** |
