# Resultado — Preset 6 (degradação encadeada, 11/11/2026)

## 1. Dados

**Fonte:** `dados/cp300_10a11nov.csv` (cliente PK2, 27 tags, de 10/11 12:30 a 11/11 17:39). Tem TAG22–33, TAG41–46, TAG61–68 e TAG73. **Não tem** TAG69–72 (slot C) nem TAG74–78 (preset, passo, estado, tempo de cenário): o fim formal do cenário não pode ser confirmado.
**Janela de treino:** de 10/11 15:06 (inclusive) a 11/11 15:06 (exclusive) = 1.440 min, conforme o pedido (24 h antes de 15:06). *Não verifiquei no código como a janela é calculada.*
**Limpeza:** mesmo método da RES1 §1.5 (reprodução em Python de `HistoricoLimpezaService.RemoverOutliersPorIqr`; média e desvio populacionais). O script reproduz **exatamente** a tabela 1.5 do preset 1 quando aplicado à janela dele.
**Nível de evidência:** tudo é **confirmado nos dados do CSV**, exceto o que está marcado como *inferência* ou *hipótese*. Nada aqui diz se a IA detectou a falha.

### 1.1 Falhas do preset 6 (slots A e B)

| Item | Slot A | Slot B |
|---|---|---|
| Código | **10, desbalanceamento** (TAG61) | **09, perda de lubrificação** (TAG65) |
| Alvo | **1 → B-01** (TAG62) | **1 → B-01** (TAG66) |
| Fase 2, rampa | **15:28 → 16:07**; severidade 4 → 881 ‰, linear, +22,5 ‰/min | **16:08 → 16:47**; severidade 0 → 959 ‰, acelerada (≈ 1000·(t/40)²) |
| Fase 3, plena | **16:08 → 17:17** (900 ‰) | **16:48 → 17:17** (1000 ‰) |
| Fase 4, recuperação | não aparece | não aparece |
| Fim (último minuto ≠ 0) | 17:17; às **17:18** tudo volta a 0 | 17:17; idem |
| Severidade máxima (TAG63 / TAG67) | 900 ‰ (alvo nominal 900 ‰) | 1000 ‰ (alvo nominal 1000 ‰) |

| Verdade de campo | Valor |
|---|---|
| TAG73 = 1 | **15:28 → 17:17** (110 min, sem buraco) |
| Minutos faltando no trecho 15:06–17:39 | 0 |

*Inferência pela inclinação e pela tabela do preset (CENARIOS §4):*
- Slot A tinha 4 ‰ às 15:28 com 22,5 ‰/min → começou em **≈15:27:49** (minuto 20) → cenário iniciado em **≈15:07:49**. A TAG22 salta de 578 para 601 (reset para nível 60 %) entre 15:07 e 15:08, o que confirma o início ≈15:07–15:08.
- Slot B começou em ≈16:07:50 (minuto 60) e chegou a 1000 ‰ em ≈16:48 (minuto 100). O cancelamento (código 99, minuto 130) cai em ≈17:17:49, coerente com TAG73 = 0 a partir de 17:18. O cancelamento zera os slots direto, sem fase 4.
- Fim previsto do cenário (minuto 150): ≈17:38. Sem TAG76 no arquivo, não dá para confirmar.

Reação da TAG29 (vibração B-01, monitorada), só como referência: antes da falha ≈150–210; **passa o máximo da janela de treino (239) às 15:32**, 4 min após o início, com 94 ‰; ≈430 às 15:47; ≈700 às 16:05; pico de **1106** às 16:44 (B-01 sozinha). Com a B-02 ligada junto, a TAG29 fica menor (≈500–950).

### 1.2 TAG73 na janela de treino

| Verificação | Resultado |
|---|---|
| Minutos com TAG73 = 1 | **12** (11/11 **13:28 → 13:39**) |
| O que é | Obstrução do preset 1: TAG61 = 02, TAG64 = 2 e 4, severidade máxima 777 ‰ |
| Slot B (TAG65–68) ≠ 0 | 0 |
| Pontos na janela | 1.434 de 1.440 (faltam 10/11 17:33, 23:08; 11/11 05:46, 07:30, 08:35, 09:04, os mesmos da RES1 §1.3) |

**Diferente do preset 1, o treino do preset 6 contém uma falha rotulada.** A janela também inclui o reset e o transitório de partida do preset 1 (TAG22 518 → 600 entre 12:55 e 12:56).

### 1.3 Bombas

