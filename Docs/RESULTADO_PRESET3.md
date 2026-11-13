# Resultado — Preset 3 (falhas de sensor, teste de 12/11/2026)

> **Conclusão principal: o preset 3 não rodou.** No histórico exportado (`dados/cp300_09a13nov.csv`, até 13/11 07:59), o simulador nunca entrou no preset 3. Em 12/11 às 14:20, o estado do cenário foi de "concluído" para "parado", mas o preset 3 não começou: o TAG74 ficou em 6 e o TAG76 não foi a 1. Também não houve reset da planta, nenhum slot de falha foi preenchido, o TAG73 ficou em 0 e nenhuma medição mostra assinatura de falha de sensor. **Não há falha para detectar nesse período**, e nenhum resultado de detecção do preset 3 pode ser afirmado (P29).
>
> O que este documento entrega no lugar disso:
> 1. as evidências de que o cenário não rodou (§1.2);
> 2. o replay com **treino limpo por período** nas três configurações, que passa a ser um **teste de alarme falso em operação normal** de 17,7 h, com tarde, noite e madrugada (§2.2–§2.3);
> 3. a comparação com a IA real, que trouxe a **idade da leitura na decisão (0,2 a 17,2 min)**, até então não medida (§2.4);
> 4. o que o código permite prever para cada falha do preset 3, como hipótese, até o cenário ser repetido (§2.5).

**Nível de evidência:** **confirmado nos dados** (CSV exportado do Bridge/PSi4) e **confirmado no replay** (código de produção sobre os dados do simulador, sem Isolation Forest, multivariado nem latência; não é validação em planta). Hipóteses estão marcadas. Nenhum código, configuração, limiar ou peso foi alterado.

## 1. Dados

### 1.1 Arquivo

- **Fonte:** `dados/cp300_09a13nov.csv` (PK2, 43 tags, de 09/11 12:30 a 13/11 07:59, 5.474 minutos por tag; o mesmo da RC-0, `PLANO_REGIME_COMBINADO.md` §5.7). Tem TAG61–TAG78 (slots A, B e C, preset, passo, estado e tempo de cenário).
- **Séries de regime:** `dados/cp300_09a13nov_derivado.csv` (= entrada + `RB01S`/`RB02S`, da RC-0). O gabarito (TAG60–TAG81) não entra em nenhuma série nem em `--tags`/`--regime`.
- **Decisões da IA real:** `dados/ia_real_preset3.csv` (`Export-AnomaliasIaReal.ps1`, 12/11 14:00 → 13/11 02:05): 50 decisões do PK2, todas entre 14:21 e 15:09 (P26, P27).

### 1.2 Pelos rótulos: o preset 3 não começou (item 1)

**Transições de preset (TAG74) e estado (TAG76) no arquivo inteiro** (confirmado nos dados):

| Minuto | TAG74 (preset) | TAG76 (estado) | TAG75 (passo) | TAG77/78 (tempo) | Leitura |
|---|---|---|---|---|---|
| 09/11 12:30 | 1 | 3 (concluído) | 4 | 1 h 1500 s | Início do arquivo |
| 11/11 12:56 | 1 | **1 (rodando)** | 0 | 0 h 37 s | Preset 1 iniciado |
| 11/11 14:23 | 1 | 3 | 4 | 1 h 1500 s | Preset 1 concluído |
| 11/11 15:07 | 1 | **0 (parado)** | 4 | 1 h 1500 s | Comando de parada |
| 11/11 15:08 | **6** | **1 (rodando)** | 0 | 0 h 11 s | **Preset 6 iniciado** um minuto depois |
| 11/11 17:38 | 6 | 3 | 5 | 2 h 1800 s | Preset 6 concluído |
| **12/11 14:20** | **6** | **0 (parado)** | 5 | 2 h 1800 s | Comando de parada… |
| 12/11 14:21 → 13/11 07:59 | 6 | 0 | 5 | 2 h 1800 s (parado) | …e **nenhum início depois disso** |

