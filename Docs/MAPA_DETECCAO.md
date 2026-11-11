# Mapa da detecção univariada (IAApi)

Data: 2026-11-11 · Branch: `feat/intelligence-lab-e2e-validation` (com 51 alterações locais não commitadas, ver `AUDITORIA_STATUS.md`).

Legenda de evidência:
- **[código]**: confirmado lendo o código nesta sessão.
- **[config]**: valor lido em `appsettings.json` do repositório. Não prova o que está ativo na máquina de teste.
- **[hipótese]**: inferência, ainda não demonstrada.

Escopo: caminho automático (serviço em background) de uma tag univariada até o registro no banco e a leitura pelo LiveMonitor. Multivariado, PERF_DIAG e LiveMonitor aparecem só nos pontos onde se conectam.

---

## 1. Fluxo em ordem

| # | Arquivo : Classe.Método | O que faz / decide | Evid. |
|---|---|---|---|
| 1 | `Services/MonitoramentoTagsService.cs` : `MonitoramentoTagsService.ExecuteAsync` | `BackgroundService`, registrado em `Services/DependencyInjection/IAApiApplicationServiceCollectionExtensions.cs` (`AddHostedService`). Em loop: lê `TagsIaConfig` com `IaAtiva = true`, agrupa por cliente (`Services/Monitoring/MonitoramentoTagsBatchPlanner.CriarLotes`), busca os valores atuais **em lote** e processa as tags **uma a uma, em sequência**. No fim espera `Task.Delay(1 min)`. **Ciclo real = 60 s + soma do tempo de todas as tags.** Se a chamada em lote ao Bridge falhar, o cliente inteiro é pulado nesse ciclo (`continue`). | [código] |
| 2 | `Services/BridgeValorAtualService.cs` : `BuscarValoresAtuais` | Valor atual + `DataHora` vindos do Bridge. | [código] |
| 3 | `Services/CurrentValuePredictionService.cs` : `DetectarComValorAtual` | Se a tag não foi encontrada no lote, lança exceção (a tag é pulada, só um log de warning). Monta `AnomaliaRequest` com `Valor` (convertido para `float`) e `DataHora` = timestamp do Bridge. | [código] |
| 4 | `Services/PredictionService.cs` : `Detectar` | Orquestrador. Ordem: config ativa → **shadow multivariado** → perfil treinado → contexto → preparação → decisão → pós-decisão → finalização. Sem perfil treinado lança exceção (tag sem decisão). Se `DataHora` vier `default`, usa `DateTime.Now`. | [código] |
| 4a | `Services/AnomalyDetection/Orchestration/PredictionContextLoader.cs` : `BuscarConfiguracaoAtiva`, `BuscarPerfil`, `CarregarContextoOperacional` | Lê do banco a config IA, o `TagPerfilIa` (média, desvio, deadband) e o contexto (`TipoRepresentacao` → `tipoTag`; default `"perfil-comportamental"`). | [código] |
| 4b | `Services/AnomalyDetection/Multivariate/MultivariateShadowRuntimeCoordinator.cs` : `ProcessarLeituraAsync` | Multivariado em *shadow*, executado **antes** da detecção univariada, no mesmo fluxo sequencial (soma latência). | [código] |
| 5 | `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs` : `PrepararAsync` | (a) **Histórico**: `IHistoricoService.BuscarHistorico` → `Services/BridgeHistoricoService.BuscarHistorico`: GET **síncrono** `{Bridge}/historico?horas=N`, N = `Bridge:HorasHistorico` (24 por padrão), timeout de 60 s, **uma chamada por tag por ciclo**. (b) **Escolha do baseline**, na ordem: (1) `TagPerfilIa` (Media/DesvioPadrao/VariacaoMinimaRelevante/ConfiancaDeadband); (2) se `OperationalRegimeBaselineProvider.Resolve` aplicar, substitui histórico e estatísticas pelos do regime; (3) senão, se `BehaviorRuntimeBaselineProvider.ResolveAsync` reconhecer o comportamento e puder aplicar, substitui pelos do comportamento. Falhas em (2) e (3) seguem adiante com o perfil (*fail-open*, só `Console.WriteLine`). (c) Monta `DetectorInput`: o setter de `Historico` passa por `DetectionHistorySanitizer.Sanitize` (remove inválidos, consolida duplicados, ordena, mede gaps). | [código] |
| 6 | `Services/AnomalyDetection/Orchestration/PredictionDecisionOrchestrationService.cs` : `ExecutarAsync` | Chama o ensemble → contexto funcional → decisão runtime → fusão multivariada (se registrada) → interpretação. | [código] |
| 7 | `Services/AnomalyDetection/DetectionInputValidator.cs` : `Validate` (chamado em `EnsembleDetectionService.Detectar`) | **Entrada inválida ⇒ `EhAnomalia = false`, silenciosamente.** Erros: histórico vazio após sanitização; média/desvio não finitos; **`DataHoraAtual` anterior ao último ponto do histórico** (proteção contra vazamento temporal). Gap crítico, duplicados e fora de ordem viram apenas *warnings*. | [código] |
| 8 | `src/IAApi.Engine/Detection/Behavior/TagBehaviorProfileResolver.cs` : `Resolve` + `Services/AnomalyDetection/DetectorApplicabilityPolicy.cs` : `Evaluate` | Classifica a tag (tipo explícito no `TipoTag`, palavras no nome como `sp`/`setpoint`/`contador`/`estado`, ou estatística da série: Discrete ≥ 0,88, Counter ≥ 0,88, Continuous ≥ 0,85). **Se o tipo for Discrete/Counter/Setpoint com confiança ≥ 0,85 (`MinimumRoutingConfidence`), todos os detectores contínuos são bloqueados ⇒ "Nenhum detector aplicável" ⇒ nunca há anomalia para essa tag.** | [código] |
| 9 | `src/IAApi.Engine/Detection/*Detector.cs` : `Detectar` | 6 detectores registrados em `Services/AnomalyDetection/DependencyInjection/LegacyAnomalyDetectionServiceCollectionExtensions.cs` (Z-Score, MAD, EWMA, RateOfChange, Persistence, LevelShift) + `IsolationForestDetector` (`IsolationForestServiceCollectionExtensions.cs`). Cada um devolve `ScoreNormalizado` = `DetectorMath.NormalizarPorLimite` = \|score\| / limite × 100, limitado a 0–100. **Dentro do deadband o score vale 0** (Z, MAD, EWMA, LevelShift). Mínimos de histórico: EWMA 10, RateOfChange 3, Persistence 10, LevelShift 20+10 pontos (janelas), IF 50. | [código] |
| 10 | `Services/AnomalyDetection/EnsembleDetectionService.cs` : `Detectar` | **Decisão central**: `score = Σ(ScoreNormalizado·Peso) / Σ(Peso)` só dos detectores aplicáveis; **`EhAnomalia = score ≥ EnsembleThreshold` (60)**. A quantidade de detectores em alerta **não** entra na decisão. Limites inferior/superior exibidos vêm do Z-Score (ou do 1º aplicável). | [código] |
| 11 | `src/IAApi.Application/Runtime/Decision/PredictionRuntimeDecisionService.cs` : `Resolve` → `RuntimeAnomalyDecisionResolver.Resolve` | Se o runtime estiver desligado ou o cliente fora do canário, `EmitAnomaly` = decisão do ensemble (legado). Config: `RuntimeDecision.CanaryOnly=true`, `AllowedClientIds=["PK7"]` ⇒ **para PK2 vale o ensemble puro**. | [código] + [config] |
| 12 | `PredictionMultivariateDecisionService.ResolveAsync` (chamado no passo 6) | Fusão multivariada; config `MultivariateDecisionFusion` restrita a PK7; `MultivariateOperationalPromotion.Enabled=false`. Não altera a decisão de PK2 pela config do repo. | [config] |
| 13 | `Services/AnomalyDetection/Orchestration/PredictionPostDecisionAnalysisService.cs` : `AnalisarAsync` | Tendência, risco, relacionadas, mensagem. Não muda `EhAnomalia`. Pode buscar histórico de novo, a menos que o reuso esteja ligado (`PredictionHistoryReuseOptions`, experimento ainda não commitado). | [código] |
| 14 | `Services/AnomalyDetection/Orchestration/PredictionFinalizationService.cs` : `Finalizar` → `Services/AnomaliaHistoricoService.cs` : `Registrar` | Grava **toda** leitura (anomalia ou não) em `AnomaliaDetectada`, com `DataDeteccao = DateTime.Now` (hora do processamento). **O timestamp original da leitura não é gravado.** | [código] |
| 15 | `tools/IAApi.LiveMonitor` (React/Vite + proxy em `server/`) | Polling somente com GET (`/monitor-api/decisions`, `/history` → `/api/tags/historico/{cliente}/{tag}?horas=2`, `/diagnostics`, `/shadow`). Exibe a hora de detecção, não a hora da leitura. | [código] |
| — | `Services/Diagnostics/PerformanceDiagnostics.cs` | PERF_DIAG: ligado pela variável de ambiente `IAAPI_PERF_DIAGNOSTICS`, lida uma vez na inicialização. Mede cada etapa (`Prediction.*`, `Preparation.*`, `Decision.*`) e registra `ReadingAgeAtStartMs` (idade da leitura no início da predição). Não altera a decisão. | [código] |

