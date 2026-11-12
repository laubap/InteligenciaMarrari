# Backlog: detecção univariada

Atualizado na sessão 2 (2026-11-11), depois do cruzamento com `Docs/CENARIOS_CP300.md` (ver `AUDITORIA_STATUS.md` §5).

Ordem: primeiro medição e diagnóstico, depois correções. **Nenhuma tarefa está aprovada**: implementar só com autorização explícita (`CLAUDE.md`). As tarefas 1 a 6 não alteram a decisão de anomalia.

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

### 6. Medir a duração do ciclo e a cadência real por tag no ambiente de teste
- Sem mudança. `IAAPI_PERF_DIAGNOSTICS` numa sessão autorizada. **Risco:** médio. **Esforço:** P.
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

*Tarefas RC-0 a RC-12 acrescentadas em 2026-11-12: implementação do item 12 (Opção A, regime combinado com 3 componentes). O detalhe de cada uma (objetivo, arquivos, aceite) está em `PLANO_REGIME_COMBINADO.md` §6. Cada tarefa cabe numa sessão e exige aprovação própria.*

**Regra comum:** tudo fica atrás da chave `AnomalyDetection:RegimeCombinado:Habilitado`, **desligada por padrão**. Com ela desligada, o replay tem de sair **idêntico byte a byte** ao atual (verificado pela RC-2 a cada tarefa de código). Só a RC-12 muda a detecção. **Rollback:** desligar a chave; depois `Ativo = false` por tag.

| # | Tarefa | Arquivos principais | Aceite resumido | Depende de | Muda comportamento |
|---|---|---|---|---|---|
| RC-0 | CSV de 48 h (com noite anterior) e equivalência da tupla pura × `RB0xS` | nenhum do repo; exportação pela equipe, com autorização (Bridge) | B-01 madrugada ≤ 30 sem fallback da 1ª noite; tupla = `RB0xS` minuto a minuto (ou diferenças listadas) | — | não |
| RC-1 | Análise do atraso da detecção confirmada da TAG29 (§5.6, itens 2 e 3) | só leitura de `dados/e0_preset6_*.csv` e dos detectores | tabela por detector e minuto `rec` × `combs`; hipótese confirmada ou descartada | — | não |
| RC-2 | Referência congelada e regressão byte a byte | `tools/Cp300OfflineReplay/Compare-Replay.ps1` (novo), `Cp300OfflineReplayTests.cs`, `IAApi.Tests/Cp300Replay/Golden/` | 0 diferenças em duas rodadas no código atual; teste golden verde | — | não |
| RC-3 | Resolver com dois ponteiros | `OperationalRegimeHistoryResolver.cs` + testes | igual ao oráculo linear em séries aleatórias; RC-2 com 0 diferenças; custo medido | RC-2 | não (provado) |
| RC-4 | Chave, mapa de estados e chave de regime (classes isoladas); fechar decisões de §6.2 | `RegimeCombinadoOptions.cs`, `OperationalRegimeStateMap.cs`, `OperationalRegimeKey.cs` (novos), DI | unitários de parse, chave e opções (padrão `false`); RC-2 com 0 diferenças | RC-0 | não |
| RC-5 | Entidade `TagRegimeComponenteIa` + migration | `Models/`, `Data/AppDbContext.cs`, `Migrations/` | `Up`/`Down` em SQLite vazio; testes atuais verdes; **não** aplicar no PK2 | RC-4 | não |
| RC-6 | Resolver com N componentes (sobrecarga; `CurrentRegimeKey`) | `OperationalRegimeHistoryResolver.cs` + testes | unitários de §3.2; 1 componente = caminho atual; RC-2 com 0 diferenças | RC-3, RC-4 | não (provado) |
| RC-7 | Bloqueio de rótulos por cliente (item 11), válido para todos os componentes | `OperationalRegimeBaselineProvider.cs` + testes | TAG73 recusada com a lista; lista vazia = idêntico | — | não (lista vazia por padrão) |
| RC-8 | Service e provider com componentes, atrás da chave | `OperationalRegimeBaselineProvider.cs`, `OperationalRegimeBaselineService.cs` + testes | chave desligada com componentes = sem componentes, sem consulta extra; ligada: 3 consultas e fallback em falha | RC-5, RC-6, RC-7 | só com a chave |
| RC-9 | `RegimeKey` no diagnóstico e no log (aditivo, omitido quando nulo) | `DiagnosticContracts.cs`, `PredictionDetectionPreparationService.cs` | JSON e log iguais com a chave desligada; RC-2 com 0 diferenças | RC-8 | só com a chave (observabilidade) |
| RC-10 | Replay: `--regime-combinado`, `TAG27=TAG42+TAG43+TAG46`, `--regime-mapa`, coluna `chave_regime` | `tools/Cp300OfflineReplay/` + testes | sem a opção, saída idêntica; TAG60–81 recusada em qualquer componente | RC-8, RC-9 | não (ferramenta) |
| RC-11 | Equivalência com a etapa 0, metas de §3.1, critério da TAG29 e custo | nenhum código; resultado em `PLANO_REGIME_COMBINADO.md` §7 | regressão byte a byte; mesmas decisões que `e0_*_combs`; TAG29 confirmada até 15:34 ou causa documentada (e parar) | RC-0, RC-1, RC-10 | não |
| RC-12 | Ativação no ambiente de teste do PK2 | banco e configuração do PK2 | `RegimeKey` no diagnóstico durante presets 1 e 6; alarmes na ordem do replay; validação no simulador, não em planta | RC-11 + aprovação | **sim** |
