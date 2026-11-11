# Cenários de teste CP300 — referência para validar a IAApi

Fonte: `SIM_ANOMALIAS_CP300.c`, `Tags_CP300_SIM_ANOMALIAS.csv` e `docs/SIMULADOR_ANOMALIAS.md`.
Tempos dos presets decodificados da tabela `SCN` no programa do CLP (estados 61–66 da `MAQUINA_12`).

## 1. Como saber o "esperado" sem anotar nada à mão

O simulador publica a **verdade de campo** em tags próprias, que **não são afetadas por falhas de comunicação**:

| Tag | Conteúdo | Uso |
|---|---|---|
| TAG73 | Anomalia ativa (0 = normal, 1 = algum slot em rampa/plena/recuperação) | Coluna "esperado" minuto a minuto |
| TAG61 / TAG62 / TAG63 / TAG64 | Slot A: código da falha, alvo, severidade ‰, fase (0 livre, 2 rampa, 3 plena, 4 recuperação) | Qual falha, em qual equipamento/sensor, com qual força |
| TAG65–TAG68 / TAG69–TAG72 | Slots B e C (mesmo layout) | Falhas sobrepostas |
| TAG74 / TAG75 / TAG76 | Preset em execução, passo (0–19), estado (0 parado, 1 rodando, 2 pausado, 3 concluído) | Saber em que parte do cenário estava |
| TAG77 / TAG78 | Tempo de cenário (horas / segundos na hora) | Alinhar com a tabela da seção 4 |
| TAG60 | Heartbeat dos rótulos (+1/s) | Confirma que a linha de rótulos é nova |
| TAG40 | Heartbeat da publicação (+~60 por minuto no histórico) | Detectar falhas de comunicação (30–33) |

**Regras:**
- As tags TAG60–TAG81 precisam estar **coletadas no PSi4** (histórico `EveryMinute`), mas **nunca** podem ser entrada da IA (`iaAtiva=false`). Se a IA usar rótulos como entrada, o teste perde validade.
- Com o histórico dessas tags, dá para comparar automaticamente, minuto a minuto, TAG73 com as anomalias da IAApi e calcular acertos, faltas, alarmes falsos e atraso de detecção.
- Para alinhar tempos exatos, também existe o log de eventos `BDE` no CLP (hora, minuto, segundo de cada início/fim de falha), descrito no guia do simulador.

## 2. Mapa das tags de medição

O PK2 tinha `iaAtiva=true` em **TAG22–TAG33** (último registro do handoff; confirmar). Tags fora disso não são analisadas pela IA hoje.

| Tag | Medida | Unidade | Monitorada no PK2? |
|---|---|---|---|
| TAG22 | LT-01 nível do reservatório | % ×10 | Sim |
| TAG23 | PT-01 pressão de sucção | mbar abs | Sim |
| TAG24 | PT-02 pressão de recalque | bar ×100 | Sim |
| TAG25 | FT-01 vazão de recalque | m³/h ×10 | Sim |
| TAG26 | FT-02 vazão de entrada | m³/h ×10 | Sim |
| TAG27 / TAG28 | IT-01 / IT-02 corrente motor B-01 / B-02 | A ×10 | Sim |
| TAG29 / TAG30 | VT-01 / VT-02 vibração B-01 / B-02 | mm/s ×100 | Sim |
| TAG31 / TAG32 | ST-01 / ST-02 frequência inversor B-01 / B-02 | Hz ×10 | Sim |
| TAG33 | JT-01 potência ativa da estação | kW ×10 | Sim |
| TAG34 / TAG35 | TT-01 / TT-02 temperatura enrolamento M-01 / M-02 | °C ×10 | **Não** |
| TAG36 / TAG37 | TT-03 / TT-04 temperatura mancal B-01 / B-02 | °C ×10 | **Não** |
| TAG38 / TAG39 | TT-05 água / TT-06 ambiente | °C ×10 | **Não** |
| TAG40 | Heartbeat da publicação | — | Não (útil para falhas de comunicação) |
| TAG41–TAG51 | Estados, set-point, referência de velocidade, alarmes, I/O empacotado | discretos | Não (são estados, não medições) |

