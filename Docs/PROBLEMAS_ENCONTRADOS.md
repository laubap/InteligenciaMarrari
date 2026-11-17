# Problemas encontrados na detecção univariada (IAApi × simulador CP300)

**Data:** 12/11/2026. **Cliente:** PK2. **Branch:** `feat/intelligence-lab-e2e-validation`.
**Fontes:** `Docs/AUDITORIA_STATUS.md` (AUD), `Docs/MAPA_DETECCAO.md` (MAPA), `Docs/RESULTADO_PRESET1.md` (RES), `Docs/RESULTADO_PRESET6.md` (RES6), `Docs/RESULTADO_PRESET3.md` (RES3), `Docs/INVENTARIO_PK2.md` (INV) e `Docs/BACKLOG.md`. Este documento **resume** essas fontes; os números completos estão nas seções indicadas.

> **Atualização com o preset 6 (12/11/2026, RES6):** o univariado **detectou** o desbalanceamento na TAG29 (15:32, ≈4 min após o início, a 94 ‰). Confirmou P01, P02, P03, P04, P07, P13, P20 e P21, com novidades em P01, P02, P03 e P20. Ajustou o alcance do P06 e trouxe um problema novo (P24). Itens marcados com **[preset 6]**.

> **Atualização com a IA real no preset 3 (13/11/2026):** a exportação das decisões gravadas pela IAApi (`tools/Cp300Export/Export-AnomaliasIaReal.ps1`, 12/11 14:00 → 13/11 02:05) trouxe só 50 decisões do PK2, todas entre 14:21 e 15:09. Dois problemas novos: P26 (cada tag avaliada a cada ~10 min) e P27 (o monitor parou de gravar às 15:09). P10 e P11 ganharam evidência da IA real, e a medição de latência subiu para o passo 1 da §4.

> **Atualização com a análise do preset 3 (13/11/2026, RES3):** **o preset 3 não rodou.** Em 12/11 às 14:20, o cenário anterior foi parado (TAG76 3 → 0), mas o TAG74 nunca foi a 3, não houve reset da planta e nenhum slot nem medição mostra falha de sensor até 13/11 07:59 (problema novo **P29**). O "teste do preset 3" foi operação normal; P26 e P27 continuam válidos (falam de cadência e parada, não de detecção). O período virou um **teste de alarme falso de 17,7 h com treino limpo** (12/11 14:15 → 13/11 07:59): sem regime 2.525, regime recomendado 1.378, combinado + set-point **297** minutos-tag. Novidades em P01, P02, P03, P08 e P22. A comparação com a IA real trouxe a **idade da leitura na decisão (0,2 a 17,2 min)**, que faltava em P18 e P26. P12 continua sem teste. Itens marcados com **[preset 3]**.

> **Atualização com o inventário do banco do PK2 (13/11/2026, `Docs/INVENTARIO_PK2.md`, INV):** a diferença "IA real ≤ 2,1 × replay 20–35" tem três causas, todas demonstradas: **(a)** o `Score` gravado é a nota ÷ 20 (2,1 = nota 42; problema novo **P30**); **(b)** 38 das 50 decisões foram **entrada inválida** com nota 0, porque a leitura do lote é mais antiga que o histórico buscado depois (**P31**; P19 deixou de ser só teórico); **(c)** o perfil de produção é de 30/10, com ~3 h de dados e a B-02 parada, e TAG28/32 estão colapsadas (**P32**). Com o perfil do banco, as 50 decisões reais foram reproduzidas offline (diferença < 0,1). O banco não tem rótulo ativo, regime, contexto, comportamento nem modelo IF do PK2 (P14 e P15 descartados no PK2).

> **Atualização com a conferência de segurança da auditoria B3.23 (16/11/2026, BACKLOG 29, fase A):** os 8 achados de segurança (B323-01 a 06, 17 e 23) foram conferidos por leitura no HEAD `05c64c3`, sem subir a API nem fazer requisições. **Todos continuam presentes** (o B323-23 em parte) e viraram **P33 a P40**. Não são problemas de detecção, mas ficam aqui pela regra da tarefa 29. Nenhuma correção foi feita; cada uma exige aprovação própria. **Antes de qualquer demo para fora da equipe, tratar pelo menos P33 e P34.**

> **Atualização com o preset 3 de 13/11 (16/11/2026, RES3 §4):** o preset 3 foi **repetido e rodou** (13/11 15:02 → 22:12; início conferido pelos rótulos, P29). Replay com treino limpo de 24 h (12/11 15:00 → 13/11 15:00) e `--historico-causal`, em quatro configurações. **Detectadas 3 de 7 falhas monitoradas** em cada uma, mas não as mesmas: saturação (27) e intermitente (23) em todas; circuito aberto (26) só no combinado; ruído (22) só sem regime e no recomendado, provavelmente por coincidência com a parada da B-02. **P12 confirmado no replay** (sensor congelado no set-point, nota 0). Falsos fora das falhas: sem regime 211, recomendado **248**, combinado **37**. O regime combinado real (Opção A) deu saída idêntica às séries derivadas. Novidades em P01, P03, P06, P07, P12, P20, P21, P22 e P29, e um problema novo: **P41** (falha de sensor dentro da malha de pressão aparece nas bombas, não no sensor). **Com aprovação, o mapa do placar passou a contar essas tags** (`efeitosPorCanal`): o combinado vai de 3 para **5 de 7**; as outras configurações não mudam. Itens marcados com **[preset 3 de 13/11]**.

> **Atualização com a medição da etapa 0 (16/11/2026, BACKLOG 6.0, `Docs/MEDICAO_ETAPA0.md`, MED0):** IA real com `HistoricoCausal` e PERF_DIAG, de 15:19 a 16:46 de 16/11, preset 1, só leitura do log.
> - **P31 resolvido em campo pelo recorte:** 0 entradas inválidas; nota 0 em 0,7 %, contra 76 %.
> - **P26 melhorou, mas continua aberto:** ~3,5 min por tag (era ~10).
> - **P10/P11:** 91 % do tempo de cada tag são **duas** buscas de 24 h.
> - **P18:** idade da leitura com p95 de 3,0 min.
> - **P27 não se repetiu.**
> - **Três problemas novos:**
>   - **P42:** a partir de 16:27, o LiveMonitor sobrecarregou o Bridge e o banco, deixou o monitor 4–5× mais lento e causou todos os timeouts da sessão;
>   - **P43:** o banco local está sem a migration RC-5 (`no such column: MapaEstados` em toda predição); **[17/11] RC-5 aplicada (BACKLOG 35.1); nenhum cliente tinha regime cadastrado**;
>   - **P44:** o log saiu sem horário e com o SQL do EF (702 MB).
>   - P42 e P43 têm prioridade alta: antes da demo e antes da RC-12.
>
> Itens marcados com **[etapa 0]**.

**Como ler o status**

| Status | Significado |
|---|---|
| **Confirmado no replay** | Observado ao rodar o código de produção sobre os dados exportados do simulador (`tools/Cp300OfflineReplay`). É simulador, não planta real. |
| **Confirmado no código** | Visto lendo o código; o efeito não foi medido. |
| **Hipótese** | Inferência ainda não demonstrada. |
| **Corrigido** | Corrigido no código de produção. **Nenhum problema está nesse estado hoje.** Quando houver solução testada, isso aparece como "solução validada no replay", e ela ainda **não** está em produção. |

**Glossário mínimo**
- **Perfil:** a média e a variação "normais" de uma tag, aprendidas no treino.
- **Regime:** um estado de operação, como bomba ligada/parada, válvula aberta/fechada ou set-point diurno/noturno.
- **Baseline por regime:** comparar a leitura só com o passado no mesmo estado.
- **Nota (0–100):** cada detector dá uma nota; a IA tira a média ponderada pelos pesos e alarma se ela for ≥ 60.
- **Rótulo / gabarito:** tags TAG60–TAG81, que o simulador usa para dizer quando há falha. A IA nunca pode lê-las.

---

## 1. Resumo

