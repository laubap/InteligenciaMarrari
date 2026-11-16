# Resultado — Preset 3 (falhas de sensor, teste de 12/11/2026)

> **Atualização (16/11/2026):** o preset 3 foi **repetido em 13/11 e rodou** (início às 15:02, fim às 22:12, 8 falhas nos slots). O resultado está na **§4, "Preset 3 de 13/11"**. As §1 a §3 abaixo continuam valendo para o teste de 12/11, que não rodou.

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

---

## 4. Preset 3 de 13/11

> **Conclusão principal: desta vez o preset 3 rodou**, das 15:02 às 22:12 de 13/11, com as 8 falhas de sensor nos horários previstos. Com treino limpo de 24 h, **3 das 7 falhas em tags monitoradas foram detectadas** pela regra do placar em cada configuração, mas **não as mesmas**: a saturação da sucção (27) e o nível intermitente (23) em todas; o circuito aberto da vibração (26) só com regime combinado; o ruído da corrente (22) só sem regime e com o regime recomendado, e aí ele coincide com a parada da B-02 (provável coincidência). O sensor congelado (20) passou sem nenhuma reação na própria tag (**P12, agora confirmado no replay**). Com o regime combinado, as duas falhas do sensor de pressão (20 e 24) aparecem nas bombas. Pela regra antiga do placar elas não contavam; com o **mapa estendido** (P41, aprovado em 16/11), contam, e o combinado chega a **5 de 7**. Fora das falhas, os alarmes falsos foram **211 sem regime, 248 com o recomendado e 37 com o combinado**. O regime combinado real (Opção A, `--regime-combinado`) deu **saída idêntica** à das séries derivadas.

**Nível de evidência:** **confirmado nos dados** (CSV exportado do Bridge/PSi4) e **confirmado no replay** (código de produção sobre os dados do simulador, sem Isolation Forest, multivariado nem latência; **não é validação em planta** nem da IA real). Hipóteses estão marcadas. Nenhum código de runtime, configuração, limiar ou peso foi alterado. Só a ferramenta de avaliação mudou, com aprovação (16/11): o mapa do placar (`placar_falhas_cp300.json`) ganhou a seção `efeitosPorCanal` e o placar passou a usá-la (P41; §4.4).

### 4.1 Resumo para apresentação

1. **O preset 3 rodou em 13/11.** Começou às 15:02 (o simulador reiniciou o nível do reservatório, como esperado) e terminou às 22:12. As 8 falhas de sensor aparecem nos rótulos exatamente nos minutos da tabela do cenário.
2. **Teste limpo:** a IA foi treinada com as 24 h anteriores (12/11 15:00 → 13/11 15:00), sem nenhuma falha no período, e reavaliada minuto a minuto das 15:00 às 22:59, em quatro configurações.
3. **Detecção pelo placar (alarme ≥ 3 min seguidos, sem alarme nos 20 min antes), em duas leituras:** a **regra antiga** procura a falha só na tag do sensor; o **mapa estendido** (P41) também conta as bombas quando o sensor com defeito é o da pressão, que comanda as bombas.

   | Falha | Sem regime | Regime recomendado | Regime combinado |
   |---|---|---|---|
   | 27 Saturação da sucção | **No 1º minuto** | **No 1º minuto** | **No 1º minuto** |
   | 23 Nível intermitente | 16 min | 16 min | 16 min |
   | 26 Circuito aberto da vibração | Alarmou aos 3 min, mas não conta (a tag já alarmava antes) | Igual | **No 1º minuto** |
   | 22 Ruído na corrente | 8 min, mas provavelmente por coincidência (a outra bomba parou nesse minuto) | Igual | Não |
   | 20 Pressão congelada | Não | Não | Regra antiga: não. **Mapa estendido: 14 min** (frequência da B-02) |
   | 24 Ganho errado na pressão | Não | Não | Regra antiga: não. **Mapa estendido: no 1º minuto** (correntes) |
   | 21 Drift da vazão | Não | Não | Não |
   | 25 Temperatura sem atualização | Tag não monitorada | — | — |
   | **Detectadas (de 7)** | **3** nas duas leituras | **3** nas duas leituras | **3** (regra antiga) → **5** (mapa estendido) |