Grupo multivariado `CP300-B01-Piloto` = TAG27 (corrente B-01), TAG29 (vibração B-01), TAG31 (frequência B-01).

Canal de sensor `k` (alvo das falhas 20–27) → tag: 1→TAG22, 2→TAG23, 3→TAG24, 4→TAG25, 5→TAG26, 6→TAG27, 7→TAG28, 8→TAG29, 9→TAG30, 10→TAG31, 11→TAG32, 12→TAG33, 13→TAG34, 14→TAG35, 15→TAG36, 16→TAG37, 17→TAG38, 18→TAG39. Alvo +100 = sentido negativo.

Falhas de bomba com alvo 0 atingem a **bomba principal do momento** (ver TAG44 antes da falha ou TAG62 durante): B-01 → TAG27/29/31, B-02 → TAG28/30/32.

## 3. Como rodar um cenário

1. Simulador ativo: `SMLX = 13579`, `TST = 1`.
2. Para resultado reproduzível: `HINI` com hora fixa (não 99) e `SEED` fixa. Anote os dois.
3. Escolha o preset em `PSC` (W01) e dê `CSC = 1` (W00). `CSC = 2` pausa, `3` retoma, `4` para.
4. **Todo início de cenário reseta a planta** (nível 60 %, bombas paradas, temperaturas na ambiente). Os primeiros minutos são um transitório de partida: espere alarmes nesse trecho e não conte como falso positivo sem analisar.
5. Injeções manuais (W03–W09): duração mínima de 10 min e rampas de vários minutos. Menos de 1 min pode cair entre duas amostras do PSi4.

## 4. Linha do tempo de cada preset

Minutos contados do início do cenário. "Esperado" = TAG73 = 1 no intervalo (rampa + plena + recuperação). Preencha as colunas "Observado".

### Preset 5 — Só operação normal (5,5 h) → **referência saudável / treino**

Nenhum rótulo de falha. **Qualquer anomalia da IA aqui é falso positivo** (exceto talvez o transitório inicial).

| Min | Evento normal | Tags afetadas | Observado na IA |
|---|---|---|---|
| 0–60 | Perfil diário de consumo | Todas variam com a demanda | |
| 60–90 | Consumo fixo 900 ‰ (alto) | TAG25, TAG27/28, TAG31/32, TAG33; bomba auxiliar pode ligar | |
| 90–120 | Consumo fixo 300 ‰ (baixo) | Mesmas, para baixo; auxiliar desliga | |
| 120–140 | Set-point 3,80 bar | TAG24, TAG31/32, TAG27/28 | |
| 140–160 | Set-point 5,00 bar | Idem | |
| 160–170 | Parada da estação | TAG25, TAG27–TAG33 vão a 0; TAG22 sobe | |
| 170–190 | Estação liberada (partida) | Tudo volta | |
| 190–210 | Rodízio forçado de bombas | Tags de B-01 e B-02 trocam de nível | |
| 210–330 | Normal (perfil diário) | — | |

### Preset 1 — Exemplo da especificação (85 min)

| Min | Falha | Alvo / intensidade / modo | Tags monitoradas que devem reagir | Esperado | Observado na IA | Atraso |
|---|---|---|---|---|---|---|
| 0–30 | Normal | — | — | Sem anomalia | | |
| 30–40 | 02 Obstrução do recalque | 800 ‰, rampa de 10 min | TAG31/32 ↑, TAG27/28 ↑, TAG33 ↑; auxiliar liga (TAG28/30/32 ou 27/29/31 saem do zero) | Anomalia | | |
| 40–~42 | Recuperação (98) em 120 s | — | Voltam ao normal | Anomalia até zerar | | |
| ~42–85 | Normal | — | — | Sem anomalia | | |

### Preset 2 — Bateria de falhas de processo (7,5 h)