| Item | Observado |
|---|---|
| Principal antes da falha (TAG44) | **B-01** (TAG44 = 1 de 11/11 12:48 até 16:51) |
| Bomba atingida | B-01 (alvo 1 nos dois slots, igual à principal) |
| Partidas da B-02 como auxiliar durante a falha (TAG43 = 2) | 16:10 → 16:17 (TAG43 = 3 às 16:18, parando); 16:24 → 16:28; **16:45 em diante** |
| **Failover** | **16:52**: TAG44 1 → 2; TAG42 2 → 0 (B-01 desliga, TAG27/TAG31 → 0). Acontece ≈4 min depois de o slot B chegar a 1000 ‰ |
| Quando a reserva ligou | A B-02 **já estava ligada como auxiliar desde 16:45**; no failover ela só assume como principal, sem nova partida |
| Depois do failover | **17:12**: a B-01 volta a ligar (TAG42 = 2) com as falhas ainda ativas (TAG29 = 925); às 17:18, com o cancelamento, a TAG29 cai para 139. A B-01 segue ligada até o fim do arquivo |

*Não determinado:* se as partidas da B-02 em 16:10 e 16:24 foram causadas pela falha ou pela demanda (no preset 1 a B-02 também ligava sem falha, RES1 §1.4). A volta da B-01 às 17:12 (minuto ≈124, antes do cancelamento) também não tem causa identificada.

Na janela de treino, a principal alternou 6 vezes: **B-02 por 819 min e B-01 por 615 min**. Estados (B-01, B-02): ambas ligadas 520 min, só B-01 466 min, só B-02 445 min, transições 3 min.

### 1.4 Limpeza de outliers (IQR) na janela de treino

| Tag | Pontos | Removidos | Removidos com TAG73 = 1 | TAG73 = 1 que ficaram | Removidos normais (TAG73 = 0) | Média / desvio sem limpeza | Média / desvio com limpeza | Preset 1: removidos |
|---|---|---|---|---|---|---|---|---|
| TAG22 | 1434 | 0 | 0 | 12 | 0 | 599,7 / 109,8 | 599,7 / 109,8 | 0 |
| TAG23 | 1434 | 0 | 0 | 12 | 0 | 1349,0 / 53,9 | 1349,0 / 53,9 | 0 |
| **TAG24** | 1434 | **306** | 0 | 12 | **306** | 434,9 / 28,7 | **449,5 / 2,6** | 305 |
| TAG25 | 1434 | 0 | 0 | 12 | 0 | 558,7 / 208,0 | 558,7 / 208,0 | 0 |
| TAG26 | 1434 | 0 | 0 | 12 | 0 | 558,8 / 399,8 | 558,8 / 399,8 | 0 |
| TAG27 | 1434 | 0 | 0 | 12 | 0 | 130,4 / 89,8 | 130,4 / 89,8 | 0 |
| **TAG28** | 1434 | **0** | 0 | 12 | 0 | 123,6 / 90,3 | 123,6 / 90,3 | **374** |
| TAG29 | 1434 | 0 | 0 | 12 | 0 | 114,7 / 60,2 | 114,7 / 60,2 | 0 |
| **TAG30** | 1434 | **0** | 0 | 12 | 0 | 114,6 / 62,4 | 114,6 / 62,4 | **499** |
| TAG31 | 1434 | 0 | 0 | 12 | 0 | 279,0 / 188,8 | 279,0 / 188,8 | 0 |
| **TAG32** | 1434 | **0** | 0 | 12 | 0 | 266,9 / 188,1 | 266,9 / 188,1 | **353** |
| TAG33 | 1434 | 0 | 0 | 12 | 0 | 99,9 / 38,5 | 99,9 / 38,5 | 0 |

O que foi removido:

| Tag | Trecho removido | Explicação provável |
|---|---|---|
| TAG24 | 300 pontos de 11/11 00:01 a 05:00 (372–388), mais 6 isolados (409–490) | **Set-point noturno**, igual ao preset 1 |
| Demais | Nada | Nas tags das duas bombas, Q1 ≈ 0–1 e o IQR fica tão largo que nada sai |

**O que mudou em relação ao preset 1:**

