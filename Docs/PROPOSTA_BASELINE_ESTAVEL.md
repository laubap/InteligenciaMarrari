# Proposta: baseline de referência estável + histórico móvel como contexto

**Data:** 12/11/2026. **Cliente de referência:** PK2 (simulador CP300). **Branch:** `feat/intelligence-lab-e2e-validation`.
**Situação:** diagnóstico e proposta. **Nada foi implementado no runtime.** Código, configuração, thresholds e decisões operacionais não foram alterados.
**Objetivo:** preservar a capacidade da IAApi de detectar deteriorações graduais e anomalias industriais sem que o baseline móvel incorpore automaticamente comportamentos anormais.
**Fontes:** `Docs/PROBLEMAS_ENCONTRADOS.md` (PROB), `Docs/RESULTADO_PRESET1.md` (RES), `Docs/RESULTADO_PRESET6.md` (RES6) e o código citado em cada ponto.

**Níveis de evidência:** **confirmado no código**, **confirmado no replay** (código de produção sobre dados do simulador) e **hipótese**.

---

## 1. Baseline treinado × baseline por regime

| | Baseline treinado (perfil) | Baseline por regime |
|---|---|---|
| Onde nasce | `Services/PerfilTagService.cs : TreinarPerfil`, chamado sob demanda (`POST /api/tags/treinar-perfil`) | `Services/AnomalyDetection/OperationalRegimeBaselineProvider.cs : Resolve` → `OperationalRegimeBaselineService.Resolve`, **a cada leitura** |
| Dados | Últimas 24 h **no momento do treino**, com limpeza IQR | O **mesmo histórico móvel de 24 h** entregue aos detectores, filtrado pelo estado atual do regime, com IQR e mínimo de 30 amostras |
| Persistência | Gravado em `PerfisIa`; só muda se alguém treinar de novo | Não é gravado; recalculado e descartado a cada leitura |
| O que fornece | Média, desvio e deadband | Média, desvio e deadband **e também o histórico** dos detectores |

**Confirmado no código:** com o regime aplicado, `PredictionDetectionPreparationService.PrepararAsync` troca o histórico (`historicoRecebido = regimeBaseline.History`, linha 163) e as estatísticas. O perfil treinado é **ignorado por completo** e só volta quando o regime não se aplica: estado raro, poucas amostras ou falha (PROB P08). Com regime ligado, **toda a referência da tag passa a ser móvel**.

O provider recebe o histórico pronto de `PrepararAsync` e só consulta o Bridge para a tag de regime (`BuscarHistoricoPorPeriodo`). Ele é `sealed` e não tem ponto de extensão para outra janela.

## 2. Que baseline cada detector usa (confirmado no código)

| Detector | Âncora de referência | Série usada | Sem regime | Com regime |
|---|---|---|---|---|
| Z-Score (`StatisticalDetector`) | `MediaHistorica` / `DesvioPadraoHistorico` | só o valor atual | **perfil treinado (estável)** | regime (24 h móveis) |
| EWMA (`EwmaDetector`) | `MediaHistorica` / `DesvioPadraoHistorico` | média móvel do histórico (λ = 0,25, ≈ últimos 10 pontos) | **perfil treinado** como alvo | regime |
| MAD (`MadDetector`) | mediana e MAD do histórico (`DetectorContext`) | valor atual | 24 h móveis | 24 h móveis do regime |
| Persistência (`PersistenceDetector`) | mediana ± MAD do histórico | pontos recentes fora da faixa | 24 h móveis | idem |
| Taxa de variação (`RateOfChangeDetector`) | mediana das taxas do histórico | última variação | 24 h móveis | idem |
| Mudança de patamar (`LevelShiftDetector`) | mediana dos 20 min anteriores | mediana dos últimos 10 min | relativo (30 min) | idem |
| Deadband e escala mínima (`BaselineProtectionProfile`) | deadband do perfil ou P95 do histórico | — | perfil | regime |
| Tipo da tag (`TagBehaviorProfileResolver`) | — | histórico | 24 h | 24 h do regime (origem do `Discrete`, PROB P20) |

Para **deterioração gradual**, Taxa de variação e Mudança de patamar ajudam pouco: a rampa lenta não produz saltos. Sobram duas âncoras:
- Z-Score e EWMA, que olham o perfil;
- MAD e Persistência, que olham a mediana móvel.

## 3. Riscos de usar só as últimas 24 h

1. **Absorção da falha (o risco principal; PROB P25).** Uma deterioração que dura horas entra na mediana e na média móveis.
   - MAD e Persistência perdem a referência quando a falha domina a janela (perto de 50 % para a mediana). Prazo estimado: ≈12 h dentro da janela de 24 h [**hipótese**; não medido].
   - Com regime, Z-Score e EWMA também passam a ser móveis, e **nenhum detector fica ancorado** em um estado saudável.
2. **O IQR não protege.** Ele remove o que é minoria extrema, não o que é anormal: é instável e não removeu os 12 min de falha do preset 1 que estavam no treino (PROB P01). Uma rampa lenta fica dentro da faixa do IQR e entra no baseline.
   - Indício (**confirmado no replay**, RES6 §2.6): o baseline da TAG29 no regime subiu de 149,6 ± 18,7 (15:28) para 152,1 ± 21,7 (16:04) durante a rampa do preset 6. O efeito foi pequeno porque a falha durou 40 min de 24 h.