---

## 2. Onde ficam thresholds, baselines e configurações

| Item | Onde | Valor no repo |
|---|---|---|
| Limiar do ensemble | `appsettings.json` → `AnomalyDetection:EnsembleThreshold`; default em `src/IAApi.Engine/Detection/AnomalyDetectionOptions.cs` | 60 |
| Pesos | `appsettings.json` `AnomalyDetection:*Weight` (**diferem dos defaults do código**) | Z 0,15 · MAD 0,15 · EWMA 0,15 · RoC 0,10 · Persist 0,10 · LevelShift 0,10 · IF 0,25 (soma 1,0) |
| Limiares por detector | `AnomalyDetection:ZScoreThreshold`, `MadThreshold`, `EwmaLambda`/`EwmaSigmaLimit`, `RateOfChangeThreshold`, `Persistence*`, `LevelShift*`, `IsolationForest:*` | 3,0 · 3,5 · 0,25/3,0 · 3,5 · 5 pts/1 min · 20/10/3,0/0,8 · IF threshold 0,62 |
| `MinHistoryPoints` | `AnomalyDetectionOptions` | 30. **Só é usado em `EnsembleV2Simulator`; não tem efeito no caminho de produção.** [código] |
| Roteamento por tipo | `DetectorApplicabilityPolicy.MinimumRoutingConfidence` (constante) + limiares em `TagBehaviorProfileResolver` (constantes) | 0,85 · 0,88 · 0,65 · 0,70 |
| Janela de histórico (detecção e treino) | `appsettings.json` `Bridge:HorasHistorico` | 24 h |
| Baseline padrão | Tabela `TagPerfilIa` no banco (`ia_config.db`), gerada por `Services/PerfilTagService.TreinarPerfil` (`POST` em `Controllers/PerfilTagController`) | Últimas 24 h **no momento do treino**; outliers removidos por IQR; deadband recomendado = percentil 95 das variações absolutas entre pontos consecutivos (`Services/TagProfileStatisticsCalculator.cs`) |
| Baseline por regime | `Services/AnomalyDetection/OperationalRegimeBaselineProvider.Resolve` (12 saídas de "não aplicado") | Depende de cadastro de regime/dependência no banco |
| Baseline comportamental | `Services/AnomalyDetection/BehaviorProfiles/Runtime/BehaviorRuntimeBaselineProvider.ResolveAsync` | Depende de snapshot comportamental no banco |
| Canário runtime / fusão | `AnomalyDetection:RuntimeDecision`, `MultivariateDecisionFusion`, `MultivariateOperationalPromotion` | Só PK7; promoção desligada |
| Intervalo do monitor | Literal `TimeSpan.FromMinutes(1)` em `MonitoramentoTagsService.ExecuteAsync` | 60 s + processamento |