| Min | Falha | Alvo / intensidade / modo | Tags monitoradas que devem reagir | Observado na IA | Atraso | Tipo de falha da IA |
|---|---|---|---|---|---|---|
| 0–30 | Normal | — | — | | | |
| 30–35 | 01 Parada inesperada (inversor) | Bomba principal, 1000 ‰, súbita, 5 min | Corrente e frequência da principal → 0; TAG24 cai; reserva assume (tags da outra bomba sobem) | | | |
| 35–50 | Normal (após failover) | — | — | | | |
| 50–80 | Normal + reset do operador | — | — | | | |
| 80–120 | 03 Entupimento do filtro de sucção | Principal, 900 ‰, rampa 30 min | TAG23 ↓; com cavitação: TAG29/30 ↑, corrente ruidosa, TAG24 ↓; possível trip por baixa sucção → failover | | | |
| 120–150 | Normal + reset | — | — | | | |
| 150–190 | 04 Desgaste do rotor | Principal, 700 ‰, rampa 40 min | TAG31/32 ↑ para a mesma pressão, TAG27/28 e TAG33 ↑; auxiliar pode ligar | | | |
| 190–210 | Normal | — | — | | | |
| 210–250 | 06 Vazamento na linha de recalque | 600 ‰, súbita | TAG25 ↑, TAG31/32 ↑, TAG27/28 e TAG33 ↑, TAG22 cai mais rápido | | | |
| 250–270 | Normal | — | — | | | |
| 270–310 | 08 Falha de refrigeração do motor | Principal, 1000 ‰, súbita | **Só TAG34/35 (não monitoradas)**; se der trip (150 °C) → failover visível nas correntes/frequências | | | |
| 310–330 | Normal | — | — | | | |
| 330–360 | 12 Oscilação da malha de pressão | 700 ‰, período 6 min | TAG24, TAG25, TAG31/32, TAG27/28, TAG33 oscilam | | | |
| 360–380 | Normal | — | — | | | |
| 380–420 | 07 Vazamento no reservatório | 1000 ‰, súbita | TAG22 cai mais rápido; TAG26 (entrada) liga com mais frequência | | | |
| 420–450 | Normal + reset | — | — | | | |

### Preset 3 — Bateria de falhas de sensor (≈7,2 h)

Cada falha afeta **uma única tag**: é o teste mais limpo para o univariado.

| Min | Falha | Tag alvo | Intensidade / modo | Monitorada? | Observado na IA | Atraso | Tipo de falha da IA |
|---|---|---|---|---|---|---|---|
| 0–20 | Normal | — | — | — | | | |
| 20–50 | 20 Sensor congelado | TAG24 (PT-02) | 1000 ‰ | Sim | | | |
| 50–70 | Normal | — | — | — | | | |
| 70–110 | 21 Drift | TAG25 (FT-01) | 600 ‰, rampa 40 min | Sim | | | |
| 110–130 | Normal | — | — | — | | | |
| 130–160 | 22 Ruído excessivo | TAG27 (IT-01) | 800 ‰ | Sim | | | |
| 160–180 | Normal | — | — | — | | | |
| 180–220 | 23 Intermitente (quedas a 0/4095) | TAG22 (LT-01) | 1000 ‰ | Sim | | | |
| 220–240 | Normal | — | — | — | | | |
| 240–270 | 24 Valor errado (ganho −) | TAG24 (PT-02) | 300 ‰ | Sim | | | |
| 270–290 | Normal | — | — | — | | | |
| 290–330 | 25 Sem atualização | TAG34 (TT-01) | 500 ‰ | **Não** | | | |
| 330–350 | Normal | — | — | — | | | |
| 350–370 | 26 Circuito aberto | TAG29 (VT-01) | 1000 ‰ | Sim | | | |
| 370–390 | Normal | — | — | — | | | |
| 390–410 | 27 Saturação | TAG23 (PT-01) | 1000 ‰ | Sim | | | |
| 410–430 | Normal + reset | — | — | — | | | |