- **Valores do TAG74 no arquivo:** só 1 (3.023 min) e 6 (2.451 min). **O valor 3 nunca aparece.**
- **Slots de falha (TAG61/65/69):** só 3 eventos no arquivo inteiro, todos de 11/11: obstrução (código 2, 13:28–13:39), desbalanceamento (10, 15:28–17:17) e lubrificação (9, 16:08–17:17). Nenhum código 20–27.
- **TAG73 = 1:** só em 11/11 13:28–13:39 e 15:28–17:17.
- **Contraste com o início do preset 6** (minuto a minuto):

  | | 11/11 (preset 6) | 12/11 (tentativa do preset 3) |
  |---|---|---|
  | TAG76 3 → 0 | 15:07 | 14:20 |
  | TAG74 vira o novo preset e TAG76 = 1 | **15:08** | **nunca** |
  | Relógio do cenário (TAG77/78) volta a contar | 15:08 (0 h 11 s, 71 s…) | parado em 2 h 1800 s |
  | Reset da planta (nível a 60 %, CENARIOS §3.4) | TAG22 578 → **601** às 15:08 | TAG22 segue a curva normal (470 → 477 → 487) |

- **Nenhuma assinatura de falha de sensor nas medições** (varredura do arquivo inteiro):
  - TAG22 nunca vai a 0 nem a 4095 (falha 23): faixa 390–803;
  - nenhuma tag (TAG22–25, TAG34) fica 8 minutos ou mais com o mesmo valor (falhas 20 e 25);
  - TAG23 fica sempre em 1175–1485, sem saturação (falha 27);
  - TAG29 nunca fica abaixo de 5 com a B-01 operando (falha 26);
  - de 12/11 13:00 a 24:00, em blocos de 10 min, nenhum padrão de drift na TAG25, ruído na TAG27 nem degrau na TAG24.
- **O simulador estava vivo:** o TAG40 (heartbeat) avança ~60 por minuto, e as medições seguem o perfil normal (rodízio, picos de demanda, set-point noturno).

**Conclusão (confirmado nos dados):** em 12/11, o primeiro passo da troca de cenário aconteceu (parada às 14:20), mas o preset 3 **não iniciou**. O "teste do preset 3" foi, na prática, **operação normal**.
- **Causa: desconhecida [hipótese].** Pode ser o comando de início (`PSC = 3`, `CSC = 1`) não escrito ou não aceito, ou um `CSC = 4` (parar) no lugar de `CSC = 1`. O CSV não registra os comandos (W00/W01). A confirmação está no log `BDE` do CLP ou no relato de quem operou.
- **Início e fim de cada falha:** **não existem** neste arquivo. A tabela do CENARIOS §4 (falhas a partir do minuto 20) não pode ser alinhada a nenhum horário.

### 1.3 Período de treino

- **Treino:** [11/11 18:00, 12/11 14:15) = **1.215 min**, com o `PerfilTagService` real (`--treino-inicio/--treino-fim`).
- **TAG73 = 0 em todo o período** (confirmado pela trava da ferramenta: "0 min com TAG73 = 1"). O preset 6 terminou às 17:17 (falhas) e às 17:38 (cenário), antes do início do treino.
- **Cobertura de estados** (contagem da ferramenta, `RB01S`): B-01 parada de dia 111, sozinha de dia 320, em paralelo 484, parada à noite 240, sozinha à noite 60 min. A noite de 12/11 (set-point 380) está no treino: TAG46 = 380 em 300 min.
- **A limpeza IQR cortou de novo a madrugada inteira da TAG24:** 304 pontos removidos (911 usados), e nada nas outras tags. **O treino por período limpo não resolve o P01:** a limpeza continua tirando um regime normal.
- **Janela móvel de 24 h** (detecção e regime; comportamento de produção): as leituras de 12/11 14:15 a 17:17 ainda veem os 110 min da falha do preset 6 no histórico. As configurações com regime precisaram de `--permitir-falha-no-treino`, porque a trava achou 110 min com TAG73 = 1 na janela móvel antes do replay. **O treino em si estava limpo.**

