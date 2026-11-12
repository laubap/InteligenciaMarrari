# Problemas encontrados na detecção univariada (IAApi × simulador CP300)

**Data:** 12/11/2026. **Cliente:** PK2. **Branch:** `feat/intelligence-lab-e2e-validation`.
**Fontes:** `Docs/AUDITORIA_STATUS.md` (AUD), `Docs/MAPA_DETECCAO.md` (MAPA), `Docs/RESULTADO_PRESET1.md` (RES), `Docs/RESULTADO_PRESET6.md` (RES6) e `Docs/BACKLOG.md`. Este documento **resume** essas fontes; os números completos estão nas seções indicadas.

> **Atualização com o preset 6 (12/11/2026, RES6):** o univariado **detectou** o desbalanceamento na TAG29 (15:32, ≈4 min após o início, a 94 ‰). Confirmou P01, P02, P03, P04, P07, P13, P20 e P21, com novidades em P01, P02, P03 e P20. Ajustou o alcance do P06 e trouxe um problema novo (P24). Itens marcados com **[preset 6]**.

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
| P01 | A limpeza de outliers (IQR) do treino joga fora estados normais (reserva parada, pressão noturna). **É instável:** com a janela deslocada 2 h, removeu 1.531 pontos (preset 1) e só 306 (preset 6). **Não removeu os 12 min de falha** presentes no treino | Confirmado nos dados e no código | Treino do preset 1 (RES §1.5); **[preset 6]** RES6 §1.4 | Alarme falso + falha não detectada | **Alta** |
| P02 | Bomba reserva parada gera alarme falso contínuo (TAG28/30/32; **[preset 6]** também a B-01 depois do failover) | Confirmado no replay; solução validada no replay | Preset 1, madrugada (RES §2.3, §4); **[preset 6]** RES6 §2.3, §2.4 | Alarme falso | **Alta** |
| P03 | A bomba alarma conforme o estado da outra bomba e o set-point; nenhuma configuração atual resolve. **[preset 6]** Também na B-02 com o regime TAG43 | Confirmado no replay | Preset 1, madrugada (RES §4.2, §4.3); **[preset 6]** RES6 §2.3 | Alarme falso | **Alta** |
| P04 | Válvula de entrada fechada (TAG26) gera alarme falso; com regime, surgem alarmes de vazão alta | Confirmado no replay (de novo no preset 6); solução parcial no replay | Preset 1, madrugada (RES §2.3, §4); **[preset 6]** RES6 §2.4 | Alarme falso | Média |
| P05 | Pressão noturna (TAG24) alarma em 300 de 300 minutos de madrugada | Confirmado no replay; solução validada no replay | Madrugada (RES §4.3) | Alarme falso | **Alta** |
| P06 | Obstrução do recalque (preset 1) não detectada: a falha não se destaca do normal. **[preset 6]** Quando a falha sai da faixa normal (vibração), o univariado detecta | Confirmado no replay | Preset 1 (RES §2.1, §2.2, §4.2); **[preset 6]** RES6 §2.1 | Falha não detectada | **Alta** (solução longa) |
| P07 | A média ponderada dilui reações isoladas: sem IF, Z+MAD+EWMA saturados dão exatamente 60 | Confirmado no código e no replay | Preset 1 (RES §2.2; MAPA §4.1); **[preset 6]** atraso de ~2 min na TAG29 (RES6 §2.2) | Falha não detectada / atraso | Média |
| P08 | O baseline por regime volta ao perfil global (ruim) em transições, set-point novo ou poucas amostras | Confirmado no replay | Madrugada, preset 1 (RES §4.2, §4.3) | Alarme falso | Média |
| P09 | Regime quase constante (bomba parada) fica com desvio 0; a proteção existe só por acaso | Confirmado no código; efeito é hipótese | Estimativa (RES §3.6) | Risco de alarme falso | Média |
| P10 | O Bridge fica ~8× mais lento com a IAApi e o simulador rodando, e a IAApi consulta 24 h de histórico por tag a cada ciclo | Lentidão do Bridge confirmada em log; efeito na detecção é hipótese | Exportação da TAG73 (`dados/preset1_parte2.csv.pedacos.csv`) | Atraso | **Alta** (medir) |
| P11 | O ciclo é sequencial: 60 s + soma do tempo de todas as tags | Confirmado no código; não medido | — (MAPA #1, §3) | Atraso | Média |
| P12 | Não há detector de valor congelado nem de falha de comunicação | Confirmado no código | Presets 3/4 (AUD §5c) | Falha não detectada | **Alta** |
| P13 | O treino do perfil usa sempre as últimas 24 h, sem escolher período, e só roda sob demanda | Confirmado no código; **[preset 6]** contaminação confirmada nos dados | — (AUD §5d, §5e-6); **[preset 6]** RES6 §1.2 | Risco (treino contaminado) | **Alta** |
| P14 | O código aceita um rótulo (TAG60–TAG81) como tag de regime | Confirmado no código | — (AUD §5a) | Risco (vazamento do gabarito) | **Alta** (barato) |
| P15 | `IaAtiva` é `true` por padrão ao cadastrar uma tag | Confirmado no código | — (AUD §5a) | Risco | Baixa |
| P16 | Os pesos do `appsettings.json` diferem dos padrões do código | Confirmado no código | — (AUD §3, §5e-4) | Risco (ferramentas divergentes) | Baixa |
| P17 | `MinHistoryPoints` (30) não tem efeito em produção | Confirmado no código | — (AUD §3) | Risco (configuração enganosa) | Baixa |
| P18 | O registro grava a hora do processamento, não a hora da leitura | Confirmado no código | — (MAPA #14) | Atraso invisível | Média |
| P19 | Entrada inválida (leitura mais antiga que o histórico) vira "sem anomalia" em silêncio | Confirmado no código; não ocorreu no preset 1 | — (MAPA #7; RES §2.2) | Risco | Baixa |
| P20 | Tag com poucos valores distintos pode ser classificada como "discreta" e ter todos os detectores bloqueados | Confirmado no código; ocorreu nos replays com regime do preset 1 (73 min, não relatado na RES) e **[preset 6]** (TAG28/32, B-02 parada; TAG27/31 na config. 3) | TAG44 (RES §2.1); **[preset 6]** RES6 §2.2 | Falha não detectada (hipótese) | Baixa |
| P21 | Alarmes que caem por acaso no horário da falha parecem acertos | Confirmado no replay (de novo no preset 6, inclusive no failover) | Preset 1 (RES §2.1, §4.2); **[preset 6]** RES6 §2.1, §2.3 | Risco de avaliação errada | Média |
| P22 | TAG23 (sucção) teve 40 alarmes de madrugada; a causa não foi investigada | Confirmado no replay | Madrugada (RES §4.3) | Alarme falso | Baixa |
| P23 | Código em execução fora de commit | **Parcialmente resolvido**: runtime e ferramentas commitados; faltam os 5 `.patch` da raiz | — (AUD §1; git em 12/11) | Risco | Baixa |
| **P24** | **[preset 6]** Falha encadeada mascarada: com a nota já saturada, uma segunda falha na mesma tag não muda nada | Confirmado no replay | Preset 6 (RES6 §2.1, §2.2) | Falha não detectada (agravamento invisível) | Média |

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
- **Solução testada:** TAG42 sozinha (piora de dia, melhora de noite). A estimativa com regime combinado B-01 × B-02 (RES §3.3, com *proxies*, sem set-point) reduziu os alarmes da B-01 a 0–1 min por tag. **Só o regime combinado (bomba × outra bomba × set-point) deve resolver** [hipótese]. Ele não foi testado no replay porque o código não suporta.
- **BACKLOG:** 12.

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
- **Solução candidata:** usar a **relação entre tags** (por exemplo, corrente e frequência contra pressão e vazão), ou seja, o multivariado. Hoje ele roda só em *shadow*, a fusão está restrita a PK7 e o grupo `ModelsML/Multivariate/PK2/CP300-B01-Piloto.json` tem ~2 h de treino (AUD §5a, §5d). **Não testado.**
- **BACKLOG:** não há item específico; sugerido criar (seção 4). Relacionados: 7 e 3 (parte B, gabarito por evento).

### P07. Diluição pela média ponderada

- **O que acontece.** A decisão é a média das notas dos detectores, ponderada pelos pesos. Para chegar a 60, detectores que somam ≥ 60 % do peso precisam reagir juntos. Sem Isolation Forest, que é o caso do PK2, Z + MAD + EWMA saturados dão **exatamente 60**. Uma reação forte de um ou dois detectores não basta.
- **Evidência:** na TAG27 às 13:30, EWMA = 100 e taxa de variação = 100, mas Z = 53, MAD = 69 e persistência = mudança de patamar = 0. Nota final: **57,7**. O máximo possível nesse minuto era 73,3 (RES §2.2). Baixar o limiar não resolveria: a 57, alarmaria antes em minutos normais.
- **Causa no código:** `Services/AnomalyDetection/EnsembleDetectionService.cs : EnsembleDetectionService.Detectar` (média ponderada; `EhAnomalia = score ≥ EnsembleThreshold`); pesos em `appsettings.json` → `AnomalyDetection:*Weight`. Com IF aplicável, a barra ficaria mais alta: o IF vale 50 no limiar (MAPA #9).
- **[preset 6]** Na TAG29, o EWMA ficou em 100 **antes** da falha (perfil misturado), sem informação. Com Persistência e Mudança de patamar em 0 no início da rampa, a nota ficou abaixo de 60 em 15:29–15:31 (45,9–53,2) e passou só às 15:32. No minuto seguinte deu **60,9**, raspando o limiar (RES6 §2.2). Atraso estimado: ~2 min.
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
- **BACKLOG:** 6 e 4.

### P11. Ciclo sequencial

- **O que acontece.** O monitor processa um cliente e uma tag por vez e só depois espera 1 minuto. O ciclo real é de 60 s mais a soma do tempo de todas as tags, e a última tag espera todas as anteriores. O multivariado em *shadow* roda antes, no mesmo fluxo.
- **Evidência:** confirmado no código (MAPA #1, #4b, §3, pontos L1–L7). Não medido.
- **Causa no código:** `Services/MonitoramentoTagsService.cs : ExecuteAsync` (`Task.Delay(1 min)`); `Services/AnomalyDetection/Multivariate/MultivariateShadowRuntimeCoordinator.cs : ProcessarLeituraAsync`.
- **BACKLOG:** 6.

### P12. Sem detector de valor congelado ou de falha de comunicação

- **O que acontece.** Se um sensor trava num valor normal ou a comunicação para, nenhum detector reage. Todos procuram valores **diferentes** do normal, e um valor parado num ponto normal não é diferente.
- **Evidência:** confirmado no código (AUD §5c, §5e-2): falhas 20, 30, 32 e 33 do CP300 não detectáveis por construção. Ainda não testado em replay (preset 3/4).
- **Causa no código:** o conjunto de detectores em `Services/AnomalyDetection/DependencyInjection/LegacyAnomalyDetectionServiceCollectionExtensions.cs` não tem esse tipo de detector.
- **Solução candidata:** detector de "sem variação além do normal" e/ou uso do heartbeat (TAG40).
- **BACKLOG:** 10.

### P13. Treino só das últimas 24 h, sem escolha de período

- **O que acontece.** O perfil é treinado com as 24 h anteriores ao momento em que alguém chama o treino. Não dá para escolher um período limpo (por exemplo, o preset 5 sem falhas), e nada verifica se houve falha nesse período. O treino só acontece quando alguém chama a API.
- **Evidência:** confirmado no código (AUD §5d, §5e-6). Mudar `Bridge:HorasHistorico` altera o treino **e** a detecção, que usam a mesma chave.
- **[preset 6] Contaminação confirmada nos dados** (RES6 §1.2, §1.4): as 24 h antes do preset 6 incluem os 12 min da obstrução do preset 1 (TAG73 = 1, 13:28–13:39) e o reset do cenário. O IQR não removeu nenhum desses pontos em nenhuma tag (P01). O efeito nos números do perfil foi desprezível (média ±1,4), porque a obstrução ficou dentro da faixa normal; com uma falha forte, não seria.
- **Causa no código:** `Services/PerfilTagService.cs : TreinarPerfil` → `IHistoricoService.BuscarHistorico`; `Controllers/PerfilTagController.cs` (`POST /api/tags/treinar-perfil`, só `ClienteId` e `TagName`).
- **BACKLOG:** 8 (e 3, parte C, no replay).

### P14. O código aceita rótulo (TAG60–TAG81) como tag de regime

- **O que acontece.** Se alguém cadastrar uma tag de gabarito (por exemplo, TAG73, "falha ativa") como tag de regime, a IA passa a escolher o baseline sabendo a resposta. O teste fica inválido.
- **Evidência:** confirmado no código (AUD §5a). O replay já recusa isso (`--regime`, RES §4.1); a produção não. Não sabemos se o banco do PK2 tem algo assim (BACKLOG 2).
- **Causa no código:** `Services/AnomalyDetection/OperationalRegimeBaselineProvider.cs : Resolve`, que aceita qualquer `TagRegime`.
- **BACKLOG:** 11. A regra precisa ser por cliente: TAG60–81 só são rótulos no simulador.

### P15. `IaAtiva = true` por padrão

- **O que acontece.** Cadastrar uma tag sem informar `IaAtiva` cria a tag **ativa**. Um rótulo cadastrado por engano entraria no monitoramento e poluiria as contagens e o LiveMonitor. Não afeta a decisão de outras tags.
- **Evidência:** confirmado no código (AUD §5a): `Models/TagIaConfig.cs`; `Controllers/TagConfigController.cs : ConfigurarTag`.
- **BACKLOG:** 2 (inventário). Não há item de correção.

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
- **BACKLOG:** 5.

### P19. Entrada inválida vira "sem anomalia" em silêncio

- **O que acontece.** Se a leitura atual tem hora anterior ao último ponto do histórico (problema de fuso ou relógio), ou se o histórico fica vazio, a IA responde "sem anomalia" sem avisar fora do log.
- **Evidência:** confirmado no código (MAPA #7): `Services/AnomalyDetection/DetectionInputValidator.cs : Validate`. **Não ocorreu no preset 1** (`entrada_invalida = 0` em 100 %; RES §2.2). Em produção, com o Bridge real, não foi verificado.
- **BACKLOG:** 4 e 6.

### P20. Bloqueio por tipo "discreto"

- **O que acontece.** Se a série de uma tag tem poucos valores distintos (por exemplo, a reserva parada nas 24 h inteiras), a IA a classifica como "discreta" e desliga todos os detectores.
- **Evidência:**
  - Confirmado no código (AUD §5b; MAPA #8): `src/IAApi.Engine/Detection/Behavior/TagBehaviorProfileResolver.cs : Resolve` e `Services/AnomalyDetection/DetectorApplicabilityPolicy.cs : Evaluate`.
  - **No preset 1, as tags monitoradas foram classificadas como `Unknown` e nada foi bloqueado** (RES §2.2). Só a TAG44 (estado, não monitorada) foi bloqueada.
  - O caso da reserva que ficou parada 24 h segue como hipótese (AUD §5b).
  - **[preset 6] Ocorreu no caminho do regime** (RES6 §2.2; seção 3.1a, linhas 17a/18a): com TAG43 como regime, o histórico do estado "B-02 parada" é quase todo 0, e TAG28/32 foram classificadas como `Discrete` em 85 min, com todos os detectores bloqueados. Revendo os CSVs, isso **já tinha ocorrido no replay com regime do preset 1** (73 min), sem ter sido relatado na RES. Aqui isso evitou alarmes falsos. O risco é uma falha que apareça com a bomba parada (por exemplo, sensor travado) passar sem análise [hipótese].
- **BACKLOG:** 9.

### P21. Coincidências que parecem acertos

- **O que acontece.** Alarmes causados por outra coisa (reserva parada, válvula fechada, B-01 sozinha) às vezes caem dentro do horário da falha. Uma comparação automática minuto a minuto com o gabarito os contaria como detecção.
- **Evidência:** confirmado no replay. No preset 1 sem regime, 31 alarmes dentro da falha, nenhum causado por ela (RES §2.1). Com TAG42, 27 alarmes na B-01 dentro da falha, que começaram 26 min antes (RES §4.2).
- **[preset 6]** (RES6 §2.1, §2.3): dentro de 15:28–17:17, sem regime, **122** alarmes sem relação com a falha (reserva parada, válvula fechada, carga alta). Mais **64 no failover** (110 com regime): B-01 parada e B-02 sozinha. Esses alarmes são efeito da troca de bomba, mas o mesmo sinal aparece em operação normal, então **não contam como detecção**. A detecção real foi só a TAG29 (86 min).
- **Solução:** a parte B do replay deve medir por **evento** e comparar com o comportamento antes e depois da falha.
- **BACKLOG:** 3 (parte B).

### P22. TAG23 (sucção) com alarmes de madrugada

- **O que acontece:** 40 alarmes noturnos (00:01–05:00) e 2 fora da noite, iguais com e sem regime (RES §4.3).
- **Causa:** não investigada. Pode ser outro regime noturno (vazão e sucção dependem da demanda) [hipótese].
- **BACKLOG:** não há item.

### P23. Código em execução fora de commit (parcialmente resolvido)

- **O que acontecia.** Na auditoria (AUD §1), parte do que roda não estava em nenhum commit: coleta em lote, PERF_DIAG, reuso de histórico e LiveMonitor (51 entradas). Os resultados valiam só para a árvore local.
- **Situação em 12/11** (conferido no git):
  - **Resolvido:** o runtime e as ferramentas foram commitados (`c5f575d`, `e3356b7`, `15cbeb9`, `930ec7c` e `15f1ca9`). Isso inclui `Services/Diagnostics/`, `Services/Monitoring/`, os arquivos `PredictionHistoryReuse*`, `tools/IAApi.LiveMonitor/`, `tools/Cp300Export/` e `tools/Cp300OfflineReplay/`.
  - **`.gitignore` corrigido:** `tools/` passa a ser versionado e `dados/` ignorado (dados industriais exportados não entram no repositório).
  - **Remoção dos 3 `Docs/B3.23_*`:** já entrou no commit `15f1ca9`. Os arquivos não existem mais na pasta nem no índice.
  - **Pendente:** os 5 arquivos `.patch` na raiz, que não estão rastreados: `IAApi_LiveMonitor_clareza_cenario1`, `IAApi_LiveMonitor_sessoes_de_teste`, `IAApi_monitoramento_bridge_em_lote`, `IAApi_observabilidade_temporal` e `IAApi_tempo_por_tag`. Falta decidir se já foram aplicados (e podem ser descartados) ou se devem ser guardados. Não verificamos o conteúdo deles.
- **BACKLOG:** não há item.

### P24. Falha encadeada mascarada pela saturação [preset 6]

- **O que acontece.** Quando uma tag já está em alarme com nota próxima de 100, uma segunda falha que afete a mesma tag não muda nem a decisão nem a nota. A IA não indica que a situação piorou.
- **Evidência** (confirmado no replay, RES6 §2.1, §2.2):
  - A perda de lubrificação começou às ≈16:08 e chegou a 1000 ‰ às 16:48.
  - A TAG29 já estava em alarme desde 15:32 (desbalanceamento), com notas de 90–100, e subiu de ≈700 para 1106 sem efeito visível.
  - O efeito principal da lubrificação está em TAG36/37 (temperatura de mancal), que **não são monitoradas** (CENARIOS §2).
- **Causa:** as notas são limitadas a 100 (`DetectorMath.NormalizarPorLimite`) e a decisão é binária (`EnsembleDetectionService.Detectar`, nota ≥ 60). O limite em 100 está confirmado no código (`Math.Clamp(…, 0, 100)` em `src/IAApi.Engine/Detection/Core/DetectorMath.cs`). Que nenhuma outra parte indique agravamento é hipótese: o pós-decisão não foi verificado para isso.
- **Solução candidata** (não testada): monitorar TAG36/37 e/ou usar a tendência ou severidade do pós-decisão (`PredictionPostDecisionAnalysisService`) para indicar agravamento, sem alterar a decisão de anomalia.
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
| Valor atual em lote | `Services/BridgeValorAtualService.cs : BuscarValoresAtuais`; `Services/CurrentValuePredictionService.cs : DetectarComValorAtual` | O valor atual vem do CSV; não há Bridge |
| Histórico via Bridge | `Services/BridgeHistoricoService.cs : BuscarHistorico`, `BuscarHistoricoPorPeriodo` | Substituído pelo CSV; isolamento obrigatório e latência fora do escopo (P10) |
| Multivariado em *shadow* | `Services/AnomalyDetection/Multivariate/MultivariateShadowRuntimeCoordinator.cs : ProcessarLeituraAsync` | Passado como `null`; não muda a decisão no PK2 e soma latência |
| Fusão multivariada | `Services/AnomalyDetection/Multivariate/PredictionMultivariateDecisionService.cs : ResolveAsync` | Não registrado; a configuração restringe a PK7 (MAPA #12) |
| Isolation Forest | `Services/AnomalyDetection/IsolationForestDetector.cs : Detectar` | Não há modelo do PK2 em `Models/IsolationForest/` |
| Decisão runtime/canário ligada | `PredictionRuntimeDecisionService.Resolve` com `RuntimeDecision.Enabled` | O PK2 está fora do canário (`AllowedClientIds = ["PK7"]`), então o replay usa desligado; a lógica ligada não foi exercitada |
| Baseline comportamental | `Services/AnomalyDetection/BehaviorProfiles/Runtime/BehaviorRuntimeBaselineProvider.cs : ResolveAsync` | Não instanciado; depende de snapshots no banco do PK2, que não foi inventariado (BACKLOG 2) |
| Banco real | `ia_config.db` (`TagsIaConfig`, `PerfisIa`, `TagsRegimesOperacionaisIa`, `TagContextoIa`) | Acesso proibido sem autorização; o replay recria as tabelas em memória |
| Gravação real e LiveMonitor | `AnomaliaHistoricoService.Registrar` no `ia_config.db`; `tools/IAApi.LiveMonitor` (polling GET) | O registro foi para SQLite em memória; o LiveMonitor não foi aberto |
| Interpretação por LLM | Implementação real de `IAnomalyInterpretationService` | Trocada por um interpretador fixo; não afeta a decisão |
| PERF_DIAG | `Services/Diagnostics/PerformanceDiagnostics.cs` | Desligado (variável `IAAPI_PERF_DIAGNOSTICS` ausente); só mede |

### 3.3 Ferramentas criadas

| Ferramenta | Arquivos | Para que serve |
|---|---|---|
| Exportação do histórico | `tools/Cp300Export/Export-Cp300Historico.ps1` | Baixa do Bridge (`GET /historico`) o histórico de um período, em pedaços com retentativa, e grava o CSV e os tempos por pedaço (`*.pedacos.csv`). **Acessa o Bridge e exige autorização.** |
| Replay offline | `tools/Cp300OfflineReplay/` (`Program.cs`, `Cp300Replay.cs`, `README.md`) | Roda o código de produção minuto a minuto sobre o CSV, com treino "como em produção", e gera uma linha por leitura e detector com conferência. Opção `--regime TAGx=TAGy` (recusa TAG60–81). Sem Bridge nem banco real. |
| Testes do replay | `IAApi.Tests/Cp300Replay/Cp300OfflineReplayTests.cs` | 9 testes offline: degrau, reserva em 0, valor congelado, pesos, rótulo recusado e regime |
| Dados e saídas (não versionados) | `dados/cp300_10a11nov.csv` (com os estados TAG41–46), `dados/replay_*.csv` | Entrada e resultados dos replays do preset 1 e da madrugada (RES §2 e §4) |

---

## 4. Próximos passos (em ordem)

1. **Barato e só de proteção:** bloquear rótulos como tag de regime em produção (P14 → BACKLOG **11**). Junto, fazer o inventário do banco do PK2 (BACKLOG **2**) para confirmar que não há rótulo cadastrado, ver quais tags estão ativas (P15) e se já existe algum regime.
2. **Ativar o regime recomendado no PK2, com aprovação:** TAG28/30/32 ← TAG43, TAG26 ← TAG45, TAG24 ← TAG46. Validado no replay: preset 1 245 → 41 alarmes; madrugada 1.282 → 660 (P02, P04, P05; RES §4). **[preset 6]** 325 → 223, e os alarmes falsos fora da falha caem de 175 para 27, mas surgem 56 minutos-tag com a B-02 sozinha depois do failover (P03; RES6 §2.3). A detecção da TAG29 não muda. Só configuração no banco, reversível.
3. **Regime combinado para as bombas** (P03, P08 → BACKLOG **12**). É o que falta para a B-01, que ainda gera 566 alarmes de madrugada. Testar no replay antes de produção, com a mesma tabela da RES §4.
4. **Medir a latência real** com o PERF_DIAG numa sessão autorizada (P10, P11 → BACKLOG **6**, **4**).
5. **Treino por período e limpeza IQR** (P01, P09, P13 → BACKLOG **8**, **9**). Permitir treinar com um período limpo e proteger o IQR em séries quase constantes.
6. **Replay dos outros presets e gabarito por evento** (P12, P21 → BACKLOG **3**, partes B e C). Rodar os presets 3, 5 e 6 e medir acerto por evento, para não contar coincidências. **[preset 6] feito por análise manual** (RES6 §2); falta a medição automática por evento.
7. **Detecção que use relação entre tags** (P06): criar um item no BACKLOG para avaliar o multivariado do PK2 com treino adequado (o grupo atual tem ~2 h) e, para a pressão, a análise TAG24 − TAG46 (BACKLOG **13**).
8. **Detector de valor congelado ou falha de comunicação** (P12 → BACKLOG **10**), depois de medir os presets 3 e 4.
9. **Só depois:** pesos e limiar do ensemble (P07 → BACKLOG **7**).
