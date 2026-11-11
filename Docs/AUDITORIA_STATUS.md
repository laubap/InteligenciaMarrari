# Auditoria inicial: status

Data: 2026-11-11. Sessão apenas de leitura: nenhum código, configuração ou banco foi alterado. Arquivos criados: `Docs/MAPA_DETECCAO.md`, `Docs/AUDITORIA_STATUS.md` e `Docs/BACKLOG.md`.

**Sessão 2 (2026-11-11):** a tabela de cenários agora é `Docs/CENARIOS_CP300.md`, que substitui a do handoff. O cruzamento com o mapa está na **seção 5**. Também só leitura; alterados apenas `Docs/AUDITORIA_STATUS.md`, `Docs/BACKLOG.md` e uma correção pontual em `Docs/MAPA_DETECCAO.md` (janelas do LevelShift, aplicabilidade e nota do IF). Nenhum teste foi executado nesta sessão.

> Observação de permissão: o `CLAUDE.md` diz que, na primeira sessão, só `docs/MAPA_DETECCAO.md` pode ser criado. A solicitação desta sessão autorizou explicitamente os três arquivos. Como o Windows não diferencia maiúsculas de minúsculas, `docs/` e `Docs/` são a mesma pasta; usei `Docs/`, que é a grafia que o git registra.

## 1. Branch e git status

- Branch: `feat/intelligence-lab-e2e-validation` (main = `main`). Último commit: `7128764 feat: add gradual drift intelligence lab scenario`.
- **51 entradas não commitadas**: 29 arquivos modificados (multivariado, observabilidade, `PredictionService`, serviços de orquestração, `BridgeHistoricoService`, `MonitoramentoTagsService`, DI, testes) e 22 não rastreados. Entre os não rastreados estão `CLAUDE.md`, `Docs/*.md`, `Services/Diagnostics/` (PERF_DIAG), `Services/Monitoring/` (lote por cliente), `PredictionHistoryReuse*`, `tools/IAApi.LiveMonitor/` inteiro e 5 arquivos `.patch` na raiz.
- **Risco**: o código que roda hoje (coleta em lote, PERF_DIAG, reuso de histórico, LiveMonitor) não está em nenhum commit. Os testes abaixo valem para esta árvore local, não para um commit identificável.

## 2. Testes offline

Isolamento verificado antes de rodar [código]: SQLite em memória (`Data Source=:memory:`) ou em pasta temporária; Bridge simulado (`http://bridge.test` com handler falso); os processos filhos executam apenas ferramentas do próprio repo (`tools/IntelligenceLab*`) com fixtures sintéticas; os testes do LiveMonitor usam `fetch` simulado (`vi.stubGlobal`). Nenhum teste usa `appsettings.json` real, `ia_config.db` ou o endereço do Bridge real.

| Suíte | Comando | Resultado |
|---|---|---|
| `IAApi.Tests` (net10.0, xUnit) | `dotnet test IAApi.Tests/IAApi.Tests.csproj --no-restore` | **783 aprovados, 0 falhas, 0 ignorados** (20 s) |
| LiveMonitor (Vitest) | `npx --no-install vitest run` em `tools/IAApi.LiveMonitor` | **28 aprovados** em 4 arquivos (domain, polling, proxy, testRuns) |

Áreas de teste em `IAApi.Tests/`: raiz (cerca de 60 arquivos: multivariado, runtime decision/canário, consistência de controllers, wiring de deadband/behavior no `PredictionService`, PERF_DIAG, reuso de histórico), `Detection/`, `IntelligenceLab/` (replay, cenários sintéticos flow drop e gradual drift), `Monitoring/`, `Services/`.
Outros projetos: `src/IAApi.Engine`, `src/IAApi.Application`, `src/IAApi.Infrastructure`, `tools/IAApi.Benchmarks`, `tools/IntelligenceLab{Comparer,Evaluator,Replay,ScenarioGenerator}`.

Não executado: API, monitor conectado, benchmarks (dependem de bases fora do repo), qualquer coisa ligada ao CP300.

**Limite**: os testes passarem prova o comportamento sintético, não a detecção no CP300.

## 3. Handoff: confirmado / divergente / não encontrado

**`docs/IAAPI_HANDOFF_CLAUDE.md` não foi encontrado**: não está na pasta, nem no histórico git (`git log --all`), nem nas pastas irmãs de `IA MARRARI/`. Por isso **não existe a seção "Próxima sessão" nem a tabela de cenários do CP300** para conferir. Esta auditoria seguiu as prioridades do `CLAUDE.md`.