| Ponto | Preset 1 (treino até 12:55) | Preset 6 (treino até 15:06) |
|---|---|---|
| Falha rotulada no treino | 0 min | **12 min** (obstrução 13:28–13:39), **nenhum ponto removido** pelo IQR em nenhuma tag |
| Total removido | 1.531 | **306** (só TAG24) |
| Tags da B-02 (TAG28/30/32) | Reserva parada cortada; perfil "B-02 ligada" (TAG32: 398,7 ± 40,5) | **Nada cortado**; perfil mistura ligada e parada (TAG32: 266,9 ± 188,1) |
| Tags da B-01 (TAG27/29/31) | Perfil misturado | Igual: perfil misturado (TAG31: 279,0 ± 188,8) |
| Por quê | B-02 parada em 353 min (24,6 % < 25 %) → Q1 da TAG28 = 133 | A janela perde 10/11 12:55–15:05 e ganha 11/11 12:55–15:05, com a B-01 sozinha → B-02 parada em **466 min (32,5 %)** → Q1 = 1 |
| Efeito dos 12 pontos de falha no perfil | — | Desprezível: média muda ≤ 1,4 e desvio ≤ 0,5 nas tags das bombas (ex.: TAG27 130,4 / 89,8 com eles, 129,6 / 89,7 sem eles). Os valores da obstrução estavam dentro do normal (RES1 §1.4) |

### Conclusão em linguagem simples

- O preset 6 rodou de **≈15:08 a ≈17:38**. A falha real foi de **15:28 a 17:17** na **B-01**: desbalanceamento em rampa (15:28–16:07), depois pleno, somado à perda de lubrificação a partir de 16:08 (plena em 16:48). A B-01 desarmou às **16:52** e a B-02, que já estava ligada como auxiliar desde 16:45, assumiu.
- **O treino do preset 6 não é limpo:** contém os 12 minutos da obstrução do preset 1, e a limpeza IQR não tirou nenhum deles. O efeito nos números do perfil é desprezível, mas o treino viola a regra "TAG73 = 0 em todo o período" (CENARIOS §6; P13).
- **A limpeza mudou de comportamento só porque a janela andou 2 h:** no preset 1 cortava a B-02 parada (1.531 pontos); agora não corta nada nas bombas. As **duas bombas passam a ter perfil misturado**, com desvio grande. É a mesma regra (P01) dando resultados opostos conforme a proporção de tempo parado fique acima ou abaixo de 25 %.
- *Hipótese, não testada:* com perfil 114,7 ± 60,2, a TAG29 a 253 (15:32, primeiro valor acima do máximo do treino) fica a só ≈2,3 desvios da média; isso pode atrasar a detecção precoce, que é o objetivo deste preset. O replay deve confirmar.

*Scripts de análise (fora do repositório):* `p6_load.py`, `p6_timeline.py`, `p6_iqr.py` e `p6_extra.py`, no scratchpad da sessão. Nenhum código do projeto foi alterado.

## 2. Resultado da IA

**Fonte:** replay offline (`tools/Cp300OfflineReplay`) sobre `dados/cp300_10a11nov.csv`, treino até 11/11 15:06, replay de 15:06 a 17:39 (154 min × 13 tags: TAG22–TAG33 e TAG44; limiar 60).

| Configuração | Opções | Saída | Leituras / alarmes |
|---|---|---|---|
| Sem regime | — | `dados/replay_preset6_sem_regime.csv` | 2.002 / **325** |
| Regime recomendado | `--regime TAG28=TAG43,TAG30=TAG43,TAG32=TAG43,TAG26=TAG45,TAG24=TAG46` | `dados/replay_preset6_regime_recomendado.csv` | 2.002 / **223** |

**Nível de evidência:** confirmado no replay (código de produção sobre dados do simulador). Sem Isolation Forest, multivariado nem latência. **Não é validação em planta.**
**Faixas usadas** (seção 1): pré-falha 15:06–15:27; slot A em rampa 15:28–16:07; A + B em rampa 16:08–16:47; ambas plenas 16:48–16:51; após o failover 16:52–17:17 (B-01 parada 16:52–17:11, religada com falha 17:12–17:17); após o cancelamento 17:18–17:39. Válvula de entrada fechada (TAG45 = 0): 15:24–15:44 e 16:38–16:55.

### 2.1 Tabela por tag

Contagens = minutos com alarme. **Falha** = causado pela falha; **Failover** = B-01 parada ou B-02 sozinha depois do trip; **Sem relação** = dentro de 15:28–17:17, mas por outra causa (P21); **Fora** = pré-falha + após o cancelamento.

