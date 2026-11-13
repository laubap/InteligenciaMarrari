# Backlog: detecção univariada

Atualizado na sessão 2 (2026-11-11), depois do cruzamento com `Docs/CENARIOS_CP300.md` (ver `AUDITORIA_STATUS.md` §5).

Ordem: primeiro medição e diagnóstico, depois correções. **Nenhuma tarefa está aprovada**: implementar só com autorização explícita (`CLAUDE.md`). As tarefas 1 a 6 não alteram a decisão de anomalia.

> **Atualização 2026-11-13 (IA real no preset 3; `PROBLEMAS_ENCONTRADOS.md` P26 e P27):** a tarefa **6 (latência) passou a ser a prioridade 1**. Entraram as tarefas de robustez para a demo **17 a 20**, nenhuma implementada. Para os próximos testes foi aprovado só subir a IAApi com a saída gravada em arquivo com data e hora (tarefa 6, "Execução aprovada"); `dotnet-stack` ainda não.

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

### 2. Inventário do banco PK2 (somente leitura)
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

### 6. Medir a duração do ciclo e a cadência real por tag no ambiente de teste — **PRIORIDADE 1 (desde 2026-11-13)**
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
- **Proposta:**
  - Um serviço singleton em memória, atualizado por `MonitoramentoTagsService` em pontos que não mudam o fluxo, com:
    - início e fim do último ciclo, duração e próximo ciclo previsto;
    - etapa atual (consultando lote, tag X, aguardando);
    - por cliente: tags ativas, avaliadas, com falha e timeouts;
    - hora da última decisão gravada e última exceção.
  - Endpoint aditivo de leitura, por exemplo `GET /api/monitoramento/status`.
  - O LiveMonitor ganha a rota `/monitor-api/monitor-status` no `server/proxy.ts` e um indicador no topo: verde (ciclo recente), amarelo (ciclo lento) ou vermelho (sem ciclo concluído há mais de X min; ver tarefa 20).
  - A "etapa atual" com a hora em que começou mostra **onde** o monitor travou, sem precisar de `dotnet-stack`.
- **Arquivos:** `Services/MonitoramentoTagsService.cs`; novo `Services/Monitoring/MonitoramentoStatusService.cs` e controller; DI; `tools/IAApi.LiveMonitor/server/proxy.ts` e `src/`; testes.
- **Risco:** baixo. Só observabilidade, sem efeito na decisão. O endpoint não pode expor dados de outros clientes além do que `/api/anomalias` já expõe; avaliar o filtro por cliente.
- **Esforço:** M
- **Aceite:** teste com relógio e Bridge falsos: o status reflete ciclo, etapa e contagens; com o laço travado de propósito, a etapa e a hora de início ficam paradas e o indicador fica vermelho; o replay (RC-2) não muda.

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

*Tarefas RC-0 a RC-12 acrescentadas em 2026-11-12: implementação do item 12 (Opção A, regime combinado com 3 componentes). O detalhe de cada uma (objetivo, arquivos, aceite) está em `PLANO_REGIME_COMBINADO.md` §6. Cada tarefa cabe numa sessão e exige aprovação própria.*

**Regra comum:** tudo fica atrás da chave `AnomalyDetection:RegimeCombinado:Habilitado`, **desligada por padrão**. Com ela desligada, o replay tem de sair **idêntico byte a byte** ao atual (verificado pela RC-2 a cada tarefa de código). Só a RC-12 muda a detecção. **Rollback:** desligar a chave; depois `Ativo = false` por tag.