O que dá para checar a partir do `CLAUDE.md` e dos documentos existentes em `Docs/`:

| Afirmação | Situação |
|---|---|
| Ciclo do monitor ≈ 60 s + processamento de todas as tags (README do LiveMonitor) | **Confirmado no código**: `MonitoramentoTagsService.ExecuteAsync`, `Task.Delay(1 min)` após processamento sequencial |
| Coleta em lote por cliente no Bridge | **Confirmado no código**, mas ainda não commitado |
| PERF_DIAG não altera decisão, ligado por variável de ambiente | **Confirmado no código** (`IAAPI_PERF_DIAGNOSTICS`); teste `PerformanceDiagnosticsTests` passou |
| Cliente do teste CP300 é PK2 (README LiveMonitor) | **Histórico a validar**. O canário runtime/fusão está só para PK7, então PK2 usa a decisão legada do ensemble [config] |
| MaxLag multivariado discretizado em minutos (`concurrency-architecture-audit.md`) | Não reverificado (fora do foco univariado) |
| Parâmetros ativos na máquina de teste iguais aos do repo | **Não verificável** sem acesso ao ambiente: o `appsettings` do repo pode diferir do implantado |

Divergências encontradas no próprio código:
- Os pesos no `appsettings.json` diferem dos defaults de `AnomalyDetectionOptions` (por exemplo, Z 0,15 contra 0,20). Vale o arquivo de configuração.
- `AnomalyDetection:MinHistoryPoints` (30) **não é usado** no caminho de produção, só em `EnsembleV2Simulator`.
- O registro gravado (`AnomaliaDetectada.DataDeteccao`) usa a hora do processamento, não a hora da leitura. O LiveMonitor não consegue mostrar a latência real.

## 4. Cenários CP300 × pontos do mapa