Atenção: no congelamento (20) e no drift do PT-02, a malha de pressão segue a leitura errada, então outras tags podem reagir também.

### Preset 4 — Bateria de falhas de comunicação (≈3,4 h)

Afeta **todas** as medições TAG22–TAG51 ao mesmo tempo. Os rótulos (TAG60–81) continuam normais.

| Min | Falha | Como aparece no histórico de 1/min | Observado na IA | Tipo de falha da IA |
|---|---|---|---|---|
| 0–20 | Normal | — | | |
| 20–35 | 30 Interrupção | TAG22–51 param; TAG40 não avança | | |
| 35–65 | Normal | — | | |
| 65–95 | 31 Atraso (até ~6 min com 800 ‰) | Valores atrasados; acima de 31 s mudam em degraus de 15 s | | |
| 95–115 | Normal | — | | |
| 115–145 | 32 Perda de amostras | Amostras seguidas repetidas; TAG40 avança menos de 60 | | |
| 145–165 | Normal | — | | |
| 165–185 | 33 Dados velhos | TAG22–51 congeladas, mas TAG40 continua avançando | | |
| 185–205 | Normal | — | | |

### Preset 6 — Degradação encadeada (2,5 h) → **teste de detecção precoce**

| Min | Falha | Alvo / intensidade / modo | Tags monitoradas que devem reagir | Observado na IA | Atraso |
|---|---|---|---|---|---|
| 0–20 | Normal | — | — | | |
| 20–60 | 10 Desbalanceamento | Principal, 900 ‰, rampa 40 min | TAG29/30 ↑ (até ~+6 mm/s) | | |
| 60–100 | 09 Perda de lubrificação (+ desbalanceamento) | Principal, 1000 ‰, rampa acelerada 40 min | TAG36/37 ↑ (não monitoradas); acima de 50 % TAG29/30 ↑ mais; trip do mancal → failover | | |
| 100–130 | Falhas continuam ativas | — | Bomba em falha, reserva operando | | |
| 130–150 | Cancela tudo (99) + reset | — | Volta ao normal | | |

Aqui interessa **quão cedo** a IA percebe a rampa, não só se percebe.

## 5. Ordem sugerida de testes

1. **Preset 5** primeiro: mede falso positivo e serve de dados de treino saudáveis.
2. **Preset 3**: falhas de uma tag só; isola problemas do univariado.
3. **Preset 1**: cenário curto da especificação.
4. **Preset 2** e **6**: falhas de processo com várias tags (bom para o multivariado).
5. **Preset 4**: falhas de comunicação; dizem mais sobre o tratamento de dados ruins do que sobre detecção.

## 6. Pontos que afetam diretamente os resultados da IA

- **A operação normal tem várias "mudanças de regime":** demanda com picos de manhã e à noite (perfil de 24 h), bomba auxiliar ligando e desligando, rodízio de bombas, set-point noturno e nível subindo e descendo com a válvula de entrada. Um baseline treinado com ~2 h de dados (como o registro da TAG22, 130 amostras) não viu boa parte disso e tende a marcar o normal como anomalia. Treinar com o preset 5 inteiro, ou idealmente com 24 h de operação normal, é o mínimo para cobrir o perfil diário.
- **Não treinar enquanto um preset de falha estiver rodando.** Confira TAG73 = 0 em todo o período de treino.
- **Tags de bomba reserva ficam em 0 a maior parte do tempo** (TAG28/30/32 ou TAG27/29/31, conforme a principal). A IA pode classificá-las como discretas ou setpoint e desligar os detectores (achado da auditoria). Isso também explica falsos positivos no rodízio.
- **Algumas falhas só aparecem em tags não monitoradas** (08 refrigeração, 09 lubrificação, 25 no TT-01). Se a IA "não detectar", pode ser porque a tag não está ativa, não porque o detector falhou.
- **Falhas de comunicação congelam valores.** Detectores estatísticos costumam ver valor parado como "estável". Para pegar essas falhas, a IA precisaria olhar o TAG40 (heartbeat) ou a repetição de amostras.