| # | Tarefa | Arquivos principais | Aceite resumido | Depende de | Muda comportamento |
|---|---|---|---|---|---|
| RC-0 | CSV de 48 h (com noite anterior) e equivalência da tupla pura × `RB0xS` | nenhum do repo; exportação pela equipe, com autorização (Bridge) | B-01 madrugada ≤ 30 sem fallback da 1ª noite; tupla = `RB0xS` minuto a minuto (ou diferenças listadas) | — | não |
| RC-1 | ✅ **Concluída (2026-11-12).** Análise do atraso da detecção confirmada da TAG29 (§5.6, itens 2 e 3). Causa: com o regime, mediana e MAD sobem (139/18 → 164–171/23,5), o limite de "fora" passa de 232 para 285–293 e a Persistência fica em 0 até 15:39; o MAD sai da saturação. O desenho da Opção A não muda. **Decisão (a), 2026-11-12:** atraso de 3 min aceito; o critério da RC-11 passou a ser "até 15:37" (§5.6.2). Ver `PLANO_REGIME_COMBINADO.md` §5.6.1 | só leitura de `dados/e0_preset6_*.csv` e dos detectores | tabela por detector e minuto `rec` × `combs`; hipótese confirmada ou descartada | — | não |
| RC-2 | ✅ **Concluída (2026-11-12).** Referência congelada e regressão byte a byte. `Compare-Replay.ps1 -Gerar` gravou `dados/rc2_referencia/` (manifesto + 6 execuções `sem`/`rec` de preset 1, madrugada e preset 6; idênticas às `e0_*`). Duas rodadas de comparação: **0 diferenças** (CSV por SHA-256, log por linha). Teste golden `Golden_BombasComRegime_SaidaIdenticaAReferencia` verde; testes da ferramenta 32/32. Execuções da RC-0 entram quando o CSV de 48 h existir. Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-2 | `tools/Cp300OfflineReplay/Compare-Replay.ps1` (novo), `Cp300OfflineReplayTests.cs`, `IAApi.Tests/Cp300Replay/Golden/` | 0 diferenças em duas rodadas no código atual; teste golden verde | — | não |
| RC-3 | ✅ **Concluída (2026-11-13).** Resolver com dois ponteiros: dentro do laço, `FindCausalRegimePoint` deu lugar a um ponteiro que só avança (O(n + m)); o ponto atual continua com a busca linear; testes de idade num helper comum. Equivalência com a implementação linear anterior (oráculo copiado no teste): 3000 cenários aleatórios com semente fixa + 5 casos de timestamps repetidos, mesmos `Applied`, `Reason`, contagens e mesmos objetos selecionados; teste com mutação (`<=`→`<`) falha. Testes relacionados 128/128; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Custo do preset 6 dentro do ruído (`rec` 17,7 s → 16,3–17,8 s). Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-3 | `OperationalRegimeHistoryResolver.cs` + testes | igual ao oráculo linear em séries aleatórias; RC-2 com 0 diferenças; custo medido | RC-2 | não (provado) |
| RC-4 | ✅ **Concluída (2026-11-13).** Decisões de §6.2 fechadas: (a) `TagRegime` + coluna `MapaEstados`; tupla pura com mapa configurável (conferir na RC-0); fora do mapa passa sem mudança e vai ao relatório; comparação e formato da chave conforme a recomendação. Classes novas e isoladas: `RegimeCombinadoOptions` (`AnomalyDetection:RegimeCombinado:Habilitado`, padrão `false`, registrada na DI, `appsettings.json` sem mudança), `OperationalRegimeStateMap` (parse `2:2;*:0`, tolerância, `*`, `OutOfMap`) e `OperationalRegimeKey` (`TAG42=2\|TAG43=0\|TAG46=380`, invariante). Nada as chama ainda. 65 testes novos; relacionados 205/205; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Ver `PLANO_REGIME_COMBINADO.md` §6.2 e §6.3, RC-4 | `RegimeCombinadoOptions.cs`, `OperationalRegimeStateMap.cs`, `OperationalRegimeKey.cs` (novos), DI | unitários de parse, chave e opções (padrão `false`); RC-2 com 0 diferenças | RC-0 | não |
| RC-5 | ✅ **Concluída (2026-11-13).** Entidade `TagRegimeComponenteIa` (componentes 2..N; tabela `TagsRegimesComponentesIa`, FK para `TagsRegimesOperacionaisIa` com cascata, índice único (`ConfiguracaoId`, `Ordem`), `CHECK Ordem >= 2`) e coluna nula `MapaEstados` em `TagsRegimesOperacionaisIa`; migration `20261113160248_RC5_AddTagRegimeComponente` + snapshot. Nada lê as tabelas novas. **Migration não aplicada em nenhum banco real** (nem `ia_config.db` local, hash conferido antes/depois, nem PK2). 7 testes novos em SQLite em memória (`Up` da cadeia inteira, restrições, cascata, `Down` preservando a configuração, `Up` de novo, `EnsureCreated`, modelo = snapshot); relacionados 212/212; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-5 | `Models/`, `Data/AppDbContext.cs`, `Migrations/` | `Up`/`Down` em SQLite vazio; testes atuais verdes; **não** aplicar no PK2 | RC-4 | não |
| RC-6 | ✅ **Concluída (2026-11-13).** Sobrecarga `Resolve(configuração, componentes 2..N, alvo, séries 1..N, instante)` com um ponteiro por componente, ponto descartado se faltar qualquer componente, estado atual exige todos; `CurrentRegimeKey` na resolução (nula no caminho atual). **Decisões de 13/11/2026:** (1) lag e tolerância de alinhamento da configuração valem para todos os componentes (aprovado); (2) comparação: os dois lados mapeados (entrada ou `*`) ⇒ exato; algum lado fora do mapa sem `*` (ou componente sem mapa) ⇒ `\|Δ\| ≤ ToleranciaEstado`. Assinatura atual sem mudança e sem delegação; nada no runtime chama a sobrecarga. Equivalência: 1 componente sem mapa = oráculo linear da RC-3 em 3000 cenários + timestamps repetidos; N componentes = oráculo de tupla em 3000 cenários; mutações (`<=`→`<` no ponteiro: 16 testes falham; fora do mapa exato: 3 falham). Relacionados 250/250; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Ver `PLANO_REGIME_COMBINADO.md` §6.2 e §6.3, RC-6 | `OperationalRegimeHistoryResolver.cs` + testes | unitários de §3.2; 1 componente = caminho atual; RC-2 com 0 diferenças | RC-3, RC-4 | não (provado) |
| RC-7 | ✅ **Concluída (2026-11-13).** Bloqueio de rótulos por cliente (item 11), válido para todos os componentes. Lista em `AnomalyDetection:RegimeOperacional:TagsProibidasPorCliente` (`OperationalRegimeForbiddenTagsOptions`, vazia por padrão; `appsettings.json` não mudou). O provider recusa a `TagRegime` da lista antes de consultar o histórico, com motivo e linha `BASELINE POR REGIME RECUSADO`. Testes do provider e relacionados 122/122; `Compare-Replay.ps1`: **0 diferenças** em 6 execuções. Ver `PLANO_REGIME_COMBINADO.md` §6.3, RC-7 | `OperationalRegimeBaselineProvider.cs` + testes | TAG73 recusada com a lista; lista vazia = idêntico | — | não (lista vazia por padrão) |
| RC-8 | Service e provider com componentes, atrás da chave | `OperationalRegimeBaselineProvider.cs`, `OperationalRegimeBaselineService.cs` + testes | chave desligada com componentes = sem componentes, sem consulta extra; ligada: 3 consultas e fallback em falha | RC-5, RC-6, RC-7 | só com a chave |
| RC-9 | `RegimeKey` no diagnóstico e no log (aditivo, omitido quando nulo) | `DiagnosticContracts.cs`, `PredictionDetectionPreparationService.cs` | JSON e log iguais com a chave desligada; RC-2 com 0 diferenças | RC-8 | só com a chave (observabilidade) |
| RC-10 | Replay: `--regime-combinado`, `TAG27=TAG42+TAG43+TAG46`, `--regime-mapa`, coluna `chave_regime` | `tools/Cp300OfflineReplay/` + testes | sem a opção, saída idêntica; TAG60–81 recusada em qualquer componente | RC-8, RC-9 | não (ferramenta) |
| RC-11 | Equivalência com a etapa 0, metas de §3.1, critério da TAG29 e custo | nenhum código; resultado em `PLANO_REGIME_COMBINADO.md` §7 | regressão byte a byte; mesmas decisões que `e0_*_combs`; **TAG29 com detecção confirmada até 15:37 no preset 6** (decisão (a), §5.6.2); se depois, documentar a causa e parar | RC-0, RC-1, RC-10 | não |
| RC-12 | Ativação no ambiente de teste do PK2 | banco e configuração do PK2 | `RegimeKey` no diagnóstico durante presets 1 e 6; alarmes na ordem do replay; validação no simulador, não em planta | RC-11 + aprovação | **sim** |