4. **Com o regime combinado, as falhas da pressão aparecem nas bombas:** a pressão congelada (20) aos 14 min, pela frequência da B-02; o ganho errado (24) no 1º minuto, pelas correntes. A pressão tem controle automático: o controle "acredita" no sensor errado e muda a bomba. A regra antiga não contava isso, porque só olhava a tag do sensor; o mapa do placar foi atualizado para contar (P41). Sem regime e com o recomendado, nada muda: a referência larga das bombas não separa essa mudança.
5. **Por que as outras não foram detectadas:**
   - **Pressão congelada:** a IA não tem detector de "valor parado" (P12). O sensor travou exatamente no set-point, e a nota da IA ficou em zero.
   - **Drift da vazão:** a vazão subiu, mas continuou dentro da faixa normal do dia. Só a relação vazão × potência mostra o erro (≈ 4,5 desvios).
   - **Ganho errado na pressão:** o controle corrige a leitura de volta ao set-point; a tag da pressão fica normal.
   - **Ruído na corrente:** com a referência certa (regime combinado), os valores ruidosos ficam dentro do normal da bomba; os detectores que pegam ruído pesam pouco na média (P07).
6. **Alarmes falsos nos intervalos normais** (3 h 50 min fora das falhas): sem regime **211**, regime recomendado **248**, regime combinado **37** (−82 % e −85 %). Nesta tarde o regime recomendado foi **pior** que o sem regime, por causa da B-02 operando sozinha (P03).
7. **Madrugada:** o cenário rodou de tarde e à noite; a madrugada ficou no treino. O teste de madrugada continua sendo o de 12/11 (§2.2).
8. **O regime combinado programado (Opção A) deu exatamente o mesmo resultado** das séries de teste usadas até agora, minuto a minuto.
9. **Próximos passos:** detector de valor congelado (P12); regime combinado no PK2 (RC-11 e RC-12); relação entre tags para drift e para sensores dentro de malha de controle (P06, P41); repetir o preset 3 com a IA real gravando em arquivo.

### 4.2 Rótulos: início do cenário e de cada falha (item 1)

**Início do cenário** (confirmado nos dados, `dados/cp300_09a13nov.csv`, agora até 13/11 22:59):

| Minuto | TAG74 (preset) | TAG75 (passo) | TAG76 (estado) | TAG77/78 (tempo) | Leitura |
|---|---|---|---|---|---|
| 13/11 até 15:01 | 6 | 5 | 0 (parado) | 2 h 1800 s (parado) | Continua o estado de 12/11 14:20 (§1.2) |
| **15:02** | **3** | 0 | **1 (rodando)** | **0 h 21 s** | **Preset 3 iniciado** (≈ 15:01:39, pelos 21 s); reset do nível: TAG22 672 → **602** |
| 15:22 → 21:52 | 3 | 1 → 16 | 1 | 0 h 1221 s → 6 h 3021 s | Passos a cada 20, 30 ou 40 min, como na tabela do CENARIOS §4 |
| **22:12** | 3 | 17 | **3 (concluído)** | 7 h 600 s (= 430 min) | Fim do cenário (430 min depois de 15:02) |

- O passo (TAG75) e o relógio (TAG77/78) andam juntos com a tabela do CENARIOS §4: minuto 20 = 15:22, minuto 70 = 16:12, e assim por diante. **Nenhuma divergência.**
- O reset não parou as bombas: a B-02 seguiu sozinha (TAG43 = 2, TAG42 = 0) antes e depois de 15:02.

**Falhas** (slot A; slots B e C vazios; TAG73 = 1 exatamente nesses intervalos e em nenhum outro minuto desde 11/11 17:17):