## 2. Resultado

### 2.1 Execuções

Mesmas 13 tags e mesmas configurações da etapa 0 e da RC-0 (`PLANO_REGIME_COMBINADO.md` §5.2). Replay de 12/11 14:15 a 13/11 07:59 (1.064 leituras por tag, 13.832 no total):

```powershell
$exe = 'tools\Cp300OfflineReplay\bin\Debug\net10.0\IAApi.Cp300OfflineReplay.exe'   # dotnet build -c Debug
$comum = '--entrada dados\cp300_09a13nov_derivado.csv --cliente PK2 --tags TAG22,...,TAG33,TAG44 ' +
         '--treino-inicio 2026-11-11T18:00 --treino-fim 2026-11-12T14:15 --inicio 2026-11-12T14:15 --fim 2026-11-13T07:59'
# sem:   $comum --saida dados\p3_sem.csv
# rec:   $comum --permitir-falha-no-treino --regime TAG28=TAG43,TAG30=TAG43,TAG32=TAG43,TAG26=TAG45,TAG24=TAG46 --saida dados\p3_rec.csv
# combs: $comum --permitir-falha-no-treino --regime TAG27=RB01S,TAG29=RB01S,TAG31=RB01S,TAG28=RB02S,TAG30=RB02S,TAG32=RB02S,TAG26=TAG45,TAG24=TAG46 --saida dados\p3_combs.csv
# placar: $exe placar --replay dados\p3_<cfg>.csv --gabarito dados\cp300_09a13nov.csv --saida dados\placar_p3_<cfg>
```

- **Conferência:** 3 de 3 execuções com código 0, **0 erros e 0 divergências**. Cada uma levou ~3,5 min.
- **Placar:** "0 eventos no período do replay (3 eventos do gabarito fora do período, ignorados)" nas três configurações e na IA real. Por isso, **todo alarme é falso positivo** (operação normal, TAG73 = 0).

### 2.2 Placar das três configurações (item 3)

**Tabela por falha:** nenhuma das falhas do preset 3 ocorreu (§1.2), então nenhuma é avaliável.

| Falha planejada (CENARIOS §4) | Tag alvo | Ocorreu? | Sem regime | Regime recomendado | Combinado + set-point |
|---|---|---|---|---|---|
| 20 Sensor congelado | TAG24 | **Não** | — | — | — |
| 21 Drift | TAG25 | **Não** | — | — | — |
| 22 Ruído excessivo | TAG27 | **Não** | — | — | — |
| 23 Intermitente (0/4095) | TAG22 | **Não** | — | — | — |
| 24 Valor errado (ganho −) | TAG24 | **Não** | — | — | — |
| 25 Sem atualização | TAG34 (não monitorada) | **Não** | — | — | — |
| 26 Circuito aberto | TAG29 | **Não** | — | — | — |
| 27 Saturação | TAG23 | **Não** | — | — | — |

Colunas "detectou, tag, atraso e severidade": **não se aplicam**, porque não há evento no gabarito.

**Alarmes falsos** (minutos-tag; confirmado no replay). A madrugada segue a definição do placar, das **00h às 05h** (00:00–05:59, 6 h). O dia é o resto: 12/11 14:15–23:59 e 13/11 06:00–07:59 (≈11,7 h).

| Configuração | Total | Dia | Madrugada | Na janela em que o preset 3 deveria rodar (12/11 14:20–21:32) |
|---|---|---|---|---|
| Sem regime | **2.525** | 1.027 | **1.498** | 418 |
| Regime recomendado (`rec`) | **1.378** | 990 | 388 | 323 |
| Combinado + set-point (`combs`) | **297** | **184** | **113** | **129** |

**Por tag (dia / madrugada):**