| Tag | O que mede | Sem regime: falha / failover / sem relação / fora | Regime recomendado | Detectou? | Observação |
|---|---|---|---|---|---|
| **TAG29** | Vibração B-01 (alvo) | **86** / 19 / 0 / 0 | igual (sem regime nas duas) | **Sim, desbalanceamento às 15:32** | Alarme contínuo 15:32–16:51 e 17:12–17:17; nenhum antes da falha nem depois do cancelamento |
| TAG27 | Corrente B-01 | 0 / 18 / 9 / 2 | igual | Não (não deve reagir à falha) | Failover: 16:54–17:11, B-01 parada. Sem relação: 16:06–16:09, 16:19, 16:23, 16:40, 16:43–44, B-01 sozinha em carga alta antes da partida da auxiliar (padrão do preset 1). Fora: 15:08–09, reset |
| TAG31 | Frequência B-01 | 0 / 17 / 1 / 0 | igual | Não (idem) | Failover: 16:55–17:11; 16:09 = carga alta |
| TAG28 | Corrente B-02 | 0 / 5 / 0 / **10** | 0 / **20** / 0 / **0** | — (B-02 não é alvo) | Fora: 15:06–15:15, B-02 parada. Com regime: isso some, mas alarma 16:52–17:11 com a B-02 sozinha |
| TAG30 | Vibração B-02 | 0 / 5 / **24** / **15** | 0 / **19** / 0 / **0** | — (não deve reagir: alvo é a B-01) | Sem regime: B-02 parada. Com regime: 16:53–17:11, B-02 sozinha |
| TAG32 | Frequência B-02 | 0 / 0 / **56** / **22** | 0 / **17** / 0 / **0** | — | Sem regime: 15:06–16:09, 16:22–23, 16:33–44, B-02 parada. Com regime: 16:55–17:11, B-02 sozinha |
| TAG26 | Vazão de entrada | 0 / 0 / 30 / 3 | 0 / 0 / 2 / 10 | — | Sem regime: válvula fechada. Com regime: vazão alta com a válvula aberta (15:10, 15:15–23, 15:57, 16:34) |
| TAG22 | Nível | 0 / 0 / 1 / 0 | igual | — | 15:44, nível caindo com a válvula fechada |
| TAG24 | Pressão de recalque | 0 / 0 / 0 / 1 | igual | — | 15:08, reset do cenário |
| TAG33 | Potência | 0 / 0 / 1 / 0 | igual | — | 16:09, carga alta antes da partida da auxiliar |
| TAG23, TAG25 | Sucção, vazão | 0 | 0 | — | Nota máxima ≤ 55,7 |
| TAG44 | Bomba principal | 0 | 0 | — | `Discrete`, todos os detectores bloqueados |
| **Total** | | **86 / 64 / 122 / 53 = 325** | **86 / 110 / 14 / 13 = 223** | | |

**Desbalanceamento (slot A), TAG29, sem regime (igual com regime):**

| Minuto | Valor | Severidade TAG63 | Nota | Z / MAD / EWMA / RoC / Persist. / Mud. patamar |
|---|---|---|---|---|
| 15:27 (antes) | 190 | 0 | 38,9 | 42 / 53 / 100 / 0 / 0 / 0 |
| 15:29 | 192 | 26 ‰ | 45,9 | 43 / 55 / 100 / 48 / 0 / 0 |
| 15:31 | 216 | 71 ‰ | 53,2 | 56 / 82 / 100 / 41 / 0 / 0 |
| **15:32** | **253** | **94 ‰** | **68,5** | 77 / 100 / 100 / 89 / 10 / 0 |
| 15:33 | 256 | 116 ‰ | 60,9 | 78 / 100 / 100 / 7 / 32 / 0 |

- **Primeiro alarme às 15:32**, ≈4 min depois do início da rampa (≈15:27:49), com a falha em **94 ‰ (≈10 % dos 900 ‰)**. Daí em diante, alarme em todos os minutos com a B-01 ligada e a falha ativa.
- **Conta como detecção, não coincidência:** nota máxima antes da falha de 53,3; 0 alarmes na TAG29 antes de 15:32 e depois de 17:17. O alarme começa no primeiro minuto em que a vibração passa do que o treino viu (máx. 239; seção 1.1).
- Às 15:33 a nota foi **60,9**, raspando o limiar.

**Perda de lubrificação (slot B, início ≈16:08):** **não há detecção separável.** A TAG29 já estava em alarme desde 15:32, com notas de 90–100 (Z, MAD, EWMA e Persistência saturados). O agravamento (TAG29 de ≈700 para 1106) não muda a decisão nem a nota de forma visível. O efeito principal da lubrificação (TAG36/37, temperatura de mancal) não é monitorado.

**Correntes e frequências (TAG27/28/31/32):** nenhum alarme causado pela falha. O desbalanceamento e a lubrificação não mexem nessas tags antes do trip: a TAG27 ficou em 200–250, como antes da falha.