| Falha (TAG61) | Alvo (TAG62) → tag | Início → fim | Duração | Severidade (TAG63) | Fase (TAG64) | Minuto do cenário | Bombas no período (TAG42/TAG43) | Leitura da tag alvo |
|---|---|---|---|---|---|---|---|---|
| 20 Congelado | 3 → TAG24 (PT-02) | 15:22 → 15:51 | 30 min | 1000 ‰ | 3 (plena) | 20–50 | B-01 parada, B-02 sozinha | **450 fixo** (= set-point; antes: 445–458) |
| 21 Drift | 4 → TAG25 (FT-01) | 16:12 → 16:51 | 40 min | **5 → 590 ‰** | 2 (rampa) o tempo todo | 70–110 | B-02; a B-01 (auxiliar) liga e desliga 4 vezes | 608–869 (antes: 584–746) |
| 22 Ruído | 6 → TAG27 (IT-01) | 17:12 → 17:41 | 30 min | 800 ‰ | 3 | 130–160 | B-01 ligada (principal); B-02 em paralelo, **parada 17:17–17:28** | 149–268 |
| 23 Intermitente | 1 → TAG22 (LT-01) | 18:02 → 18:41 | 40 min | 1000 ‰ | 3 | 180–220 | As duas; **a B-02 desarma em 18:18, 18:20 e 18:41** | Quedas a **0** (4 min) e saltos a **1000** (4 min); no resto, o nível real caindo de 697 a 327 |
| 24 Ganho − | 103 → TAG24 (sentido negativo) | 19:02 → 19:31 | 30 min | 300 ‰ | 3 | 240–270 | As duas em paralelo | **445–453 (normal)** |
| 25 Sem atualização | 13 → TAG34 (TT-01) | 19:52 → 20:31 | 40 min | 500 ‰ | 3 | 290–330 | As duas | 529 fixo (tag não monitorada) |
| 26 Circuito aberto | 8 → TAG29 (VT-01) | 20:52 → 21:11 | 20 min | 1000 ‰ | 3 | 350–370 | B-01 sozinha até 21:04; **rodízio às 21:05**: B-01 parada | **0** (B-01 parada normal: 21–45) |
| 27 Saturação | 2 → TAG23 (PT-01) | 21:32 → 21:51 | 20 min | 1000 ‰ | 3 | 390–410 | B-02 sozinha | **2500** fixo (antes: 1275–1330) |

**Observações dos dados** (confirmado nos dados; causa marcada quando é hipótese):
- **Nenhuma fase de recuperação (4)** apareceu nos minutos do histórico; cada falha passa direto de "plena" (ou "rampa") para 0.
- **Falha 23 mexeu no processo real.** O nível medido caiu de 697 para 327–410 durante a falha e só voltou a ~400 por volta de 19:40 (TAG22 mínimo de 332 depois da falha, o menor do arquivo). A sucção (TAG23) caiu junto (1300 → ~1160) e a válvula de entrada (TAG45) mudou de estado 8 vezes. **A B-02 aparece parada em 3 dos 4 minutos com leitura 0.** [Hipótese] O controle de nível e a proteção de nível baixo usam o LT-01 com defeito.
- **Falhas 20 e 24 aparecem nas bombas, não no sensor.** Na 20, com a leitura parada no set-point, a frequência da B-02 desce em rampa de 438 a 303 (−5 por minuto), com vazão e potência caindo junto, e volta a 437 no minuto em que a falha acaba (15:52). Na 24, às 19:02 a frequência sobe +35 (404 → 439), a corrente +25, a potência +48 e a vazão +168, enquanto a TAG24 fica em 450. [Hipótese] A malha de pressão segue a leitura errada (o CENARIOS §4 já previa isso para o congelamento).
- **Escala do LT-01:** o intermitente vai a 0 e a **1000** (% × 10), não a 4095 como diz o CENARIOS §4 (4095 seria a contagem bruta do CLP).

### 4.3 Execuções (item 2)

**Treino limpo:** [12/11 15:00, 13/11 15:00) = 1.437 min por tag (`--treino-inicio/--treino-fim`, `PerfilTagService` real).
- **TAG73 = 0 em todo o treino** (trava da ferramenta: "0 min com TAG73 = 1"; e nos dados, o primeiro TAG73 = 1 depois de 11/11 é às 13/11 15:22).
- **Janela móvel de 24 h antes do replay também limpa** (trava: 0 min). Desta vez **não foi preciso `--permitir-falha-no-treino`**. Os 250 min de falha do replay entram nas janelas das leituras seguintes, como em produção.
- O treino contém uma noite inteira (TAG46 = 380 em 299 min) e todos os estados de bomba: B-01 parada + B-02 sozinha 457 min, B-01 sozinha 385, paralelo 595.
- **IQR (P01), de novo:** **302 pontos da TAG24 removidos** (a madrugada inteira); nada nas bombas.