| Tag | Sem regime | `rec` | `combs` |
|---|---|---|---|
| TAG22 nível | 4 / 0 | 4 / 0 | 4 / 0 |
| TAG23 sucção | 24 / 21 | 24 / 21 | 24 / 21 |
| TAG24 pressão | 1 / **299** | 1 / 2 | 1 / 2 |
| TAG25 vazão | 4 / 0 | 4 / 0 | 4 / 0 |
| TAG26 vazão de entrada | 124 / 221 | 23 / 2 | 23 / 2 |
| TAG27 corrente B-01 | 231 / 68 | 231 / 68 | **21 / 3** |
| TAG28 corrente B-02 | 15 / 236 | 101 / 46 | 33 / 24 |
| TAG29 vibração B-01 | 193 / 69 | 193 / 69 | **1 / 2** |
| TAG30 vibração B-02 | 77 / 231 | 84 / 8 | 6 / 22 |
| TAG31 frequência B-01 | 231 / 115 | 231 / 115 | **26 / 11** |
| TAG32 frequência B-02 | 118 / 235 | 89 / 54 | 36 / 23 |
| TAG33 potência | 5 / 3 | 5 / 3 | 5 / 3 |
| **Total** | **1.027 / 1.498** | **990 / 388** | **184 / 113** |

- Contra o sem regime, o `combs` reduz os alarmes falsos em **88 %** (2.525 → 297). Contra o `rec` (a configuração recomendada hoje), reduz em **78 %** (1.378 → 297).
- Na B-01 (TAG27/29/31), `combs` cai de 907 (sem e `rec`, iguais porque a B-01 não tem regime no `rec`) para **64**.

### 2.3 De onde vêm os alarmes falsos

Todos os blocos abaixo estão **confirmados no replay**; o estado da planta em cada minuto está **confirmado nos dados**. As causas marcadas como hipótese não foram verificadas detector a detector.

| Horário | O que acontece na planta | Sem regime | `rec` | `combs` | Problema |
|---|---|---|---|---|---|
| 12/11 14:15–16:11 | B-01 sozinha, **B-02 parada** (reserva) | TAG32 117, TAG30 ~70 | 0 | 0 | P02 |
| 12/11 **20:47** | **Rodízio:** a B-01 para (TAG42 → 0) e a B-02 assume sozinha | — | — | TAG29 1 (minuto da troca) | — |
| 12/11 20:51 → 13/11 00:46 | **B-01 parada** depois do rodízio | TAG27 185, TAG29 149, TAG31 236 | **igual** (a B-01 não tem regime no `rec`) | 0 | P02 (mesmo mecanismo da reserva, agora na B-01) |
| 12/11 20:47–21:50 | **B-02 sozinha** depois do rodízio | TAG28 9 | TAG28/30/32 ≈ 160 | TAG28/30/32 17 (21:34–21:41) | P03 |
| 13/11 00:01–05:00 | Set-point noturno (TAG46 = 380) | **TAG24 300 de 300 min** | TAG24 2 | TAG24 2 | P05 (e P01: o IQR cortou a noite do treino) |
| 13/11 00:47 e 04:47 | Mais dois rodízios (B-02 para; depois B-01 para) | B-02 parada 00:52–04:47: TAG28/30/32 ≈ 700; B-01 parada 04:52–06:39: ≈ 325 | B-02 em vários trechos (00:01–00:25, 04:47–06:39); B-01 igual ao sem | ver linha abaixo e 1–3 min por troca | P02, P03 |
| 13/11 **00:52–01:12** | B-02 parada com set-point 380 (classe `RB02S = 10`) | (incluído acima) | — | **TAG28/30/32: 21 + 21 + 21 = 63**, com `origem_baseline = Global` | **P08** (fallback) |
| 12/11 17:51–18:00, 18:38–18:51 e 13/11 07:08–07:16 | **Picos de demanda com as duas bombas em paralelo:** TAG25 até 991 (o maior do arquivo), frequência 421–425 | incluído acima | incluído acima | TAG27/28/31/32 ≈ 95, TAG25 4, TAG33 6 | P03 (regime "paralelo" não homogêneo) [hipótese] |
| 12/11 18:36–19:09 e madrugada (00:50, 02:02, 03:19, 04:36) | **Nível no extremo:** TAG22 a 390–410 (mínimo do arquivo) às 18:39–18:51; 766–796 de madrugada | TAG23 45 | TAG23 45 | TAG23 45 | P22 |
| Abertura e fechamento da válvula de entrada (15:07–16:14 e madrugada) | Válvula fechada (TAG45 = 0) ou vazão alta com ela aberta | TAG26 345 | TAG26 25 | TAG26 25 | P04 |