### 2.2 O que impediu ou limitou a detecção

| Mecanismo | Efeito | Evidência |
|---|---|---|
| Tipo da tag | Não impediu | TAG27–33 = `Unknown`, todos os detectores aplicáveis. Com regime, TAG28/32 viram `Discrete` com a B-02 parada (85 min) e os detectores são bloqueados; aqui isso **evitou** alarmes falsos |
| Deadband | Não impediu | TAG29: 25, pequeno perto dos desvios da falha (+60 a +900) |
| Entrada inválida / erro | Não | 0 em 2.002 leituras |
| **Diluição + EWMA sem informação** | **Atrasou ~2 min** e deixou margem pequena | O EWMA fica em 100 **antes** da falha: o perfil (114,7 ± 60,2) mistura B-01 parada e ligada, e a B-01 sozinha roda a ≈165. Com Persistência e Mudança de patamar em 0, a nota ficou abaixo de 60 em 15:29–15:31 (26–71 ‰) |
| **Saturação** | **Mascarou a lubrificação** | Com a nota já em 90–100, uma segunda falha na mesma tag não aparece. A IA não indica que a falha piorou |
| Sinal que não se destaca | Não se aplica à TAG29 | Ao contrário do preset 1, o desvio (até +900) é muito maior que a variação normal |

### 2.3 Failover e reserva assumindo

| Evento | Alarmes | Classificação |
|---|---|---|
| B-01 desarma às 16:52 e fica parada até 17:11 | TAG27 16:54–17:11, TAG31 16:55–17:11, TAG29 16:52–17:11 (exceto 17:00): MAD = 100, Persistência = 100, Mudança de patamar alta | **Efeito do failover, não detecção específica.** É o mesmo mecanismo da "reserva parada" (P02), agora na B-01. Antes da falha, a TAG32 sem regime alarmou do mesmo jeito com a B-02 parada, sem falha nenhuma (nota 62,8: MAD, EWMA e Persistência = 100) |
| B-02 assume sozinha (16:52–17:11) | Sem regime: TAG28 5, TAG30 5. **Com regime: TAG28 20, TAG30 19, TAG32 17** (Z = 100, MAD = 100) | **Coincidência (P21).** O regime "B-02 ligada" (TAG43 = 2) tem média 168 ± 20 na TAG28 porque é dominado pela operação em paralelo; o IQR dentro do regime corta a B-02 sozinha em carga alta (TAG28 > 234, TAG30 > 174,5). Com a B-02 sozinha a 232–271, valores normais no treino (máx. 284), o regime alarma: é o **P03 espelhado na B-02** |
| B-01 religa com falha (17:12–17:17) | TAG29, notas 77–92 | Detecção da falha ainda ativa |

Uma comparação minuto a minuto com a TAG73 contaria esses 64 (sem regime) ou 110 (com regime) minutos como acertos. Pelo P21, **não** devem contar: o mesmo sinal aparece em operação normal.

### 2.4 Alarmes falsos fora das falhas e sem relação com elas

| Causa | Tags | Sem regime | Regime recomendado |
|---|---|---|---|
| Reserva B-02 parada (P02) | TAG28 / TAG30 / TAG32 | 10 / 39 / 78 = **127** | **0** (baseline de regime ou bloqueio `Discrete`) |
| Válvula de entrada fechada (P04) | TAG26, TAG22 | 33 | 1 (TAG22) |
| Vazão alta com a válvula aberta (P04) | TAG26 | 1 | 12 |
| B-01 sozinha em carga alta (P03) | TAG27 / TAG31 / TAG33 | 11 | 11 |
| Reset do cenário (transitório) | TAG24, TAG27 | 3 | 3 |
| Após o cancelamento (17:18–17:39) | todas | 0 | 0 |
| **Total** | | **175** | **27** |

Somando o failover (2.3), os alarmes que não são a falha caem de 239 para 137 com o regime: somem os de reserva parada e válvula fechada, aparecem os da B-02 sozinha.

### 2.5 Conferência

| Item | Resultado |
|---|---|
| `conferencia` | `ok` em **12.012 de 12.012 linhas** em cada configuração |
| `decisao_final` vs. `decisao_ensemble` | Iguais em 100 % |
| Erros / entradas inválidas | 0 / 0 |
| Perfis treinados | Iguais aos calculados na seção 1.4 (ex.: TAG24 449,5 ± 2,55 com 306 removidos; TAG29 114,7 ± 60,2) |
| Tags sem regime nas duas configurações | Idênticas linha a linha (0 diferenças) |
| Reprodutibilidade | `dados/replay_preset6.csv` (execução anterior, sem regime) é idêntico ao novo sem regime: 0 diferenças em 12.012 linhas |