---

## 3. Diagrama textual com pontos de latência (⏱)

```
[CP300] → [PowerServer/Bridge]
              │
   ⏱L1 aguarda o próximo ciclo: até (60 s + duração do ciclo anterior)
              ▼
MonitoramentoTagsService.ExecuteAsync  (1 cliente por vez, 1 tag por vez)
  ├─ ⏱L2 BridgeValorAtualService.BuscarValoresAtuais (lote do cliente)
  └─ para cada tag, em sequência:   ⏱L3 a tag N espera as N-1 anteriores
       CurrentValuePredictionService.DetectarComValorAtual
         └─ PredictionService.Detectar
              ├─ ⏱L4 MultivariateShadowRuntimeCoordinator.ProcessarLeituraAsync
              ├─ PredictionContextLoader (banco: config, perfil, contexto)
              ├─ PredictionDetectionPreparationService.PrepararAsync
              │    ├─ ⏱L5 BridgeHistoricoService.BuscarHistorico  (HTTP síncrono, 24 h, timeout 60 s)
              │    ├─ baseline: Perfil → Regime? → Comportamento?
              │    └─ DetectorInput (+ sanitização)
              ├─ PredictionDecisionOrchestrationService.ExecutarAsync
              │    └─ EnsembleDetectionService.Detectar
              │         ├─ DetectionInputValidator ──inválido──► EhAnomalia=false
              │         ├─ TagBehaviorProfileResolver + ApplicabilityPolicy
              │         │        └─ tipo não contínuo ≥0,85 ──► nenhum detector ──► false
              │         ├─ 7 detectores (deadband ⇒ score 0)
              │         └─ média ponderada ≥ 60 ? ──► EhAnomalia
              │    └─ RuntimeDecision (PK2 ⇒ legado = ensemble)
              ├─ ⏱L6 PredictionPostDecisionAnalysisService (pode buscar histórico de novo)
              └─ PredictionFinalizationService → AnomaliaHistoricoService.Registrar
                     (grava DataDeteccao = agora; hora da leitura é perdida)
                                   │
            ⏱L7 polling do LiveMonitor (~2 s + HTTP)
                                   ▼
                            LiveMonitor (exibe)
```