**Detalhes confirmados:**
- **Fallback das 00:52–01:12 (P08), confirmado nos dados e no replay.** O rodízio das 00:47 deixou a B-02 parada com o set-point 380. Nas 24 h anteriores, essa classe tinha **14 pontos** às 00:52 e só chegou a **30** (`MinimoAmostrasPorRegime`) às **01:13**, exatamente quando os alarmes param. É o mesmo mecanismo dos 9 fallbacks da RC-0 (§5.7.3): as bombas se alternam de uma noite para outra, e a janela móvel de 24 h não guarda a classe rara. Desta vez foram **63 minutos-tag**: 21 % dos falsos do `combs`.
- **Rodízio diurno (20:47).** O replay pegou um caso que os presets 1 e 6 não tinham: um rodízio com a B-01 parando **de dia**. No `rec`, a B-01 parada gera 570 minutos-tag (P02 na B-01). No `combs`, ela vira regime próprio (`Discrete`, detectores bloqueados; P20) e gera 1 alarme, no minuto da troca.
- **Picos de demanda [hipótese].** Em `combs`, os alarmes de dia nas bombas se concentram nos três picos de vazão do período (≈ 95 dos 184 de dia), com o regime "paralelo" aplicado. É compatível com a hipótese do PLANO §5.6.1 de que o regime precisa de mais uma dimensão (demanda), mas isso não foi verificado nas notas por detector.
- **TAG23 (P22).** Os 45 alarmes são **idênticos nas três configurações** (a TAG23 não tem regime) e caem sempre com o **nível do reservatório num extremo**. Às 18:39–18:51, nível e sucção ficam nos menores valores do arquivo inteiro (TAG22 = 390, TAG23 = 1175). De madrugada, o nível fica perto do máximo. A coincidência está confirmada nos dados. Que a sucção acompanhe o nível (coluna d'água) e que o perfil não cubra os extremos é **hipótese**.

### 2.4 Comparação com a IA real (item 4)

**Lembrete do P18:** a IA real grava a hora do **processamento** (`DataDeteccao = DateTime.Now`), não a hora da leitura. A comparação abaixo usa o **minuto da leitura**, recuperado pelos valores.

**a) Idade da leitura na decisão (novo; confirmado nos dados e no código).**
- Em cada ciclo do PK2, o valor que a IA real gravou bate com o CSV **num único minuto, comum a todas as tags do ciclo**. Os candidatos de cada tag foram procurados em até 30 min antes do processamento.
- Isso confirma o que o código faz (`Services/MonitoramentoTagsService.cs : ExecuteAsync`, linhas 54–108): **uma consulta em lote dos valores atuais por cliente no início do ciclo**, seguida das predições **tag a tag com esses mesmos valores**.
- Assim, a leitura envelhece enquanto o ciclo anda.

| Ciclo (processamento) | Minuto da leitura (CSV) | Tags | Idade na decisão (min): primeira → última tag |
|---|---|---|---|
| 14:21:10 → 14:29:01 | 14:20 | 9 | 1,2 → **9,0** |
| 14:34:45 → 14:35:38 | 14:32 | 11 (TAG22 com timeout) | 2,8 → 3,6 |
| 14:38:11 → 14:39:02 | 14:38 | 12 | **0,2** → 1,0 |
| 14:43:09 → 14:50:08 | 14:41 | 12 | 2,1 → 9,1 |
| 14:53:58 → 15:09:13 | 14:52 | 6 (TAG25–30 com timeout) | 2,0 → **17,2** |