**Tabela original indisponível.** Abaixo estão as **hipóteses por tipo de falha típico**, a reaplicar quando a tabela for fornecida. Nada aqui foi demonstrado: tudo é **[hipótese]** apoiado em comportamento confirmado no código. Os números (#) referem-se às linhas do fluxo em `MAPA_DETECCAO.md`.

| Tipo de cenário (provável) | Pontos do mapa que podem explicar a falha | O que medir para confirmar ou descartar |
|---|---|---|
| **Degrau / mudança de patamar não detectada** | #10 diluição (só parte dos detectores reage, média < 60); #9 deadband zera Z/MAD/EWMA; LevelShift precisa de 10+ pontos recentes; #5 baseline do perfil com desvio inflado | Para a tag e o intervalo do teste: `DetalhesDetectores` (score normalizado e aplicabilidade de cada detector), score final, deadband, média e desvio usados (logs `BASELINE …` e diagnóstico do IntelligenceLab/PERF_DIAG `DetectorResults`) |
| **Pico curto / transitório perdido** | L1+L3: a leitura é amostrada no máximo 1 vez por ciclo de 60 s ou mais, então um pico mais curto que o ciclo pode nunca ser lido; RoC sozinho pesa 0,10 | Duração do pico no simulador × intervalo real entre leituras por tag (`ReadingAgeAtStartMs` e diferença entre `PredictionStartedUtc` consecutivos no PERF_DIAG) |
| **Deriva lenta (drift)** | Janela de 24 h acompanha a deriva; baseline por regime ou comportamento recalculado do próprio histórico absorve a deriva; EWMA e LevelShift comparam contra janelas que já estão derivando | Origem do baseline aplicado em cada leitura (Perfil, Regime ou Comportamento); média do perfil vs. média das últimas 24 h; data do último treino de `TagPerfilIa` |
| **Queda de vazão / ida a zero** | #8 série com muitos zeros ou poucos valores distintos pode ser classificada como Discrete e bloquear todos os detectores; #7 histórico vazio após sanitização | Tipo comportamental e confiança no `DetectorResult.Metricas` (`tipoComportamental`, `confiancaTipoComportamental`, `quantidadeDetectoresAplicaveis`) |
| **Valor congelado / sensor travado** | Desvio ~0 → deadband; detectores de amplitude não reagem a ausência de variação; Persistence é o único candidato | Score do Persistence e aplicabilidade; desvio usado |
| **Tag nunca avaliada ou detecção atrasada** | #3 tag não encontrada no lote → exceção; falha do lote pula o cliente; sem `TagPerfilIa` → exceção; #7 `DataHoraAtual` < último ponto do histórico → inválido; L5 timeout de 60 s | Logs `Falha ao monitorar tag`, `Falha na consulta em lote`, `Predição finalizada … Falha`; explicação `entrada inválida` no resultado; comparação de `ReadingOriginal` com `HistoryLastOriginal` no PERF_DIAG (fuso/Kind) |
| **Anomalia detectada, mas aparece tarde ou na hora errada no LiveMonitor** | L1–L7; `DataDeteccao = DateTime.Now`, hora da leitura não gravada | PERF_DIAG `Monitoring.Reading` (idade da leitura, duração por tag); duração total do ciclo por cliente (`Predições cliente … em X ms`) |

> Sessão 2: a tabela chegou (`Docs/CENARIOS_CP300.md`). A análise por cenário está na seção 5; esta seção fica como referência genérica.

**Próximo passo para fechar esta seção:** receber a tabela do CP300 (cenário, tag, horário de início e fim, resultado esperado e observado) e os logs ou o export de diagnóstico desses intervalos. Com isso, cada linha acima vira confirmada ou descartada por cenário.

---

## 5. Cruzamento `CENARIOS_CP300.md` × `MAPA_DETECCAO.md` (sessão 2)

Legenda: **[código]** confirmado lendo o código · **[repo]** confirmado nos arquivos do repositório (pode diferir da máquina de teste) · **[hipótese]** inferência a confirmar com dados · **[a validar]** depende do banco ou do ambiente, não acessados.

### 5a. Quais tags têm IA ativa e se rótulos (TAG60–TAG81) podem entrar na IA

**Onde se define a IA ativa** [código]
- Somente no banco: tabela `TagsIaConfig` (`Models/TagIaConfig.cs`), gravada por `Controllers/TagConfigController.cs : ConfigurarTag` (`POST /api/tags/configurar`). Não há seed, `appsettings` nem arquivo no repo com a lista do PK2.
- Lida em dois pontos: `Services/MonitoramentoTagsService.cs : ExecuteAsync` (`.Where(x => x.IaAtiva)`, decide quais tags entram no ciclo) e `PredictionContextLoader.cs : BuscarConfiguracaoAtiva` (a predição só continua se a tag estiver ativa).
- **Armadilha:** em `TagIaConfig`, `IaAtiva` tem valor padrão `true`. Um `POST /api/tags/configurar` que omita o campo cria a tag **ativa**.
- **Lista ativa do PK2: [a validar].** O "TAG22–TAG33" do `CENARIOS_CP300.md` não pode ser confirmado sem o banco. Dá para conferir só com leitura via `GET /api/tags` (`TagConfigController.Listar`), que devolve todas as configurações.

**Caminhos pelos quais um rótulo poderia influenciar a IA**

| Caminho | Afeta a decisão de outra tag? | Evid. |
|---|---|---|
| Rótulo com `IaAtiva = true` em `TagsIaConfig` | Não muda outras tags, mas o rótulo vira uma "tag monitorada" própria e gera decisões/anomalias que poluem contagens e o LiveMonitor | [código] + [a validar] |
| `TagsRegimesOperacionaisIa.TagRegime` apontando para TAG60–81 (`OperationalRegimeBaselineProvider.Resolve` → `BuscarHistoricoPorPeriodo(cliente, configuration.TagRegime, …)`) | **Sim.** O baseline da tag de medição seria escolhido pelo estado do rótulo (ex.: TAG73 ou TAG76): vazamento direto do gabarito | [código] + [a validar] |
| Grupo multivariado `ModelsML/Multivariate/PK2/CP300-B01-Piloto.json` | Não: só TAG27, TAG29, TAG31. E a fusão está restrita a PK7 | [repo] |
| Baseline comportamental (`BehaviorRuntimeBaselineProvider`) e Isolation Forest | Não: usam só o histórico da própria tag | [código] |
| Dependências/relacionadas (`TagDependenciaIa`, `AnaliseRelacionadasService`, `InterpretacaoDependenciasService`) | Não: entram só na pós-decisão (explicação, risco), sem alterar `EhAnomalia` | [código] |

Conclusão: pelo código, um rótulo só contamina a decisão de uma medição via **regime operacional**. Conferir no banco (`TagsIaConfig` e `TagsRegimesOperacionaisIa`) que nenhuma TAG60–81 aparece como ativa ou como `TagRegime`.

### 5b. Tags que ficam em 0 (bomba reserva) e tags com degraus

Classificação: `TagBehaviorProfileResolver.Resolve` + `AnalyzeHistory` sobre o **histórico de detecção** (24 h do Bridge, cerca de 1.440 pontos a 1/min) [código]:

1. `TipoTag` explícito vem de `TagContextoIa.TipoRepresentacao`. A comparação é por **igualdade exata** (`ContainsAny` usa `Equals`): "estado", "discreto", "sp", "setpoint" etc. ⇒ tipo fixo com confiança 0,99. Sem contexto, usa-se `"perfil-comportamental"` ⇒ desconhecido, segue para a estatística. **Os valores no banco do PK2 estão [a validar].**
2. Nome: "TAG22"…"TAG33" não contêm `sp`/`estado`/`status`/`contador` ⇒ sem efeito [código].
3. Estatística, com `integerLike` = fração de valores inteiros:
   - **Discrete 0,97**: ≤ 4 valores distintos, razão distintos/total ≤ 0,10 e ≥ 95 % inteiros.
   - **Discrete 0,90**: ≤ 12 valores distintos, razão ≤ 0,20 e ≥ 90 % inteiros.
   - **Continuous 0,94** exige `integerLike < 0,95`. **Continuous 0,86** exige ≥ 10 distintos **e razão ≥ 0,25** (≥ 360 valores distintos em 1.440 pontos).
   - Caso contrário ⇒ **Unknown**, e `DetectorApplicabilityPolicy.Evaluate` **libera todos os detectores** (comportamento legado).

**Bomba reserva (TAG28/30/32, ou 27/29/31 conforme a principal)**
- Se a reserva **não rodou nas 24 h** da janela: 1 valor distinto (0) ⇒ **Discrete 0,97 ⇒ todos os detectores bloqueados** ⇒ "nenhum detector aplicável" ⇒ nunca há anomalia [código]. Quando a bomba liga, a classificação usa só o histórico (sem a leitura atual): a tag continua Discrete enquanto o histórico tiver ≤ 12 valores distintos (com valores inteiros), ou seja, **cerca de 10 a 12 minutos de bomba ligada** antes de os detectores voltarem [hipótese, depende da variação dos valores].
- Quando os detectores voltam, o **perfil treinado** dessa tag tende a estar **colapsado em zero**: em `HistoricoLimpezaService.RemoverOutliersPorIqr`, se ≥ ~75 % dos pontos de treino são 0, Q1 = Q3 = 0 e IQR = 0, e **todo valor ≠ 0 é removido como outlier**. Resultado: `Media = 0`, `DesvioPadrao = 0`, deadband = 0 (`TagProfileStatisticsCalculator.Calculate`) [código]. Na detecção, a escala efetiva cai no piso `max(|0| × 0,001; 1e-6)` (`BaselineProtectionProfile.ResolveEffectiveScale`), então **qualquer valor ≠ 0 dá Z e MAD = 100** [código].
- Efeito esperado: a bomba reserva/auxiliar liga ⇒ ~10 min de silêncio (bloqueio) ⇒ **alarme forte e contínuo enquanto ela roda**. Isso é compatível com os falsos positivos no rodízio e na partida da auxiliar citados no `CENARIOS_CP300.md` §6 [hipótese apoiada no código].
- Principal com ~50 % do tempo parada (rodízio): IQR não colapsa, mas o perfil mistura "ligada" e "parada", com **média no meio e desvio inflado** ⇒ Z e EWMA (que usam o perfil) perdem sensibilidade [hipótese].

**Tags com degraus (set-point, demanda, rodízio)**
- As tags monitoradas (TAG22–33) só seriam Discrete se tivessem ≤ 12 valores distintos em 24 h: improvável com ruído de medição. O caso mais provável é **Unknown (detectores liberados)**, não Discrete/Setpoint [hipótese].
- **Depende de como o Bridge entrega os valores [a validar]:** se vierem **brutos inteiros** (% × 10, bar × 100…), `integerLike = 1`, então Continuous só com ≥ 360 valores distintos em 24 h. Senão fica Unknown, que também libera os detectores. Se vierem **em unidade de engenharia** (decimais), tendem a Continuous 0,94. Nos dois casos não há bloqueio para tags com ruído.
- O risco real dos degraus é outro: cada mudança de regime normal (set-point 3,8 → 5,0 bar, demanda 900 ‰/300 ‰, rodízio, parada) é um desvio grande em relação a um perfil global único ⇒ **falso positivo**. E, se o perfil foi treinado com vários regimes, **desvio inflado** ⇒ falha real some.
- Bloqueio por tipo também pode ocorrer em **comunicação parada** (preset 4, falhas 30/33) se durar o bastante para reduzir os valores distintos. Com 15–20 min dentro de 24 h, não chega a isso [hipótese].

### 5c. Preset 3 (uma tag por vez): quem deveria reagir e se a regra ≥ 60 permite

Premissas [repo]: pesos do `appsettings.json` (Z 0,15 · MAD 0,15 · EWMA 0,15 · RoC 0,10 · Persist. 0,10 · LevelShift 0,10 · IF 0,25); **IF provavelmente não aplicável no PK2** (sem modelo em `Models/IsolationForest/`). Então o denominador é 0,75 e são precisos **≥ 45 pontos ponderados** para nota ≥ 60. Com IF seriam 60 pontos.

Referências de cada detector [código]:
- Z (`StatisticalDetector`) e EWMA usam a **média/desvio do perfil** (`TagPerfilIa`, treino).
- MAD e Persistence usam a **mediana das 24 h** do histórico (`DetectorContext`).
- RoC compara a taxa atual com as taxas das 24 h.
- LevelShift compara a mediana dos últimos 10 min com a dos 20 min anteriores.
- Deadband: o do perfil (P95 das variações consecutivas no treino). Se for 0, usa o P95 das 24 h.

| Falha (tag) | Detectores que deveriam reagir | A regra ≥ 60 permite? | Evid. |
|---|---|---|---|
| **20 Congelado** (TAG24) | **Nenhum.** O valor congela num ponto normal: desvio ≈ 0 para Z/MAD/EWMA; taxa 0 ≈ mediana das taxas para RoC; sem mudança para LevelShift. **Não existe detector de "valor parado".** Como a malha segue a leitura congelada, as tags que mudam são outras (TAG31/27/33) e só com a demanda | **Não detectável por construção** | [código] |
| **21 Drift** (TAG25, rampa 40 min) | LevelShift cedo (compara 10 min com 20 min, escala pequena) mas vale só 10 pts. Z/MAD/EWMA só quando a deriva passar de ~3 σ do perfil; Persistence depois disso. RoC ≈ 0 (rampa lenta) | Só quando Z+MAD+EWMA saturarem juntos (45) ou quase. **O σ de TAG25 inclui o perfil diário de demanda ⇒ provavelmente tarde ou nunca, conforme a amplitude de 600 ‰** | [hipótese] |
| **22 Ruído** (TAG27) | Z e MAD nas amostras com pico > 3 σ; RoC (salto entre amostras); Persistence só com ≥ 5 amostras seguidas fora. **EWMA quase não reage** (ruído de média zero se cancela); LevelShift ≈ 0 | Máximo realista sem EWMA: 15+15+10+10 = 50 ⇒ 67. **Detecção intermitente**, só em minutos com picos grandes. Se B-01 estiver como reserva, TAG27 está em 0 e cai no caso 5b | [hipótese] |
| **23 Intermitente** (TAG22, quedas a 0/4095) | Na amostra com queda: Z, MAD e RoC = 100; EWMA parcial (α = 0,25) | ≈ 40 + parte do EWMA ⇒ ~55–73 ⇒ **detecta, mas só nos minutos em que a amostragem de 1/min pega a queda**. O evento tende a ser detectado; a cobertura minuto a minuto, não | [hipótese] |
| **24 Ganho −** (TAG24, 300 ‰) | No degrau: Z/MAD/RoC/EWMA, se o salto > 3 σ. **Depois a malha de pressão traz a leitura de volta ao set-point** e a anomalia passa para TAG31/27/33/25 (todos ↑ moderado) | **Só o transiente (1–3 min)** em TAG24. Nas outras tags, desvio moderado contra σ inflado por demanda e set-point ⇒ provável falta | [hipótese] |
| **25 Sem atualização** (TAG34) | — | Tag não monitorada ⇒ "não detectado" **não é falha do detector** | `CENARIOS_CP300.md` |
| **26 Circuito aberto** (TAG29) | Degrau sustentado ao extremo: Z, MAD e RoC = 100 na 1ª amostra; EWMA satura em 1–2 amostras; Persistence após ~5 min; LevelShift após alguns min | 1ª amostra ≈ 55/75 = 73; depois ~65/75. **Deve detectar logo.** Exceção: se B-01 for a reserva no momento, TAG29 já está em 0 (caso 5b) e a falha pode não mudar nada | [hipótese apoiada no código] |
| **27 Saturação** (TAG23) | Igual a 26 (degrau sustentado ao máximo) | **Deve detectar logo**, se o valor saturado sair do deadband | [hipótese apoiada no código] |

Pontos comuns aos cenários:
- O **início de todo preset reseta a planta** (bombas paradas): espera-se uma rajada de anomalias e um período de bloqueio Discrete nas tags que foram a 0.
- Detecção **minuto a minuto** ≠ detecção de **evento**: o replay deve medir as duas.
- Se a máquina de teste tiver modelos IF para PK2, a barra sobe para 60 pontos e a nota do IF no limiar vale só 50 × 0,25 = 12,5 ⇒ **com IF, a detecção fica mais difícil**, não mais fácil.

### 5d. Como o treino escolhe o período e se dá para apontar um intervalo

| Modelo | Período usado | Dá para apontar intervalo (ex.: preset 5)? | Evid. |
|---|---|---|---|
| **Perfil univariado** (`TagPerfilIa`: média, desvio, deadband) | `PerfilTagService.TreinarPerfil` → `IHistoricoService.BuscarHistorico` ⇒ **últimas `Bridge:HorasHistorico` (24) horas no instante da chamada** de `POST /api/tags/treinar-perfil`. O request só tem `ClienteId` e `TagName`. **Ignora `JanelasTreinamentoTagsIa`** | **Não.** A única forma é chamar o treino logo após o preset 5, e mesmo assim entram as ~18,5 h anteriores. Mudar `HorasHistorico` também muda a janela de detecção (mesma chave) | [código] |
| **Isolation Forest univariado** | `IsolationForestTrainingService` → `DadosTreinamentoTagService.Obter`: usa as **janelas cadastradas e ativas** (`POST /api/janelas-treinamento`) via `BuscarHistoricoPorPeriodo(início, fim)`. Sem janelas, usa as últimas 24 h | **Sim**, por janela cadastrada. Mas não há IF do PK2 hoje, e treinar ou ativar exige aprovação | [código] |
| **Multivariado** (grupo) | `GrupoAnaliseTreinamentoController.Treinar` recebe `Inicio`/`Fim` | **Sim.** O `CP300-B01-Piloto.json` atual tem 119 amostras (~2 h), pouco para o perfil diário | [código] + [repo] |
| Baseline por regime / comportamental | Recalculados a partir do histórico de 24 h da detecção | Não diretamente | [código] |

Também não há verificação de que o período de treino estava livre de falhas (TAG73 = 0). Conferir isso é responsabilidade de quem treina.

### 5e. Achados novos que mudam prioridades

1. **Perfil de tag "quase sempre zero" colapsa em média 0 / desvio 0** pela limpeza IQR ⇒ alarmes fortes quando a bomba liga (5b). [código]
2. **Não há detector de valor congelado/comunicação parada** ⇒ falhas 20, 30, 32 e 33 não são detectáveis pelo univariado atual, por construção. [código]
3. **O perfil não aceita período de treino** ⇒ não dá para treinar com o preset 5 sem alteração de código. [código]
4. **O replay existente (`tools/IntelligenceLabReplay`) não reproduz o PK2:** força `TipoRepresentacao = "continuous"` (pula a classificação e o bloqueio Discrete), usa os pesos **padrão do código** (`new AnomalyDetectionOptions()`: Z 0,20…) e não os do `appsettings.json`, e trata uma tag fixa por execução. [código]
5. **Um rótulo pode vazar para a decisão só via `TagRegime`** do regime operacional (5a). [código] + [a validar no banco]
6. **(Sessão 3) O treino do perfil só ocorre sob demanda.** `PerfilTagService.TreinarPerfil` é chamado apenas por `Controllers/PerfilTagController.cs` (`TreinarPerfil`, `POST /api/tags/treinar-perfil`; `RetreinarPerfil`, `POST /api/tags/retreinar-perfil`). Não há agendamento, serviço em background nem treino na inicialização. [código]