**Replay:** 13/11 15:00 → 22:59 (480 leituras por tag, 6.240 no total), mesmas 13 tags da §2.1, com `--historico-causal` nas quatro:

```powershell
$exe = 'tools\Cp300OfflineReplay\bin\Debug\net10.0\IAApi.Cp300OfflineReplay.exe'   # build de 16/11 12:59, posterior ao último commit de código
$comum = '--entrada dados\cp300_09a13nov_derivado.csv --cliente PK2 --tags TAG22,...,TAG33,TAG44 ' +
         '--treino-inicio 2026-11-12T15:00 --treino-fim 2026-11-13T15:00 --inicio 2026-11-13T15:00 --fim 2026-11-13T22:59 --historico-causal'
# sem:   $comum --saida dados\p3b_sem.csv
# rec:   $comum --regime TAG28=TAG43,TAG30=TAG43,TAG32=TAG43,TAG26=TAG45,TAG24=TAG46 --saida dados\p3b_rec.csv
# combs: $comum --regime TAG27=RB01S,TAG29=RB01S,TAG31=RB01S,TAG28=RB02S,TAG30=RB02S,TAG32=RB02S,TAG26=TAG45,TAG24=TAG46 --saida dados\p3b_combs.csv
# combr: $comum --regime-combinado --regime TAG27=TAG42+TAG43+TAG46 --regime TAG29=TAG42+TAG43+TAG46 --regime TAG31=TAG42+TAG43+TAG46 `
#        --regime TAG28=TAG43+TAG42+TAG46 --regime TAG30=TAG43+TAG42+TAG46 --regime TAG32=TAG43+TAG42+TAG46 --regime TAG26=TAG45 --regime TAG24=TAG46 `
#        --regime-mapa "TAG42=2:2;*:0" --regime-mapa "TAG43=2:2;*:0" --regime-mapa "TAG46=380:380;*:450" --saida dados\p3b_combr.csv
# regra antiga:   $exe placar --replay dados\p3b_<cfg>.csv --gabarito dados\cp300_09a13nov.csv --mapa <mapa do commit 226fca9> --saida dados\placar_p3b_antigo_<cfg>
# mapa estendido: $exe placar --replay dados\p3b_<cfg>.csv --gabarito dados\cp300_09a13nov.csv --saida dados\placar_p3b_p41_<cfg>   # mapa versionado atual (P41)
```

- `combr` usa a configuração alvo do PLANO_REGIME_COMBINADO §6.2 (3 componentes e os mapas). A TAG24 ← TAG46 tem 1 componente: segue o caminho atual e o mapa da TAG46 é ignorado nela (aviso no log).
- **Conferência:** 4 de 4 execuções com código 0; em cada uma, 6.240 leituras, **0 erros, 0 divergências**, 0 valores descartados. Rodaram em paralelo, 80–96 s cada. O CSV derivado tem as mesmas 273.996 leituras do original (0 diferenças), mais as 6 séries de regime.
- **Combinado real = combinado das séries derivadas (confirmado no replay):** `p3b_combr.csv` e `p3b_combs.csv` têm as mesmas 37.440 linhas, **iguais em todas as colunas** exceto `valor_regime`, `motivo_baseline` e `chave_regime` (as três que o PLANO §6.3, RC-11 item 2, permite diferir). Mesmos placares. É mais um período para a equivalência da RC-11; a RC-11 completa (presets 1 e 6, madrugada, TAG29, custo) **não** foi feita aqui.
- **Fallback para o perfil global** nas tags com regime: só 4 leituras, todas na TAG26 (15:40, 18:04, 18:35, 22:29). Nenhum nas bombas (P08 não ocorreu).

### 4.4 Placar das quatro configurações (item 3)

**Regra:** a do placar (README §6.1; P21): detecção = alarme numa tag afetada pela falha (mapa `placar_falhas_cp300.json`), ≥ 3 min seguidos, sem alarme dessa tag nos 20 min antes. Atraso = início da sequência − início da falha. "Sinal breve" = sequência < 3 min antes da detecção. `combr` deu **o mesmo placar** que `combs` em todos os campos, nas duas leituras; por isso as duas aparecem numa coluna.

**Duas leituras** (as mesmas saídas do replay; muda só o mapa):
- **Regra antiga:** mapa até o commit `226fca9`. Falha de sensor (20–27) = só a tag do canal (aqui, a TAG24 nas falhas 20 e 24).
- **Mapa estendido (P41, aprovado e versionado em 16/11):** a seção nova `efeitosPorCanal` faz qualquer falha de sensor no **canal 3 (PT-02, TAG24)** contar também **TAG25, TAG27, TAG28, TAG31, TAG32 e TAG33** (vazão, corrente e frequência das duas bombas e potência), que o controle de pressão mexe. Os outros canais não mudam. No preset 3, só as falhas 20 e 24 são no canal 3, e **só elas mudaram**: os outros 6 eventos têm resultado idêntico nas duas leituras (conferido campo a campo).

| Falha | Tag(s) | Sem regime | Regime recomendado (`rec`) | Combinado + set-point (`combs` = `combr`) |
|---|---|---|---|---|
| 20 Congelado — regra antiga | TAG24 | Não (nota máx. 3,4) | Não (3,4) | Não (3,4) |
| 20 Congelado — **mapa estendido** | TAG24 + malha | Não (TAG27/31 excluídas: alarmavam antes, B-01 parada; TAG32 sem alarme) | Não (TAG27/28/31/32 excluídas) | **Sim**, **TAG32** às **15:36**, atraso **14 min**, 1000 ‰, 16 min seguidos (frequência da B-02 em rampa de descida) |
| 21 Drift | TAG25 | Não (máx. 53,4) | Não (53,4) | Não (53,4) |
| 22 Ruído | TAG27 | **Sim**, 17:20, atraso **8 min**, 800 ‰. Sinal breve 17:17 (2 min, atraso 5) | **Sim**, igual ao sem regime | Não. Sinal breve 17:31 (1 min, atraso 19, 800 ‰) |
| 23 Intermitente | TAG22 | **Sim**, 18:18, atraso **16 min**, 1000 ‰. Sinal breve 18:04 (1 min, atraso 2) | **Sim**, igual | **Sim**, igual |
| 24 Ganho − — regra antiga | TAG24 | Não (máx. 5,1) | Não (5,1) | Não (5,1) |
| 24 Ganho − — **mapa estendido** | TAG24 + malha | Não. Sinal breve TAG33 às 19:02 (1 min, atraso 0, 300 ‰) | Igual ao sem regime | **Sim**, **TAG27 + TAG28** às **19:02**, atraso **0**, 300 ‰ (4 min seguidos). TAG31/32 alarmaram 28 dos 30 min, mas ficaram excluídas por **1 alarme cada às 18:53** |
| 25 Sem atualização | TAG34 | Fora do replay (não monitorada) | — | — |
| 26 Circuito aberto | TAG29 | Não pela regra: TAG29 **excluída** (alarmou 20:41–20:43); alarme contínuo desde **20:55** (atraso 3) | Igual ao sem regime | **Sim**, 20:52, atraso **0**, 1000 ‰ (20 min seguidos) |
| 27 Saturação | TAG23 | **Sim**, 21:32, atraso **0**, 1000 ‰ (20 min) | **Sim**, igual | **Sim**, igual |
| **Detectadas** (de 7 monitoradas) — regra antiga | | **3** (22, 23, 27) | **3** (22, 23, 27) | **3** (23, 26, 27) |
| **Detectadas** — **mapa estendido** | | **3** (22, 23, 27) | **3** (22, 23, 27) | **5** (20, 23, 24, 26, 27) |

**Cuidado ao ler o mapa estendido:** os sinais nas bombas vêm do processo real (§4.2), mas a mesma combinação de tags também alarma em picos de demanda (P03; §2.3). A regra dos 20 min antes protege em parte: na falha 24, TAG31/32 ficaram fora justamente por um alarme de demanda às 18:53. Cada detecção pelo mapa estendido aparece no relatório do placar com a observação `efeitosPorCanal, P41`.

**Alarmes (minutos-tag), classificados pelo placar** (a soma dá o total; atribuídos e coincidências em "regra antiga → mapa estendido"; o total e os falsos fora não dependem do mapa):

| Configuração | Total | Atribuídos às falhas | Coincidências (falha ativa, outra tag) | **Falsos fora das falhas** | Tarde (15h–17h) | Noite (18h–22h) | Madrugada (00h–05h) |
|---|---|---|---|---|---|---|---|
| Sem regime | 568 | 38 → 45 | 319 → 312 | **211** (7,1 %) | 125 (12,0 %) | 86 (4,4 %) | **—** |
| Regime recomendado | 595 | 38 → 45 | 309 → 302 | **248** (8,3 %) | 190 (18,3 %) | 58 (3,0 %) | **—** |
| Combinado + set-point | 324 | 53 → **119** | 234 → **168** | **37** (1,2 %) | 15 (1,4 %) | 22 (1,1 %) | **—** |
| Combinado real (`combr`) | 324 | 53 → **119** | 234 → **168** | **37** (1,2 %) | 15 (1,4 %) | 22 (1,1 %) | **—** |

- **Base:** 2.990 minutos-tag fora das falhas (230 min × 13 tags): 1.040 à tarde e 1.950 à noite. Pela definição do placar, tudo isso é "dia" (06h–23h).
- **Madrugada: não se aplica.** O replay vai de 15:00 a 22:59; a madrugada de 13/11 está no treino, e usá-la no replay seria avaliar sobre os dados de treino. O resultado de madrugada com treino limpo é o de 12/11 (§2.2).
- **Coincidências** misturam efeitos reais da falha em outras tags (bombas nas falhas 20 e 24; sucção e bombas com o nível baixo na 23) e alarmes de estado (bomba parada ou sozinha). Com o mapa estendido, as do `combs` caem de 234 para 168. As que sobram estão quase todas em duas falhas. Na **24 (94)**: TAG31/32, excluídas pela regra dos 20 min, e sucção e válvula de entrada com o nível ainda baixo. Na **23 (56)**: sucção e bombas com o nível real baixo e a B-02 desarmando; o mapa não estende essa falha.

**Falsos fora das falhas, por tag e causa:**

| Tag | Sem regime | `rec` | `combs` | Quando e por quê |
|---|---|---|---|---|
| TAG27/29/31 (B-01) | 25 / 56 / 61 | 25 / 56 / 61 | 0 / 0 / 0 | B-01 **parada** (15:00–15:21, 15:52–16:03, 16:52–17:04, 21:12–21:31) e B-01 **sozinha** (20:39–20:43): **P02/P03**. A B-01 não tem regime no `rec` |
| TAG28/30/32 (B-02) | 9 / 13 / 7 | **34 / 22 / 19** | 2 / 1 / 2 | `rec`: B-02 **sozinha** com a B-01 parada (15:00–16:03, 16:52–17:04, 21:13–21:17), regime "B-02 ligada" dominado pelo paralelo: **P03**. `combs`: 16:00–16:03, B-02 sozinha em carga alta logo antes de a auxiliar partir (16:04) [hipótese: demanda, P03] |
| TAG26 vazão de entrada | 32 | 23 | 23 | 15:07–15:17 (11 no `rec`/`combs`): logo depois do reset de 15:02, vazão de entrada 889 → 966 com a válvula aberta, até ela fechar às 15:18 [hipótese: transitório de partida, CENARIOS §3.4]. O resto: abertura e fechamento da válvula (**P04**) |
| TAG22 nível | 4 | 4 | 4 | 18:42–18:45, logo depois da falha 23, com o nível real ainda no mínimo (≈ 347): efeito residual da falha |
| TAG23 sucção | 4 | 4 | 4 | 18:53 e 19:35–19:37, nível real ainda baixo depois da falha 23 (**P22**) |
| TAG31/32 em `combs` | — | — | 1 / 1 | 18:53: pico de demanda (vazão 948) com o nível baixo (P03, P22) |
| **Total** | **211** | **248** | **37** | |

- No `combs`, **11 dos 37** vêm do transitório do reset e **10** do efeito residual da falha 23. Os outros **16** são falsos "de operação": válvula de entrada (12) e B-02 sozinha em carga alta (4).
- Contra o teste de 12/11 (§2.2, só os minutos de dia), a taxa de falsos caiu nas três configurações (sem regime ≈ 11 % → 7,1 %; `combs` ≈ 2,0 % → 1,2 %). Os períodos têm estados diferentes, então a comparação é só de ordem de grandeza.

### 4.5 Falhas não detectadas e motivo (item 4)

| Falha | Configurações sem detecção | Motivo | Evidência |
|---|---|---|---|
| 20 Congelado (TAG24) | Todas pela regra antiga; sem regime e `rec` também com o mapa estendido | **P12**: não há detector de valor congelado. A leitura congelou em **450**, o próprio set-point e a média do perfil (449,5): Z, MAD, EWMA, persistência e mudança de patamar ficaram em **0 nos 30 min**; nota máx. 3,4. Efeito real só nas bombas (P41): com o mapa estendido, o `combs` detecta na TAG32 aos 14 min; sem regime e `rec`, não | Confirmado no replay e nos dados |
| 21 Drift (TAG25) | Todas | A vazão subiu, mas ficou **dentro da faixa normal** (máx. 869; p99 do treino 924; desvio do perfil 216,6, inflado pela demanda do dia): mesma família do **P06**. EWMA (99,7) e mudança de patamar (100) reagiram às 16:43, mas Z (31) e MAD (20) não: nota **53,4**, abaixo de 60 (**P07**). A TAG25 não tem regime em nenhuma configuração. A severidade ficou em rampa e não passou de 590 ‰ | Confirmado no replay. **Na relação vazão × potência** (ajuste linear no treino, resíduo σ = 23,7), o desvio vai a **+106 (≈ 4,5 σ)** na segunda metade da falha, contra −30 antes e −26 depois [análise de dados, não é detector da IA] |
| 22 Ruído (TAG27) | `combs`/`combr` | Com o regime certo, os valores ruidosos ficam **dentro do normal do regime**: B-01 sozinha 240,6 ± 14,5 com leituras de 227–268; no paralelo (178,8 ± 7,0), o Z chega a 100 em minutos soltos, mas o **EWMA fica em 0** (ruído de média zero) e a persistência em 0–10: nota máx. 62,8, só 2 alarmes (**P07**, como previsto na §2.5) | Confirmado no replay |
| 22 Ruído (TAG27) | Sem regime / `rec` (detecção pela regra) | **Provável coincidência (P21, P03)** [hipótese forte]: os 7 alarmes caem só em 17:17–17:27, exatamente com a **B-02 parada** e a B-01 sozinha em carga maior (TAG27 227–268 contra o perfil global 121,7 ± 85,9), e param quando a B-02 volta (17:29). A B-01 sozinha alarma igual **sem** a falha (20:57 e 20:59, mesmo replay) | Coincidência no tempo confirmada nos dados e no replay; a causa é hipótese |
| 23 Intermitente (TAG22) | — (todas detectaram, com 16 min) | O primeiro salto a 1000 (18:04) deu 1 min de alarme (sinal breve). A sequência de 5 min começa em **18:18**, com as leituras a 0 | Confirmado no replay |
| 24 Ganho − (TAG24) | Todas pela regra antiga; sem regime e `rec` também com o mapa estendido | **A malha corrige a leitura**: a TAG24 fica em 445–453 (nota máx. 5,1). O erro vai para as bombas (P41). Com o mapa estendido, o `combs` detecta no 1º minuto (TAG27/28); sem regime e `rec`, o perfil largo da B-01/B-02 não separa o degrau (+35 na frequência) | Confirmado no replay e nos dados |
| 25 Sem atualização (TAG34) | Todas | **Tag não monitorada** (`IaAtiva`; fora do replay). Congelou em 529 por 40 min; mesmo monitorada, cairia no P12 | Confirmado nos dados |
| 26 Circuito aberto (TAG29) | Sem regime / `rec` | **Regra P21 + P03:** a TAG29 alarmou 20:41–20:43 com a **B-01 sozinha** (B-02 parada 20:35–20:43), então ficou excluída. A falha em si foi vista: alarme **contínuo de 20:55 a 21:11** (atraso 3 min; MAD 100, Z 64). **Bomba parada:** a B-01 parou no rodízio das 21:05, no meio da falha. No `combs`, o alarme continuou mesmo assim, porque a vibração da B-01 parada não é 0 (31 ± 6 no regime) e o circuito aberto dá 0. O risco do P20 (bomba parada bloqueando os detectores) **não** se concretizou aqui | Confirmado no replay e nos dados |

**Resumo dos motivos:**
- **P12 (sem detector de valor congelado):** 20 (e 25, se fosse monitorada).
- **Falha dentro da faixa normal / diluição (P06, P07):** 21; 22 com regime combinado.
- **Sensor dentro de malha fechada (P41):** 20 e 24 não aparecem na própria tag. Com o mapa estendido, o combinado as detecta nas bombas; sem regime e o `rec`, não.
- **Tag não monitorada:** 25.
- **Regra de avaliação + estado da bomba (P21, P03):** 26 sem regime e com o `rec`; e a "detecção" da 22 nessas duas configurações é provavelmente coincidência.

### 4.6 Conferência e limitações

- Replay: 4 execuções, 0 erros, 0 divergências. Placar: soma das classes = total nas 8 execuções (4 com a regra antiga e 4 com o mapa estendido). `combr` = `combs` em todas as colunas comparáveis.
- **Regra antiga reproduzida:** o placar com o mapa de `226fca9` e o código novo deu `_eventos.csv`, `_por_tag.csv` e `_falsos_por_hora.csv` **idênticos byte a byte** aos da primeira análise, nas 4 configurações. Ou seja, o código novo não muda nada para um mapa sem `efeitosPorCanal`.
- **Mudança na ferramenta (16/11, aprovada):** `tools/Cp300OfflineReplay/Cp300Placar.cs` (seção opcional `efeitosPorCanal`: validação de canal existente e só `TAGnn` sem rótulos; inclusão das tags quando o token `CANAL` resolve para o canal; observação no evento), `placar_falhas_cp300.json` (canal 3) e `README.md` §6. Testes em `IAApi.Tests/Cp300Replay/Cp300PlacarTests.cs`: 1 teste de placar (canal 3 conta a malha, canal 4 não), 4 casos de validação e asserções do P41 no teste do mapa do repositório. `dotnet test --filter Cp300`: **49/49**. Sensibilidade: sem a inclusão das tags extras (mutação temporária, restaurada e conferida com `cmp`), 2 testes falham. Runtime da IAApi: sem mudança.
- **Simulador e replay, não planta nem IA real.** O replay não reproduz Isolation Forest, multivariado, latência, o ciclo sequencial (P11, P26) nem a entrada inválida (P31). **As decisões da IA real de 13/11 não foram exportadas**, então não há comparação com ela.
- O mapa estendido usa as tags que o controle de pressão mexe. Essas tags também reagem à demanda (P03), então uma detecção por elas é menos específica que uma detecção na tag do sensor. Por isso as duas leituras ficam lado a lado.
- A ligação entre a falha 23, a queda real do nível e o desarme da B-02 está confirmada no tempo; o mecanismo (controle e proteção usando o LT-01) é hipótese, a confirmar no código do simulador (`Docs/Simulador/SIM_ANOMALIAS_CP300.c`).
- Uma execução só, com uma semente do simulador. Atrasos com resolução de 1 min.

*Scripts de análise (fora do repositório, no scratchpad da sessão):* `p3b_rotulos.py`, `p3b_falhas.py`, `p3b_det.py`, `p3b_alvo.py`, `p3b_min.py`, `p3b_coinc.py`, `p3b_eq.py`, `p3b_run.sh`, `placar_estendido.json` (cópia usada antes da aprovação, também com o nível na falha 23; substituída pelo mapa versionado) e `placar_mapa_HEAD.json` (mapa de `226fca9`, para a regra antiga). Saídas em `dados/p3b_{sem,rec,combs,combr}.{csv,stdout}` e `dados/placar_p3b_*` (não versionados).