- A idade vai de **0,2 a 17,2 min**. A resolução é de 1 min (o minuto do histórico do PSi4). Somada à cadência de ~10 min por tag (P26), uma falha só seria vista entre 10 e ~27 min depois de começar, no pior caso.
- **Consequência para o placar da IA real:** com `data_hora` = minuto do processamento, o atraso medido inclui essa idade. Em minutos com falha, uma leitura de antes da falha pode ser contada como "não detectou dentro da falha".

**b) Decisões e notas** (50 decisões, 12 tags, 14:21–15:09; replay no **minuto da leitura**).

> **Corrigido em 13/11 (P30).** A primeira versão desta tabela comparava escalas diferentes: para a IA real, usava o `Score` gravado em `AnomaliaDetectada`, que é a **nota ÷ 20** (0–5; `EnsembleDetectionService.cs:406-410`), e para o replay a nota de 0 a 100. Agora **todas as colunas estão na escala 0–100**: a IA real aparece como `Score × 20` (coluna `nota_0_100` da exportação). Fonte da IA real: `dados/ia_real_preset3.csv`; os números batem com `INVENTARIO_PK2.md` §4.3.

| Tag | IA real (0–100): média / máx. das decisões | Decisões válidas / total | Sem regime | `rec` | `combs` |
|---|---|---|---|---|---|
| TAG22 | 14,8 / 42,0 | 2 / 4 | 27,4 / 42,1 | 27,4 / 42,1 | 27,4 / 42,1 |
| TAG23 | 3,7 / 18,6 | 1 / 5 | 30,0 / 47,0 | 30,0 / 47,0 | 30,0 / 47,0 |
| TAG24 | 2,9 / 14,6 | 1 / 5 | 7,1 / 24,1 | 12,0 / 43,1 | 12,0 / 43,1 |
| TAG27 | 10,3 / 41,1 | 1 / 4 | 47,4 / 53,2 | 47,4 / 53,2 | 40,3 / 46,6 |
| TAG29 | 13,7 / 41,1 | 1 / 3 | 51,6 / 54,8 | 51,6 / 54,8 | 30,6 / 38,1 |
| TAG30 | 7,1 / 21,3 | 1 / 3 | 60,5 / 65,0 (**2 alarmes**) | 3,7 / 7,3 | 3,7 / 7,3 |
| TAG32 | 6,7 / 33,3 | 1 / 5 | 63,6 / 65,7 (**5 alarmes**) | 0 / 0 | 0 / 0 |
| Demais (TAG25, 26, 28, 31, 33) | ≤ 11,6 / ≤ 38,6 (TAG33: 0 / 0) | 4 / 21 | 17–46 | 0–39 | 0–35 |
| **Total** | **7,0 / 42,0** | **12 / 50** (38 com nota 0) | — | — | — |
| **Alarmes** | **0** | | **7** (B-02 parada, P02) | **0** | **0** |

**Só as 12 decisões válidas** (nota ≠ 0; ciclo de 14:38 e a primeira tag do ciclo de 14:21), lado a lado e na mesma escala (`dados/inventario_pk2/perfil_real_50_decisoes_12nov.csv`):

| | IA real (`Score × 20`) | Replay sem regime, mesmo minuto | Perfil do banco (30/10), mesmo minuto |
|---|---|---|---|
| Média (12 decisões) | **29,1** | 36,6 | 34,5 |
| Faixa | 14,6 – 42,0 | 3,4 – 65,7 | 18,4 – 52,9 |
| Maiores diferenças real × replay | TAG30 21,3 × 64,1; TAG32 33,3 × 65,7 (B-02 parada; perfil diferente, P32) | | |