3. **Retreino contamina.** Mesmo sem regime, o perfil "estável" é refeito das últimas 24 h de quem chamar o treino. Treinar durante uma deterioração congela a deterioração como normal (PROB P13; no preset 6, 12 min de falha entraram no treino).
4. **Estados ausentes na janela** (B-02 sozinha, set-point novo, início de histórico): o regime cai no perfil global ou fica estreito (PROB P03, P08).
5. **Custo e latência:** 24 h por tag, a cada ciclo, mais uma segunda consulta para a tag de regime (PROB P10, P11).

## 4. Proposta: referência estável + contexto móvel

**Princípio:** a referência do "normal" vem de um **período treinado, escolhido e validado**, e não muda sozinha. O histórico móvel serve só para três coisas:
- saber o **estado atual** (qual regime vale agora);
- a **dinâmica de curto prazo** (taxa de variação, mudança de patamar, persistência dos últimos minutos);
- comparar o **recente contra a referência**, para medir deriva.

| Peça | O que faz | Onde |
|---|---|---|
| **A. Treino por período** | Treinar perfil global e **perfil por regime** a partir de `[início, fim)` escolhido; recusar se houver anomalia ou rótulo no período; versionar (data, período, pontos por regime) | `PerfilTagService` + armazenamento aditivo (perfis por valor de regime) |
| **B. Seleção da referência** | Ordem: perfil treinado do regime atual → perfil treinado global → (opcional, legado) regime móvel. O **histórico dos detectores continua sendo as 24 h móveis**: só média, desvio e deadband vêm da referência | `PrepararAsync` / provider, atrás de flag por cliente e tag, **padrão desligado** |
| **C. Monitor de deriva** | Mediana das últimas N h contra a referência treinada, em desvios. Pega a deterioração lenta que MAD e Persistência absorvem | Detector novo; **exige aprovação** de peso e limiar |
| **D. Shadow primeiro** | Calcula a referência nova e grava no diagnóstico (`BaselineDiagnostic`), **sem mudar a decisão** | Mesmo ponto de B; só observabilidade |

### Fases, cada uma reversível

| Fase | Escopo | Muda a decisão? |
|---|---|---|
| **F0** | Só ferramenta de replay: `--treino-inicio/--treino-fim` com a trava de TAG73 e relatório do período (**em andamento**). Depois, um modo **experimental, rotulado como não-produção**, que calcula Z e EWMA contra a referência por regime treinada | Não (ferramenta) |
| **F1** | Treino por período e perfis por regime gravados (aditivo) | Não |
| **F2** | Shadow (D) | Não |
| **F3** | Ativar B por flag, por cliente e tag, depois de aceito no replay | Sim, com aprovação |
| **F4** | Monitor de deriva (C) | Sim, com aprovação de peso e limiar |

**Rollback:** desligar a flag devolve exatamente o comportamento atual. As tabelas novas são aditivas.

## 5. Testes de regressão e aceite

1. **Byte a byte:** com a flag desligada, os replays do preset 1, da madrugada e do preset 6 (sem regime, regime recomendado e regime B-01) ficam idênticos aos CSVs atuais (0 diferenças, como já conferido entre execuções).
2. **Unitários do provider e da preparação:**
   - flag desligada ⇒ resolução idêntica;
   - referência presente ⇒ média e desvio da referência, histórico móvel intacto;
   - regime sem referência ⇒ cai no perfil global treinado;
   - referência treinada com rótulo ou anomalia ⇒ recusada.
3. **Deriva sintética longa** (série com rampa de 30 h): documenta que o modo atual com regime **deixa de alarmar** depois de X h e que a referência estável **continua**. É o teste que mede o P25, hoje hipótese.
4. **CP300, critérios de aceite:**
   - preset 6: TAG29 com primeiro alarme ≤ 15:32 e falsos fora da falha ≤ 27 (regime recomendado; RES6 §2.4);
   - preset 1 e madrugada: não piorar a tabela da RES §4;
   - preset 5: contar falsos positivos.
5. **Isolamento:** rótulos TAG60–81 continuam recusados como entrada e como regime.

## 6. Impacto esperado nos cenários CP300

- **Preset 6 (rampas de 40 min):** ganho **pequeno** na detecção.
  - Em 40 min a absorção é mínima (149,6 → 152,1).
  - O que limita o tempo de detecção é a amplitude do sinal e a mistura de estados no regime, não a janela móvel.
  - Uma referência "B-01 sozinha" treinada (164,6 ± 28,2) daria **menos** sensibilidade no início da rampa do que o regime misturado: 199 fica a 1,2σ contra 2,6σ [estimativa sobre números medidos].
  - O ganho concreto aqui é **excluir os 12 min de falha do treino** e estabilizar os falsos alarmes.
- **Presets 1, 2 e 3** (rampas de 10–40 min): mesmo raciocínio. O benefício principal é o treino limpo e controlado, não a detecção antecipada.
- **Onde o ganho apareceria:** deteriorações de **horas a dias**, que **nenhum preset do CP300 cobre**. Para demonstrar, é preciso o teste sintético (§5, item 3) ou uma injeção manual longa no simulador. Se o simulador aceita uma rampa de várias horas ainda está **a verificar** na documentação dele.
- **Risco da mudança:** uma referência treinada num período que não viu certo estado (noite, B-02 sozinha, set-point novo) gera falso alarme. Por isso a ordem de fallback e a exigência de um relatório de pontos por regime no treino.

## 7. Próximo passo

F0, primeira parte (só ferramenta): treino por período, trava de TAG73 e relatório do período e dos pontos por tag e por regime. O `--regime` mantém o comportamento de produção (24 h móveis), com a mesma trava aplicada às janelas que ele usa. O modo experimental de referência estável fica para uma tarefa seguinte.