Latência total da mudança física até a tela ≈ L1 + L2 + L3 + (L4..L6 da própria tag) + L7. Nenhum desses números foi medido nesta sessão. O PERF_DIAG já mede L2–L6 e a idade da leitura (`ReadingAgeAtStartMs`).

---

## 4. Observações que importam para falhas de detecção [código]

1. **Diluição pela média ponderada.** Um detector no limiar vale 100 × seu peso. Com IF aplicável, Z+MAD+EWMA juntos no máximo dão 45 < 60. Uma anomalia só passa se detectores que somam ≥ 60 % do peso aplicável estiverem perto ou acima do limiar ao mesmo tempo. Detectores não aplicáveis saem do denominador (com menos detectores, fica mais fácil).
2. **Deadband zera o score.** Se o deadband do perfil for grande, desvios moderados valem 0.
3. **Baseline de 24 h no momento do treino.** Se o treino ocorreu com o processo fora do regime de teste, ou durante uma falha, média e desvio ficam deslocados ou inflados.
4. **Ordem temporal.** Leitura atual com `DataHora` < último ponto do histórico ⇒ entrada inválida ⇒ sem anomalia, e só aparece em log/diagnóstico.
5. **Roteamento por tipo.** Nome contendo `sp`/`setpoint`, `contador`, `estado`, ou série com poucos valores distintos, pode bloquear todos os detectores.
6. **Persistência e LevelShift** precisam de janelas (5 pontos/1 min; 20+10 pontos). Com cadência de cerca de 1 leitura/min, uma mudança de patamar leva cerca de 10 ciclos para ganhar peso.