- **Decisão:** a IA real não alarmou (0 de 50), como o `rec` e o `combs` nesses minutos. O sem regime teria alarmado 7 vezes, todas na reserva parada (P02). **Mas 38 das 50 decisões não passaram pelos detectores** (entrada inválida, P31), então o "0 alarmes" da IA real não é comparável ao das configurações do replay.
- **As notas não são "quase zero" (confirmado no código e nos dados).** A afirmação anterior ("IA real ≤ 2,1, replay 20–35") vinha da mistura de escalas. Na mesma escala, as notas válidas da IA real (14,6–42,0, média 29,1) ficam na **mesma faixa do replay**. A diferença que sobra tem duas causas, as duas demonstradas no `INVENTARIO_PK2.md` §4.2:
  - **38 zeros por entrada inválida** (P31): a leitura do lote é mais antiga que o último ponto do histórico buscado depois, e a IA grava nota 0, "sem anomalia";
  - **perfil diferente** (P32): o `PerfisIa` do PK2 é de 30/10, com a B-02 parada; por isso TAG30/32 dão 21–33 na IA real e 64–66 no replay.
  
  Com o perfil do banco, as 50 decisões foram reproduzidas offline com diferença < 0,1 (INV §4.3). As hipóteses anteriores (baseline comportamental, contexto de tag) foram **descartadas** no PK2: o banco não tem esses cadastros (INV §3).
- **Cobertura:** 50 decisões em 48 min, 12 tags, a partir das 15:09 nenhuma (P26, P27). A IA real avaliou ~9 % dos minutos-tag do período e nenhum minuto de madrugada. Não dá para comparar alarmes falsos de madrugada com a IA real.

### 2.5 Falhas não detectadas e motivo (item 5)

**Nenhuma falha do preset 3 deixou de ser detectada neste teste, porque nenhuma ocorreu (P29).** O motivo de "não detecção" de todas é: **cenário não iniciado**.

Para orientar a repetição, segue o que o **código** permite prever para cada falha (AUD §5c, atualizado com os presets 1 e 6). Tudo isso é **hipótese, não observado**:

| Falha | Tag | Previsão | Motivo provável / problema |
|---|---|---|---|
| 20 Congelado | TAG24 | **Não detecta** | **P12:** não há detector de valor congelado; o valor para num ponto normal. As tags que reagem são outras (malha de pressão), dentro da faixa normal (P06) |
| 21 Drift (600 ‰, 40 min) | TAG25 | Tarde ou nunca | Desvio do perfil inflado pela demanda diária (σ = 218 no treino limpo); P07 (diluição); LevelShift pesa só 10 |
| 22 Ruído | TAG27 | Intermitente | EWMA não reage a ruído de média zero; sem EWMA o máximo é ~50/75 (P07). Se a B-01 estiver parada (rodízio), TAG27 = 0 e, em `combs`, os detectores ficam bloqueados (`Discrete`, P20) |
| 23 Intermitente (0/4095) | TAG22 | Detecta, mas só nos minutos em que a amostra pega a queda | Amostragem de 1/min; na IA real, cadência de ~10 min (P26) e idade da leitura (§2.4) |
| 24 Ganho − (300 ‰) | TAG24 | Só o transiente | A malha devolve a leitura ao set-point. O desvio da pressão de dia é estreito (2,6 no perfil), o que ajuda no degrau |
| 25 Sem atualização | TAG34 | **Não avaliável** | Tag não monitorada (`IaAtiva`); não é falha do detector. Mesmo monitorada: P12 |
| 26 Circuito aberto | TAG29 | Detecta logo, **se a B-01 estiver operando** | Com a B-01 parada, a TAG29 já está perto de 0 e, em `combs`, bloqueada (`Discrete`, P20) |
| 27 Saturação | TAG23 | Detecta logo | Degrau ao extremo; a TAG23 já alarma nos extremos de nível (P22), então pode não se separar de alarmes anteriores (regra P21) |

**Dependência nova, vista neste teste:** o rodízio acontece várias vezes por dia (12/11 20:47, 13/11 00:47 e 04:47). Se o preset 3 for repetido, **a bomba alvo das falhas 22 e 26 (B-01) precisa estar operando**. Senão, a falha cai numa tag parada e o resultado não diz nada sobre o detector. Anotar o TAG42 no momento de cada falha.

### 2.6 Conferência e limitações