**Não houve divergência.**

### 2.6 Complemento: regime da B-01 (TAG27/29/31 ← TAG42)

**Terceira configuração:** regime recomendado + `TAG27=TAG42,TAG29=TAG42,TAG31=TAG42`. Saída: `dados/replay_preset6_regime_b01.csv`. 2.002 leituras, **230 alarmes**, 0 erros, 0 divergências (`conferencia = ok` em todas as linhas; decisão final = ensemble em 100 %). As tags que não mudaram de configuração ficaram iguais às da configuração 2.

**Nota da TAG29, minuto a minuto** (* = alarme, nota ≥ 60). Configurações 1 e 2 são idênticas porque a TAG29 não tem regime em nenhuma delas. Baseline da configuração 3: regime "B-01 ligada", média ≈150 ± 19 (contra 114,7 ± 60,2 do perfil global).

| Minuto | TAG29 | TAG63 | 1. Sem regime | 2. Recomendado | 3. Recomendado + TAG42 | Detectores na config. 3 (Z / MAD / EWMA / RoC / Persist. / Mud. patamar) |
|---|---|---|---|---|---|---|
| 15:26 | 190 | 0 | 39,3 | 39,3 | 52,4 | 76 / 85 / 100 / 2 / 0 / 0 |
| 15:27 | 190 | 0 | 38,9 | 38,9 | 51,7 | 74 / 85 / 100 / 0 / 0 / 0 |
| 15:28 | 172 | 4 | 39,9 | 39,9 | 35,8 | 0 / 50 / 100 / 43 / 0 / 0 |
| 15:29 | 192 | 26 | 45,9 | 45,9 | 59,3 | 76 / 89 / 100 / 48 / 0 / 0 |
| **15:30** | 199 | **49** | 44,2 | 44,2 | **61,1*** | 88 / 100 / 100 / 17 / 10 / 0 |
| 15:31 | 216 | 71 | 53,2 | 53,2 | 69,8* | 100 / 100 / 100 / 41 / 32 / 0 |
| **15:32** | 253 | **94** | **68,5*** | **68,5*** | 79,2* | 100 / 100 / 100 / 89 / 55 / 0 |
| 15:33 | 256 | 116 | 60,9* | 60,9* | 71,3* | 100 / 100 / 100 / 7 / 78 / 0 |
| 15:34 | 249 | 139 | 64,4* | 64,4* | 75,6* | 100 / 100 / 100 / 17 / 100 / 0 |
| 15:35 | 268 | 161 | 73,4* | 73,4* | 79,4* | 100 / 100 / 100 / 46 / 100 / 0 |
| 15:36 | 274 | 184 | 81,3* | 81,3* | 83,7* | 100 / 100 / 100 / 14 / 100 / 63 |
| 15:37 | 287 | 206 | 89,6* | 89,6* | 90,5* | 100 / 100 / 100 / 31 / 100 / 97 |
| 15:38 | 283 | 229 | 86,2* | 86,2* | 87,6* | 100 / 100 / 100 / 10 / 100 / 97 |
| 15:39–16:10 | 333 → 836 | 251 → 900 | 82,8–100* | igual | **igual à config. 1** | Z, MAD, EWMA e Persistência em 100 nas três |

A partir de 15:39 as três configurações dão **exatamente a mesma nota**: os detectores saturam e o baseline deixa de importar.

**Quando cada uma detectaria:**

| Configuração | Primeiro alarme da TAG29 | TAG63 nesse minuto | Após o início da rampa (≈15:27:49) | Alarmes na TAG29 antes da falha (15:06–15:27) | Nota máxima antes da falha |
|---|---|---|---|---|---|
| 1. Sem regime | 15:32 | 94 ‰ | ≈4 min | 0 | 53,3 |
| 2. Recomendado | 15:32 | 94 ‰ | ≈4 min | 0 | 53,3 |
| 3. Recomendado + TAG42 | **15:30** | **49 ‰** | **≈2 min** | **5** (15:06, 15:08, 15:19, 15:21–22) | **69,0** |

**A detecção 2 min mais cedo não é confiável.** Na configuração 3, a TAG29 alarmou 5 vezes antes da falha, com nota até 69,0, maior que a nota da "detecção" às 15:30 (61,1) e às 15:31 (69,8). Pelo P21, um alarme às 15:30 não se distingue dos falsos de 15:19–15:22. O que muda é que, a partir de 15:30, o alarme fica **contínuo**: quem exigisse ≥ 3 minutos seguidos teria detectado às 15:32 na configuração 3 e às 15:34 nas configurações 1 e 2 [estimativa sobre as notas acima; essa regra não existe no código].