| ID | Problema | Status | Cenário que mostrou | Impacto | Prioridade |
|---|---|---|---|---|---|
| P01 | A limpeza de outliers (IQR) do treino joga fora estados normais (reserva parada, pressão noturna). **É instável:** com a janela deslocada 2 h, removeu 1.531 pontos (preset 1) e só 306 (preset 6). **Não removeu os 12 min de falha** presentes no treino. **[preset 3]** Com treino por período limpo, cortou de novo a madrugada inteira da TAG24 (304 pontos); **[preset 3 de 13/11]** e de novo com outro período limpo de 24 h (302 pontos) | Confirmado nos dados e no código | Treino do preset 1 (RES §1.5); **[preset 6]** RES6 §1.4; **[preset 3]** RES3 §1.3 | Alarme falso + falha não detectada | **Alta** |
| P02 | Bomba reserva parada gera alarme falso contínuo (TAG28/30/32; **[preset 6]** também a B-01 depois do failover; **[preset 3]** e a B-01 parada depois do rodízio: 570 minutos-tag no regime recomendado, 0 no combinado) | Confirmado no replay; solução validada no replay (TAG43 para a B-02; **regime combinado com set-point** para as duas bombas, inclusive no failover: etapa 0) | Preset 1, madrugada (RES §2.3, §4); **[preset 6]** RES6 §2.3, §2.4; PLANO_REGIME_COMBINADO §5; **[preset 3]** RES3 §2.3 | Alarme falso | **Alta** |
| P03 | A bomba alarma conforme o estado da outra bomba e o set-point; nenhuma configuração atual resolve. **[preset 6]** Também na B-02 com o regime TAG43 | Confirmado no replay; **regime combinado (própria × outra × set-point) validado no replay com séries derivadas** (etapa 0); falta suporte no código (Opção A) | Preset 1, madrugada (RES §4.2, §4.3); **[preset 6]** RES6 §2.3; **[preset 3]** RES3 §2.2–§2.3 (17,7 h normais: 1.378 → 297 com o combinado; sobram picos de demanda em paralelo); **[preset 3 de 13/11]** RES3 §4.4 (falsos fora das falhas: recomendado **248**, pior que sem regime, 211, pela B-02 sozinha; combinado 37; Opção A real = séries derivadas) | Alarme falso | **Alta** |
| P04 | Válvula de entrada fechada (TAG26) gera alarme falso; com regime, surgem alarmes de vazão alta | Confirmado no replay (de novo no preset 6); solução parcial no replay | Preset 1, madrugada (RES §2.3, §4); **[preset 6]** RES6 §2.4 | Alarme falso | Média |
| P05 | Pressão noturna (TAG24) alarma em 300 de 300 minutos de madrugada | Confirmado no replay; solução validada no replay | Madrugada (RES §4.3) | Alarme falso | **Alta** |
| P06 | Obstrução do recalque (preset 1) não detectada: a falha não se destaca do normal. **[preset 6]** Quando a falha sai da faixa normal (vibração), o univariado detecta. **[preset 3 de 13/11]** Drift da vazão (21) também não detectado: fica dentro da faixa normal, e só a relação vazão × potência mostra o erro (≈ 4,5 σ) | Confirmado no replay | Preset 1 (RES §2.1, §2.2, §4.2); **[preset 6]** RES6 §2.1; **RES3 §4.5** | Falha não detectada | **Alta** (solução longa) |
| P07 | A média ponderada dilui reações isoladas: sem IF, Z+MAD+EWMA saturados dão exatamente 60 | Confirmado no código e no replay | Preset 1 (RES §2.2; MAPA §4.1); **[preset 6]** atraso de ~2 min na TAG29 (RES6 §2.2); **[preset 3 de 13/11]** drift (21): EWMA 99,7 e mudança de patamar 100 deram nota 53,4; ruído (22) com regime: Z 100, EWMA 0, nota 62,8 em 1 min (RES3 §4.5) | Falha não detectada / atraso | Média |
| P08 | O baseline por regime volta ao perfil global (ruim) em transições, set-point novo ou poucas amostras | Confirmado no replay | Madrugada, preset 1 (RES §4.2, §4.3); **[preset 3]** 63 minutos-tag após o rodízio de 00:47 no combinado (RES3 §2.3) | Alarme falso | Média |
| P09 | Regime quase constante (bomba parada) fica com desvio 0; a proteção existe só por acaso | Confirmado no código; efeito é hipótese | Estimativa (RES §3.6) | Risco de alarme falso | Média |
| P10 | O Bridge fica ~8× mais lento com a IAApi e o simulador rodando, e a IAApi consulta 24 h de histórico por tag a cada ciclo | Lentidão do Bridge confirmada em log; **na IA real, 9 consultas de histórico estouraram o timeout de 60 s em 48 min, e essas leituras ficaram sem decisão (P26, confirmado em log)**; atraso da detecção ainda não medido. **[etapa 0] Medido (MED0 §3, §6):** cada tag faz **2** buscas de 24 h (preparação e tendência), de ~4,6 s cada com o Bridge sem carga externa, que somam **91 %** do tempo da predição; 0 timeouts no monitor; com o LiveMonitor aberto, as buscas foram a ~22 s, e os timeouts apareceram nas consultas do próprio LiveMonitor | Exportação da TAG73 (`dados/preset1_parte2.csv.pedacos.csv`); **IA real no preset 3 (P26)**; **[etapa 0] MED0** | Atraso + leituras sem decisão | **Alta** |
| P11 | O ciclo é sequencial: 60 s + soma do tempo de todas as tags | Confirmado no código; **na IA real, o ciclo do PK2 se repetiu a cada 5 a 18 min (P26)**. **[etapa 0] Confirmado em log:** ciclo do PK2 de **131 s** em média (94–162 s), ou 12 tags × ~10,5 s + lote de ~5,6 s; um ciclo começa a cada ~3,5 min; a 12ª tag espera em média 2,5 min, contra 0,5 min da 1ª | — (MAPA #1, §3); **IA real no preset 3 (P26)**; **[etapa 0] MED0 §2** | Atraso | **Alta** (era Média; subiu com P26) |
| P12 | Não há detector de valor congelado nem de falha de comunicação **no ensemble**. **[16/11]** A lógica existe (`FrozenSensorEvidenceCalculator`), mas só no canário runtime do PK7, e exige sinal `Continuous`: a TAG24 sai `Unknown` | Confirmado no código; **[preset 3 de 13/11] confirmado no replay:** a falha 20 (TAG24 congelada em 450, o set-point) deu nota 0 em todos os detectores, nos 30 min, nas 4 configurações | Presets 3/4 (AUD §5c); **RES3 §4.5** | Falha não detectada | **Alta** |
| P13 | O treino do perfil usa sempre as últimas 24 h, sem escolher período, e só roda sob demanda | Confirmado no código; **[preset 6]** contaminação confirmada nos dados; **[preset 3]** o treino por período limpo (replay) não resolve o P01 | — (AUD §5d, §5e-6); **[preset 6]** RES6 §1.2 | Risco (treino contaminado) | **Alta** |
| P14 | O código aceita um rótulo (TAG60–TAG81) como tag de regime | Confirmado no código | — (AUD §5a) | Risco (vazamento do gabarito) | **Alta** (barato) |
| P15 | `IaAtiva` é `true` por padrão ao cadastrar uma tag | Confirmado no código | — (AUD §5a) | Risco | Baixa |
| P16 | Os pesos do `appsettings.json` diferem dos padrões do código | Confirmado no código | — (AUD §3, §5e-4) | Risco (ferramentas divergentes) | Baixa |
| P17 | `MinHistoryPoints` (30) não tem efeito em produção | Confirmado no código | — (AUD §3) | Risco (configuração enganosa) | Baixa |
| P18 | O registro grava a hora do processamento, não a hora da leitura | Confirmado no código; **[preset 3]** idade da leitura na IA real de **0,2 a 17,2 min**, confirmada nos dados (valores do lote do início do ciclo). **[etapa 0]** Medida pelo PERF_DIAG: na decisão, p50 de 1,7 min, **p95 de 3,2 min** e máx. de 12,0 min (3,5 min sem o LiveMonitor); meta de p95 < 2 min não atingida | — (MAPA #14); **[preset 3]** RES3 §2.4; **[etapa 0]** MED0 §4 | Atraso invisível | **Alta** (era Média) |
| P19 | Entrada inválida (leitura mais antiga que o histórico) vira "sem anomalia" em silêncio | Confirmado no código; não ocorreu no preset 1; **na IA real, 38 de 50 decisões (12/11), reproduzido (P31)** | — (MAPA #7; RES §2.2); **IA real 12/11 (INV §4.2)** | **Falha não detectada** (era Risco) | **Alta** (era Baixa; ver P31) |
| P20 | Tag com poucos valores distintos pode ser classificada como "discreta" e ter todos os detectores bloqueados | Confirmado no código; ocorreu nos replays com regime do preset 1 (73 min, não relatado na RES) e **[preset 6]** (TAG28/32, B-02 parada; TAG27/31 na config. 3) | TAG44 (RES §2.1); **[preset 6]** RES6 §2.2 | Falha não detectada (hipótese) | Baixa |
| P21 | Alarmes que caem por acaso no horário da falha parecem acertos | Confirmado no replay (de novo no preset 6, inclusive no failover); **[preset 3 de 13/11]** a "detecção" do ruído (22) sem regime e no recomendado cai só nos minutos com a B-02 parada; e a regra excluiu o circuito aberto (26) por 3 alarmes antes | Preset 1 (RES §2.1, §4.2); **[preset 6]** RES6 §2.1, §2.3; **RES3 §4.4–§4.5** | Risco de avaliação errada | Média |
| P22 | TAG23 (sucção) teve 40 alarmes de madrugada. **[preset 3]** 45 alarmes iguais nas 3 configurações, sempre com o nível do reservatório num extremo; causa: hipótese | Confirmado no replay | Madrugada (RES §4.3); **[preset 3]** RES3 §2.3 | Alarme falso | Baixa |
| P23 | Código em execução fora de commit | **Resolvido**: runtime e ferramentas commitados; os 5 `.patch` estavam incorporados e foram movidos para fora do repositório (16/11) | — (AUD §1; git em 12/11) | Risco | Baixa |
| **P24** | **[preset 6]** Falha encadeada mascarada: com a nota já saturada, uma segunda falha na mesma tag não muda nada | Confirmado no replay | Preset 6 (RES6 §2.1, §2.2) | Falha não detectada (agravamento invisível) | Média |
| **P25** | Absorção da falha pelo baseline móvel: uma deterioração longa entra nas 24 h móveis e passa a ser "normal"; com regime, nenhum detector fica ancorado em referência estável | **Hipótese** (mecanismo confirmado no código; efeito a medir com o teste de deriva sintética) | Indício pequeno no preset 6 (RES6 §2.6); proposta em `Docs/PROPOSTA_BASELINE_ESTAVEL.md` | Falha não detectada (deterioração lenta) | **Alta** (medir) |
| **P26** | A IA real avaliou cada tag do PK2 só 3 a 5 vezes em ~48 min (12/11 14:21–15:09): cerca de uma avaliação a cada ~10 min por tag, enquanto os dados chegam a 1 por minuto. Parte das tentativas estourou o timeout de 60 s do Bridge e ficou sem decisão | Confirmado em log (decisões da API + Event Viewer); efeito na detecção é inferência. **[etapa 0] Melhorou, mas continua aberto:** uma avaliação a cada **3,5 min** por tag (p95 de 3,9; máx. de 4,1 sem o LiveMonitor; 16,2 com ele); **14,6 avaliações por tag por hora** e **24 % dos minutos-tag**, contra a meta de 60/h e ≥ 90 % | IA real em 12/11 (`dados/ia_real_preset3.csv`; o preset 3 não rodou, P29); **[preset 3]** idade da leitura até 17,2 min (RES3 §2.4); **[etapa 0]** MED0 §2 | Falha não detectada / atraso (~76% dos minutos sem avaliação em 16/11; ~90% em 12/11) | **Alta** |
| **P27** | A IAApi parou de gravar decisões do PK2 (e do PK7) às 15:09 de 12/11, sem nenhum aviso ou erro no log, e só voltou depois de reiniciar em 13/11. Causa desconhecida | Parada confirmada nos dados e no Event Viewer; **causa: hipótese**. **[etapa 0] Não se repetiu** em ~87 min (log em arquivo, sem janela de console, PERF_DIAG sem lacunas > 90 s); a causa continua em aberto | IA real no preset 3; **[etapa 0]** MED0 §6.4 | Monitoramento parado sem alerta: cerca de 11 h de teste perdidas | **Alta** |
| **P28** | O endereço do Bridge está fixo no código (`http://localhost:5001`) em `BridgeValorAtualService` e `BridgeTagsService`, sem configuração nem ponto de substituição para teste. Só o histórico lê `Bridge:BaseUrl` | Confirmado no código (13/11). **Corrigido em 16/11 (BACKLOG 22, passos 1 e 2, sem commit):** os dois serviços recebem o `HttpClient` pela DI, com o endereço de `Bridge:BaseUrl`; testes offline com handler falso. **[etapa 0]** Linhas `Bridge lote…` conferidas em 16/11: contagens e tempos normais (MED0 §7) | — (levantamento da BACKLOG 19) | Risco: configuração enganosa; o laço do monitor não pode ser testado offline com tags | Média |
| **P29** | **[preset 3]** O preset 3 de 12/11 **não iniciou** no simulador (TAG74 nunca foi a 3; TAG76 ficou em 0 desde 14:20; sem reset da planta nem falhas nos slots), e nada na rotina de teste conferiu o início. O período foi tratado como teste do preset 3 até a análise dos rótulos | Confirmado nos dados; **causa: hipótese** (comando de início não escrito ou não aceito). **[13/11] Repetido e rodou** (15:02 → 22:12), conferido pelos rótulos (RES3 §4.2); a conferência continua fora do roteiro automático | Preset 3 (RES3 §1.2, §4.2) | Teste perdido sem aviso; risco de avaliação errada | **Alta** (barato) |
| **P30** | O `Score` gravado em `AnomaliaDetectada` (e exportado como `nota_final`) é a **nota do ensemble ÷ 20** (0–5), enquanto o replay grava a nota de 0 a 100. A comparação de notas da RES3 §2.4b misturou as escalas. **[13/11] Corrigido nas análises** (não no runtime): a exportação ganhou `nota_0_100` (`Score × 20`), o README §1.1 diz a escala de cada coluna, `dados/ia_real_preset3.csv` tem a coluna e a RES3 §2.4b foi refeita na escala 0–100 | Confirmado no código e nos dados (131 anomalias com `Score` 3,0–3,7); **correção da análise conferida** (50 de 50 iguais ao INV §4.3) | IA real 12/11 (INV §4.2a) | Risco de avaliação errada (o placar não é afetado: usa só a decisão) | Média (barato); **pendente só o LiveMonitor** |
| **P31** | O valor atual vem do **lote do início do ciclo**, mas o histórico de 24 h é buscado depois. Quando o histórico já tem um ponto mais novo, a entrada é **inválida** e a IA grava nota 0, "sem anomalia" (P19). Em 12/11, **38 de 50** decisões; só ≈ 2 % dos minutos-tag foram de fato avaliados | Confirmado no código; **reproduzido** offline (50 de 50); efeito em outros dias: hipótese (zeros em 67–91 % das decisões desde 05/11). **[etapa 0] Correção (`HistoricoCausal`) confirmada em campo:** 0 entradas inválidas; 270 de 270 recortadas, 0 acima da tolerância; nota 0 em 2 de 269 (0,7 %), ambas válidas; sem o recorte, 67 % seriam inválidas | IA real 12/11 (INV §4.2b, §4.3); **[etapa 0]** MED0 §5 | **Falha não detectada** (a maioria das leituras não chega aos detectores) | **Alta** |
| **P32** | O perfil de produção do PK2 (`PerfisIa`) é de **30/10**, com **161–232 pontos** (~3 h) e a B-02 quase sempre parada: TAG28 (0,35 ± 0,72, deadband 2) e TAG32 (0,03 ± 0,17, deadband 0,04) **colapsadas**. Nunca retreinado | Confirmado nos dados e no código; efeito com a B-02 ligada: hipótese | Inventário (INV §2) | Alarme falso (B-02 ligada) e falha não detectada (desvio inflado em TAG25/26) | **Alta** |
| **P33** | `POST /api/dev/reset` apaga análises, perfis, configurações, processos, equipamentos, dependências e contextos de **todos os clientes**, sem autenticação e em qualquer ambiente (B323-01) | Confirmado no código (16/11). **Corrigido em 16/11 (BACKLOG 30, sem commit):** 404 fora de Development e fora de loopback, com testes | — (auditoria B3.23; BACKLOG 29, 30) | Risco: perda total da configuração e do histórico de decisões | ~~Alta~~ resolvido no código |
| **P34** | A API não tem autenticação nem autorização. Fora os endpoints do Intelligence Lab, qualquer um que alcance a porta lê e altera dados de todos os clientes (B323-02) | Confirmado no código (16/11). **Mitigado para a demo em 16/11 (BACKLOG 30, sem commit):** o `Start-IAApiComLog.ps1` recusa endereço que não seja local. A falta de autenticação continua | — (auditoria B3.23; BACKLOG 29, 30) | Risco: exposição e alteração de dados entre clientes | **Alta** (definitiva: G) |
| **P35** | `GET /api/tags/{tagName}/relacoes` filtra só pelo nome da tag e devolve processos e equipamentos de qualquer cliente com tag de mesmo nome (B323-03) | Confirmado no código (16/11) | — (auditoria B3.23; BACKLOG 29) | Risco: leitura de dados de outro cliente | Média |
| **P36** | Vincular tag a equipamento usa o `ClienteId` do pedido, sem comparar com o cliente do equipamento (B323-04) | Confirmado no código (16/11) | — (auditoria B3.23; BACKLOG 29) | Risco: vínculo entre clientes diferentes | Média |
| **P37** | Vincular equipamento a processo confere só se os dois IDs existem, não se são do mesmo cliente (B323-05) | Confirmado no código (16/11) | — (auditoria B3.23; BACKLOG 29) | Risco: vínculo entre clientes diferentes | Média |
| **P38** | O feedback de anomalia aceita motivo de qualquer cliente (e motivo desativado): confere só o ID (B323-06) | Confirmado no código (16/11) | — (auditoria B3.23; BACKLOG 29) | Risco: taxonomia de falhas misturada entre clientes | Média |
| **P39** | A resposta bruta da LLM e o corpo de erro HTTP do LM Studio vão para o log; `POST /api/anomalias/tag` escreve no console o pedido e a resposta inteiros e devolve o **stack trace** ao chamador (B323-17) | Confirmado no código (16/11). **Corrigido em parte em 16/11 (BACKLOG 30, sem commit):** sem stack trace na resposta, resposta bruta da LLM só em Debug e erro da LLM sem o corpo. Falta: console com pedido e resposta inteiros e `ex.Message` em outros controllers | — (auditoria B3.23; BACKLOG 29, 30) | Risco: vazamento de dados industriais e de detalhes internos | Média (resto: Baixa) |
| **P40** | Dependências com alertas: SQLite nativo (alta) na API e em todos os projetos com EF; `System.Security.Cryptography.Xml`/`Pkcs` (alta) nos testes e em 2 ferramentas; `tinypool` (crítica) e `vitest` (moderada) no LiveMonitor, só em desenvolvimento (B323-23) | Confirmado em `dotnet list package --vulnerable` e `npm audit` (16/11) | — (auditoria B3.23; BACKLOG 29) | Risco: falha explorável em biblioteca de terceiros | Média |
| **P41** | **[preset 3 de 13/11]** Falha no sensor de pressão PT-02 (TAG24), que está dentro da malha de controle, **não aparece na própria tag**: congelado (20) e ganho errado (24) deixam a leitura no set-point, e o efeito vai para as bombas (frequência, corrente, potência, vazão). O mapa do placar só olhava a tag do sensor | Confirmado nos dados e no replay. **Avaliação corrigida em 16/11 (aprovado):** o mapa ganhou `efeitosPorCanal` (canal 3 → tags da malha); com ele, o combinado detecta 20 e 24 nas bombas (3 → 5 de 7). A detecção da IA não mudou | Preset 3 de 13/11 (RES3 §4.2, §4.4) | Falha não detectada na tag alvo; risco de avaliação errada (resolvido no placar) | **Alta** (detecção); avaliação resolvida |
| **P42** | **[etapa 0]** O LiveMonitor sobrecarrega o Bridge e o banco. Para um gráfico de 2 h, cada consulta de histórico busca **24 h** no Bridge; o proxy desiste em 12 s, mas a busca continua (até 12 simultâneas). As decisões vêm de `GET /api/anomalias` a cada 2 s, que lista **todas** as decisões de todos os clientes e faz **uma consulta de feedback por linha** (N+1, ~3.200 por chamada) | Mecanismo confirmado no código; efeito confirmado em log (16/11, a partir de 16:27); a causalidade é inferência forte, por tempo e mecanismo | Sessão da etapa 0 (MED0 §6.2) | Monitor 4–5× mais lento (ciclo de 131 s → 306 s e ≥ 697 s; idade da leitura até 12 min); 4 timeouts; 1 leitura sem decisão; log de 702 MB (P44). A ferramenta da demo degrada a IA que ela mostra. **[17/11] Decisões corrigidas no código, confirmado em teste offline (BACKLOG 34.1–34.2, sem commit): 2 consultas por chamada, filtro por cliente e `desde`. Não medido em sessão.** **[17/11] Histórico corrigido no código, confirmado em teste offline (BACKLOG 34.3–34.4, sem commit): a IAApi busca no Bridge só o período pedido (2 h; período vazio → 24 h, como antes), e o proxy do LiveMonitor espera a IAApi (125 s), não cancela a chamada quando a página fecha e faz uma busca por tag de cada vez; histórico a cada 60 s. Não medido em sessão** | **Alta** (antes da demo) |
| **P43** | **[etapa 0]** O banco local (`ia_config.db`) está **sem a migration RC-5**. Toda predição falha ao ler `TagsRegimesOperacionaisIa` (`no such column: t.MapaEstados`), e o provider cai em silêncio no baseline global. A IAApi não aplica migrations ao subir | Confirmado em log (270 de 270 predições) e no código. **[17/11] Confirmado no banco (BACKLOG 35.0):** 32 de 33 migrations aplicadas, só a RC-5 pendente; `TagsRegimesOperacionaisIa` com 0 linhas (PK2 e PK7 sem regime). **RC-5 aplicada pela 35.1** (cópia de segurança com SHA-256; dados iguais) | Sessão da etapa 0 (MED0 §6.3) | Nenhum efeito na decisão até aqui (nenhum cliente tem regime). Sem a 35.1, **a RC-12 não teria efeito**, sem nenhum aviso; o aviso continua faltando (35.3) | **Alta** → **banco corrigido em 17/11**; falta a 35.2 (log sem o erro) |
| **P44** | **[etapa 0]** O log da sessão sai **sem data e hora** no início das linhas: o `TimestampFormat` passado pelo `Start-IAApiComLog.ps1` não teve efeito, embora as outras variáveis tenham tido. Com `LogLevel:Default = Information`, o EF grava **cada comando SQL** (960 mil entradas, 702 MB em 87 min). O texto está na página de código do console (acentos trocados) | Confirmado em log; causa do horário ausente: hipótese | Sessão da etapa 0 (MED0 §1, §6.2) | Linha do tempo só pelo PERF_DIAG (sem ele, nenhuma); passo a do P27 incompleto; disco e custo de log; análise difícil | Média (barato; antes da próxima sessão longa) |

---

## 2. Fichas por problema

### P01. A limpeza IQR do treino remove regimes normais

- **O que acontece.** Antes de calcular a média e o desvio "normais", o treino joga fora como erro os valores muito diferentes da maioria (regra IQR). Mas, nesta planta, "diferente da maioria" muitas vezes é só outro estado normal: a bomba reserva parada ou a pressão reduzida à noite. Resultado:
  - o perfil da B-02 passa a descrever só "B-02 ligada";
  - o da TAG24 passa a descrever só o dia;
  - nas tags da B-01, que ficou mais tempo parada, nada sai, e o perfil mistura ligada e parada, com desvio enorme.
- **Evidência** (confirmado nos dados, RES §1.5):

  | Tag | Removidos pelo IQR | O que foi removido |
  |---|---|---|
  | TAG24 | 305 | Toda a madrugada (set-point noturno) |
  | TAG28 / TAG32 / TAG30 | 374 / 353 / 499 | Reserva parada, até 35 % dos pontos |
  | TAG27/29/31 (B-01) | 0 | Nada; o perfil fica misturado (TAG31: 254 ± 193) |

  Não havia falha rotulada no treino (TAG73 = 0), então a limpeza não tirou falhas: tirou só dados normais. São **1.531 pontos** no total.
- **Causa no código:** `Services/HistoricoLimpezaService.cs : HistoricoLimpezaService.RemoverOutliersPorIqr`, chamada por `Services/TagProfileStatisticsCalculator.cs : TagProfileStatisticsCalculator.Calculate`, a partir de `Services/PerfilTagService.cs : PerfilTagService.TreinarPerfil`.
  - Quando ≥ 75 % dos pontos são 0, o IQR vale 0 e todo valor ≠ 0 é removido (AUD §5b).
- **Solução testada:**
  - **Baseline por regime** (RES §3.6, §4). Dentro de cada regime, a limpeza deixa de cortar o estado normal (TAG24 de noite: 0 de 299 removidos). Funcionou nas tags com regime bem definido (P02, P04, P05).
  - Fica a falhar onde o regime ainda mistura casos (P03) e onde o perfil global continua em uso (P08).
- **[preset 6] A limpeza é instável e não tira falhas** (RES6 §1.4, §2.7):

  | Treino | Fim da janela | Pontos removidos | Minutos com TAG73 = 1 no treino | Desses, removidos |
  |---|---|---|---|---|
  | Preset 1 | 11/11 12:55 | **1.531** (TAG24, 28, 30, 32) | 0 | — |
  | Preset 6 | 11/11 15:06 | **306** (só TAG24) | **12** (obstrução 13:28–13:39) | **0** em todas as tags |

  - **Instável:** deslocar a janela em ~2 h mudou em 5 vezes o que é removido. O perfil de uma mesma tag depende de quando alguém chama o treino.
  - **Não protege contra contaminação:** os 12 minutos da obstrução ficaram no perfil, porque os valores da falha estavam dentro da faixa normal. A limpeza remove regimes normais (noite, reserva parada) e deixa passar a falha. Ver P13.
- **[preset 6] O resultado muda só por mover a janela** (RES6 §1.4). Com o treino até 15:06, apenas 2 h depois do preset 1, a B-02 ficou parada 32,5 % do tempo (contra 24,6 %). O Q1 caiu para ~0 e o IQR **não cortou nada** nas bombas: 306 pontos removidos no total (só a TAG24 noturna), contra 1.531. As duas bombas passam a ter perfil misturado (TAG32: 266,9 ± 188,1 contra 398,7 ± 40,5). A mesma regra dá resultados opostos conforme o tempo parado fique acima ou abaixo de 25 %.
- **[preset 6] Dentro do regime, o IQR corta a operação minoritária:** no regime TAG43 = 2, corta a B-02 sozinha em carga alta (TAG28 > 234, TAG30 > 174,5; RES6 §2.3). Ver P03.
- **[preset 3] O treino por período limpo não resolve** (RES3 §1.3). Com o treino escolhido em [11/11 18:00, 12/11 14:15) (1.215 min, TAG73 = 0 confirmado), o IQR removeu **304 pontos da TAG24**, a madrugada inteira, e nada nas bombas: a B-02 ficou parada ~31 % do tempo, acima do limiar de 25 % que faz o IQR deixar os zeros. Escolher o período tira a falha do treino (P13), mas a limpeza continua cortando um regime normal. Sem regime, a TAG24 alarmou **299 dos 300 minutos** da madrugada seguinte (P05).
- **[preset 3 de 13/11] Repetiu com outro período limpo** (RES3 §4.3): treino [12/11 15:00, 13/11 15:00), 1.437 min, TAG73 = 0. O IQR removeu **302 pontos da TAG24** (a madrugada) e nada nas outras tags. Nas falhas de pressão do replay (15:00–22:59), a TAG24 não sofreu com isso, porque o set-point era o diurno.
- **BACKLOG:** 9 (opção b: proteger o IQR para séries com maioria de zeros), 12 e 8.

### P02. Alarmes falsos da bomba reserva B-02 (TAG28/30/32)

- **O que acontece.** Com a B-02 parada (valor ~0), as tags dela ficam muito longe do perfil, que só conhece "B-02 ligada" (P01). A IA alarma o tempo todo enquanto a reserva está parada. Quando a reserva de fato liga, as notas **caem**.
- **Evidência** (confirmado no replay):

  | Janela | TAG28 / TAG30 / TAG32 sem regime | Com regime TAG43 |
  |---|---|---|
  | Preset 1 (91 min) | 61 / 73 / 73 alarmes | **0 / 0 / 0** |
  | Madrugada (331 min) | 20 / 45 / 87 | **0 / 0 / 10** |

  Fontes: RES §2.3, §4.2 e §4.3. No preset 1, 9 desses alarmes caíram dentro da falha por coincidência (P21).
- **Causa no código:** P01 (perfil sem os zeros) mais `src/IAApi.Engine/Detection/StatisticalDetector.cs`, `MadDetector.cs` e `EwmaDetector.cs` (`Detectar`), que medem a distância ao perfil. Na madrugada, a `PersistenceDetector.Detectar` soma 100 depois de ~5 min.
- **Solução testada:** baseline por regime com TAG43 (estado da B-02). **Validada no replay** (RES §4). Não está em produção: exige cadastrar em `TagsRegimesOperacionaisIa` e aprovação.
  - O que sobra: 10 min na TAG32 logo após o rodízio da madrugada (00:44–00:55), da mesma família de P03.
- **[preset 6] Novidades** (RES6 §2.3, §2.4):
  - **O IQR não é a única causa.** No preset 6 o treino manteve os zeros (P01), e mesmo assim a reserva parada alarmou: TAG28/30/32 = 10/39/78 minutos sem regime. A nota fica em 62,8 com Z = 47, mas MAD = 100 (distância à mediana das 24 h), EWMA = 100 e Persistência = 100.
  - **O regime TAG43 eliminou os 127 alarmes.** Com a B-02 parada, TAG28/32 viram `Discrete` e os detectores são bloqueados (P20).
  - **A B-01 parada depois do failover alarma pelo mesmo mecanismo** (TAG27 16:54–17:11, TAG31 16:55–17:11, TAG29 16:52–17:11). A B-01 não tem regime na configuração recomendada.
- **[Etapa 0 do regime combinado] Solução que cobre as duas bombas** (`Docs/PLANO_REGIME_COMBINADO.md` §5; replay com séries derivadas `RB01S/RB02S` = própria bomba × outra bomba × set-point, sem mudar código):
  - A reserva parada vira regime próprio (`Discrete`, detectores bloqueados), para a B-02 e também para a **B-01 depois do failover**.
  - No preset 6, os alarmes do failover caíram de 110 (regime recomendado) para **0**.
  - Na madrugada, os alarmes das bombas com regime aplicado ficaram em 3 (B-01) e 2 (B-02). Os outros 79 + 30 vieram do fallback para o perfil global na primeira noite do arquivo, que não tem noite anterior (P08) [hipótese: some com histórico de 48 h].
- **[preset 3] Rodízio de bombas em operação normal** (RES3 §2.3; replay de 17,7 h, treino limpo). O rodízio das 20:47 de 12/11 parou a B-01 por 4 h:
  - sem regime e com o regime recomendado (a B-01 não tem regime nele): **570 minutos-tag** (TAG27 185, TAG29 149, TAG31 236);
  - com o combinado + set-point: **1** (no minuto da troca). A B-01 parada vira `Discrete` e os detectores são bloqueados (P20).
  - Sem regime, a B-02 parada também alarmou: 117 minutos na TAG32 à tarde e ≈ 700 minutos-tag na madrugada.
- **BACKLOG:** 9 (opção a, agora testada) e 12.

### P03. Alarmes da bomba B-01 (TAG27/29/31)

- **O que acontece.** A corrente, a vibração e a frequência da B-01 mudam conforme três fatores: se ela está ligada, se a B-02 está ligada junto e qual é o set-point. Nenhuma configuração atual cobre os três:
  - **Sem regime**, o perfil mistura tudo. De dia alarma pouco, mas de madrugada (set-point mais baixo, outra carga) alarma quase o tempo todo.
  - **Com TAG42** (estado da B-01), o estado "B-01 ligada" fica dominado pela operação em paralelo com a B-02. A B-01 sozinha vira anomalia: de dia ela passa a alarmar **sempre que a B-02 desliga**.
- **Evidência** (confirmado no replay; soma de TAG27 + TAG29 + TAG31):

  | Janela | Sem regime | Com TAG42 |
  |---|---|---|
  | Preset 1 (dia) | **23** | **190** |
  | Madrugada | **566** | **87** |

  - Com TAG42, os alarmes de dia começam às 13:02 (B-02 desliga) e param quando ela liga (RES §4.2).
  - De madrugada, os que sobram com TAG42 caem onde a B-01 opera sozinha com o set-point noturno (00:01–00:42 e 04:43–05:00; RES §4.3).
  - Na janela de treino, os estados (B-01, B-02) foram: ambas 560 min, só B-02 520 min, só B-01 353 min (RES §3.3).
- **Causa no código:**
  - `Models/TagRegimeOperationalIa.cs` e o índice único (`ClienteId`, `TagName`) em `Data/AppDbContext.cs` permitem **uma só** tag de regime por tag.
  - `Services/AnomalyDetection/OperationalRegimeHistoryResolver.cs : Resolve` compara um único valor de estado.
  - Dentro do regime, `TagProfileStatisticsCalculator.Calculate` aplica o IQR e corta a operação sozinha, que é minoria (142–167 pontos; RES §3.6).
- **[preset 6] O mesmo problema na B-02 com o regime recomendado** (RES6 §2.3). Depois do failover, a B-02 operou **sozinha** (16:52–17:11) a TAG28 232–271, valores normais no treino (máx. 284). O regime "B-02 ligada" (TAG43 = 2) tem média 168 ± 20, porque é dominado pela operação em paralelo, e o IQR do regime corta a B-02 sozinha em carga alta. Resultado: **56 minutos-tag de alarme** (TAG28 20, TAG30 19, TAG32 17) contra 10 sem regime. No preset 1 isso não apareceu porque a B-02 nunca operou sozinha. A B-01 sozinha em carga alta voltou a alarmar antes das partidas da auxiliar (TAG27/31/33: 11 min).
- **[preset 6] TAG42 como regime da B-01** (RES6 §2.6), somada ao regime recomendado:
  - adianta o primeiro alarme da TAG29 de 15:32 (94 ‰) para 15:30 (49 ‰);
  - mas a TAG29 também alarma 5 vezes **antes** da falha, com nota até 69,0, acima da nota da "detecção" (61,1). O ganho não é confiável;
  - TAG27/31 passam a alarmar sempre que a B-01 opera sozinha: os falsos fora da falha sobem de 27 para 86;
  - o lado bom: com a B-01 parada no failover, as tags dela viram `Discrete` e os alarmes do failover na B-01 somem (110 → 56).
  
  Confirma de novo que só um regime que separe "sozinha" de "em paralelo" resolve [hipótese].
- **[Etapa 0] Regime combinado testado no replay, sem mudar código** (`Docs/PLANO_REGIME_COMBINADO.md` §5). Séries derivadas no CSV: `RB01/RB02` (0 parada, 1 sozinha, 2 paralelo, a partir de TAG42/TAG43) e `RB01S/RB02S` (+10 com o set-point noturno, TAG46 = 380). Contra o regime recomendado atual:

  | Medida | Recomendado | Combinado | Combinado + set-point | Meta |
  |---|---|---|---|---|
  | B-01, preset 1, fora da falha | 23 | 6 | **4** | ≤ 5 |
  | B-01, madrugada | 566 | **6** | 82 (79 por fallback na 1ª noite; 3 com regime) | ≤ 30 |
  | B-02 sozinha após o failover, preset 6 | 56 | 40 | **0** | ≤ 5 |
  | Preset 6, falsos fora da falha | 27 | 20 | **20** | ≤ 27 |
  | TAG29, primeiro alarme | 15:32 | 15:32 | **15:32** | ≤ 15:32 |

  - **Confirmado no replay:** separar parada, sozinha e paralelo resolve a B-01. O set-point é necessário para a B-02 sozinha, porque sem ele o regime mistura dia e noite (madrugada 130, failover 40).
  - Com o set-point, todas as metas são atendidas, exceto a madrugada da B-01, que depende de histórico noturno anterior [hipótese].
  - Sinal novo: às 13:37–13:38, dentro da obstrução do preset 1, as 4 tags das bombas alarmam na entrada em paralelo (frequência 435 contra 396 ± 7). A partida normal das 14:00 não alarma. Ainda é **hipótese** de detecção (P06/P21).
  - **Próximo passo:** Opção A do plano com 3 componentes (própria, outra e TAG46), depois de confirmar com um CSV de 48 h.
- **[preset 3] Confirmação em 17,7 h de operação normal com treino limpo** (RES3 §2.2–§2.3; 3 rodízios, picos de demanda, madrugada):

  | Configuração | Total | Dia | Madrugada (00h–05h) | B-01 (TAG27/29/31) |
  |---|---|---|---|---|
  | Sem regime | 2.525 | 1.027 | 1.498 | 907 |
  | Recomendado | 1.378 | 990 | 388 | 907 |
  | **Combinado + set-point** | **297** | **184** | **113** | **64** |

  - Com o recomendado, a B-02 **sozinha** depois do rodízio das 20:47 alarmou ≈ 160 minutos-tag (TAG28/30/32); com o combinado, 17.
  - **O que sobra no combinado:** ≈ 95 dos 184 alarmes de dia caem nos três **picos de demanda com as duas bombas em paralelo** (12/11 17:51–18:00 e 18:38–18:51; 13/11 07:08–07:16; vazão de até 991, frequência de 421–425). Isso é compatível com a hipótese de que o regime "paralelo" precisa de uma dimensão de demanda (PLANO §5.6.1; BACKLOG 15) [hipótese; notas por detector não abertas].
- **[preset 3 de 13/11] Tarde e noite com o preset 3 rodando** (RES3 §4.3–§4.4; treino limpo de 24 h; 230 min fora das falhas × 13 tags):

  | Configuração | Falsos fora das falhas | B-01 (TAG27/29/31) | B-02 (TAG28/30/32) |
  |---|---|---|---|
  | Sem regime | 211 | 142 | 29 |
  | Recomendado | **248** | 142 | **75** |
  | **Combinado + set-point** (= combinado real) | **37** | **0** | **5** |

  - **O regime recomendado foi pior que o sem regime** nesta tarde: a B-02 operou **sozinha** com a B-01 parada (15:00–16:03, 16:52–17:04, 21:13–21:17), e o regime "B-02 ligada" (TAG43 = 2) é dominado pelo paralelo. É o mesmo mecanismo do failover do preset 6.
  - **Opção A real = etapa 0:** com `--regime-combinado` e a configuração alvo de PLANO §6.2, a saída é igual à das séries `RB0xS` em todas as colunas comparáveis (37.440 linhas), com o mesmo placar. Fallback nas bombas: 0. É mais um período a favor da RC-11 (que não foi feita aqui).
  - O que sobra no combinado: 4 alarmes da B-02 sozinha em carga alta (16:00–16:03) e 1 + 1 num pico de demanda com o nível baixo (18:53) [hipótese: demanda, BACKLOG 15].
  - **Custo de detecção:** o regime combinado perdeu a "detecção" do ruído (22), que nas outras configurações era provavelmente coincidência com a B-01 sozinha (P21), e ganhou o circuito aberto (26) no 1º minuto e, nas bombas, as falhas de pressão 20 e 24 (P41).
- **Solução testada:** TAG42 sozinha (piora de dia, melhora de noite). A estimativa com regime combinado B-01 × B-02 (RES §3.3, com *proxies*, sem set-point) reduziu os alarmes da B-01 a 0–1 min por tag. **Só o regime combinado (bomba × outra bomba × set-point) deve resolver** [hipótese]. Ele não foi testado no replay porque o código não suporta.
- **BACKLOG:** 12. Plano: `Docs/PLANO_REGIME_COMBINADO.md`.

### P04. Válvula de entrada fechada (TAG26)

- **O que acontece.** Com a válvula fechada, a vazão de entrada vai a 0 (operação normal, 18 % do tempo no treino). O perfil mistura aberta e fechada, e o 0 prolongado alarma (persistência e mudança de patamar). Com a válvula como regime, o fechamento deixa de alarmar, mas o regime "aberta" fica estreito, e vazões altas e normais passam a alarmar.
- **Evidência** (confirmado no replay):

  | Janela | Sem regime | Com TAG45 |
  |---|---|---|
  | Preset 1 | 14 (válvula fechada 13:17–13:35) | 17 (vazão alta 13:01–13:16 com a válvula aberta, mais 13:47) |
  | Madrugada | 215 | **5** (na abertura e no fechamento) |

  Fontes: RES §2.3 e §4. A TAG22 (nível) alarmou às 13:35 por causa do nível caindo com a válvula fechada; ela ficou sem regime nos testes.
- **[preset 6] Repetiu** (RES6 §2.4): sem regime, 33 alarmes com a válvula fechada (15:24–15:44, 16:38–16:55), mais a TAG22 às 15:44. Com TAG45, sobram 12 de vazão alta com a válvula aberta (15:10, 15:15–15:23, 15:57, 16:34) e 1 na TAG22.
- **Causa no código:** mesma família de P01 e P02 (perfil global misturado); `src/IAApi.Engine/Detection/PersistenceDetector.cs` e `LevelShiftDetector.cs` (`Detectar`).
- **Solução testada:** regime com TAG45. Ganho claro de madrugada, neutro no preset 1. O pico de vazão de 13:01–13:16 vem logo após o reset do nível no início do cenário, provavelmente um transitório do simulador [hipótese].
- **BACKLOG:** 9 e 12. Para a TAG22, não há item.

### P05. Alarmes noturnos da pressão (TAG24)

- **O que acontece.** A planta reduz a pressão de 4,50 para 3,80 bar entre 0 h e 5 h. O treino cortou a noite inteira (P01), então o perfil só conhece o dia (449,6 ± 2,4). A pressão noturna fica a ~28 desvios da média e alarma **todos os minutos de toda madrugada**. O preset 1 não mostrou isso porque foi diurno.
- **Evidência** (confirmado no replay, RES §4.3): **300 de 300 minutos** de madrugada com alarme sem regime. Com TAG46 (set-point em uso), sobram **30**.
- **Causa no código:** P01. O simulador define o set-point em `Docs/Simulador/SIM_ANOMALIAS_CP300.c` (`PSD = 4500`, `PSN = 3800`, 0 h–5 h, rampa de 20 mbar/s; RES §3.5).
- **Solução testada:** regime com TAG46. **Validada no replay.**
  - Os 30 minutos que sobram (00:01–00:30) existem porque o arquivo começa às 12:30 do dia anterior e não há uma noite anterior para formar o regime (P08). Em produção, com a noite anterior no histórico, eles provavelmente não aconteceriam [hipótese].
  - A troca de set-point das 05:01 não gerou alarme.
- **Alternativa ainda não testada:** analisar a diferença TAG24 − TAG46. As duas estão na mesma unidade e a diferença fica perto de zero em qualquer set-point.
- **BACKLOG:** 13.

### P06. Obstrução do recalque não detectada (preset 1)

- **O que acontece.** A falha do preset 1 (obstrução, 13:28–13:39, até 777 ‰ de severidade) mexeu pouco nas tags da B-01. Os valores ficaram dentro do que a planta faz normalmente. Os detectores até reagiram, mas com a mesma força com que reagem em minutos normais. Nenhuma configuração testada fez a falha se destacar.
- **Evidência** (confirmado no replay):

  | Tag | Nota máxima na falha / fora dela, sem regime (RES §2.1) |
  |---|---|
  | TAG27 | 57,7 / 72,5 |
  | TAG31 | 51,4 / 62,2 |
  | TAG33 | 40,0 / 45,6 |

  - Os valores da falha (TAG27 ≤ 258, TAG31 ≤ 487, TAG33 ≤ 151) se repetem em minutos normais.
  - Com TAG42, os alarmes no horário da falha começam 26 min antes dela: não são detecção (RES §4.2).
  - Na estimativa com o regime "certo" (B-01 sozinha), a TAG27 na falha fica a 1,3–2,0 desvios (RES §3.3).
  - O único sinal possível é às 13:37 (vibração da B-01 alta quando a B-02 parte), em um único minuto [hipótese].
- **Causa:** não é um defeito pontual. É limite do método univariado: cada tag é olhada sozinha, contra o próprio normal. Agrava com a diluição (P07).
- **[preset 6] Contraprova** (RES6 §2.1): no desbalanceamento, a vibração da B-01 (TAG29) saiu da faixa normal, e o univariado **detectou às 15:32**, ≈4 min após o início da rampa, com a falha a 94 ‰ (≈10 % da intensidade final). Não houve alarme na TAG29 antes nem depois da falha. Isso confirma que o P06 é específico de falhas cujo efeito fica **dentro** da faixa normal, e não um defeito geral do pipeline.
- **[preset 3 de 13/11] Mesmo padrão no drift da vazão** (falha 21, TAG25, rampa até 590 ‰; RES3 §4.5): a leitura chegou a 869, abaixo do p99 do treino (924), e o desvio do perfil (216,6) é inflado pela demanda do dia. Nota máx. 53,4, nenhum alarme nas 4 configurações. Na **relação vazão × potência** (ajuste linear no treino, resíduo σ = 23,7), o desvio vai a **+106 (≈ 4,5 σ)** na segunda metade da falha [análise de dados, não detector]. Reforça a solução candidata abaixo. Na mesma linha, as falhas do PT-02 aparecem na relação pressão × bomba, não na pressão (P41).
- **Solução candidata:** usar a **relação entre tags** (por exemplo, corrente e frequência contra pressão e vazão), ou seja, o multivariado. Hoje ele roda só em *shadow*, a fusão está restrita a PK7 e o grupo `ModelsML/Multivariate/PK2/CP300-B01-Piloto.json` tem ~2 h de treino (AUD §5a, §5d). **Não testado.**
- **BACKLOG:** não há item específico; sugerido criar (seção 4). Relacionados: 7 e 3 (parte B, gabarito por evento).

### P07. Diluição pela média ponderada

- **O que acontece.** A decisão é a média das notas dos detectores, ponderada pelos pesos. Para chegar a 60, detectores que somam ≥ 60 % do peso precisam reagir juntos. Sem Isolation Forest, que é o caso do PK2, Z + MAD + EWMA saturados dão **exatamente 60**. Uma reação forte de um ou dois detectores não basta.
- **Evidência:** na TAG27 às 13:30, EWMA = 100 e taxa de variação = 100, mas Z = 53, MAD = 69 e persistência = mudança de patamar = 0. Nota final: **57,7**. O máximo possível nesse minuto era 73,3 (RES §2.2). Baixar o limiar não resolveria: a 57, alarmaria antes em minutos normais.
- **Causa no código:** `Services/AnomalyDetection/EnsembleDetectionService.cs : EnsembleDetectionService.Detectar` (média ponderada; `EhAnomalia = score ≥ EnsembleThreshold`); pesos em `appsettings.json` → `AnomalyDetection:*Weight`. Com IF aplicável, a barra ficaria mais alta: o IF vale 50 no limiar (MAPA #9).
- **[preset 6]** Na TAG29, o EWMA ficou em 100 **antes** da falha (perfil misturado), sem informação. Com Persistência e Mudança de patamar em 0 no início da rampa, a nota ficou abaixo de 60 em 15:29–15:31 (45,9–53,2) e passou só às 15:32. No minuto seguinte deu **60,9**, raspando o limiar (RES6 §2.2). Atraso estimado: ~2 min.
- **[preset 3 de 13/11]** (RES3 §4.5), confirmado no replay:
  - **Drift (21, TAG25):** às 16:43, EWMA 99,7 e mudança de patamar 100, mas Z 31 e MAD 20: nota **53,4**. Nenhum alarme em 40 min.
  - **Ruído (22, TAG27) com regime combinado:** no regime "paralelo" (178,8 ± 7,0), Z e MAD chegam a 100 em minutos soltos, mas o **EWMA fica em 0** (ruído de média zero não move a média móvel) e a persistência em 0–10: nota máx. **62,8**, 2 alarmes isolados em 30 min. É a previsão da RES3 §2.5 ("EWMA não reage a ruído de média zero").
- **Solução:** não testada. Mexer em pesos ou limiar sem corrigir P01–P05 trocaria falha não detectada por mais alarmes falsos.
- **BACKLOG:** 7.

### P08. O baseline por regime volta ao perfil global

- **O que acontece.** O regime só é usado quando há pelo menos 30 pontos do mesmo estado nas últimas 24 h. Nos outros casos, a IA volta ao perfil global, que é o perfil ruim de P01:
  - estados curtos ("Parando", "Abrindo");
  - set-point novo;
  - início de histórico.
- **Evidência** (confirmado no replay): TAG24 em 00:01–00:30 (30 alarmes), TAG43 = 3 às 13:41 (sem alarme) e TAG45 = 1 às 01:28 (RES §4.2, §4.3).
- **Causa no código:** `Services/AnomalyDetection/OperationalRegimeHistoryResolver.cs : Resolve` (`MinimoAmostrasPorRegime` = 30, igualdade de estado) e o *fail-open* em `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs : PrepararAsync`.
- **Solução candidata** (não testada; RES §3.6, opções d/e): perfil de reserva calculado por regime, ou manter o regime anterior por alguns minutos durante transições.
- **[preset 3] Fallback depois de rodízio noturno** (RES3 §2.3; confirmado nos dados e no replay). Às 00:47 de 13/11, a B-02 parou com o set-point 380 (classe `RB02S = 10`). Nas 24 h anteriores, essa classe tinha **14 pontos** às 00:52 e só chegou a 30 às **01:13**. De 00:52 a 01:12, TAG28/30/32 caíram no perfil global e alarmaram 21 min cada: **63 minutos-tag**, 21 % dos falsos do regime combinado no período. É o mesmo mecanismo dos 9 fallbacks da RC-0 (PLANO §5.7.3), agora maior. A referência treinada por período e por regime (PROP §4) deve eliminar esse caso [hipótese].
- **BACKLOG:** 12, 8 e 9.

### P09. Regime quase constante (bomba parada) fica com desvio zero

- **O que acontece.** Com a bomba parada, os valores são quase todos 0. Dentro desse regime, o IQR vale 0 e remove todo valor ≠ 0. Desvio e deadband do regime ficam 0. Hoje isso só não gera alarme porque o detector recalcula uma margem mínima a partir do histórico não limpo (P95 = 1). Um valor 2 com a bomba parada já daria nota 100 no Z-Score.
- **Evidência:** confirmado no código e nos dados (TAG31/TAG32 paradas: 96 % zeros, 4 % uns; RES §3.6). O efeito é hipótese: não ocorreu nos replays.
- **Causa no código:** `HistoricoLimpezaService.RemoverOutliersPorIqr`; `src/IAApi.Engine/Detection/Core/BaselineProtectionProfile.cs : Build / ResolveEffectiveScale`.
- **Solução candidata:** não aplicar o IQR quando ele valer ~0, ou usar um piso de escala pela resolução do sensor, só no caminho do regime (RES §3.6, opção b). Não testada.
- **BACKLOG:** 9 (opção b).

### P10. Latência do Bridge

- **O que acontece.** A IAApi pede ao Bridge, a cada ciclo e para cada tag, as últimas 24 h de histórico (chamada síncrona, timeout de 60 s). Se o Bridge fica lento com a IAApi e o simulador rodando, cada tag demora, o ciclo inteiro atrasa e a leitura chega velha à decisão.
- **Evidência** (log da exportação, `dados/*.pedacos.csv`; mesmo endpoint e mesmo tamanho de pedaço, 2 h ≈ 120 pontos):

  | Execução | Pedaços de 2 h | Tempo por pedaço | Origem |
  |---|---|---|---|
  | **TAG73, com a IAApi e o simulador rodando** (11/11, ~16:30) | 13 (10/11 12:30 → 11/11 14:30) | **média de 34,7 s**, máximo de 53,2 s, mínimo de 3,5 s | Confirmado em log (`preset1_parte2.csv.pedacos.csv`, linhas 2–14) |
  | **TAG73, Bridge livre** (12/11, `-Completar`) | 8 | **média de 1,0 s**, máximo de 3,8 s | Confirmado em log (mesmo arquivo, linhas 164–171) |
  | Tags TAG22–TAG31, Bridge livre (12/11) | 105 | médias de 1,4–4,9 s por tag; máximo de 7,9 s | Confirmado em log (`preset1.csv.pedacos.csv`) |
  | Estados TAG41–TAG46, Bridge livre (12/11) | 75 | médias de 0,8–5,3 s por tag; máximo de 8,0 s | Confirmado em log (`estados_10a11nov.csv.pedacos.csv`) |
  | Pedaços de 6 h, com o simulador rodando (11/11) | — | **Timeouts em muitos pedaços** (limite de 60 s); TAG24/TAG27 com 357 de ~1.552 leituras | Relatado (BACKLOG 6) |

  Conclusão: com a IAApi e o simulador rodando, o mesmo pedaço levou **~35 s contra ~1–5 s com o Bridge livre** (cerca de 8× ou mais). O log **não tem coluna de data e hora da execução**: o momento e a condição de carga de cada execução vêm do relato da equipe e da ordem das linhas no arquivo. Outros pedaços lentos no mesmo arquivo (TAG44 até 72 s, TAG61 até 68 s, TAG33 até 64 s) são compatíveis com a mesma carga.

  Para comparar: a IAApi pede **24 h** (12× um pedaço de 2 h) **por tag, a cada ciclo**, com timeout de 60 s.

- **Causa no código:** `Services/BridgeHistoricoService.cs : BuscarHistorico` (GET `/historico?horas=24`, síncrono), chamada por `PredictionDetectionPreparationService.PrepararAsync` em toda leitura. Com o baseline por regime, há uma segunda consulta por tag (`BuscarHistoricoPorPeriodo` da tag de regime). O pós-decisão pode buscar o histórico de novo (MAPA #13).
- **Status:** a lentidão do Bridge sob carga está **confirmada em log**. O **efeito na detecção** continua **hipótese**: não medimos quanto a consulta de 24 h da IAApi demora sob carga, se ela estoura o timeout de 60 s, nem a idade das leituras na decisão. O replay não reproduz latência.
- **Solução candidata:** medir com PERF_DIAG (`IAAPI_PERF_DIAGNOSTICS`, já existente) e depois avaliar o reuso do histórico entre etapas (experimento ainda não commitado) ou um cache incremental.
- **[13/11] Evidência da IA real (P26):** entre 14:25 e 15:04 de 12/11, 9 avaliações de tags do PK2 falharam com "Tempo limite excedido ao consultar o Bridge" (timeout de 60 s da consulta de histórico, `BridgeHistoricoService.BuscarJson`), e às 15:13 a consulta em lote dos valores atuais também estourou. Essas leituras **não geraram decisão**; a falha aparece só como Warning no Event Viewer. O timeout sob carga deixou de ser só hipótese.
- **[etapa 0] Medido com PERF_DIAG** (16/11, 15:19–16:46; `Docs/MEDICAO_ETAPA0.md` §3, §6; confirmado em log):
  - **Cada predição faz 2 buscas de 24 h** (`HistoricalRequests = 2`): a da preparação e a da tendência pós-decisão. O `PredictionHistoryReuse` estava desligado.
  - Antes do LiveMonitor, cada busca levou ~4,6 s (p95 de ~5,9 s; máx. de 9,1 s), quase tudo em `Bridge.HttpBuffered`. As duas somam **91 % do tempo da predição** (4,9 s + 4,7 s de 10,5 s).
  - Cada resposta tem **1.436–1.439 pontos** (1 por minuto).
  - **0 timeouts no monitor.**
  - **Carga concorrente confirmada:** a partir de 16:27, o LiveMonitor pediu o histórico de 24 h das 12 tags (via `GET /api/tags/historico`, que busca 24 h para mostrar 2 h). Foram até **12 consultas simultâneas**: o proxy abandona a requisição em 12 s, mas o Bridge continua. Essas consultas levaram 36,6 s em média, com **4 timeouts de 60 s**. No mesmo período, as buscas do monitor subiram para ~22 s cada (máx. de 54,4 s), e o Bridge devolveu um HTTP 500 interno ("open operation … 00:00:03") para a TAG27. A ligação entre a carga e a lentidão é inferência forte, por tempo e mecanismo.
  - É a mesma lentidão sob carga da exportação de 11/11, agora com a fonte da carga identificada.
- **BACKLOG:** 6 e 4.

### P11. Ciclo sequencial

- **O que acontece.** O monitor processa um cliente e uma tag por vez e só depois espera 1 minuto. O ciclo real é de 60 s mais a soma do tempo de todas as tags, e a última tag espera todas as anteriores. O multivariado em *shadow* roda antes, no mesmo fluxo.
- **Evidência:** confirmado no código (MAPA #1, #4b, §3, pontos L1–L7). **[13/11] Medido indiretamente na IA real (P26):** com 12 tags do PK2 e 1 do PK7, o ciclo do PK2 se repetiu a cada 5 a 18 min. Cada tag levou de 2 s a 110 s, e cada timeout custou 60 s.
- **Causa no código:** `Services/MonitoramentoTagsService.cs : ExecuteAsync` (`Task.Delay(1 min)`); `Services/AnomalyDetection/Multivariate/MultivariateShadowRuntimeCoordinator.cs : ProcessarLeituraAsync`.
- **Prioridade:** subiu de Média para **Alta** em 13/11 por causa do P26.
- **[etapa 0] Confirmado em log** (16/11; `Docs/MEDICAO_ETAPA0.md` §2, §3):
  - **Ciclo do PK2:** 131 s em média (94–162 s) antes do LiveMonitor, ou seja, lote de ~5,6 s + 12 tags × ~10,5 s; 306 s e ≥ 697 s com o LiveMonitor aberto.
  - **Laço:** PK7 (~17 s) + PK2 + 60 s ≈ 208 s, o que bate com a mediana de 210 s entre inícios de ciclo.
  - Em cada tag, o que não é Bridge soma **~0,9 s**; os detectores, ~4 ms.
  - Para chegar a 1 min, sobram ≈ 3 s por tag para o histórico [inferência].
- **BACKLOG:** 6.

### P12. Sem detector de valor congelado ou de falha de comunicação

- **O que acontece.** Se um sensor trava num valor normal ou a comunicação para, nenhum detector reage. Todos procuram valores **diferentes** do normal, e um valor parado num ponto normal não é diferente.
- **Evidência:** confirmado no código (AUD §5c, §5e-2): falhas 20, 30, 32 e 33 do CP300 não detectáveis por construção. Ainda não testado em replay (preset 3/4).
- **[preset 3]** Continua **sem teste**: o preset 3 de 12/11 não rodou (P29). A previsão por falha está em RES3 §2.5 (hipótese).
- **[preset 3 de 13/11] Confirmado no replay** (RES3 §4.5): na falha 20, a TAG24 congelou em **450** por 30 min, exatamente o set-point e a média do perfil (449,5). Z, MAD, EWMA, persistência e mudança de patamar ficaram em **0** em todos os minutos, nas 4 configurações (nota máx. 3,4, da taxa de variação no primeiro minuto). O pior caso possível: o valor parado é o mais "normal" que existe. A TAG34 (falha 25) também congelou (529 por 40 min), mas não é monitorada.
  - O efeito da falha só aparece nas bombas, porque a malha de pressão segue o valor congelado (P41).
  - Um detector de "sem variação além do normal" pegaria a TAG24 [hipótese para o limiar]: de 09/11 a 13/11 07:59, em operação normal, nenhuma das tags TAG22–25 e TAG34 ficou 8 min seguidos com o mesmo valor (RES3 §1.2), e aqui a TAG24 ficou 30.
- **Causa no código:** o conjunto de detectores em `Services/AnomalyDetection/DependencyInjection/LegacyAnomalyDetectionServiceCollectionExtensions.cs` não tem esse tipo de detector.
- **[16/11] Correção da descrição: a lógica existe, mas fora do ensemble** (confirmado no código; BACKLOG 33.0):
  - `src/IAApi.Engine/Runtime/Regime/Evidence/FrozenSensorEvidenceCalculator.cs` (B3.22.4) detecta congelamento: ≥ 10 pontos e ≥ 10 min de valor idêntico, com taxa de passos iguais ≤ 0,60 nos 30 pontos anteriores. `IncreasingNoiseEvidenceCalculator.cs` (B3.22.5) faz o mesmo para o ruído. Só a decisão runtime as usa (`RuntimeFunctionalDecisionCandidateFactory`: `FrozenSensor` ⇒ `EmitAnomaly`), e ela está ligada apenas para o **canário PK7**. No PK2 e no replay (`RuntimeDecision.Enabled = false`), o contexto é calculado e descartado. Então "nenhum detector reage" vale para o ensemble, que é o que decide no PK2.
  - **Mesmo ligada, essa lógica não pegaria a falha 20:** o calculador só atua em sinal `Continuous`, e no replay do preset 3 de 13/11 a TAG24 ficou com `tipo_tag = Unknown` (confiança 0) nos 480 minutos, inclusive nos 30 da falha (`dados/p3b_{sem,combs}.csv`; confirmado no replay). O adaptador recebe a mesma entrada (sem gap crítico), então devolveria `signal-not-continuous`. Das 13 tags do replay, só TAG22 e TAG25 saem `Continuous`.
- **Solução candidata:** detector de "sem variação além do normal" e/ou uso do heartbeat (TAG40). **[16/11] Proposta detalhada na tarefa 33 (33.C):** reaproveitar o `FrozenSensorEvidenceCalculator` num detector que aceita `Unknown`, com guarda de "repouso conhecido", como evidência independente fora da média ("OU", P07). **Passo 1 (estrutura do "OU") implementado em 16/11, sem commit, com as chaves desligadas; a detecção não mudou** (BACKLOG 33). **Passo 2 (detector `Valor congelado`, chave `AnomalyDetection:ValorCongelado` desligada por padrão, opção `--detector-congelado` no replay) implementado em 16/11, sem commit** (BACKLOG 33.P2). Confirmado só em teste sintético; **ainda não medido nos dados do CP300** (passo 3).
- **BACKLOG:** 10 e 33.

### P13. Treino só das últimas 24 h, sem escolha de período

- **O que acontece.** O perfil é treinado com as 24 h anteriores ao momento em que alguém chama o treino. Não dá para escolher um período limpo (por exemplo, o preset 5 sem falhas), e nada verifica se houve falha nesse período. O treino só acontece quando alguém chama a API.
- **Evidência:** confirmado no código (AUD §5d, §5e-6). Mudar `Bridge:HorasHistorico` altera o treino **e** a detecção, que usam a mesma chave.
- **[preset 6] Contaminação confirmada nos dados** (RES6 §1.2, §1.4): as 24 h antes do preset 6 incluem os 12 min da obstrução do preset 1 (TAG73 = 1, 13:28–13:39) e o reset do cenário. O IQR não removeu nenhum desses pontos em nenhuma tag (P01). O efeito nos números do perfil foi desprezível (média ±1,4), porque a obstrução ficou dentro da faixa normal; com uma falha forte, não seria.
- **Causa no código:** `Services/PerfilTagService.cs : TreinarPerfil` → `IHistoricoService.BuscarHistorico`; `Controllers/PerfilTagController.cs` (`POST /api/tags/treinar-perfil`, só `ClienteId` e `TagName`).
- **BACKLOG:** 8 (e 3, parte C, no replay).

### P14. O código aceita rótulo (TAG60–TAG81) como tag de regime

- **O que acontece.** Se alguém cadastrar uma tag de gabarito (por exemplo, TAG73, "falha ativa") como tag de regime, a IA passa a escolher o baseline sabendo a resposta. O teste fica inválido.
- **Evidência:** confirmado no código (AUD §5a). O replay já recusa isso (`--regime`, RES §4.1); a produção não. **Inventário (13/11, INV §3):** o banco local não tem nenhuma linha em `TagsRegimesOperacionaisIa`, então não há vazamento hoje no PK2. A proteção no código continua faltando.
- **Causa no código:** `Services/AnomalyDetection/OperationalRegimeBaselineProvider.cs : Resolve`, que aceita qualquer `TagRegime`.
- **BACKLOG:** 11. A regra precisa ser por cliente: TAG60–81 só são rótulos no simulador.

### P15. `IaAtiva = true` por padrão

- **O que acontece.** Cadastrar uma tag sem informar `IaAtiva` cria a tag **ativa**. Um rótulo cadastrado por engano entraria no monitoramento e poluiria as contagens e o LiveMonitor. Não afeta a decisão de outras tags.
- **Evidência:** confirmado no código (AUD §5a): `Models/TagIaConfig.cs`; `Controllers/TagConfigController.cs : ConfigurarTag`.
- **Inventário (13/11, INV §1):** o PK2 tem só TAG22–TAG33 ativas (12 tags, configuradas em 30/10). Nenhum rótulo ou estado está ativo.
- **BACKLOG:** 2 (inventário, feito). Não há item de correção.

### P16. Pesos do `appsettings.json` diferentes dos padrões do código

- **O que acontece.** O código tem pesos padrão (Z 0,20…), mas o `appsettings.json` usa outros (Z 0,15, MAD 0,15, EWMA 0,15, RoC/Persistência/Mudança de patamar 0,10, IF 0,25). Em produção vale o arquivo. Qualquer ferramenta que use os padrões do código dá resultado diferente da produção (era o caso do `tools/IntelligenceLabReplay`). O replay CP300 lê o `appsettings.json`.
- **Evidência:** confirmado no código (AUD §3, §5e-4): `src/IAApi.Engine/Detection/AnomalyDetectionOptions.cs` contra `appsettings.json`. Não verificamos se a máquina de teste usa o mesmo `appsettings`.
- **BACKLOG:** 3 (resolvido no replay CP300) e 7.

### P17. `MinHistoryPoints` sem uso

- **O que acontece.** `AnomalyDetection:MinHistoryPoints = 30` parece exigir 30 pontos de histórico, mas não é usado em produção. Cada detector tem o próprio mínimo (EWMA 10, RoC 3, Persistência 10, Mudança de patamar 3 por janela).
- **Evidência:** confirmado no código (AUD §3; MAPA §2): só é usado em `EnsembleV2Simulator`.
- **BACKLOG:** não há item.

### P18. Registro com a hora do processamento

- **O que acontece.** O registro de cada decisão guarda a hora em que a IA processou (`DateTime.Now`), não a hora da leitura. Não dá para medir pela tela quanto tempo a IA demorou para reagir.
- **Evidência:** confirmado no código (MAPA #14, #15): `Services/AnomalyDetection/Orchestration/PredictionFinalizationService.cs : Finalizar` → `Services/AnomaliaHistoricoService.cs : Registrar`.
- **[preset 3] Idade da leitura na decisão, medida pela primeira vez** (RES3 §2.4; confirmado nos dados e no código):
  - Em cada um dos 5 ciclos do PK2 de 12/11 (14:21–15:09), os valores gravados pela IA real batem com o CSV num **único minuto comum a todas as tags do ciclo**: 14:20, 14:32, 14:38, 14:41 e 14:52.
  - Isso confirma o código de `Services/MonitoramentoTagsService.cs : ExecuteAsync`: **um lote de valores atuais por cliente no início do ciclo**, seguido das predições tag a tag sobre esses valores.
  - **Idade na decisão:** de 0,2 min (ciclo rápido, primeira tag) a **17,2 min** (TAG33 no ciclo de 14:54, com 6 timeouts antes; P26). A resolução é de 1 min.
  - A hora gravada esconde esse atraso. Um placar da IA real com `data_hora` = processamento conta como atraso de detecção o tempo em que o dado ficou parado na fila.
  - **Prioridade:** subiu para Alta.
- **[etapa 0] Idade medida pelo PERF_DIAG** (16/11; `Docs/MEDICAO_ETAPA0.md` §4; confirmado em log):
  - O `ReadingAgeAtStartMs` veio nulo, como previsto no código (`ReadingKind=Unspecified`, sem fuso presumido). A idade foi calculada como `PredictionStartedUtc − (ReadingOriginal + 3 h)`, com hora local = UTC−3. A leitura tem resolução de 1 min, então a idade inclui de 0 a 60 s de truncamento.
  - **PK2 na decisão:** média de 1,9 min; p50 de 1,7; **p95 de 3,2**; **máx. de 12,0 min**. Sem o LiveMonitor: p95 de 2,8 e máx. de 3,5. PK7: máx. de 1,4 min.
  - No fim do lote, a idade é ≤ 1 min (o Bridge entrega o minuto corrente). O atraso vem da **fila sequencial**: a 1ª tag começa, em média, com 0,5 min; a 12ª, com 2,5 min.
  - **Meta de p95 < 2 min não atingida.** A hora gravada no registro continua sendo a do processamento (o problema deste item não mudou).
- **BACKLOG:** 5.

### P19. Entrada inválida vira "sem anomalia" em silêncio

- **O que acontece.** Se a leitura atual tem hora anterior ao último ponto do histórico (problema de fuso ou relógio), ou se o histórico fica vazio, a IA responde "sem anomalia" sem avisar fora do log.
- **Evidência:** confirmado no código (MAPA #7): `Services/AnomalyDetection/DetectionInputValidator.cs : Validate`. **Não ocorreu no preset 1** (`entrada_invalida = 0` em 100 %; RES §2.2). Em produção, com o Bridge real, não foi verificado.
- **Na IA real (13/11, INV §4.2b):** ocorreu em **38 de 50** decisões do PK2 em 12/11, reproduzido offline. A causa não é fuso nem relógio: é o fluxo do monitor (P31). O replay não reproduz isso porque serve o histórico até a leitura.
- **Impacto e prioridade revistos:** falha não detectada, Alta.
- **BACKLOG:** 4 e 6.

### P20. Bloqueio por tipo "discreto"

- **O que acontece.** Se a série de uma tag tem poucos valores distintos (por exemplo, a reserva parada nas 24 h inteiras), a IA a classifica como "discreta" e desliga todos os detectores.
- **Evidência:**
  - Confirmado no código (AUD §5b; MAPA #8): `src/IAApi.Engine/Detection/Behavior/TagBehaviorProfileResolver.cs : Resolve` e `Services/AnomalyDetection/DetectorApplicabilityPolicy.cs : Evaluate`.
  - **No preset 1, as tags monitoradas foram classificadas como `Unknown` e nada foi bloqueado** (RES §2.2). Só a TAG44 (estado, não monitorada) foi bloqueada.
  - O caso da reserva que ficou parada 24 h segue como hipótese (AUD §5b).
  - **[preset 6] Ocorreu no caminho do regime** (RES6 §2.2; seção 3.1a, linhas 17a/18a): com TAG43 como regime, o histórico do estado "B-02 parada" é quase todo 0, e TAG28/32 foram classificadas como `Discrete` em 85 min, com todos os detectores bloqueados. Revendo os CSVs, isso **já tinha ocorrido no replay com regime do preset 1** (73 min), sem ter sido relatado na RES. Aqui isso evitou alarmes falsos. O risco é uma falha que apareça com a bomba parada (por exemplo, sensor travado) passar sem análise [hipótese].
  - **[preset 3 de 13/11] Um caso em que o risco não se concretizou** (RES3 §4.5): no circuito aberto da TAG29 (falha 26), a B-01 parou no rodízio das 21:05, no meio da falha. Com o regime combinado, a TAG29 no regime "B-01 parada" ficou com 30,9 ± 6,3 (a vibração da bomba parada não é zero: 21–45 nos dados), e o circuito aberto dá 0. O alarme continuou até o fim da falha. Isso depende de a tag parada ter algum sinal; numa tag que vai a 0 com a bomba parada (corrente, frequência), o risco continua [hipótese].
- **BACKLOG:** 9.

### P21. Coincidências que parecem acertos

- **O que acontece.** Alarmes causados por outra coisa (reserva parada, válvula fechada, B-01 sozinha) às vezes caem dentro do horário da falha. Uma comparação automática minuto a minuto com o gabarito os contaria como detecção.
- **Evidência:** confirmado no replay. No preset 1 sem regime, 31 alarmes dentro da falha, nenhum causado por ela (RES §2.1). Com TAG42, 27 alarmes na B-01 dentro da falha, que começaram 26 min antes (RES §4.2).
- **[preset 6]** (RES6 §2.1, §2.3): dentro de 15:28–17:17, sem regime, **122** alarmes sem relação com a falha (reserva parada, válvula fechada, carga alta). Mais **64 no failover** (110 com regime): B-01 parada e B-02 sozinha. Esses alarmes são efeito da troca de bomba, mas o mesmo sinal aparece em operação normal, então **não contam como detecção**. A detecção real foi só a TAG29 (86 min).
- **Solução:** a parte B do replay deve medir por **evento** e comparar com o comportamento antes e depois da falha.
- **Ferramenta (12/11/2026):** subcomando `placar` de `tools/Cp300OfflineReplay` (README §6). Um alarme só conta como detecção se for numa tag que a falha afeta (mapa editável `placar_falhas_cp300.json`, montado a partir do CENARIOS §4), se essa tag não alarmou nos N minutos antes da falha (padrão 20) e se ficar em alarme **D minutos seguidos** (padrão 3). Sequências mais curtas saem ao lado, como **sinal breve**. Os demais alarmes com falha ativa saem como **coincidências**; os de fora das falhas, por tag e por hora.
- **Validação do placar** (confirmado no replay; mesmos CSVs `dados/e0_*`; N = 20, D = 3):

  | Replay | Evento | Detecção (≥ 3 min) | Sinal breve (< 3 min) | Documento | Alarmes: total = atribuídos + coincidências + fora | Documento |
  |---|---|---|---|---|---|---|
  | Preset 1, sem regime | 02 obstrução | Não (TAG27/31/28/30/32 alarmavam antes) | — | Não detectada (RES §2.1) | 245 = 0 + 31 + 214 | 245; 31 na falha; 18 + 196 fora (RES §2.1, §2.3) |
  | Preset 1, regime recomendado | 02 obstrução | Não | — | Não detectada (RES §4.2) | 41 = 0 + 1 + 40 | 41 = 1 + 6 + 34 (RES §4.2) |
  | Preset 1, combinado + set-point | 02 obstrução | **Não** | **TAG31, TAG30, TAG32 às 13:37, 2 min, atraso 9 min, 777 ‰** | Hipótese, não conta como detecção (PLANO §5.4, item 6) | 31 = 5 + 2 + 24 | 31; B-01 fora da falha 4 (PLANO §5.3) |
  | Preset 6, sem regime / recomendado | 10 desbalanceamento | TAG29 às 15:32 (confirmada 15:34), atraso 4 min, 94 ‰ | — | 15:32 (RES6 §2.1); "≥ 3 min seguidos: 15:34" (RES6 §2.6) | 325 = 105 + 167 + 53 / 223 = 105 + 105 + 13 | 325 e 223; TAG29 86 + 19 no failover (RES6 §2.1) |
  | Preset 6, todos | 09 lubrificação | Não (TAG29 já alarmava; TAG36 fora do replay) | — | "Sem detecção separável" (RES6 §2.1) | — | — |
  | Preset 6, combinado + set-point | 10 desbalanceamento | TAG29 às **15:35** (confirmada 15:37), atraso 7 min, 161 ‰ | TAG29 às 15:32, 1 min, atraso 4 min, 94 ‰ | 15:32, com buraco em 15:33–15:34 (PLANO §5.3, §5.4 item 5) | 104 = 84 + 7 + 13 | 104; TAG29 84; falsos 20 = 7 + 13 (PLANO §5.3, §5.4) |

  - Todos os totais, horários, atrasos e severidades conferem com os documentos. Também conferem as madrugadas (`e0_madrugada_sem`: 1.282, TAG24 = 300 de 00:01 a 05:00; `e0_madrugada_combs`: 198, B-01 = 82, B-02 = 32). Com `--duracao-minima 1` (sem duração), o preset 1 combinado dá "detectada às 13:37" e o preset 6 combinado, 15:32.
  - **Efeito da duração mínima:** o preset 1 combinado passa a concordar com o PLANO (13:37 é sinal breve, não detecção). No preset 6 com regime combinado, o buraco de 2 min na TAG29 (15:33–15:34) atrasa a detecção confirmada em 3 min (15:34 → 15:37) em relação ao sem regime. Antes, os documentos não diferenciavam essas duas configurações nesse ponto.
  - Os documentos separam à mão o failover (B-01 parada); o placar conta os alarmes da TAG29 no failover como atribuídos (105 = 86 + 19) e os das outras tags como coincidências.
- **[preset 3 de 13/11] A regra errou nos dois sentidos** (RES3 §4.4–§4.5; confirmado no replay):
  - **Coincidência contada como detecção:** sem regime e com o recomendado, o ruído na TAG27 (falha 22) foi "detectado" às 17:20 (atraso 8 min). Os 7 alarmes caem só em 17:17–17:27, exatamente com a **B-02 parada** e a B-01 sozinha em carga maior, e param quando a B-02 volta. A B-01 sozinha alarma igual sem a falha (20:57 e 20:59 do mesmo replay). Com o regime combinado, que tira esse efeito, o ruído não foi detectado. Causa da detecção: hipótese forte de coincidência (P03).
  - **Detecção real excluída:** sem regime e com o recomendado, a TAG29 alarmou **continuamente** de 20:55 a 21:11 no circuito aberto (falha 26), mas ficou excluída porque alarmou 3 min antes (20:41–20:43, B-01 sozinha). No regime combinado, sem alarme antes, contou no 1º minuto.
  - **Tags fora do mapa:** as falhas do PT-02 (20 e 24) aparecem nas bombas, e o mapa só olhava a TAG24 (P41). **Corrigido no mapa em 16/11** (`efeitosPorCanal`, canal 3); a RES3 §4.4 mostra as duas leituras.
  - Ler sempre o placar junto com o estado das bombas em cada falha (RES3 §4.2, coluna "Bombas no período").
- **BACKLOG:** 3 (parte B).

### P22. TAG23 (sucção) com alarmes de madrugada

- **O que acontece:** 40 alarmes noturnos (00:01–05:00) e 2 fora da noite, iguais com e sem regime (RES §4.3).
- **Causa:** não investigada. Pode ser outro regime noturno (vazão e sucção dependem da demanda) [hipótese].
- **[preset 3]** (RES3 §2.3). **45 alarmes**, iguais sem regime, com o recomendado e com o combinado (a TAG23 não tem regime). Caem sempre com o **nível do reservatório num extremo** (confirmado nos dados):
  - 18:36–19:09 de 12/11, com TAG22 = 390–410 e TAG23 = 1175–1243, os menores valores do arquivo inteiro;
  - madrugada de 13/11 (00:50, 02:02–02:09, 03:19–03:24, 04:36–04:40), com o nível em 766–796, perto do máximo.
  
  [Hipótese] A sucção acompanha a coluna d'água do reservatório, e o perfil não cobre os extremos de nível. Um regime ou uma relação com a TAG22 resolveria; não testado.
- **[preset 3 de 13/11]** (RES3 §4.2, §4.4). A falha 23 (LT-01 intermitente) derrubou o **nível real** para 327–410, o menor do arquivo, e a TAG23 acompanhou (1300 → ~1160). A TAG23 alarmou 25 min dentro da falha 23, 19 dentro da 24 e mais 4 depois (18:53, 19:35–19:37), sempre com o nível baixo. Reforça a hipótese de que a sucção segue o nível. Esses alarmes são efeito real da falha 23 no processo, não falsos de operação normal.
- **BACKLOG:** não há item.

### P23. Código em execução fora de commit (resolvido em 16/11)

- **O que acontecia.** Na auditoria (AUD §1), parte do que roda não estava em nenhum commit: coleta em lote, PERF_DIAG, reuso de histórico e LiveMonitor (51 entradas). Os resultados valiam só para a árvore local.
- **Situação em 12/11** (conferido no git):
  - **Resolvido:** o runtime e as ferramentas foram commitados (`c5f575d`, `e3356b7`, `15cbeb9`, `930ec7c` e `15f1ca9`). Isso inclui `Services/Diagnostics/`, `Services/Monitoring/`, os arquivos `PredictionHistoryReuse*`, `tools/IAApi.LiveMonitor/`, `tools/Cp300Export/` e `tools/Cp300OfflineReplay/`.
  - **`.gitignore` corrigido:** `tools/` passa a ser versionado e `dados/` ignorado (dados industriais exportados não entram no repositório).
  - **Remoção dos 3 `Docs/B3.23_*`:** já entrou no commit `15f1ca9`. Os arquivos não existem mais na pasta nem no índice.
  - ~~**Pendente:** os 5 arquivos `.patch` na raiz~~ **Resolvido em 16/11.** Os 5 patches (`IAApi_LiveMonitor_clareza_cenario1`, `IAApi_LiveMonitor_sessoes_de_teste`, `IAApi_monitoramento_bridge_em_lote`, `IAApi_observabilidade_temporal` e `IAApi_tempo_por_tag`) estão **inteiramente incorporados** ao código desde `c5f575d`.
    - Evidência (confirmado no código): `git apply --check`, desfazer em ordem numa cópia e busca dos símbolos. Detalhe em BACKLOG 21.1.
    - Por decisão da responsável, foram **movidos, sem apagar**, para `C:\Users\Laura Marrari\Documents\IA MARRARI\arquivo_patches_2026-11\`, fora do repositório. O SHA-256 de cada arquivo é o mesmo antes e depois. A raiz não tem mais arquivos não rastreados.
- **BACKLOG:** item 21.1 (decisão sobre os patches).

### P24. Falha encadeada mascarada pela saturação [preset 6]

- **O que acontece.** Quando uma tag já está em alarme com nota próxima de 100, uma segunda falha que afete a mesma tag não muda nem a decisão nem a nota. A IA não indica que a situação piorou.
- **Evidência** (confirmado no replay, RES6 §2.1, §2.2):
  - A perda de lubrificação começou às ≈16:08 e chegou a 1000 ‰ às 16:48.
  - A TAG29 já estava em alarme desde 15:32 (desbalanceamento), com notas de 90–100, e subiu de ≈700 para 1106 sem efeito visível.
  - O efeito principal da lubrificação está em TAG36/37 (temperatura de mancal), que **não são monitoradas** (CENARIOS §2).
- **Causa:** as notas são limitadas a 100 (`DetectorMath.NormalizarPorLimite`) e a decisão é binária (`EnsembleDetectionService.Detectar`, nota ≥ 60). O limite em 100 está confirmado no código (`Math.Clamp(…, 0, 100)` em `src/IAApi.Engine/Detection/Core/DetectorMath.cs`). Que nenhuma outra parte indique agravamento é hipótese: o pós-decisão não foi verificado para isso.
- **Solução candidata** (não testada): monitorar TAG36/37 e/ou usar a tendência ou severidade do pós-decisão (`PredictionPostDecisionAnalysisService`) para indicar agravamento, sem alterar a decisão de anomalia.
- **BACKLOG:** não há item.

### P25. Absorção da falha pelo baseline móvel

- **O que acontece** [hipótese]. Parte da referência do "normal" vem das últimas 24 h móveis. Uma deterioração que dura horas entra nessa janela e, com o tempo, passa a ser o próprio normal; a IA deixa de alarmar sem que a falha tenha acabado.
- **Mecanismo** (confirmado no código; detalhes em `Docs/PROPOSTA_BASELINE_ESTAVEL.md` §1–§3):
  - **Sem regime:** MAD, Persistência e Taxa de variação usam a mediana das 24 h móveis; só Z-Score e EWMA usam o perfil treinado. Esse perfil também vem das 24 h anteriores a quem chamou o treino (P13).
  - **Com regime:** `PredictionDetectionPreparationService.PrepararAsync` troca média, desvio, deadband **e o histórico** pelos do regime calculado sobre as 24 h móveis. O perfil treinado é ignorado, e nenhum detector fica ancorado em uma referência estável.
  - O IQR não impede a absorção: remove minorias extremas, não valores anormais (P01).
- **Evidência:**
  - Indício no replay (RES6 §2.6): durante a rampa de 40 min do preset 6, o baseline de regime da TAG29 subiu de 149,6 ± 18,7 para 152,1 ± 21,7. O efeito é pequeno porque a falha ocupou só 40 min da janela.
  - **Não medido** para deteriorações longas. Estimativa: MAD e Persistência perdem a referência quando a falha domina a janela (≈12 h dentro de 24 h). Nenhum preset do CP300 tem rampa de horas.
- **Como medir:** teste de deriva sintética (rampa de ~30 h): em que hora cada configuração (sem regime, com regime) deixa de alarmar (`Docs/PROPOSTA_BASELINE_ESTAVEL.md` §5, item 3).
- **Solução candidata** (não testada): referência estável treinada por período e por regime, com o histórico móvel só como contexto (proposta, fases F0–F4).
- **BACKLOG:** não há item; relacionados: 8, 9 e 12.

### P26. A IA real avalia cada tag só a cada ~10 min

- **O que acontece.** No teste do preset 3 com a IA real (12/11), cada tag do PK2 foi avaliada só 3 a 5 vezes entre 14:21 e 15:09 (~48 min). Isso dá uma avaliação a cada ~10 min por tag, enquanto o simulador gera 1 leitura por minuto. Em ~90% dos minutos, a IA não olhou a tag; uma falha curta pode começar e terminar entre duas avaliações.
- **Evidência** (confirmado em log):
  - **Decisões:** `GET /api/anomalias` na IAApi local, filtrado por `dados/ia_real_preset3.csv` (13/11). Foram 50 decisões do PK2, 0 anomalias. Por tag: TAG22, 25, 26 e 27 com 4; TAG23, 24, 31, 32 e 33 com 5; TAG28, 29 e 30 com 3.
  - **Falhas:** Event Viewer → Aplicativo → origem `.NET Runtime`, categoria `MonitoramentoTagsService`. Foram 9 Warnings "Falha ao monitorar tag … Tempo limite excedido ao consultar o Bridge" (HttpClient.Timeout de 60 s), em TAG28–30 (14:25–14:27), TAG22 (14:33) e TAG25–30 (14:58–15:03).
  - **Linha do tempo do ciclo** (horário de processamento, P18):

    | Ciclo do PK2 (início) | Duração | Tags avaliadas | Timeouts |
    |---|---|---|---|
    | 14:21 | ~8 min | 9 | 3 (TAG28–30) |
    | ~14:32 | ~3 min | 11 | 1 (TAG22) |
    | 14:38 | ~1 min | 12 | 0 |
    | 14:43 | ~7 min | 12 | 0 |
    | 14:54 | ~15 min | 6 | 6 (TAG25–30) |
    | ~15:12 | — | 0 | consulta em lote estourou às 15:13 |

    Entre os ciclos, o laço espera 1 min e processa o PK7 (1 tag). Do início de um ciclo do PK2 ao do seguinte: 11, 6, 5, 11 e ~18 min.
- **Ligação com P10 e P11:** a duração depende diretamente do Bridge (P10). Com o Bridge livre, uma tag levou 2–10 s (ciclo de 14:38). Sob carga, levou 60–110 s, ou estourou 60 s e ficou sem decisão. Como o ciclo é sequencial (P11), o tempo de cada tag se soma, e a última tag espera todas as anteriores.
- **Consequências:**
  - O placar da IA real mede uma amostra de ~1 em cada 10 minutos, não a detecção minuto a minuto do replay.
  - As leituras que deram timeout não aparecem na tabela de decisões (só no Event Viewer), e o LiveMonitor não as mostra.
  - A IA real avalia **o valor atual no momento da consulta**, não as leituras intermediárias: os minutos não avaliados não são recuperados depois.
- **O que não foi medido:** a idade da leitura na hora da decisão (P18; precisa do PERF_DIAG) e o tempo de cada etapa dentro da tag.
- **[preset 3] Contexto corrigido e idade da leitura** (RES3 §1.2, §2.4):
  - O preset 3 **não rodou** (P29): as 50 decisões são de operação normal. As conclusões sobre cadência, timeouts e parada continuam válidas, porque não dependem do cenário.
  - A idade da leitura, antes não medida, foi recuperada casando valores com o CSV: **0,2 a 17,2 min** (P18). Somada à cadência de ~10 min por tag, uma falha pode levar de ~10 a ~27 min para chegar a uma decisão, no pior caso [inferência].
  - As notas da IA real ficaram **≤ 2,1 em todas as 50 decisões** (38 exatamente 0). O replay deu 20–35 de média nos mesmos minutos de leitura, inclusive em tags sem regime (TAG22, TAG23). ~~A IA real não está com o mesmo perfil ou baseline do replay. Causa: hipótese.~~ **Resolvido pelo inventário (13/11, INV §4):**
    - o valor gravado é a nota ÷ 20 (P30): 2,1 = 42;
    - os 38 zeros são entrada inválida (P31);
    - o perfil de produção é outro (P32).
    - Não há baseline comportamental nem contexto do PK2 no banco.
    - As 50 decisões foram reproduzidas offline.
- **Solução candidata:** primeiro medir (PERF_DIAG, BACKLOG 6). Depois avaliar reuso ou cache incremental do histórico (P10) e processamento não sequencial (P11). Nada aprovado.
- **[etapa 0] Medido com PERF_DIAG** (16/11, 15:19–16:46, preset 1; `Docs/MEDICAO_ETAPA0.md` §2, §8; confirmado em log):
  - **Cadência por tag (PK2):** uma avaliação a cada **3,47 min** em média (p95 de 3,91; máx. de 4,14) antes do LiveMonitor (16:27); na sessão inteira, 3,8 / 4,1 / **16,2 min**.
  - **14,6 avaliações por tag por hora; 248 de ~1.022 minutos-tag (24 %)**, todos com entrada válida (P31). Em 12/11, era ~10 min e ≈ 2 %.
  - **0 timeouts no monitor.** 1 tag sem decisão: TAG27 às 16:40, HTTP 500 do próprio Bridge, que não abriu a conexão com o servidor de dados em 3 s.
  - **Metas da 6.0** (60/h; ≥ 90 %): não atingidas. O limite é o ciclo sequencial (P11) com duas buscas de 24 h por tag (P10).
  - **Fator externo novo:** com o LiveMonitor aberto (a partir de 16:27), cada tag levou ~46 s em vez de ~10,5 s, e o último ciclo teve 9 tags em 11,6 min.
- **BACKLOG:** 6 (prioridade elevada em 13/11) e 4.

### P27. O monitor parou de gravar decisões sem nenhum aviso

- **O que acontece.** A última decisão gravada na IAApi em 12/11 é PK2.TAG33 às 15:09:13. Depois disso, não há nenhuma decisão do PK2 nem do PK7 até a IAApi ser iniciada de novo em 13/11 (~11:06). O processo continuava aberto, segundo a equipe.
- **Evidência:**
  - **Decisões** (confirmado nos dados): por hora de processamento, o PK2 tem 47 decisões às 14 h e 3 às 15 h de 12/11, e as próximas são de 13/11 às 11 h.
  - **Event Viewer** (confirmado em log; origem `.NET Runtime`, categoria `MonitoramentoTagsService`): os últimos registros são timeouts de 60 s em PK7.TAG1 (15:12:09), no lote do PK2 (15:13:12) e no lote do PK7 (15:15:14). Depois disso, **nenhum Warning nem Error** da IAApi até 13/11 11:06. Em 13/11 11:06, o Bridge recusou a conexão (`localhost:5001`), porque ainda não tinha subido.
  - **Log do sistema** (confirmado): não houve suspensão nem hibernação entre 15:15 e 02:05. **Às 02:01 de 13/11, o Windows Update reiniciou a máquina** (eventos 1074 e 6006; reinícios às 02:05, 02:07 e 02:08). O processo da IAApi terminou aí, se ainda estava vivo. O fim da janela do teste (02:05) coincide com esse reinício.
  - SQL Server: nada anormal. A IAApi usa SQLite (`ia_config.db`), não SQL Server.
- **O que o código mostra** (`Services/MonitoramentoTagsService.cs : ExecuteAsync`, confirmado no código):
  - O corpo inteiro do laço está em `try/catch (Exception)`. Uma exceção geral é registrada como **Error** ("Erro geral no monitoramento automático de tags."), e o laço espera 1 min e continua.
  - Falha no lote do Bridge → Warning e `continue` para o próximo cliente. Falha numa tag → Warning e próxima tag.
  - O laço só sai por cancelamento do host (`stoppingToken`).
  - Os timeouts do Bridge (`HttpClient.Timeout` de 60 s em `BridgeValorAtualService` e `BridgeHistoricoService`) viram `TaskCanceledException` sem relação com o `stoppingToken`. Por isso caem nos `catch` comuns e são logados, como se viu às 15:13 e 15:15.
  - Se uma exceção escapasse do `ExecuteAsync`, o .NET 10 pararia o host (`BackgroundServiceExceptionBehavior.StopHost` é o padrão; o projeto não altera). O processo fecharia, o que contradiz "processo aberto".
  - Toda leitura que chega a `PredictionService.Detectar` grava uma decisão ou lança exceção, e a exceção é logada como Warning. "Valor não encontrado" também lança exceção (`CurrentValuePredictionService.DetectarComValorAtual`).
  - **Conclusão:** pelo código, o serviço **não morre em silêncio** por exceção nem por timeout do Bridge. Sem decisão e sem Warning por ~11 h, o laço não concluiu nem falhou nenhuma tag. O mais provável é que ele tenha ficado **bloqueado** num ponto sem timeout, ou que o processo não estivesse rodando.
- **Hipóteses** (nenhuma confirmada):
  1. **Saída do console bloqueada** (a mais compatível com os sinais). `BridgeHistoricoService` usa `Console.WriteLine` síncrono na thread do monitor. O logger de console tem fila limitada e, por padrão, espera quando ela enche, e é o primeiro provedor de log. Se a janela do console parar de consumir a saída (por exemplo, um clique que seleciona texto no modo QuickEdit, com "Selecionar" no título), a thread do monitor trava sem exceção. O Warning também ficaria preso antes de chegar ao Event Viewer, e a API HTTP poderia continuar respondendo.
  2. **Todas as tags com `IaAtiva = false`:** o laço rodaria sem trabalho e sem log. Pouco provável, porque as decisões voltaram após o reinício sem reativação conhecida.
  3. **Bloqueio em trava interna:** improvável. As travas de `IsolationForestDetector` e `MultivariateGroupBufferService` são liberadas em `finally`, e o SQLite tem timeout de comando de 30 s, que lançaria exceção.
  4. **O processo não estava rodando:** contradiz o relato. Seria confirmado se nenhum processo `IAApi`/`dotnet` existisse no período.
- **Onde ficam os logs da IAApi** (confirmado no código e na máquina; não há Serilog nem log em arquivo):
  - **Console:** todos os níveis, sem data e hora. Perde-se ao fechar a janela.
  - **Event Viewer → Aplicativo, origem `.NET Runtime`:** só Warning e Error (padrão do ASP.NET Core no Windows). Persiste: os eventos de 12/11 ainda estão lá.
  - **Debug e EventSource:** só com depurador ou coletor anexado.
- **Como confirmar no próximo teste** (nenhum passo muda código; os itens a e c mudam o modo de execução e precisam de aprovação):
  - **a.** Iniciar a IAApi com a saída gravada em arquivo e com data e hora no log. A saída deixa de depender da janela do console, o que testa a hipótese 1 e guarda os logs Information (`Bridge lote…`, `Predição iniciada/finalizada…`). Exemplo: definir `Logging__Console__FormatterOptions__TimestampFormat="yyyy-MM-dd HH:mm:ss "` e redirecionar com `*> dados\logs\iaapi_<data>.log`.
  - **b.** Durante o teste, vigiar a última `DataDeteccao` do PK2 a cada 5 min com um GET somente leitura em `/api/anomalias`. Se ficar mais de 20 min sem decisão, registrar a hora.
  - **c.** Se parar: **antes de reiniciar**, (1) ver a última linha do log (um "Predição iniciada" sem "finalizada" indica a tag e a etapa); (2) ver se a janela do console está em modo "Selecionar"; (3) registrar o PID, o uso de CPU e o número de threads do processo; (4) se aprovado, tirar uma pilha com `dotnet-stack report -p <PID>` (exige instalar a ferramenta) para ver onde a thread do monitor está parada.
  - **d.** Conferir o Event Viewer (Aplicativo, `.NET Runtime`, `MonitoramentoTagsService`) da janela do teste.
  - **e.** Adiar as reinicializações do Windows Update durante testes longos: o de 12/11 foi interrompido pelo reinício das 02:01.
- **[etapa 0] Não se repetiu** (16/11, 15:19–16:46; `Docs/MEDICAO_ETAPA0.md` §6.4; confirmado em log):
  - IAApi subida pelo `Start-IAApiComLog.ps1`, com a saída em arquivo e sem janela de console (passo a);
  - linhas PERF_DIAG contínuas, sem lacuna maior que 90 s; 20 ciclos completos do PK2 e o 21º interrompido pelo encerramento do processo (última decisão às 16:45:00; log cortado às 16:46:34);
  - nenhum "Erro geral";
  - **limitação do passo a:** o log saiu **sem data e hora no início das linhas** (o `TimestampFormat` não teve efeito; causa não investigada) e com **702 MB**, por causa do SQL do EF em Information e do LiveMonitor (§6.2 da medição). O tempo foi recuperado pelos campos do PERF_DIAG;
  - 87 min sem parada **não descartam** as hipóteses (em 12/11, parou após ~50 min). Apenas não há sinal de parada sem a janela de console.
- **BACKLOG** (atualizado em 13/11; nenhuma tarefa aprovada nem implementada):
  - **6:** execução já aprovada com o log em arquivo, com data e hora (passos a, b e d acima).
  - **17:** trocar `Console.WriteLine` por `ILogger` no caminho do monitor. Ataca a hipótese 1, mas não basta sozinha.
  - **18:** leituras com timeout ou falha passam a gerar um registro visível de "sem dados".
  - **19:** "sinal de vida" do monitor: último ciclo, duração, tags avaliadas e etapa atual, com endpoint e indicador no LiveMonitor. Mostra **onde** o laço parou, sem `dotnet-stack`.
  - **20:** alerta quando o ciclo passa de X minutos, com verificação independente da thread do laço.

### P28. Endereço do Bridge fixo no código, sem configuração nem ponto de substituição para teste

- **O que acontece** (confirmado no código, 13/11, no levantamento da tarefa 19):
  - `Services/BridgeValorAtualService.cs` chama `http://localhost:5001/valores-atuais` com o endereço fixo no código. O `HttpClient` é criado no próprio campo (`new() { Timeout = 60 s }`). Não há interface, método virtual nem construtor que receba o cliente ou a configuração.
  - `Services/BridgeTagsService.cs` faz o mesmo em `http://localhost:5001/tags?cliente=…`, com `new HttpClient()` sem timeout explícito (o padrão do .NET é 100 s).
  - Só o `Services/BridgeHistoricoService.cs` lê a chave `Bridge:BaseUrl` (método `ObterBaseUrl`, com `http://localhost:5001` como valor padrão). Mesmo assim, ele também cria o próprio `HttpClient` no construtor.
  - O valor de `Bridge:BaseUrl` no `appsettings.json` hoje é igual ao endereço fixo. Por isso, as três consultas vão para o mesmo lugar **na configuração atual**.
  - O projeto já registra `HttpClient` pela DI em outro lugar: `AddHttpClient<ILlmProvider, LmStudioLlmProvider>` em `DependencyInjection/Llm/LlmServiceCollectionExtensions.cs`, com `BaseAddress` e `Timeout` vindos da configuração.
- **Consequências:**
  - **Configuração enganosa** (confirmado no código; não ocorreu): mudar `Bridge:BaseUrl` muda só o histórico. O valor atual (usado em toda leitura do monitor) e a lista de tags continuam em `localhost:5001`, sem nenhum aviso.
  - **Testes sem ponto de substituição suportado** (confirmado no código e nos testes): um teste que passe pelo serviço real do valor atual chama o Bridge real, se ele estiver ligado na máquina. Hoje, os testes contornam isso de três formas:
    - `IAApi.Tests/IntelligenceLab/IntelligenceLabBehaviorBaselineEndToEndTests.cs` usa `new BridgeValorAtualService()` e confia que o fluxo "retorna antes de chamar". O próprio comentário avisa que, se o fluxo mudar, o teste tenta acessar localhost;
    - `tools/Cp300OfflineReplay/Cp300Replay.cs` passa `null!`;
    - `IAApi.Tests/PerformanceDiagnosticsTests.cs` e `PredictionHistoryReuseTests.cs` trocam por **reflexão** o campo privado `_httpClient` do `BridgeHistoricoService` por um cliente com handler falso (`http://bridge.test`). O contorno funciona, mas quebra sem aviso se o campo mudar de nome ou de forma.
  - **Tarefas afetadas:** sem um ponto de substituição suportado, um teste automático do laço `MonitoramentoTagsService` com tags (BACKLOG 19, passo 2) e o aceite da tarefa 18 ("Bridge simulado fora") ficam dependentes do mesmo contorno por reflexão.
  - **Hipótese, não observada:** como `BridgeValorAtualService` e `BridgeTagsService` são `Scoped`, cada escopo cria um `HttpClient` novo e não o descarta. É o uso que o .NET desaconselha, porque pode esgotar conexões em uso prolongado. Não há evidência de que isso tenha contribuído para P26 ou P27.
- **Status:** ~~confirmado no código~~ **corrigido no código em 16/11** (BACKLOG 22, passos 1 e 2, sem commit; testes offline; a API não foi iniciada). Não é causa demonstrada de nenhuma falha de detecção.
- **Correção (16/11, confirmado no código e em teste):**
  - `BridgeValorAtualService` e `BridgeTagsService` recebem o `HttpClient` pelo construtor. Em `AddBridgeServices`, viraram clientes tipados (`AddHttpClient<…>`, `Transient`), com `BaseAddress` vindo de `Bridge:BaseUrl` (normalizado com `/` no fim) e caminhos relativos `valores-atuais` e `tags?cliente=…`.
  - Timeouts iguais aos de antes: 60 s no valor atual e 100 s nos tags (antes implícito, agora explícito). `RemoveAllLoggers()` mantém o log igual (sem as 4 linhas do `IHttpClientFactory` por requisição).
  - `http://localhost:5001` ficou num só lugar do código de produção: `BridgeServiceCollectionExtensions.BaseUrlPadrao`, usado também pelo `ObterBaseUrl` do histórico (mesmo valor; o histórico não mudou de forma).
  - **Antes de mudar** (sem expor o valor): `Bridge:BaseUrl` só está definida no `appsettings.json` e é **igual** ao endereço fixo antigo; não está no `appsettings.Development.json`, no `launchSettings.json` nem na variável `Bridge__BaseUrl` desta máquina. Com a configuração atual, as URLs não mudam.
  - A hipótese do `HttpClient` não descartado por escopo deixa de se aplicar a esses dois serviços. O histórico (`Singleton`) continua com o próprio cliente e com a troca por reflexão nos testes (passo 3 da tarefa 22, não feito).
- **[etapa 0] Aceite (e) da tarefa 22 conferido em 16/11** (`Docs/MEDICAO_ETAPA0.md` §7):
  - 21 lotes do PK2 com 12 tags e 21 do PK7 com 1 tag, 0 falhas;
  - mediana de 5,6 s e 4,4 s, na ordem da referência de 05/11;
  - nenhuma linha de log do `IHttpClientFactory`.
- **Impacto:** risco (testabilidade e configuração enganosa). **Prioridade:** ~~Média~~ resolvido no código (falta a conferência (e)).
- **BACKLOG:** 22.

### P29. O preset 3 não iniciou, e nada na rotina de teste avisou [preset 3]

- **O que acontece.** Em 12/11, a equipe tratou o período a partir das ~14:20 como o teste do preset 3. Pelos rótulos do simulador, o cenário anterior foi parado às 14:20, mas o preset 3 **nunca começou**. Ninguém conferiu os rótulos de início, e o problema só apareceu na análise de 13/11. Nesse meio-tempo, a exportação da IA real (P26, P27) e o pedido de replay foram feitos como se fosse o preset 3.
- **Evidência** (confirmado nos dados, `dados/cp300_09a13nov.csv`, 09/11 12:30 → 13/11 07:59; RES3 §1.2):
  - TAG74 (preset) só vale 1 ou 6 no arquivo inteiro. **Nunca 3.**
  - TAG76 (estado) vai de 3 para **0 (parado)** às 14:20 de 12/11 e fica em 0 até o fim. O relógio do cenário (TAG77/78) fica parado em 2 h 1800 s.
  - **Comparação:** no início do preset 6 (11/11), TAG76 também foi 3 → 0 (15:07), mas no minuto seguinte veio TAG74 = 6, TAG76 = 1, o relógio voltou a contar e o nível foi resetado (TAG22 578 → 601). Em 12/11, só o primeiro passo aconteceu.
  - Slots TAG61/65/69: só os 3 eventos de 11/11 (códigos 2, 10 e 9). TAG73 = 0 depois de 11/11 17:17.
  - **Nenhuma assinatura** das falhas 20–27 nas medições: TAG22 nunca a 0/4095; nenhuma tag congelada por 8 min ou mais; TAG23 sem saturação; TAG29 nunca em 0 com a B-01 operando.
  - O simulador seguia publicando: TAG40 (heartbeat) +60 por minuto, operação normal com rodízios e set-point noturno.
- **Causa: hipótese.** O comando de início (`PSC = 3` e `CSC = 1`, CENARIOS §3) não foi escrito ou não foi aceito, ou foi enviado `CSC = 4` (parar). O CSV não guarda os comandos. Confirmar no log `BDE` do CLP ou com quem operou.
- **Consequências:**
  - Nenhum resultado de detecção do preset 3 existe. P12 e as previsões da AUD §5c continuam sem teste.
  - As conclusões de P26 e P27 (cadência, timeouts, parada do monitor) **continuam válidas**, porque não dependem do cenário.
  - O período virou um teste útil de alarme falso em operação normal, com treino limpo (RES3 §2.2–§2.3).
- **Proteção que já existe:** o `placar` informa "0 eventos no período do replay" quando o gabarito não tem falha. Isso só aparece depois da exportação.
- **Solução candidata (barata, sem código):** no roteiro de teste, conferir até 2 min depois do comando que TAG74 = preset escolhido, TAG76 = 1, TAG77/78 contando e TAG22 ≈ 600 (reset). Repetir a conferência a cada ~1 h (TAG75 avançando). Opcional: um aviso no LiveMonitor ou no script de exportação quando TAG76 ≠ 1 no período (só leitura dos rótulos; precisa de aprovação).
- **Impacto:** teste perdido sem aviso; risco de avaliação errada ("a IA não detectou nada no preset 3").
- **[13/11] Repetição** (RES3 §4.2; confirmado nos dados): às 15:02, TAG74 = 3, TAG76 = 1, TAG77/78 = 0 h 21 s e reset do nível (TAG22 672 → 602). Os 17 passos andaram como na tabela do CENARIOS §4, e o cenário terminou às 22:12 (TAG76 = 3). A conferência pelos rótulos funcionou, mas foi feita na análise, não no momento do teste; o roteiro com conferência continua pendente.
- **BACKLOG:** não há item; sugerido criar (roteiro de teste com conferência do início do cenário).

### P30. A nota gravada pela IA real está em outra escala (÷ 20)

- **O que acontece.** O ensemble calcula a nota de 0 a 100 e decide por ≥ 60. Mas o campo `Score` gravado em `AnomaliaDetectada`, devolvido por `GET /api/anomalias`, é essa nota **dividida por 20**. A exportação da IA real grava esse campo como `nota_final`, a mesma coluna em que o replay grava a nota de 0 a 100.
- **Evidência:**
  - Confirmado no código: `Services/AnomalyDetection/EnsembleDetectionService.cs:406-410` (`Score = scoreFinalNormalizado / 20.0`, `ScoreNormalizado = scoreFinalNormalizado`); `Services/PredictionService.cs:250-251` → `AnomaliaHistoricoService.Registrar`; `tools/Cp300Export/Export-AnomaliasIaReal.ps1:227`. O replay usa `ScoreNormalizado` (`tools/Cp300OfflineReplay/Cp300Replay.cs:680`).
  - Confirmado nos dados: as 131 anomalias do PK2 no banco têm `Score` de 3,0 a 3,73 (nota 60–75).
- **Consequência:** a tabela da RES3 §2.4b compara 0–5 com 0–100. Nas 12 decisões válidas, a nota real × 20 vai de 14,6 a 42,0, a mesma faixa do replay. O **placar não é afetado** (usa só `decisao_final`).
- **Solução candidata (barata, só ferramenta e documentação):** a exportação passa a gravar também a nota 0–100 e a dizer a escala no cabeçalho; corrigir a RES3 §2.4b. Mudar o contrato da API não é proposto (contrato HTTP preservado).
- **Feito em 13/11 (só ferramenta, dados e documentação; runtime, valor gravado e contrato HTTP inalterados):**
  - `tools/Cp300Export/Export-AnomaliasIaReal.ps1` grava a coluna nova `nota_0_100` = `score × 20`, no fim da linha. `nota_final` continua como veio da API (0–5), para não mudar o arquivo de quem já o lê. A escala de cada coluna está no cabeçalho do script e numa tabela do `tools/Cp300OfflineReplay/README.md` §1.1. Sintaxe validada no PowerShell 5.1, mais um teste offline com JSON sintético (3,05 → 61).
  - `dados/ia_real_preset3.csv` ganhou `nota_0_100`. **Não foi regenerado pela API:** a resposta JSON da exportação de 13/11 não foi guardada. A coluna foi calculada a partir do próprio `nota_final` (mesma regra × 20), e as 13 colunas originais ficaram idênticas. Conferência: 50 de 50 iguais a `nota_real_x20` do INV §4.3 (diferença máx. 0,004, arredondamento). O placar não muda (as colunas que ele lê não mudaram).
  - RES3 §2.4b refeita na escala 0–100, com as 12 decisões válidas lado a lado com o replay (29,1 × 36,6 de média). O item 8 do resumo da RES3 também foi corrigido.
- **Pendente: LiveMonitor** (confirmado no código, não visto na tela). A tabela de decisões e o cartão da tag mostram o `score` persistido **cru, em 0–5** ("Score persistido" / "Score · persistido"; `tools/IAApi.LiveMonitor/src/domain.ts:16`, `TagCard.tsx:59,80`). Quando a fonte é o diagnóstico, o mesmo cartão mostra o `normalizedScore` **em 0–100** ao lado do "Threshold · ensemble" (`domain.ts:45`). O mesmo campo muda de escala conforme a fonte, e o 0–5 aparece perto de um limiar 60. Corrigir exige mudar o LiveMonitor (aprovação própria). **Plano: BACKLOG 28** (converter no domínio, `domain.ts`; não implementado).
- **BACKLOG:** 26 e 28.

### P31. A leitura do lote envelhece e a validação descarta a maioria das avaliações

- **O que acontece.** O monitor busca os valores atuais de todas as tags do cliente **uma vez, no início do ciclo**. Depois avalia tag a tag, e cada avaliação busca as 24 h de histórico naquele momento. Se, nesse meio-tempo, o Bridge já tem o ponto de um minuto posterior à leitura, a leitura fica "mais antiga que o histórico". Então:
  - o validador recusa a entrada;
  - o ensemble devolve **nota 0, sem anomalia**;
  - a decisão é gravada como se a tag tivesse sido avaliada e estivesse normal (P19).
- **Evidência:**
  - **Confirmado no código:** `Services/MonitoramentoTagsService.cs:54` (lote); `Services/CurrentValuePredictionService.cs:83-84` (`DataHora = valorAtual.DataHora`); `PredictionDetectionPreparationService.PrepararAsync` (histórico na hora da busca); `DetectionInputValidator.Validate:78-84`; `EnsembleDetectionService.CriarResultadoEntradaInvalida`.
  - **Reproduzido** (INV §4.3): com as classes de produção, o perfil do banco e o histórico cortado na hora da busca, as **50 de 50** decisões reais de 12/11 foram reproduzidas, com diferença < 0,1 na nota 0–100. Os 38 zeros são entrada inválida. Os 12 casos válidos são o ciclo rápido de 14:38 (idade da leitura ≤ 1 min) e a primeira tag do ciclo de 14:21.
  - **Contraprova:** com a leitura avaliada no próprio minuto, nenhuma das 50 teria nota abaixo de 13,8.
  - **Hipótese:** a fração de notas exatamente 0 no PK2 subiu de 4–29 % (30/10–04/11) para 67 % (05/11) e 76–91 % (11–13/11), sem anomalias desde 06/11. É compatível com o mesmo mecanismo nesses dias, mas não foi reproduzido.
- **Consequências:**
  - Somado ao P26, a IA real avaliou ≈ 12 de ~576 minutos-tag em 12/11 14:21–15:09 (≈ 2 %).
  - Nenhum teste da IA real mede detecção enquanto isso acontecer.
  - O LiveMonitor e o placar mostram essas decisões como "normal".
  - Quanto mais lento o ciclo (P10, P11, P26), pior.
- **Ligação:** P18 (idade da leitura), P19 (mecanismo), P26 (cadência). O replay não reproduz este problema: ele serve o histórico até a leitura (§3.2, "Valor atual em lote").
- **Soluções candidatas (nada aprovado; todas exigem proposta, testes e rollback):**
  - buscar o valor atual por tag, imediatamente antes da predição;
  - ou recortar o histórico até a hora da leitura;
  - ou, no mínimo, gravar "não avaliada" em vez de "sem anomalia" (observabilidade).
  As duas primeiras mudam o que chega aos detectores.
- **Correção implementada em 13/11 (recorte), atrás de chave DESLIGADA; não ligada na IA real e não commitada:**
  - **Chave:** `AnomalyDetection:HistoricoCausal` (`Services/AnomalyDetection/HistoricoCausalOptions.cs`): `Habilitado = false` e `ToleranciaMinutos = 30` por padrão, no código. O `appsettings.json` não tem a seção (há teste que confere isso).
  - **Onde:** `PredictionDetectionPreparationService.PrepararAsync` → `RecortarAteLeitura`. Com a chave ligada, só os pontos com `DataHora < dataHoraAtual` seguem para o `DetectorInput`, o regime e o comportamento. O ponto do próprio minuto também sai, como no replay.
  - **Proteção de relógio e fuso:** se os pontos posteriores à leitura avançam mais que a tolerância, ou se a tolerância é inválida, nada é recortado e o validador recusa como antes. O log grava `HISTÓRICO NÃO RECORTADO (TOLERÂNCIA)`; quando recorta, grava `HISTÓRICO RECORTADO NA LEITURA` com a quantidade de pontos removidos.
  - **Não muda:** o validador, o contrato HTTP, o lote, a idade da leitura (P18), a cadência (P26), a escala (P30) e o snapshot bruto usado na tendência pós-decisão.
  - **Testes:** `IAApi.Tests/Services/AnomalyDetection/HistoricoCausalTests.cs` (18 casos).
    - T1 reproduz 12/11 com dados sintéticos: leitura às 14:37, histórico buscado às 14:54.
    - T1b percorre a cadeia `CurrentValuePredictionService.DetectarComValorAtual` → `AnomaliaDetectada`.
    - Antes do recorte, T1 e T1b **falhavam** pelo motivo do P31: "anterior ao último ponto" e nota gravada 0 contra 4,1 da referência. Depois, passam com nota igual à do histórico que termina antes da leitura.
    - O golden do replay passa com a chave desligada e ligada.
  - **Replay:** opção `--historico-causal` e `Compare-Replay.ps1 -HistoricoCausal`.
    - Resultado: **0 diferenças em 12 execuções com a chave desligada e com ela ligada**. O CSV do replay já serve só pontos anteriores à leitura, então o recorte não remove nada.
    - Suíte completa: 967 aprovados, 0 falhas.
  - **Rollback:** chave em `false` (ou sem a seção) e reiniciar a IAApi; ou reverter o código.
  - **Falta:**
    - ligar numa sessão autorizada no simulador e medir a fração de entradas inválidas e de notas 0 (esperado ≈ 0);
    - o caso acima da tolerância continua sendo nota 0 silenciosa (opção (a) do BACKLOG 25);
    - risco de alarme falso com a B-02 ligada pelo perfil colapsado (P32) [hipótese].
- **[etapa 0] Correção confirmada em campo** (16/11, 15:19–16:46, preset 1, chave ligada; `Docs/MEDICAO_ETAPA0.md` §5; confirmado em log):
  - **270 de 270** predições com `HISTORICO RECORTADO NA LEITURA` e **0** `NÃO RECORTADO (TOLERÂNCIA)`. O maior avanço foi de 12 min, abaixo dos 30 de tolerância.
  - **0 entradas inválidas.** Nota 0 em **2 de 269 (0,7 %)**, contra 76 % em 12/11. As duas são avaliações válidas: detectores aplicáveis, todos com nota 0 (TAG29 às 15:28, TAG27 às 16:20).
  - Pontos removidos: 1 ponto (só o do próprio minuto) em 33 %; 2 em 42 %; 3 em 21 %; 4 a 12 em 4 %. **Sem o recorte, 181 de 270 (67 %) teriam ponto posterior à leitura e seriam recusadas** [inferência pelo validador].
  - Minutos-tag avaliados: **24 %** (era ≈ 2 %). O limite agora é a cadência (P26), não a validação.
  - Continuam pendentes a opção (a) (caso acima da tolerância, que não ocorreu) e os alarmes com a B-02 ligada (P32). As 9 anomalias da sessão **não foram avaliadas** contra o gabarito.
- **BACKLOG:** **25 (prioridade 1)**, 6 (medir) e 4.

### P32. Perfil de produção do PK2 antigo, curto e colapsado nas tags da B-02

- **O que acontece.** Sem regime e sem comportamento (o caso do PK2), a média, o desvio e o deadband da detecção vêm do `PerfisIa`. No PK2, esse perfil foi treinado uma única vez, em **30/10** (14:10–15:23, tag a tag), com 161–232 pontos, ~2,7–3,9 h a 1/min. Nunca foi retreinado: só existe treino manual (P13).
- **Evidência** (confirmado nos dados e no código, INV §2):
  - **TAG28:** média 0,35, desvio 0,72, 77 % zeros, deadband 2;
  - **TAG32:** média 0,03, desvio 0,17, 97 % zeros, deadband 0,04. Perfil da "B-02 parada".
  - **TAG25/26:** desvio inflado (273 e 418) pela mistura de estados.
  - **TAG24:** desvio 32,8, contra 2,6 no treino limpo.
  - No trecho de 12/11, isso **evitou** os 7 alarmes de reserva parada que o replay deu (P02): o perfil do banco só conhece a B-02 parada.
- **Efeito com a B-02 ligada:** hipótese. TAG28 e TAG32 ficariam a dezenas de desvios da média, com Z e EWMA saturados (alarme falso provável). As 131 anomalias de 30/10–05/11 não foram analisadas.
- **Solução candidata:** retreinar com período limpo e longo, com aprovação, porque treinar altera a detecção. Depende de BACKLOG 8 (escolha do período) e do regime (P02, P03).
- **BACKLOG:** 27 (depende de 8 e 25); 2 (feito).

### P41. Falha no sensor de pressão dentro da malha aparece nas bombas, não no sensor [preset 3 de 13/11]

- **O que acontece.** O PT-02 (TAG24) é a medida que a malha de pressão usa para comandar as bombas. Quando o sensor erra, a malha "acredita" nele e mexe nas bombas para trazer a leitura de volta ao set-point. A tag do sensor continua normal, e o erro aparece na frequência, na corrente, na potência e na vazão. A IA olha cada tag sozinha e o placar só procura a falha na tag do sensor, então os dois perdem a falha.
- **Evidência** (RES3 §4.2, §4.4; confirmado nos dados e no replay):
  - **Falha 20 (congelado, 15:22–15:51):** TAG24 fixa em 450. A frequência da B-02 desce em rampa de 438 a 303, com vazão e potência caindo junto, e volta a 437 no minuto em que a falha acaba.
  - **Falha 24 (ganho −30 %, 19:02–19:31):** TAG24 em 445–453 (normal). Às 19:02, a frequência sobe +35, a corrente +25, a potência +48 e a vazão +168.
  - **Na TAG24:** nota máx. 3,4 e 5,1, nenhum alarme, nas 4 configurações.
  - **Nas bombas, com o regime combinado** (placar com o mapa estendido): falha 20 detectada na **TAG32 às 15:36** (atraso 14 min); falha 24 na **TAG27/28 às 19:02** (atraso 0). TAG31/32 alarmaram 28 dos 30 min da falha 24, mas ficaram excluídas por 1 alarme às 18:53. **Sem regime e com o recomendado, nada:** o perfil largo das bombas não separa a mudança.
  - O CENARIOS §4 já avisava, para o congelamento: "a malha de pressão segue a leitura errada, então outras tags podem reagir também".
- **Por que importa:** num teste por tag, essas falhas parecem "não detectadas", e na prática o sinal existe. O mesmo sinal nas bombas também aparece em picos de demanda (P03), então não basta somar as tags ao mapa: é preciso ligar a mudança da bomba à pressão que não mudou [hipótese].
- **Causa:** limite do método univariado (P06) mais a regra do placar (P21). O mecanismo da malha está em `Docs/Simulador/SIM_ANOMALIAS_CP300.c` [mecanismo exato no simulador não conferido].
- **Avaliação: feito em 16/11, com aprovação** (só ferramenta offline; runtime da IAApi sem mudança):
  - `placar_falhas_cp300.json` ganhou a seção opcional **`efeitosPorCanal`**: o canal 3 (PT-02) inclui TAG25, TAG27, TAG28, TAG31, TAG32 e TAG33, para **qualquer** falha de sensor nesse canal, nos dois sentidos. Os outros canais não mudam.
  - `Cp300Placar.cs` lê e valida a seção (canal existente; só `TAGnn`, sem rótulos) e marca o evento com a observação `efeitosPorCanal, P41`. Sem a seção, a saída é a anterior: com o mapa antigo, os 4 placares do preset 3 saíram **idênticos byte a byte** aos de antes. README §6.1 atualizado.
  - Testes (`Cp300PlacarTests`): canal 3 conta a malha e canal 4 não; 4 casos de validação; P41 no teste do mapa do repositório. `dotnet test --filter Cp300`: 49/49; com a inclusão desligada (mutação temporária, restaurada), 2 falham.
  - **Resultado no preset 3** (RES3 §4.4): sem regime e `rec`, 3 → 3; combinado, **3 → 5** (20 às 15:36 na TAG32; 24 às 19:02 na TAG27/28). A RES3 mostra as duas leituras lado a lado, porque as tags da malha também reagem à demanda (P03).
- **Solução candidata para a detecção** (não testada):
  - Relação entre tags, por exemplo pressão (ou TAG24 − TAG46) × frequência e vazão (BACKLOG 13; multivariado, P06). Para o congelamento, o detector de valor parado (P12).
- **BACKLOG:** não há item; relacionados: 3 (parte B), 10 e 13.

### P42. O LiveMonitor sobrecarrega o Bridge e o banco [etapa 0]

- **O que acontece.** O LiveMonitor, ferramenta de acompanhamento da demo, faz duas consultas periódicas que custam muito mais do que mostram:
  - **Histórico:** para cada tag selecionada, chama `/monitor-api/history` → `GET /api/tags/historico/{cliente}/{tag}?horas=2` (`tools/IAApi.LiveMonitor/server/proxy.ts:33`). São 3 de cada vez (`mapLimited`, `src/polling.ts`), e uma nova rodada começa 15 s depois da anterior (`MONITOR_HISTORY_MS`, padrão 15.000).
    - O `TagHistoricoController.BuscarHistorico` chama `IHistoricoService.BuscarHistorico`, que pede ao Bridge as **24 h** de `Bridge:HorasHistorico` (~1.437 pontos). Só depois filtra as 2 h.
    - O proxy aborta a requisição após **12 s** (`proxy.ts:66-67`). Mas `BridgeHistoricoService.BuscarJson` faz `GetAsync(url).GetAwaiter().GetResult()` (linhas 229–230), bloqueante e sem cancelamento, com timeout de 60 s. Então a busca continua no Bridge enquanto o LiveMonitor já pediu a próxima.
  - **Decisões:** a cada **2 s** (`MONITOR_POLL_MS`), chama `/monitor-api/decisions` → `GET /api/anomalias`.
    - O `AnomaliaController.Listar` (linhas 85–116) carrega **todas** as `AnomaliasDetectadas` de **todos os clientes**, sem filtro nem paginação (~3.200 linhas em 16/11).
    - Para cada linha, chama `FeedbackAnomaliaAnaliseService.GerarAnaliseFeedbacks`, ou seja, uma consulta ao SQLite (N+1).
    - O filtro por cliente é feito no navegador (`src/domain.ts:11`).
    - O `Export-AnomaliasIaReal.ps1` usa a mesma rota e também filtra localmente.
- **Evidência** (log de 16/11, `Docs/MEDICAO_ETAPA0.md` §6.2; confirmado em log):
  - A partir de 16:27:17 (primeira requisição HTTP da sessão), apareceram **212 buscas de 24 h ao Bridge fora das predições** (17–18 por tag do PK2): média de **36,6 s**; **até 12 simultâneas** (mediana de ~6); **4 timeouts de 60 s** (TAG31 e TAG33, de 16:41 a 16:44).
  - No mesmo período, **~958 mil consultas de feedback** ao SQLite (~3.200 por chamada; só 270 são do monitor).
  - **No monitor:**
    - busca da preparação de 4,9 s → **21,9 s**; da tendência, de 4,7 s → **23,2 s** (o aumento está na espera do Bridge, `Bridge.HttpBuffered`);
    - predição de 10,5 s → **45,8 s**; ciclo do PK2 de 131 s → **306 s** e **≥ 697 s**;
    - idade máxima da leitura de 3,5 → **12,0 min**; lote do PK2 de 5,6 → 35,2 s;
    - **HTTP 500 do Bridge** na TAG27 ("open operation … 00:00:03"), que ficou sem decisão.
- **Status:** mecanismo **confirmado no código**; efeito **confirmado em log**, pela coincidência no tempo. A causalidade é **inferência forte**: não houve fase controlada com o LiveMonitor fechado depois de 16:27. **Hipóteses complementares:** fila de threads ocupada pelas chamadas síncronas abandonadas; disputa de disco pelo log (P44).
- **Por que importa:**
  - A ferramenta que mostra a IA na demo deixa a IA 4–5× mais lenta, e o que se vê na tela é uma IA degradada.
  - As medições da tarefa 6 (6.0b, 6.1, 6.2) não são comparáveis se o LiveMonitor estiver aberto numa fase e fechado na outra.
  - `GET /api/anomalias` também devolve decisões de todos os clientes; isso é exposição entre clientes, tratada com o P34.
- **Enquanto não corrigido:** nas sessões de medição, deixar o LiveMonitor fechado ou registrar quando foi aberto. Rodar o export só no fim.
- **Solução** (BACKLOG 34):
  - ✅ histórico do controller só no período pedido (34.3, 17/11, sem commit): **1 busca de 2 h no Bridge, contra 24 h**, com a mesma resposta de antes (status e JSON, teste com referência congelada em 120 combinações). Com período vazio, HTTP de erro do Bridge ou `horas` fora de 1…`Bridge:HorasHistorico`, volta ao caminho de 24 h, sem mudança nele. Timeout ou Bridge inacessível não repetem a busca em 24 h;
  - ✅ `GET /api/anomalias` sem N+1 (34.1, 17/11, sem commit): **2 consultas por chamada, contra 1 + N** (241 na base de teste; ~3.200 em 16/11). JSON igual byte a byte ao de antes, no controller e pelo HTTP (teste com referência congelada);
  - ✅ parâmetros opcionais `clienteId`, `desde` e `limite` (34.2, 17/11, sem commit). Sem eles, a resposta é a de antes. O LiveMonitor passa `clienteId` e, depois da 1ª consulta, `desde` (últimos ~5 min). O export passa `clienteId` e `desde`. A lista acumulada no monitor e o CSV do export saem iguais aos de antes (testes);
  - ✅ LiveMonitor com intervalo maior e sem abandonar buscas no Bridge (34.4, 17/11, sem commit): na rota de histórico, o proxy espera até **125 s** (2 buscas de 60 s na IAApi + margem), **não cancela** a chamada à IAApi quando a página fecha e faz **uma busca por cliente/tag de cada vez** (um pedido repetido espera a mesma resposta). Intervalo padrão de histórico de **60 s** (era 15 s). As outras rotas continuam com 12 s;
  - O contrato HTTP atual fica preservado quando os parâmetros novos não são enviados.
- **Status [17/11]:** a parte das **decisões** está corrigida no código, confirmada em teste offline. **Não medida em sessão.** ~~A parte do **histórico** (24 h por chamada, buscas abandonadas no Bridge) continua aberta (34.3 e 34.4). Até lá, vale a regra de operação da 34.0.~~ **[17/11, mais tarde]** A parte do **histórico** também está corrigida no código (34.3 e 34.4), confirmada em teste offline: suíte 1298/1298, LiveMonitor 39/39, `Compare-Replay.ps1` com 0 diferenças sem e com `-HistoricoCausal`. **Nada disso foi medido em sessão** (é o aceite da tarefa 34: 30 min com o LiveMonitor nas 12 tags e PERF_DIAG). Até lá, e até o commit, vale a regra de operação da 34.0 nas sessões de medição.
  - **Limites que continuam:** a IAApi segue sem propagar o cancelamento até o Bridge (chamada síncrona; tarefa 22, passo 3); a busca única vale por processo do LiveMonitor; com período vazio ou HTTP de erro, são duas buscas (2 h + 24 h).
- **BACKLOG:** **34** (prioridade alta, antes da demo); relacionados: 6 (6.0b–6.2), 19, 28, 30 e P34.

### P43. Banco local sem a migration RC-5: o regime falha em toda predição [etapa 0]

- **O que acontece.** A migration `20261113160248_RC5_AddTagRegimeComponente` (13/11) acrescenta a coluna `MapaEstados` em `TagsRegimesOperacionaisIa` e cria a tabela `TagsRegimesComponentesIa`.
  - O banco local da IAApi (`ia_config.db`) não tem a coluna. A consulta do `OperationalRegimeBaselineProvider` (linhas 104–120) falha com `SQLite Error 1: 'no such column: t.MapaEstados'`.
  - O provider captura a exceção e devolve "não aplicado", ou seja, o **baseline global**.
  - O único sinal é o par de linhas `fail:` do EF no log. Não há Warning do provider nem nada no LiveMonitor.
- **Evidência:**
  - **Confirmado em log:** 270 de 270 predições (PK2 e PK7), em `Preparation.RegimeBaseline`, de 15:19 a 16:46 de 16/11.
  - **Confirmado no código:** a migration existe. O projeto não chama `Migrate()`, `EnsureCreated()` nem `GetPendingMigrations()` em nenhum lugar fora dos testes, então o banco só muda com `dotnet ef database update` manual.
  - ~~**Não verificado** (exige abrir o banco, com aprovação): quais migrations estão aplicadas e se o PK7 tem regime cadastrado.~~ **[17/11] Confirmado no banco** (BACKLOG 35.0, numa cópia): 32 de 33 migrations aplicadas (última `20261002183911_B325_AddRichBehaviorSnapshotIdentity`), **só a RC-5 pendente**; `TagsRegimesOperacionaisIa` com **0 linhas**.
- **Efeito na decisão:**
  - **PK2:** nenhum. ~~Em 13/11, o banco não tinha nenhuma linha em `TagsRegimesOperacionaisIa` (INV §3) [histórico a validar]~~ confirmado em 17/11: 0 linhas; "sem configuração" também cai no global.
  - **PK7:** ~~se tiver regime cadastrado, está rodando com o baseline global [hipótese]~~ **descartado em 17/11:** o PK7 não tem regime cadastrado, então também não mudou de decisão. A diferença de tempo do `RegimeBaseline` (321 ms × 50 ms) tem outra causa, não investigada.
  - **RC-12:** com o banco assim, ligar o regime combinado no PK2 **não teria efeito, sem aviso**. O aceite da RC-12 (`RegimeKey` no diagnóstico) pegaria isso, mas só depois de gastar a sessão.
  - **Testes e replay** usam bancos temporários ou em memória, criados pelo modelo atual. Por isso a suíte e o replay passam e não pegam este problema.
- **Prioridade:** alta. Precisa ser resolvido antes da RC-12, da 6.4 e da demo.
- **Solução** (BACKLOG 35):
  - ✅ conferir o banco em só leitura (35.0, 17/11);
  - ✅ fazer cópia de segurança e aplicar a migration (35.1, 17/11, sem commit). Cópia com SHA-256 em `C:\Users\Laura Marrari\Documents\IA MARRARI\backup_ia_config_2026-11-17\`. Schema conferido (`MapaEstados` e `TagsRegimesComponentesIa`). Contagem e conteúdo das tabelas iguais antes e depois, exceto `__EFMigrationsHistory` (+1). Rollback na tarefa 35;
  - ✅ confirmar na próxima sessão com a IAApi que o `no such column` sumiu (35.2). **Confirmado em log na sessão 6.0b (17/11):** 0 ocorrências; `RegimeBaseline` `Succeeded` em 448 de 448 predições; baseline global em 448 de 448 (sem regime cadastrado; `MEDICAO_ETAPA0.md` §10.8);
  - ⏳ na subida, avisar quando houver migration pendente, sem aplicar sozinho (35.3, opcional, não aprovada).
- **BACKLOG:** **35** (prioridade alta, antes da RC-12); relacionados: RC-5, RC-8, RC-12, 6.4 e 19.

### P44. Log da sessão sem horário e com o SQL do EF [etapa 0]

- **O que acontece:**
  - **Sem data e hora:** o `tools/Start-IAApiComLog.ps1` define `Logging__Console__FormatterOptions__TimestampFormat = "yyyy-MM-dd HH:mm:ss "` para o processo filho (linha 210). Mesmo assim, as linhas saíram sem data e hora (ex.: `info: Microsoft.Hosting.Lifetime[14]`).
    - As outras variáveis, definidas do mesmo jeito, tiveram efeito: `HISTORICO RECORTADO` e PERF_DIAG aparecem.
    - O `Program.cs` não configura o logging (provedores padrão).
    - **Causa (confirmada em teste, 17/11):** sem `Logging:Console:FormatterName`, o provedor de console escolhe o formatador `simple` copiando as opções antigas (`Logging:Console:TimestampFormat`) e descarta `Logging:Console:FormatterOptions`. O script só definia `FormatterOptions__TimestampFormat`. Teste: `IAApi.Tests/Monitoring/ConsoleLogFormatoSessaoTests.cs`. **Corrigido no script (36.2, sem commit)** com `Logging__Console__FormatterName = simple`; falta confirmar na próxima sessão real.
  - **Volume:** `LogLevel:Default = Information` (`appsettings.json` e `appsettings.Development.json`) inclui `Microsoft.EntityFrameworkCore.Database.Command`, que grava cada comando SQL com o texto. Foram **960.008** entradas do EF entre as ~974 mil do log, e **702 MB** em 87 min (~36 MB/min depois de 16:27, por causa do P42).
  - **Codificação:** o arquivo está na página de código do console (cp850 nesta máquina). Acentos aparecem trocados (`Predi�ao`, `Conclu¡da` lido como latin-1).
  - As linhas de `Console.WriteLine` (`HISTORICO RECORTADO`, `LIMPEZA DE HISTORICO`, interpretação) nunca tiveram horário (P27, tarefa 17).
- **Evidência:** confirmado em log (`dados/logs/iaapi_20261116_151930.log`; `MEDICAO_ETAPA0.md` §1).
- **Efeito:**
  - A linha do tempo da sessão só pôde ser montada pelos campos UTC do PERF_DIAG. Sem PERF_DIAG, não haveria nenhuma.
  - O passo a do P27 ("log em arquivo **com data e hora**") ficou incompleto, e o aceite da tarefa 6 ("tabela por ciclo com início") depende disso.
  - Sessões longas (≥ 2 h, ou noturnas) podem gerar vários GB.
  - Neste log, o custo de logging não apareceu no monitor (o "restante" dentro da predição ficou em ~40 ms), mas não foi isolado.
- **Prioridade:** média. É barato e deve ser feito antes da próxima sessão longa.
- **Situação (17/11, sem commit):** horário e SQL do EF em `Warning` **corrigidos no script** (36.1 e 36.2); codificação (36.3) e tamanho (36.4) abertas. ~~Ainda não validado numa subida real.~~
  - **Validado na sessão 6.0b (17/11), em log** (`MEDICAO_ETAPA0.md` §10.7):
    - 18.527 de 18.527 cabeçalhos do `ILogger` com data e hora;
    - 0 `Executed DbCommand`;
    - 15,6 MB em 77 min (~12 MB/h).
  - Continuam sem horário as linhas de `Console.WriteLine` (tarefa 17), e a codificação continua a do console (36.3).
  - **Correção do "Efeito" acima [inferência]:** o custo de logging aparecia, sim. Com o EF em `Warning`, a finalização caiu de 347 para 37 ms em média (p95 de 2,3 s para 0,1 s), mas junto com outras mudanças, sem isolar.
- **BACKLOG:** **36**; relacionados: 6, 17, 19 e P27.

### P45. `Stop-IAApi.ps1`: o processo continua vivo 15 s depois do `Stop-Process -Force` [sessão 6.0b]

- **O que aconteceu (17/11, fim da sessão 6.0b; relato da equipe + arquivos):**
  - O `tools/Stop-IAApi.ps1` não conseguiu encerrar a IAApi (PID 105992) em 15 s. O script usa `Stop-Process -Force` + `Wait-Process -Timeout 15` (`tools/Stop-IAApi.ps1:104-110`) e sai com código 3, mantendo o arquivo de PID.
  - O `dados/logs/iaapi.pid.json` continuava lá depois da sessão, o que confirma esse caminho.
  - Horas depois, o processo já não existia: PID ausente, porta 5145 sem escuta, banco abrindo em modo exclusivo.
- **O que a IAApi fazia no momento (confirmado em log):**
  - A última linha é de **13:01:35**, no meio da predição da PK2.TAG27. Logo antes estão "Predição iniciada", o bloco `BUSCANDO HISTORICO RECENTE NO BRIDGE` (busca síncrona de 24 h, `BridgeHistoricoService.BuscarHistorico` → `BuscarJson`, sem cancelamento) e o PERF_DIAG de `Prediction.OperationalContext`.
  - Não há `Bridge.Transport` dessa busca nem nenhuma linha de encerramento.
  - A última decisão gravada é a da TAG26, às 13:01:34,8; a da TAG27 se perdeu (comportamento já documentado no script).
- **Leitura [inferência]:**
  - Depois de 13:01:35 não há nenhuma linha. Se o processo tivesse seguido rodando, a busca terminaria ou estouraria o timeout de 60 s, e isso apareceria no log.
  - O mais provável é que as threads tenham parado por volta de 13:01:35 e que o **fim do processo no Windows** tenha passado de 15 s.
  - A hora exata do `Stop` não está registrada (o relato da equipe diz ~13:05).
- **Hipóteses (não verificadas):**
  - E/S de rede síncrona pendente no kernel (a busca ao Bridge em andamento) atrasando o término;
  - antivírus ou indexação segurando o log ou o `ia_config.db`.
  - O Event Viewer não foi consultado.
- **Efeito:**
  - Nenhum na decisão.
  - **Risco operacional:** procedimentos que exigem "IAApi parada" (cópia de segurança da 35.1, migration da 27.3b na demo, cópias do banco para análise) podem começar com o processo ainda terminando e o `-wal` aberto.
  - O `Start-IAApiComLog.ps1` não fica bloqueado: só recusa subir se o PID salvo estiver vivo, com a mesma hora de início (linhas 195-210).
- **Mitigação já usada (17/11):** antes de copiar o banco, conferir o PID, a porta e a **abertura exclusiva** dos três arquivos (`ia_config.db`, `-wal`, `-shm`).
- **Solução proposta (não aprovada; BACKLOG 36.5):** no `Stop-IAApi.ps1`, quando der a saída 3:
  - esperar mais (por exemplo, até 60 s) e informar o estado a cada 15 s;
  - registrar a hora do `Stop` e a última linha do log num arquivo ao lado do log;
  - dizer como conferir antes de mexer no banco (`-Diagnostico`, porta, abertura exclusiva).
  - Investigar a causa só se o caso se repetir (Event Viewer; `handle.exe` ou Resource Monitor no PID).
- **Prioridade:** baixa. A ferramenta de sessão funciona, e o risco é coberto pela conferência antes de mexer no banco.
- **BACKLOG:** 36.5; relacionados: 27.3b (backup antes da migration), 35.1, P27 e tarefa 22, passo 3 (cancelamento até o Bridge).

### Problemas de segurança da auditoria B3.23 (P33 a P40)

**Contexto comum** (confirmado no código, HEAD `05c64c3`, 16/11). Conferência só por leitura (BACKLOG 29, fase A): a API não foi iniciada e nenhum endpoint foi chamado. Fonte original: `git show 15f1ca9^:Docs/B3.23_CODEX_AUDIT_RESULT.md`, §9, §11, §12 e §18.

- `Program.cs:7-44` não registra autenticação nem autorização (`AddAuthentication`, `UseAuthorization` e `[Authorize]` não aparecem no projeto). `app.MapControllers()` (`Program.cs:44`) mapeia todos os controllers em qualquer ambiente; só o Swagger depende de `IsDevelopment()` (`Program.cs:34`).
- A única guarda existente é a do Intelligence Lab (`Controllers/IntelligenceLabLocalReadAccess.cs:35-58`): só em Development, só de loopback e com chave no cabeçalho. Ela não cobre os demais controllers.
- **O que hoje reduz a exposição** (não é proteção no código): a porta padrão é `localhost` (`Properties/launchSettings.json:8`; `tools/Start-IAApiComLog.ps1`, `-Url` padrão `http://localhost:5145`). Basta subir com `-Url http://0.0.0.0:…` ou um IP da rede, por exemplo numa demo, para tudo abaixo ficar acessível a quem estiver na rede. `AllowedHosts` é `*` (`appsettings.json:113`).
- O CORS (`Program.cs:10-19`) não substitui autenticação: ele só impede que outra página **leia** a resposta, não que o pedido chegue.

### P33. `POST /api/dev/reset` apaga a base de todos os clientes, sem proteção (B323-01)

- **O que acontece** (confirmado no código): `Controllers/DevController.cs:14-35` remove todas as linhas de `AnomaliasDetectadas`, `PerfisIa`, `TagsIaConfig`, `ProcessoTagsIa`, `ProcessoEquipamentosIa`, `ProcessosIa`, `EquipamentoTagsIa`, `EquipamentosIa`, `TagDependenciasIa` e `TagsContextoIa`, sem filtro de cliente. Não há condição de ambiente, de origem nem de chave. Nada no repositório chama esse endpoint (busca por `dev/reset` fora de `Docs/`).
- **Risco em uma frase:** quem alcançar a porta da API apaga, com um único pedido sem corpo, a configuração e o histórico de decisões de todos os clientes.
- **[Hipótese, não testada]** Mesmo com a API só em `localhost`, uma página aberta no navegador da mesma máquina poderia disparar esse POST (pedido "simples", sem corpo; o CORS bloqueia só a leitura da resposta). Navegadores recentes podem bloquear pedidos para a rede local; não foi verificado.
- **Correção mínima proposta:** remover o `DevController`, ou fazê-lo responder `404` fora de Development e fora de loopback, com a mesma regra já usada em `IntelligenceLabLocalReadAccess` (sem criar guarda nova). Teste unitário com `DefaultHttpContext`, como os do Intelligence Lab (`IAApi.Tests/IntelligenceLab/IntelligenceLabHealthEndpointTests.cs`). **Esforço: P** (< 1 h + teste). Não afeta a detecção. **Rollback:** reverter o commit.
- **Correção aplicada (16/11, aprovada pela responsável, sem commit; BACKLOG 30):**
  - `DevController` recebe `IHostEnvironment` e responde `404` sem tocar no banco, a menos que o ambiente seja Development **e** o pedido venha de loopback.
  - A regra foi extraída do `IntelligenceLabLocalReadAccess` para `Controllers/LocalDevelopmentRequestGuard.cs`, e as duas guardas usam a mesma. O comportamento do Intelligence Lab não muda: os 39 testes dele passam.
  - Testes: `IAApi.Tests/Security/DevControllerResetGuardTests.cs`, 8 casos (Production, Staging, 3 IPs de fora, sem endereço, loopback IPv4 e IPv6), todos aprovados.
  - Em Development e na própria máquina, o reset continua funcionando como antes.
  - **Não muda decisão de anomalia.**
  - **Rollback:** reverter `Controllers/DevController.cs`, `Controllers/LocalDevelopmentRequestGuard.cs` e `Controllers/IntelligenceLabLocalReadAccess.cs`.
- **Status:** ~~confirmado no código~~ **corrigido no código** (testes unitários; a API não foi iniciada). **Prioridade:** resolvido. **Origem:** B323-01.
- **BACKLOG:** 30.

### P34. API sem autenticação: leitura e alteração de dados de todos os clientes (B323-02)

- **O que acontece** (confirmado no código; ver o contexto comum). Exemplos no HEAD:
  - **leitura global:** `GET /api/anomalias` devolve as anomalias de todos os clientes (`Controllers/AnomaliaController.cs:81-86`); `GET /api/equipamentos` devolve todos os equipamentos com tags e processos (`Controllers/EquipamentoController.cs:51-61`); `GET /api/anomalias/{id}/feedback` sem escopo (`Controllers/FeedbackAnomaliaController.cs:97-103`);
  - **alteração só por ID:** `PUT`/`DELETE` de motivos (`Controllers/MotivosIAController.cs:67-74`, `:91-100`), e `DELETE` em `AcoesIAController.cs:106`, `CategoriasFalhaIAController.cs:84`, `GrupoAnaliseController.cs:104` e `JanelasTreinamentoController.cs:410`. Ao todo, 25 controllers têm `POST`/`PUT`/`DELETE`.
- **Risco em uma frase:** com a API acessível na rede, qualquer pessoa vê os dados industriais de todos os clientes e pode mudar configuração, treino, grupos e feedback, sem rastro de quem fez.
- **Correção mínima proposta**, em dois níveis:
  1. **antes de demo (mitigação):** manter a API só em `localhost` e fazer o `Start-IAApiComLog.ps1` recusar `-Url` fora de loopback sem uma chave explícita, como o `Export-AnomaliasIaReal.ps1` já faz com `-PermitirRemoto` (`tools/Cp300Export/Export-AnomaliasIaReal.ps1:186`). **Esforço: P.** Não muda código da API;
  2. **definitiva:** autenticação (por exemplo, chave de API ou token) com política por cliente em todos os controllers e no LiveMonitor. Muda o contrato HTTP de todos os consumidores. **Esforço: G**, com tarefa e aprovação próprias.
- **Mitigação aplicada para a demo (16/11, aprovada pela responsável, sem commit; BACKLOG 30):**
  - O `tools/Start-IAApiComLog.ps1` recusa, **antes de qualquer outra ação**, um `-Url` que não seja `http`/`https` em `localhost`, `127.0.0.1` ou `[::1]`. Recusa, por exemplo, `0.0.0.0`, `*`, `+`, `[::]`, IP da rede, nome de máquina e várias URLs separadas por `;`.
  - Teste: `tools/Start-IAApiComLog.Tests.ps1`, 25 verificações, todas aprovadas. Ele lê o script pela árvore sintática e **não o executa**: testa a função e confere que a checagem é a primeira ação. O script real com `-Url http://0.0.0.0:5145` foi recusado sem criar nenhum arquivo.
  - **Limites:** só protege quem sobe pelo script. `dotnet run` e o perfil `https` do `launchSettings.json` (`https://localhost:7295;http://localhost:5145`) já são locais, mas uma variável `ASPNETCORE_URLS` definida à mão continua podendo expor a API. **A falta de autenticação continua (correção definitiva, esforço G).**
  - **Rollback:** reverter `tools/Start-IAApiComLog.ps1` e apagar `tools/Start-IAApiComLog.Tests.ps1`.
- **Status:** confirmado no código; **mitigado** para subidas pelo script. **Prioridade:** Alta (definitiva). **Origem:** B323-02. Inclui as leituras globais da tabela §9 da B3.23.
- **BACKLOG:** 30 (mitigação). Correção definitiva: não há item.

### P35. Relações de uma tag sem filtro de cliente (B323-03)

- **O que acontece** (confirmado no código): `Controllers/TagRelacoesController.cs:23-43` busca `ProcessoTagsIa` e `EquipamentoTagsIa` só por `TagName`, embora as duas tabelas tenham `ClienteId` (`Models/ProcessoTagIa.cs:12`, `Models/EquipamentoTagIa.cs:13`).
- **Risco em uma frase:** tags com o mesmo nome em clientes diferentes (por exemplo, `TAG24`) fazem a resposta misturar processos e equipamentos de outro cliente.
- **Correção mínima proposta:** receber `clienteId` (rota ou query) e filtrar `x.ClienteId == clienteId` nas duas consultas. Muda o contrato HTTP (parâmetro novo obrigatório): conferir os consumidores antes. **Esforço: P.**
- **Correção aplicada (16/11, pedida pela responsável, sem commit; BACKLOG 31), na forma aditiva:**
  - **Consumidores conferidos:** nada no repositório chama `GET /api/tags/{tagName}/relacoes` (LiveMonitor, ferramentas e `IAApi.http`). Mesmo assim, a pedido da responsável, a mudança no contrato é **aditiva**: parâmetro **opcional** `?clienteId=…`.
  - **Com `clienteId`:** as duas consultas filtram `x.ClienteId == clienteId`. Um cliente sem relações para a tag recebe as listas vazias.
  - **Sem `clienteId`** (ou com `?clienteId=` vazio, que o ASP.NET entrega como nulo): a resposta é **a mesma de antes**, com os mesmos campos e as relações de todos os clientes.
  - Testes (primeira etapa): `IAApi.Tests/Security/ClienteIsolamentoTests.cs`, 4 casos `P35_*`. Os 3 com `clienteId` **falhavam antes**: com o HEAD não compilavam (o parâmetro não existia), e com o parâmetro acrescentado mas ignorado falhavam por misturar C1 e C2. Depois da correção, passam. O 4º confere que, sem `clienteId`, a resposta continua igual à anterior e passa antes e depois.
  - **Rollback:** reverter `Controllers/TagRelacoesController.cs`.
- ~~**Falta:** sem `clienteId`, a resposta continua misturando clientes.~~ **Fechado pela opção (a), aprovada pela responsável em 16/11 (sem commit; BACKLOG 31):**
  - Sem `clienteId`, o controller conta os clientes distintos da tag em `ProcessoTagsIa` e `EquipamentoTagsIa`, **juntas**: processo de C1 e equipamento de C2 também contam como dois clientes.
  - **Mais de um cliente:** `400` com `{ "erro": "A tag existe em mais de um cliente. Informe o cliente em ?clienteId=." }`. Não devolve nenhuma relação e não cita os clientes.
  - **Um cliente só, ou nenhuma relação:** resposta **igual à de antes**, com os mesmos campos e `200`.
  - **Com `clienteId`:** sem mudança em relação à correção anterior (filtra; nunca dá `400` por ambiguidade).
  - **Mudança de contrato:** só o caso que antes misturava clientes passa a ser `400`. Nada no repositório chama o endpoint.
  - Testes: `ClienteIsolamentoTests.cs`, agora com 8 casos `P35_*`. O teste anterior "sem `clienteId`, resposta igual" (dois clientes) deu lugar a dois de `400`: tag nos dois clientes, e processo de um com equipamento de outro. Os dois **falhavam antes** (o código devolvia `200` com a mistura) e passam depois. Os 3 de compatibilidade sem `clienteId` (um cliente só, processo e equipamento do mesmo cliente, tag sem relações) e os 3 com `clienteId` passam antes e depois.
- **Limite:** sem autenticação (P34), o `clienteId` delimita o escopo da resposta, mas **não é controle de acesso**.
- **Status:** ~~corrigido em parte~~ **corrigido no código** (testes unitários; a API não foi iniciada). **Prioridade:** resolvido. **Origem:** B323-03.
- **BACKLOG:** 31.

### P36. Vínculo equipamento ↔ tag sem conferir o cliente (B323-04)

- **O que acontece** (confirmado no código): `Controllers/EquipamentoController.cs:72-73` busca o equipamento só pelo ID; `:103` grava o vínculo com `request.ClienteId`, sem comparar com `equipamento.ClienteId`.
- **Risco em uma frase:** um pedido pode ligar a tag de um cliente ao equipamento de outro, e o vínculo fica gravado com um cliente que não é o dono do equipamento.
- **Correção mínima proposta:** recusar com `400` quando `request.ClienteId != equipamento.ClienteId` (ou gravar sempre o cliente do equipamento). **Esforço: P.**
- **Correção aplicada (16/11, sem commit; BACKLOG 31):** primeira opção, o `400`.
  - Logo depois de encontrar o equipamento, `VincularTag` responde `400` (`"O cliente informado não é o cliente do equipamento."`) quando `request.ClienteId` é diferente de `equipamento.ClienteId`. A comparação é exata, como no resto do código.
  - A checagem vale **para criar e para atualizar** um vínculo existente: antes, a atualização também não conferia o cliente.
  - **Mudança de comportamento:** um pedido com `ClienteId` vazio, ou com o cliente errado, que antes gravava o vínculo com esse cliente, agora recebe `400`. Nada no repositório chama esse endpoint. Equipamento inexistente continua `404`.
  - Testes: `ClienteIsolamentoTests.cs`, 6 casos `P36_*`. Os 3 de recusa (cliente diferente, cliente vazio e atualização com cliente diferente, que não altera o vínculo) **falhavam antes** e passam depois. Os 3 de compatibilidade (criar e atualizar com o mesmo cliente, equipamento inexistente) passam antes e depois.
  - **Rollback:** reverter `Controllers/EquipamentoController.cs`.
- **Não corrigido aqui:** vínculos com cliente errado já gravados no banco não foram procurados nem alterados.
- **Status:** ~~confirmado no código~~ **corrigido no código** (testes unitários; a API não foi iniciada). **Prioridade:** resolvido. **Origem:** B323-04.
- **BACKLOG:** 31.

### P37. Vínculo processo ↔ equipamento sem conferir o cliente (B323-05)

- **O que acontece** (confirmado no código): `Controllers/ProcessoEquipamentoController.cs:24-44` confere só se o processo e o equipamento existem. `ProcessoEquipamentoIa` não tem `ClienteId` (`Models/ProcoessoEquipamentoIa.cs`); `ProcessoIa` e `EquipamentoIa` têm, mas não são comparados.
- **Risco em uma frase:** o processo de um cliente pode ficar ligado ao equipamento de outro.
- **Correção mínima proposta:** recusar com `400` quando `processo.ClienteId != equipamento.ClienteId`. **Esforço: P.**
- **Correção aplicada (16/11, sem commit; BACKLOG 31):**
  - Depois de encontrar os dois, `Vincular` responde `400` (`"Processo e equipamento são de clientes diferentes."`) quando `processo.ClienteId` é diferente de `equipamento.ClienteId`, antes de procurar vínculo repetido.
  - Processo ou equipamento inexistente continua `404`, e vínculo repetido continua `400` com a mesma mensagem. A tabela `ProcessoEquipamentosIa` não ganhou coluna.
  - Testes: `ClienteIsolamentoTests.cs`, 3 casos `P37_*`. O de clientes diferentes **falhava antes** e passa depois. Os de mesmo cliente e processo inexistente passam antes e depois.
  - **Rollback:** reverter `Controllers/ProcessoEquipamentoController.cs`.
- **Não corrigido aqui:** vínculos entre clientes que já estejam no banco não foram procurados.
- **Status:** ~~confirmado no código~~ **corrigido no código** (testes unitários; a API não foi iniciada). **Prioridade:** resolvido. **Origem:** B323-05.
- **BACKLOG:** 31.

### P38. Feedback de anomalia aceita motivo de outro cliente (B323-06)

- **O que acontece** (confirmado no código): `Controllers/FeedbackAnomaliaController.cs:34-35` confere só `x.Id == request.MotivoAnomaliaIAId`. `MotivoAnomaliaIA` tem `ClienteId` e `Ativo` (`Models/MotivoAnomaliaIA.cs:5`, `:10`), e nenhum dos dois é conferido.
- **Risco em uma frase:** o feedback de uma anomalia pode citar o motivo de falha de outro cliente, ou um motivo desativado, e misturar a taxonomia de falhas entre clientes.
- **Correção mínima proposta:** acrescentar `&& x.ClienteId == anomalia.ClienteId && x.Ativo` na mesma consulta. **Esforço: P.**
- **Correção aplicada (16/11, sem commit; BACKLOG 31):**
  - A consulta passou a ser `x.Id == request.MotivoAnomaliaIAId && x.ClienteId == anomalia.ClienteId && x.Ativo`.
  - Motivo de outro cliente ou desativado recebe o **mesmo `400`** e a **mesma mensagem** de motivo inexistente (`"O tipo de falha informado não existe."`), para não revelar que o motivo existe em outro cliente.
  - Nesse caso, nenhum feedback é gravado, e o status da anomalia **não muda** (continua `Pendente`).
  - Testes: 4 casos `P38_*` acrescentados a `IAApi.Tests/FeedbackAnomaliaConsistencyTests.cs`, o arquivo que já testava esse controller. Os 2 de recusa (outro cliente, inativo) **falhavam antes** e passam depois. O de motivo inexistente e o de motivo válido (grava e muda o status para `Confirmada`) passam antes e depois.
  - **Não muda decisão de anomalia:** o feedback é posterior à detecção. O que muda é só a recusa de um feedback inconsistente, e com ela a troca de status (`Confirmada`/`Descartada`) que esse feedback faria.
  - **Rollback:** reverter `Controllers/FeedbackAnomaliaController.cs`.
- **Não corrigido aqui:** feedbacks já gravados com motivo de outro cliente não foram procurados.
- **Status:** ~~confirmado no código~~ **corrigido no código** (testes unitários; a API não foi iniciada). **Prioridade:** resolvido. **Origem:** B323-06.
- **BACKLOG:** 31.

### P39. Resposta da LLM, corpo de erro e stack trace em log e na resposta HTTP (B323-17)

- **O que acontece** (confirmado no código; nenhum log foi lido):
  - `Services/AnomalyDetection/Interpretation/LocalLlmAnomalyInterpretationService.cs:75-82` registra em `Information` a **resposta bruta inteira** da LLM;
  - `src/IAApi.Infrastructure/Llm/Providers/LmStudioLlmProvider.cs:96-101` registra em `Error` o **corpo inteiro** de uma resposta HTTP de erro. O mesmo corpo vai para `Erro` (`CriarFalha`, `:345-360`) e é registrado de novo em `Warning` (`LocalLlmAnomalyInterpretationService.cs:61-67`). O `OllamaLlmProvider.cs:99-103` faz o mesmo, mas não está registrado na DI (só o LM Studio, em `DependencyInjection/Llm/LlmServiceCollectionExtensions.cs:15-17`);
  - `Controllers/AnomaliaController.cs:33-47` escreve no console o cliente, a tag e a **resposta inteira** de `POST /api/anomalias/tag`; em erro, `:53` escreve `ex.ToString()` e `:55-59` **devolve o stack trace** ao chamador (`stack = ex.ToString()`);
  - o console vai para arquivo: o `Start-IAApiComLog.ps1` grava stdout em `dados\logs\iaapi_<data>.log` (`tools/Start-IAApiComLog.ps1:155`, `:183`). `dados/` está fora do git.
  - Os controllers devolvem `ex.Message` em 25 pontos (por exemplo, `Controllers/BridgeTagsController.cs:37`); é menos grave, mas pode expor detalhes internos.
- **Risco em uma frase:** dados industriais (cliente, tag, valores, equipamento, causas) ficam em arquivos de log sem controle, e o stack trace mostra a estrutura interna da aplicação a quem fizer um pedido inválido.
- **Correção mínima proposta:** (a) em `AnomaliaController`, devolver só uma mensagem genérica, sem `stack`, e trocar os `Console.WriteLine` por `ILogger` em `Debug`; (b) rebaixar a "RESPOSTA BRUTA DA IA" para `Debug`; (c) nos provedores, registrar só o status HTTP e o tamanho do corpo. Retirar `stack` muda o corpo de erro do contrato HTTP (nada no repositório lê esse campo). Não muda a decisão de anomalia. **Esforço: P.**
- **Correção aplicada (16/11, aprovada pela responsável, sem commit; BACKLOG 30):**
  - `POST /api/anomalias/tag`: em erro, devolve `400` só com `{ "erro": "Não foi possível analisar a leitura. O detalhe do erro está no log da IAApi." }`, sem `stack` e sem o texto da exceção. O status continua `400`, e o detalhe continua no log do servidor (`Console.WriteLine(ex)`).
  - "RESPOSTA BRUTA DA IA" passou de `Information` para `Debug`. Com a configuração atual, ela deixa de aparecer no log.
  - LM Studio e Ollama: o erro HTTP é registrado só com o código, e o corpo da resposta não vai mais para `LlmResponse.Erro`, que também era registrado em `Warning`. Isso vale também nas 2 (Ollama) ou 3 (LM Studio) falhas por resposta mal formada.
  - **Custo:** quando a LLM devolve resposta mal formada, o log não mostra mais o corpo. Para diagnosticar, é preciso ligar Debug.
  - Testes: `IAApi.Tests/Security/SensitiveDataExposureTests.cs`, 6 casos (controller sem stack; LM Studio com erro 500, sem escolha e com resposta válida inalterada; Ollama com erro 502; resposta bruta só em Debug), todos aprovados. O HTTP é falso (`http://llm.test`), sem rede.
  - **Não muda decisão de anomalia:** a interpretação por LLM vem depois da decisão, e só mudou o que vai para o log e para o corpo de erro.
  - **Rollback:** reverter `Controllers/AnomaliaController.cs`, `Services/AnomalyDetection/Interpretation/LocalLlmAnomalyInterpretationService.cs` e os dois provedores em `src/IAApi.Infrastructure/Llm/Providers/`.
- **Falta (fora da aprovação de 16/11):**
  - `AnomaliaController.cs:33-47` ainda escreve no console o cliente, a tag e a resposta inteira de cada análise (vai para `dados\logs` pelo `Start-IAApiComLog.ps1`);
  - os controllers ainda devolvem `ex.Message` em 24 pontos (inclui `POST /api/anomalias/tag/valor-atual`).
- **Status:** **corrigido em parte** (testes unitários; a API não foi iniciada). **Prioridade:** Média para o que falta; Baixa se a API ficar só local (P34). **Origem:** B323-17.
- **BACKLOG:** 30 (parte feita). O resto: não há item; o console do caminho do monitor está na tarefa 17.

### P40. Dependências com alertas de vulnerabilidade (B323-23)

- **O que acontece** (confirmado em `dotnet list package --vulnerable --include-transitive` e `npm audit`, 16/11, com autorização da responsável; só consulta, nada instalado):
  - **`SQLitePCLRaw.lib.e_sqlite3` 2.1.11, alta** ([GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)): na **API** (`IAApi`), em `IAApi.Infrastructure`, `IAApi.Tests`, `Cp300OfflineReplay`, `IAApi.Benchmarks` e `IntelligenceLabReplay`. Vem de `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9 → `SQLitePCLRaw.bundle_e_sqlite3` 2.1.11 (`IAApi.csproj:15`, `src/IAApi.Infrastructure/IAApi.Infrastructure.csproj:8`);
  - **`System.Security.Cryptography.Xml` e `.Pkcs` 10.0.0, alta** ([GHSA-37gx-xxp4-5rgx](https://github.com/advisories/GHSA-37gx-xxp4-5rgx), [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) e outros): só em `IAApi.Tests`, `Cp300OfflineReplay` e `IntelligenceLabReplay`. Vêm de `System.ServiceModel.Primitives` (`IAApi.csproj:18-22`). **Na API o pacote é podado** (`obj/project.assets.json`, `packagesToPrune`) e usa o ASP.NET Core instalado (10.0.11), que não aparece com alerta. **Parte corrigida** em relação à B3.23, onde a API também tinha o alerta;
  - os 4 pacotes `protobuf-net.ServiceModel` e `System.ServiceModel.*` **não têm uso** em nenhum `.cs` (busca por namespaces e atributos);
  - **LiveMonitor** (`tools/IAApi.LiveMonitor`): `tinypool` ≤ 2.1.1 **crítica** (2 alertas de execução de código) e `@vitest/mocker` moderada, ambos via `vitest` 3.2.7. É **só dependência de desenvolvimento** (`package.json`, `devDependencies`); não entra no build servido.
- **Risco em uma frase:** a API carrega uma versão do SQLite com falha conhecida de severidade alta; os demais alertas ficam no ambiente de teste e desenvolvimento.
- **Correção mínima proposta**, em tarefas separadas:
  1. **SQLite:** referência direta a uma versão do `SQLitePCLRaw.bundle_e_sqlite3` fora da faixa do alerta (conferir a versão no advisory no dia da mudança) e rodar a suíte, os replays e a abertura do banco existente. **Esforço: M.**
  2. **Xml/Pkcs:** retirar do `IAApi.csproj` os 4 pacotes ServiceModel/protobuf sem uso (testes e ferramentas herdam a correção), confirmando antes com build e suíte. **Esforço: P.**
  3. **LiveMonitor:** subir o `vitest` para uma versão fora das faixas (o `npm audit` sugere a 5.1.0, que quebra compatibilidade) e rodar os testes do monitor. **Esforço: P–M.** Baixa prioridade (só desenvolvimento).
- **Status:** confirmado nas consultas de vulnerabilidade. **Prioridade:** Média (SQLite); Baixa (o resto). **Origem:** B323-23.
- **BACKLOG:** não há item.

---

## 3. Fluxo exercitado nos presets 1 e 6: arquivos e métodos

### 3.1 O que rodou no replay (do histórico até a decisão)

Colunas: **Verificação** = *executado no replay* (o método rodou no `tools/Cp300OfflineReplay`) ou *lido no código*. No replay, o histórico vem do CSV (`CsvHistorySource`) e o banco é SQLite em memória; o resto é código de produção.

| Ordem | Arquivo | Classe.método | O que faz | Problemas | Verificação |
|---|---|---|---|---|---|
| 0a | `appsettings.json` | seções `AnomalyDetection` e `Bridge:HorasHistorico` | Pesos, limiares por detector, limiar do ensemble (60), janela de 24 h | P07, P16, P17 | Executado (o replay lê o arquivo) |
| 0b | `src/IAApi.Engine/Detection/AnomalyDetectionOptions.cs` | `AnomalyDetectionOptions` (+ `AnomalyDetectionOptionsValidator`) | Recebe e valida os valores do `appsettings`; tem padrões próprios diferentes | P16, P17 | Executado |
| 0c | `Models/TagIaConfig.cs` (tabela `TagsIaConfig`) | — | Diz quais tags têm IA ativa | P15 | Executado (linhas criadas pelo replay) |
| 0d | `Models/TagRegimeOperationalIa.cs` (tabela `TagsRegimesOperacionaisIa`) | — | Tag analisada → tag de regime | P03, P14 | Executado só com `--regime` |
| 1 | `Services/PerfilTagService.cs` | `PerfilTagService.TreinarPerfil` | Treina o perfil da tag com as últimas 24 h | P01, P13 | Executado |
| 2 | `Services/IHistoricoService.cs` (produção: `Services/BridgeHistoricoService.cs`) | `IHistoricoService.BuscarHistorico` | Devolve as últimas 24 h da tag (no replay, do CSV) | P10, P13 | Executado com o CSV; o Bridge não |
| 3 | `Services/TagProfileStatisticsCalculator.cs` | `TagProfileStatisticsCalculator.Calculate` | Média, desvio, deadband (P95 das variações) | P01, P09 | Executado |
| 4 | `Services/HistoricoLimpezaService.cs` | `HistoricoLimpezaService.RemoverOutliersPorIqr` | Remove outliers por IQR antes das estatísticas | **P01**, P09 | Executado |
| 5 | `Models/TagPerfilIa.cs` (tabela `PerfisIa`) | — | Guarda o perfil treinado | P01 | Executado (SQLite em memória) |
| 6 | `Services/PredictionService.cs` | `PredictionService.Detectar` | Orquestra a detecção de uma leitura | — | Executado |
| 7 | `Services/AnomalyDetection/Orchestration/PredictionContextLoader.cs` | `BuscarConfiguracaoAtiva`, `BuscarPerfil`, `CarregarContextoOperacional` | Lê a configuração, o perfil e o tipo cadastrado da tag | P15 | Executado |
| 8 | `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs` | `PrepararAsync` | Busca 24 h de histórico e escolhe o baseline (perfil → regime → comportamento) | P08, P10 | Executado (o comportamental não é instanciado) |
| 9 | `Services/AnomalyDetection/OperationalRegimeBaselineProvider.cs` | `OperationalRegimeBaselineProvider.Resolve` | Lê a configuração de regime e o histórico da tag de estado | P08, P14 | Executado só com `--regime` |
| 10 | `Services/AnomalyDetection/OperationalRegimeBaselineService.cs` | `OperationalRegimeBaselineService.Resolve` | Calcula as estatísticas do regime atual (com IQR) | P03, P09 | Executado só com `--regime` |
| 11 | `Services/AnomalyDetection/OperationalRegimeHistoryResolver.cs` | `OperationalRegimeHistoryResolver.Resolve` | Separa os pontos do mesmo estado; mínimo de 30 | P03, P08 | Executado só com `--regime` |
| 12 | `src/IAApi.Engine/Detection/Input/DetectorInput.cs` + `Core/DetectionHistorySanitizer.cs` | `DetectorInput.Historico` (setter) → `DetectionHistorySanitizer.Sanitize` | Limpa duplicados, ordena e mede gaps do histórico | P19 | Executado |
| 13 | `src/IAApi.Engine/Detection/Core/DetectorContext.cs` | `DetectorInput.Contexto` → `DetectorContext` | Mediana, MAD e taxas do histórico (usadas por MAD e RoC) | — | Executado |
| 14 | `Services/AnomalyDetection/Orchestration/PredictionDecisionOrchestrationService.cs` | `ExecutarAsync` | Ensemble → contexto → decisão runtime → interpretação | — | Executado |
| 15 | `Services/AnomalyDetection/EnsembleDetectionService.cs` | `EnsembleDetectionService.Detectar` | Chama a validação, a classificação e os 6 detectores | — | Executado |
| 16 | `Services/AnomalyDetection/DetectionInputValidator.cs` | `DetectionInputValidator.Validate` | Entrada inválida ⇒ sem anomalia | P19 | Executado (0 inválidas) |
| 17 | `src/IAApi.Engine/Detection/Behavior/TagBehaviorProfileResolver.cs` | `TagBehaviorProfileResolver.Resolve` | Classifica a tag (contínua/discreta/contador/setpoint) | P20 | Executado (monitoradas = `Unknown`) |
| 18 | `Services/AnomalyDetection/DetectorApplicabilityPolicy.cs` | `DetectorApplicabilityPolicy.Evaluate` | Bloqueia os detectores para tipos não contínuos | P20 | Executado (bloqueou só a TAG44) |
| 19 | `src/IAApi.Engine/Detection/Core/BaselineProtectionProfile.cs` | `Build`, `ResolveEffectiveScale`, `IsInsideDeadband` | Deadband (nota 0 dentro dele) e escala mínima | P09 | Executado |
| 20 | `src/IAApi.Engine/Detection/StatisticalDetector.cs` | `StatisticalDetector.Detectar` (Z-Score, peso 0,15) | Distância à média do perfil, em desvios | P01, P02, P05 | Executado |
| 21 | `src/IAApi.Engine/Detection/MadDetector.cs` | `MadDetector.Detectar` (peso 0,15) | Distância à mediana das 24 h (robusta) | P02 | Executado |
| 22 | `src/IAApi.Engine/Detection/EwmaDetector.cs` | `EwmaDetector.Detectar` (peso 0,15) | Média móvel contra o perfil | P03 | Executado |
| 23 | `src/IAApi.Engine/Detection/RateOfChangeDetector.cs` | `RateOfChangeDetector.Detectar` (peso 0,10) | Velocidade de variação contra a habitual | P03 | Executado |
| 24 | `src/IAApi.Engine/Detection/PersistenceDetector.cs` | `PersistenceDetector.Detectar` (peso 0,10) | Desvio sustentado (≥ 5 pontos / 1 min) | P02, P04 | Executado |
| 25 | `src/IAApi.Engine/Detection/LevelShiftDetector.cs` | `LevelShiftDetector.Detectar` (peso 0,10) | Mediana dos últimos 10 min contra os 20 min anteriores | P04 | Executado |
| 26 | `src/IAApi.Engine/Detection/Core/DetectorMath.cs` | `DetectorMath.NormalizarPorLimite` (e `Median`, `Mad`) | Converte o score bruto em nota 0–100 | P07 | Executado |
| 27 | `Services/AnomalyDetection/EnsembleDetectionService.cs` | `EnsembleDetectionService.Detectar` (agregação) | Média ponderada das notas aplicáveis; **anomalia se ≥ 60** | **P07** | Executado |
| 28 | `Services/AnomalyDetection/Runtime/RuntimeFunctionalDecisionContextProvider.cs` | `Evaluate` | Contexto funcional para a decisão runtime | — | Executado |
| 29 | `src/IAApi.Application/Runtime/Decision/PredictionRuntimeDecisionService.cs` → `RuntimeAnomalyDecisionResolver.cs` | `Resolve` | Runtime desligado ⇒ decisão = ensemble (caso do PK2) | — | Executado desligado (ver 3.2) |
| 30 | `Services/AnomalyDetection/Interpretation/PredictionInterpretationService.cs` | `InterpretarAsync` | Texto de interpretação; não muda a decisão | — | Executado com interpretador falso (sem LLM) |
| 31 | `Services/AnomalyDetection/Orchestration/PredictionPostDecisionAnalysisService.cs` | `AnalisarAsync` | Tendência, risco, relacionadas; não muda a decisão | P10 | Executado |
| 32 | `Services/AnomalyDetection/Orchestration/PredictionFinalizationService.cs` → `Services/AnomaliaHistoricoService.cs` → `Presentation/PredictionResponseBuilder.cs` | `Finalizar` → `Registrar` → `Build` | Grava a decisão (com a hora do processamento) e monta a resposta | P18 | Executado em SQLite em memória |

### 3.1a Caminhos que o preset 6 exercitou além da tabela 3.1 [preset 6]

A tabela 3.1 lista **quais** métodos rodaram. Estas linhas detalham **caminhos de decisão** dentro desses métodos que não estavam descritos (ou não tinham efeito visível) no preset 1. Na coluna **Cenário**, "preset 1" sem mais detalhe é o replay com `--regime` (RES §4), e "preset 6" cobre as três configurações da RES6 §2. Todas as linhas foram **executadas no replay** e conferidas no código.

| Ordem | Arquivo | Classe.método | Caminho / o que faz | Cenário | Problemas |
|---|---|---|---|---|---|
| 8a | `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs` | `PrepararAsync` (ramo `regimeBaseline.Applied`) | Com o regime aplicado, **troca o histórico** do `DetectorInput` pelo histórico do regime (`historicoRecebido = regimeBaseline.History`), além de média, desvio e deadband. Com isso, a classificação do tipo, o MAD (mediana) e o deadband também passam a olhar só o regime | Ambos (preset 1 §4; preset 6 configs. 2 e 3) | P03, P09, P20 |
| 8b | idem | `PrepararAsync` (*fail-open* de regime) → `CriarBaselineEfetivo` → `CriarMotivoBaselineEfetivo` | **Troca para o perfil global** quando o regime não é aplicado: `origem_baseline = Global` e motivo "regime operacional não foi aplicado". Ocorreu com TAG43 = 3 (Parando): TAG28/30/32 às 13:41 (preset 1) e às 16:18 (preset 6); sem alarme nos dois casos | Ambos | P08 |
| 10a | `Services/AnomalyDetection/OperationalRegimeBaselineService.cs` → `Services/TagProfileStatisticsCalculator.cs` → `Services/HistoricoLimpezaService.cs` | `OperationalRegimeBaselineService.Resolve` → `TagProfileStatisticsCalculator.Calculate` → `RemoverOutliersPorIqr` | **IQR dentro do regime corta a operação minoritária.** No regime "B-02 ligada" (TAG43 = 2), cortou a B-02 sozinha em carga alta (TAG28 > 234, TAG30 > 174,5) → média 168 ± 20 → 56 minutos-tag de alarme com a B-02 sozinha depois do failover | Ambos; efeito visível só no preset 6 | P01, P03 |
| 17a | `src/IAApi.Engine/Detection/Behavior/TagBehaviorProfileResolver.cs` | `TagBehaviorProfileResolver.Resolve` (sobre o histórico do regime, ver 8a) | Com a bomba parada, o histórico do regime é quase todo 0 → tag classificada como `Discrete` com alta confiança. TAG28/32: 73 min no preset 1 (não relatado na RES) e 85 min no preset 6; TAG27/31: 20 min no preset 6, config. 3 (B-01 parada depois do failover) | Ambos | P09, P20 |
| 18a | `Services/AnomalyDetection/DetectorApplicabilityPolicy.cs` | `DetectorApplicabilityPolicy.Evaluate` (ramo `Discrete` → `Block`) | **Bloqueia os 6 detectores** de uma tag **monitorada** (`BlockedByApplicabilityPolicy`, nota 0). Antes, só a TAG44 (estado) tinha sido bloqueada. Efeito: elimina o alarme de reserva parada (P02) e, na config. 3, o da B-01 parada no failover | Ambos (TAG28/32); preset 6 (TAG27/31) | P02, P20 |
| 26a | `src/IAApi.Engine/Detection/Core/DetectorMath.cs` | `DetectorMath.NormalizarPorLimite` (`Math.Clamp(…, 0, 100)`) | **Saturação da nota:** Z, MAD, EWMA e Persistência ficam em 100 a partir de 15:39 nas três configurações; as notas ficam idênticas e o baseline deixa de importar. Também: o EWMA preso em 100 antes da falha, por causa do perfil misturado | Ambos (EWMA em 100 já no preset 1); mascaramento só no preset 6 | P07, P24 |
| 27a | `Services/AnomalyDetection/EnsembleDetectionService.cs` | `EnsembleDetectionService.Detectar` (agregação e `EhAnomalia = score ≥ 60`) | Nota final de 90–100 durante toda a falha: a decisão binária não muda quando a segunda falha (lubrificação) começa. Também foi aqui a **primeira detecção real** de uma falha (TAG29, 15:32, nota 68,5) | Preset 6 | P07, P24 |

**Linhas da tabela 3.1 com efeito novo no preset 6:**
- **1 e 4** (`PerfilTagService.TreinarPerfil`, `RemoverOutliersPorIqr`): o treino continha 12 min de falha rotulada e o IQR não os removeu; o mesmo método removeu 1.531 pontos no preset 1 e 306 no preset 6 (P01, P13).
- **21, 24 e 25** (`MadDetector`, `PersistenceDetector`, `LevelShiftDetector`): alarmes com a **B-01** parada depois do failover, o mesmo mecanismo da reserva B-02 parada (P02, P21).

### 3.2 Existe em produção, mas não foi exercitado no replay

| Parte | Arquivo : método | Por que ficou de fora |
|---|---|---|
| Ciclo do monitor | `Services/MonitoramentoTagsService.cs : ExecuteAsync` | O replay chama `PredictionService.Detectar` diretamente, minuto a minuto, sem ciclo de 60 s nem fila sequencial (P11) |
| Valor atual em lote | `Services/BridgeValorAtualService.cs : BuscarValoresAtuais`; `Services/CurrentValuePredictionService.cs : DetectarComValorAtual` | O valor atual vem do CSV; não há Bridge. **Por isso o replay não reproduz o P31** (entrada inválida pela leitura envelhecida); o rascunho do INV §4.3 reproduz |
| Histórico via Bridge | `Services/BridgeHistoricoService.cs : BuscarHistorico`, `BuscarHistoricoPorPeriodo` | Substituído pelo CSV; isolamento obrigatório e latência fora do escopo (P10) |
| Multivariado em *shadow* | `Services/AnomalyDetection/Multivariate/MultivariateShadowRuntimeCoordinator.cs : ProcessarLeituraAsync` | Passado como `null`; não muda a decisão no PK2 e soma latência |
| Fusão multivariada | `Services/AnomalyDetection/Multivariate/PredictionMultivariateDecisionService.cs : ResolveAsync` | Não registrado; a configuração restringe a PK7 (MAPA #12) |
| Isolation Forest | `Services/AnomalyDetection/IsolationForestDetector.cs : Detectar` | Não há modelo do PK2 em `Models/IsolationForest/` |
| Decisão runtime/canário ligada | `PredictionRuntimeDecisionService.Resolve` com `RuntimeDecision.Enabled` | O PK2 está fora do canário (`AllowedClientIds = ["PK7"]`), então o replay usa desligado; a lógica ligada não foi exercitada |
| Baseline comportamental | `Services/AnomalyDetection/BehaviorProfiles/Runtime/BehaviorRuntimeBaselineProvider.cs : ResolveAsync` | Não instanciado. **Inventário (INV §3): o PK2 não tem comportamento cadastrado**, então não se aplica |
| Banco real | `ia_config.db` (`TagsIaConfig`, `PerfisIa`, `TagsRegimesOperacionaisIa`, `TagContextoIa`) | O replay recria as tabelas em memória. **Inventariado em 13/11 (INV)**, numa cópia autorizada. A diferença relevante é o perfil de 30/10 (P32) |
| Gravação real e LiveMonitor | `AnomaliaHistoricoService.Registrar` no `ia_config.db`; `tools/IAApi.LiveMonitor` (polling GET) | O registro foi para SQLite em memória; o LiveMonitor não foi aberto |
| Interpretação por LLM | Implementação real de `IAnomalyInterpretationService` | Trocada por um interpretador fixo; não afeta a decisão |
| PERF_DIAG | `Services/Diagnostics/PerformanceDiagnostics.cs` | Desligado (variável `IAAPI_PERF_DIAGNOSTICS` ausente); só mede |

### 3.3 Ferramentas criadas

| Ferramenta | Arquivos | Para que serve |
|---|---|---|
| Exportação do histórico | `tools/Cp300Export/Export-Cp300Historico.ps1` | Baixa do Bridge (`GET /historico`) o histórico de um período, em pedaços com retentativa, e grava o CSV e os tempos por pedaço (`*.pedacos.csv`). **Acessa o Bridge e exige autorização.** |
| Replay offline | `tools/Cp300OfflineReplay/` (`Program.cs`, `Cp300Replay.cs`, `README.md`) | Roda o código de produção minuto a minuto sobre o CSV, com treino "como em produção", e gera uma linha por leitura e detector com conferência. Opção `--regime TAGx=TAGy` (recusa TAG60–81). Sem Bridge nem banco real. |
| Placar por evento | `tools/Cp300OfflineReplay/Cp300Placar.cs`, `placar_falhas_cp300.json`; subcomando `placar` (README §6) | Compara a saída do replay com o gabarito (slots TAG61–72 e TAG73) por evento: detecção, tag, minuto, atraso e severidade, com a regra do P21; alarmes falsos fora das falhas por tag e por hora. Só lê CSVs. Desde 16/11, o mapa tem `efeitosPorCanal` (canal 3 → tags da malha de pressão, P41). Testes: `IAApi.Tests/Cp300Replay/Cp300PlacarTests.cs` (dados sintéticos) |
| Testes do replay | `IAApi.Tests/Cp300Replay/Cp300OfflineReplayTests.cs` | 9 testes offline: degrau, reserva em 0, valor congelado, pesos, rótulo recusado e regime |
| Dados e saídas (não versionados) | `dados/cp300_10a11nov.csv` (com os estados TAG41–46), `dados/replay_*.csv` | Entrada e resultados dos replays do preset 1 e da madrugada (RES §2 e §4) |

---

## 4. Próximos passos (em ordem)

1. **Medir a latência real e garantir que o monitor fique de pé** (P10, P11, P26, P27 → BACKLOG **6**, **4**). Subiu do passo 4 em 13/11: a IA real avaliou cada tag só a cada ~10 min e parou sem aviso às 15:09. Sem isso, nenhum teste da IA real mede a detecção. Fazer numa sessão autorizada: PERF_DIAG, console gravado em arquivo com data e hora e vigia da última decisão (P27, "Como confirmar"). **[preset 3]** No mesmo teste, conferir o início do cenário pelos rótulos (P29), porque o "preset 3" de 12/11 não chegou a rodar. A idade da leitura já tem uma medida indireta: até 17,2 min (P18).
   - **[13/11, inventário] Antes de qualquer novo teste da IA real, tratar o P31.** Hoje ≈ 3/4 das decisões do PK2 são entrada inválida gravada como "normal". Sem isso, medir latência e cadência não basta para medir detecção. Precisa de proposta e aprovação. Nas análises, usar a nota × 20 (P30).
   - **[etapa 0, 16/11] Antes da demo e das próximas sessões de medição:**
     - corrigir o P42 (LiveMonitor sobrecarregando o Bridge e o banco → BACKLOG **34**) e o P43 (migration RC-5 ausente no banco local → BACKLOG **35**, também antes da RC-12);
     - antes da próxima sessão longa, o P44 (log → BACKLOG **36**);
     - depois, a 6.0b (reuso do histórico) e a 6.1 (`BACKLOG.md` 6.x).
2. **Barato e só de proteção:** bloquear rótulos como tag de regime em produção (P14 → BACKLOG **11**). ~~Junto, fazer o inventário do banco do PK2.~~ **Inventário feito em 13/11 (`Docs/INVENTARIO_PK2.md`):** só TAG22–33 ativas, nenhum regime, contexto, comportamento ou modelo IF. Perfil de 30/10 colapsado na B-02 (P32): retreinar só com aprovação e período limpo (passo 5).
3. **Ativar o regime recomendado no PK2, com aprovação:** TAG28/30/32 ← TAG43, TAG26 ← TAG45, TAG24 ← TAG46. Validado no replay: preset 1 245 → 41 alarmes; madrugada 1.282 → 660 (P02, P04, P05; RES §4). **[preset 6]** 325 → 223, e os alarmes falsos fora da falha caem de 175 para 27, mas surgem 56 minutos-tag com a B-02 sozinha depois do failover (P03; RES6 §2.3). A detecção da TAG29 não muda. Só configuração no banco, reversível.
4. **Regime combinado para as bombas** (P03, P08 → BACKLOG **12**). É o que falta para a B-01, que ainda gera 566 alarmes de madrugada. Testar no replay antes de produção, com a mesma tabela da RES §4.
5. **Treino por período e limpeza IQR** (P01, P09, P13 → BACKLOG **8**, **9**). Permitir treinar com um período limpo e proteger o IQR em séries quase constantes.
6. **Replay dos outros presets e gabarito por evento** (P12, P21 → BACKLOG **3**, partes B e C). Rodar os presets 3, 5 e 6 e medir acerto por evento, para não contar coincidências. **[preset 6] feito por análise manual** (RES6 §2). A medição automática por evento existe desde 12/11 (`placar`, P21) e reproduz os números dos presets 1 e 6. **[preset 3] O preset 3 não rodou (P29).** O período de 12/11 14:15 → 13/11 07:59 foi usado como teste de alarme falso com treino limpo (RES3). Falta **repetir o preset 3** conferindo o início pelos rótulos (P29, RES3 §3), com o log da IAApi em arquivo (passo 1), e rodar o preset 5. **[preset 3 de 13/11] Feito no replay** (RES3 §4): o preset 3 rodou; 3 de 7 falhas monitoradas detectadas em cada configuração; P12 confirmado; problema novo P41. Falta a **IA real** no mesmo cenário (exportar as decisões de 13/11 ou repetir com o log em arquivo) e o preset 5.
7. **Detecção que use relação entre tags** (P06): criar um item no BACKLOG para avaliar o multivariado do PK2 com treino adequado (o grupo atual tem ~2 h) e, para a pressão, a análise TAG24 − TAG46 (BACKLOG **13**).
8. **Detector de valor congelado ou falha de comunicação** (P12 → BACKLOG **10**), depois de medir os presets 3 e 4. **[preset 3 de 13/11]** O preset 3 já mostrou o caso (falha 20, nota 0); falta o preset 4. O mapa do placar para falhas de sensor em malha fechada já foi atualizado (P41, 16/11).
9. **Só depois:** pesos e limiar do ensemble (P07 → BACKLOG **7**).