- Replay: 0 erros e 0 divergências nas três execuções. Placar: soma das classes = total nas quatro execuções (as três do replay e a IA real).
- O replay não reproduz Isolation Forest, multivariado, decisão runtime, baseline comportamental, latência nem o ciclo sequencial (README §4). Por isso, as notas do replay **não** são as da IA real (§2.4 b). Ao comparar notas, usar a mesma escala: a IA real grava `Score` = nota ÷ 20 (P30).
- As configurações com regime usam a janela móvel de 24 h, como em produção; o período de treino só vale para o perfil (fallback global). As janelas das primeiras 3 h do replay contêm a falha do preset 6 (§1.3).
- A idade da leitura (§2.4 a) foi obtida casando valores. Ela depende de o minuto comum ser único, e foi único nos 5 ciclos. Medir diretamente exige o PERF_DIAG (BACKLOG 6) ou gravar a hora da leitura (BACKLOG 5).

*Scripts de análise (fora do repositório, no scratchpad da sessão):* `p3_rotulos.py`, `p3_slots.py`, `p3_varre.py`, `p3_assin.py`, `p3_1420.py`, `p3_cruza.py`, `p3_idade.py`, `p3_analise.py`, `p3_det2.py` e `p3_ctx.py`. Saídas em `dados/p3_{sem,rec,combs}.{csv,log}` e `dados/placar_p3_{sem,rec,combs,iareal}*` (não versionados). Nenhum código do projeto foi alterado.

## 3. Para repetir o preset 3

1. **Conferir o início** (P29): até 2 min depois do comando, TAG74 = 3, TAG76 = 1, TAG77/78 voltando a contar e o reset do nível (TAG22 ≈ 600). Se não acontecer, o teste não começou. Repetir a conferência a cada ~1 h (TAG75 avançando).
2. Subir a IAApi com o log em arquivo com data e hora (BACKLOG 6, execução já aprovada) e vigiar a última decisão (P27).
3. Exportar o histórico **com a noite anterior** e TAG61–78, como em `cp300_09a13nov.csv`.
4. Rodar o mesmo replay (§2.1) e o placar. O treino limpo pode ser qualquer período com TAG73 = 0 que inclua uma noite.
5. Anotar o estado das bombas (TAG42/43) nas falhas 22 e 26 (§2.5).

## Resumo para apresentação

1. **O preset 3 não rodou.** Em 12/11 às 14:20, o simulador parou o cenário anterior, mas o preset 3 nunca começou: sem reset da planta, sem falhas nos rótulos e sem sinais de falha de sensor nas medições.
2. Então o teste de 12/11 **não mede detecção**. Ele serve como um **teste de alarme falso em 17,7 h de operação normal**, com tarde, noite e madrugada.
3. Com treino limpo (20 h sem falhas), a IA **sem regime** deu **2.525 alarmes falsos**; com o **regime recomendado de hoje**, **1.378**; com o **regime combinado + set-point**, **297** (−88 % e −78 %).
4. O ganho maior veio de um caso novo: a **troca de bombas de noite e de dia**. Com o regime de hoje, a bomba que para depois do rodízio alarma por horas. Com o regime combinado, ela deixa de alarmar.
5. O que sobra no regime combinado: **picos de demanda** com as duas bombas ligadas, **nível do reservatório nos extremos** (sucção) e **20 min logo após um rodízio noturno**, quando a IA ainda não tem histórico suficiente do novo estado.
6. **Treinar só com período limpo não basta:** a limpeza do treino ainda joga fora a madrugada inteira da pressão.
7. Na IA real, descobrimos **quão velha a leitura chega à decisão: até 17 minutos**. A IA lê todas as tags no início do ciclo e decide uma por uma; com o Bridge lento, a última tag decide sobre um dado antigo.
8. As notas da IA real **pareciam** perto de zero, mas era escala: a IA grava a nota dividida por 20 (P30). Na mesma escala, as decisões válidas ficam na faixa do replay. O problema real é outro: **38 de 50 decisões nem chegaram aos detectores** (entrada inválida, P31), e o perfil do PK2 é antigo, de 30/10 (P32).
9. Próximo passo: **repetir o preset 3** conferindo o início do cenário pelos rótulos, com o log da IAApi em arquivo.