**Custo nas outras tags da B-01:**

| Tag | 2. Recomendado: falha / failover / sem relação / fora | 3. Recomendado + TAG42 | O que mudou |
|---|---|---|---|
| TAG29 | 86 / 19 / 0 / 0 | 88 / **0** / 0 / **5** | +2 na falha (15:30–15:31); some o alarme da B-01 parada no failover (`Discrete`, bloqueada); +5 falsos antes da falha |
| TAG27 | 0 / 18 / 9 / 2 | 0 / **0** / **29** / **11** | Failover zerado; alarmes sempre que a B-01 opera sozinha (15:06–10, 15:18–23, 15:59–16:09, 16:19–23, 16:33–38, 16:40–44): **P03**, como no preset 1 |
| TAG31 | 0 / 17 / 1 / 0 | 0 / **0** / **20** / **6** | Idem |
| **Total (13 tags)** | **86 / 110 / 14 / 13 = 223** | **88 / 56 / 53 / 33 = 230** | Falsos fora da falha e sem relação: 27 → **86**; failover: 110 → **56** (sobra só a B-02 sozinha) |

**Conclusão do complemento:** separar "B-01 ligada" adianta o primeiro alarme da TAG29 em 2 min (49 ‰ em vez de 94 ‰), mas esse ganho fica dentro do ruído criado pela própria configuração. Ela triplica os alarmes falsos (27 → 86) porque o regime "B-01 ligada" mistura sozinha e em paralelo (P03). O ganho real seria com um regime que separe B-01 sozinha de B-01 em paralelo [hipótese; o código aceita uma só tag de regime].

### 2.7 Limpeza IQR: instabilidade registrada

| Treino | Fim da janela | Pontos removidos | Minutos com TAG73 = 1 no treino | Desses, removidos |
|---|---|---|---|---|
| Preset 1 (RES1 §1.5) | 11/11 12:55 | **1.531** (TAG24, TAG28, TAG30, TAG32) | 0 | — |
| Preset 6 (§1.4) | 11/11 15:06 | **306** (só TAG24) | **12** (obstrução 13:28–13:39) | **0** em todas as tags |

Deslocar a janela em ~2 h mudou o resultado da limpeza em 5 vezes, e ela não tirou os minutos de falha que estavam no treino. Registrado no `Docs/PROBLEMAS_ENCONTRADOS.md` (P01 e P13).

### Resumo

1. A IA **detectou o desbalanceamento** na vibração da B-01 (TAG29) às 15:32, ≈4 min depois do início da rampa, com a falha em ≈10 % da intensidade final, e manteve o alarme enquanto a bomba rodou com falha.
2. Não houve alarme na TAG29 antes nem depois da falha: é detecção real, não coincidência.
3. A **perda de lubrificação não apareceu como algo novo**: a vibração já estava em alarme máximo, e a temperatura do mancal não é monitorada.
4. A detecção passou perto do limite (60,9 no segundo minuto) porque o perfil mistura bomba parada e ligada; uma rampa mais suave poderia atrasá-la.
5. Os alarmes no failover (B-01 parada, B-02 sozinha) são efeito da troca de bomba e aparecem também em operação normal: **não contam como acerto**.
6. Sem regime, a reserva parada gerou 127 alarmes falsos, **mesmo com a limpeza não tendo cortado os zeros desta vez**.
7. O regime recomendado eliminou esses 127 e reduziu os falsos fora da falha de 175 para 27.
8. Mas o regime criou alarmes quando a B-02 assumiu sozinha (56 minutos-tag): o regime "B-02 ligada" só conhece a operação em paralelo.
9. Correntes e frequências não reagiram à falha, como esperado para esse tipo de defeito.
10. Conferência sem divergência; replay offline, sem multivariado, Isolation Forest nem latência. Complemento (§2.6): com a B-01 também em regime, o alarme vem 2 min antes (15:30, 49 ‰), mas os falsos sobem de 27 para 86 e a TAG29 alarma antes da falha; o ganho não é confiável.

*Scripts de análise (fora do repositório):* `p6_rp.py`, `p6_det.py`, `p6_b02.py`, `p6_chk.py` e `p6_cmp.py`, no scratchpad da sessão. Nenhum código do projeto foi alterado.

## 3. Fluxo exercitado

Métodos de produção que participaram das decisões do preset 6 no replay, em ordem. Para o que cada um faz, os problemas relacionados e o que ficou de fora do replay, ver `Docs/PROBLEMAS_ENCONTRADOS.md`, **seção 3** (tabela 3.1 e caminhos novos em 3.1a).

**Treino**
- `Services/PerfilTagService.cs : PerfilTagService.TreinarPerfil`
- `Services/TagProfileStatisticsCalculator.cs : TagProfileStatisticsCalculator.Calculate`
- `Services/HistoricoLimpezaService.cs : HistoricoLimpezaService.RemoverOutliersPorIqr`

**Preparação e baseline**
- `Services/PredictionService.cs : PredictionService.Detectar`
- `Services/AnomalyDetection/Orchestration/PredictionContextLoader.cs : PredictionContextLoader.BuscarConfiguracaoAtiva / BuscarPerfil / CarregarContextoOperacional`
- `Services/AnomalyDetection/Orchestration/PredictionDetectionPreparationService.cs : PredictionDetectionPreparationService.PrepararAsync` (+ `CriarBaselineEfetivo`, `CriarMotivoBaselineEfetivo`)
- `Services/AnomalyDetection/OperationalRegimeBaselineProvider.cs : OperationalRegimeBaselineProvider.Resolve` (configs. 2 e 3)
- `Services/AnomalyDetection/OperationalRegimeBaselineService.cs : OperationalRegimeBaselineService.Resolve` (configs. 2 e 3)
- `Services/AnomalyDetection/OperationalRegimeHistoryResolver.cs : OperationalRegimeHistoryResolver.Resolve` (configs. 2 e 3)
- `src/IAApi.Engine/Detection/Core/DetectionHistorySanitizer.cs : DetectionHistorySanitizer.Sanitize`

**Decisão**
- `Services/AnomalyDetection/Orchestration/PredictionDecisionOrchestrationService.cs : PredictionDecisionOrchestrationService.ExecutarAsync`
- `Services/AnomalyDetection/EnsembleDetectionService.cs : EnsembleDetectionService.Detectar`
- `Services/AnomalyDetection/DetectionInputValidator.cs : DetectionInputValidator.Validate`
- `src/IAApi.Engine/Detection/Behavior/TagBehaviorProfileResolver.cs : TagBehaviorProfileResolver.Resolve`
- `Services/AnomalyDetection/DetectorApplicabilityPolicy.cs : DetectorApplicabilityPolicy.Evaluate`
- `src/IAApi.Engine/Detection/Core/BaselineProtectionProfile.cs : BaselineProtectionProfile.Build / ResolveEffectiveScale`
- `src/IAApi.Engine/Detection/StatisticalDetector.cs`, `MadDetector.cs`, `EwmaDetector.cs`, `RateOfChangeDetector.cs`, `PersistenceDetector.cs`, `LevelShiftDetector.cs` : `Detectar`
- `src/IAApi.Engine/Detection/Core/DetectorMath.cs : DetectorMath.NormalizarPorLimite`
- `src/IAApi.Application/Runtime/Decision/PredictionRuntimeDecisionService.cs : Resolve` (desligado: decisão = ensemble)

**Registro**
- `Services/AnomalyDetection/Orchestration/PredictionFinalizationService.cs : Finalizar` → `Services/AnomaliaHistoricoService.cs : Registrar` (SQLite em memória)

## Resumo para apresentação

1. No preset 6, a bomba B-01 sofreu desbalanceamento a partir de 15:28 e perda de lubrificação a partir de 16:08, até desarmar às 16:52.
2. A IA **percebeu o desbalanceamento cedo**: às 15:32, cerca de 4 minutos depois do início, com a falha em apenas 10 % da força final.
3. Foi um acerto de verdade: a vibração não alarmou antes da falha nem depois dela.
4. A perda de lubrificação **não apareceu como algo novo**, porque a vibração já estava no alarme máximo e a temperatura do mancal não é monitorada.
5. Os alarmes na troca de bomba não contam como acerto: aparecem também quando uma bomba fica parada em operação normal.
6. Sem configuração de regime, a IA deu 175 alarmes falsos fora da falha, a maioria pela bomba reserva parada; com o regime recomendado, caíram para 27.
7. O regime recomendado ainda gera alarmes quando a B-02 assume sozinha; separar só "B-01 ligada" piora os alarmes falsos.
8. A limpeza do treino é instável (1.531 pontos removidos no preset 1, 306 aqui) e não retirou a falha do preset 1 que estava no treino.
