# Resultado — Preset 1 (SEED 1500, 11/11/2026)

## 1. Dados

**Fonte:** `dados/cp300_10a11nov.csv` (cliente PK2, 22 tags, de 10/11 12:30 a 11/11 17:39 = 1.750 min, 1.742 pontos por tag).
**Janela de treino usada:** de 10/11 12:55 (inclusive) a 11/11 12:55 (exclusive) = 1.440 min, conforme definido no pedido. *Não verifiquei no código como a janela é calculada.*
**Limpeza:** reproduzi `HistoricoLimpezaService.RemoverOutliersPorIqr` em Python (percentil com interpolação, limites Q1 − 1,5·IQR e Q3 + 1,5·IQR, inclusivos); a média e o desvio são populacionais, como em `TagProfileStatisticsCalculator`.
**Nível de evidência:** tudo abaixo é **confirmado nos dados do CSV**, exceto o que está marcado como *inferência*. Nada aqui diz se a IA detectou a falha ou não.

### 1.1 Falha do preset 1 (slot A)

| Item | Valor observado |
|---|---|
| Código (TAG61) | **02, obstrução do recalque** |
| Alvo (TAG62) | **0, bomba principal** (TAG44 = 1 → **B-01**) |
| TAG73 = 1 | **13:28 → 13:39** (12 amostras) |
| Fase 2, rampa (TAG64) | 13:28 → 13:37; severidade 57 → 777 ‰, +80 ‰/min |
| Fase 3, plena | não aparece; a rampa passa direto para a recuperação |
| Fase 4, recuperação | 13:38 (514 ‰) → 13:39 (114 ‰); a partir de 13:40 tudo volta a 0 |
| Severidade máxima amostrada (TAG63) | 777 ‰ (alvo nominal: 800 ‰) |

*Inferência pelas inclinações:* com a rampa de 80 ‰/min e a recuperação de 400 ‰/min (800 ‰ em 120 s), a falha começou em **≈13:27:17**, chegou a 800 ‰ em ≈13:37:17 e terminou em **≈13:39:17**. Pela tabela do preset (falha no minuto 30), o cenário teria começado em **≈12:57:17**, e não às 12:56. *Nota (seção 2):* a TAG22 salta de 518 para 600 (nível de 60 %, o reset do cenário) entre 12:55 e 12:56. Isso sugere que o início real foi ≈12:55–12:56 e que a falha começou ≈31–32 min depois do início, e não aos 30. A divergência não muda as conclusões.

Fora do preset 1: entre **15:28 e 17:17** o arquivo tem outro evento. Slot A = código 10 (desbalanceamento), alvo 1, 900 ‰; slot B (a partir de 16:08) = código 09 (perda de lubrificação), alvo 1, 1000 ‰; em 16:52 a TAG44 passa de 1 para 2 (failover). Pelo padrão, esse evento é do **preset 6**. Esse trecho não pode entrar em nenhum treino.

### 1.2 TAG73 na janela de treino

| Verificação | Resultado |
|---|---|
| Minutos com TAG73 = 1 | **0** |
| Minutos com TAG61 ≠ 0 / TAG64 ≠ 0 | 0 / 0 |
| Slot B (TAG65–68) ≠ 0 | 0 (só aparece em 11/11 a partir de 16:08) |

**Não houve nenhuma falha rotulada na janela de treino.** O treino é limpo do ponto de vista dos rótulos.

### 1.3 Minutos faltando

Faltam os **mesmos 8 minutos em todas as 22 tags**, inclusive as de rótulo. Isso indica uma lacuna na coleta ou na exportação, e não uma falha de uma tag específica.

| # | Minuto faltando | Onde cai |
|---|---|---|
| 1 | 10/11 12:52 | Antes da janela de treino |
| 2 | 10/11 14:26 | Treino |
| 3 | 10/11 17:33 | Treino |
| 4 | 10/11 23:08 | Treino |
| 5 | 11/11 05:46 | Treino |
| 6 | 11/11 07:30 | Treino |
| 7 | 11/11 08:35 | Treino |
| 8 | 11/11 09:04 | Treino |

Assim, a janela de treino tem 1.433 de 1.440 pontos (−0,5 %). **Nenhum minuto falta no cenário** (12:55 → 14:22).

### 1.4 Bombas

| Item | Observado |
|---|---|
| Principal durante a falha (TAG44) | **B-01** (TAG44 = 1 de 11/11 12:48 até 16:52) |
| A reserva B-02 ligou durante a falha? | **Sim, de 13:37 a 13:41.** TAG28 sai de 0 para 179 e TAG32 de 0 para 435; ao mesmo tempo, TAG27/TAG31 da B-01 caem de 251/487 para 181/435. A partida acontece no último minuto da rampa (777 ‰) e a B-02 desliga às 13:42. |
| Essa partida é típica da falha? | **Não.** A B-02 também ligou sem nenhuma falha de 12:55 a 13:00 e de 14:00 a 14:05, com valores iguais (TAG28 ≈ 175–179, TAG32 ≈ 393–399). |
| Efeito da falha na B-01 | Pequeno. O pico da TAG31 foi 487 às 13:36, e o mesmo valor apareceu às 13:58 sem falha. O pico da TAG33 foi 151 às 13:37, e 154 às 13:58 sem falha. |

Na janela de treino, a principal alternou 6 vezes: a **B-02 foi a principal em 894 min** e a **B-01 em 539 min**. A reserva ficou ligada em 560 min, por exemplo de 10/11 17:34 a 20:42 e de 11/11 06:38 a 08:43.

### 1.5 Limpeza de outliers (IQR) na janela de treino

| Tag | Pontos | Removidos | Removidos com TAG73 = 1 | TAG73 = 1 que ficaram | Removidos normais (TAG73 = 0) | Média / desvio sem limpeza | Média / desvio com limpeza |
|---|---|---|---|---|---|---|---|
| TAG22 | 1433 | 0 | 0 | 0 | 0 | 602,3 / 108,9 | 602,3 / 108,9 |
| TAG23 | 1433 | 0 | 0 | 0 | 0 | 1349,4 / 53,7 | 1349,4 / 53,7 |
| **TAG24** | 1433 | **305** | 0 | 0 | **305** | 434,9 / 28,7 | **449,6 / 2,5** |
| TAG25 | 1433 | 0 | 0 | 0 | 0 | 561,7 / 209,8 | 561,7 / 209,8 |
| TAG26 | 1433 | 0 | 0 | 0 | 0 | 562,2 / 397,7 | 562,2 / 397,7 |
| TAG27 | 1433 | 0 | 0 | 0 | 0 | 116,8 / 89,3 | 116,8 / 89,3 |
| **TAG28** | 1433 | **374** | 0 | 0 | **374** | 140,1 / 85,8 | **184,0 / 34,7** |
| TAG29 | 1433 | 0 | 0 | 0 | 0 | 104,8 / 58,6 | 104,8 / 58,6 |
| **TAG30** | 1433 | **499** | 0 | 0 | **499** | 125,6 / 59,6 | **147,8 / 16,8** |
| TAG31 | 1433 | 0 | 0 | 0 | 0 | 254,0 / 192,9 | 254,0 / 192,9 |
| **TAG32** | 1433 | **353** | 0 | 0 | **353** | 300,5 / 175,3 | **398,7 / 40,5** |
| TAG33 | 1433 | 0 | 0 | 0 | 0 | 100,1 / 38,6 | 100,1 / 38,6 |

O que foi removido (todos os pontos são de operação normal):

| Tag | Trecho removido | Explicação provável |
|---|---|---|
| TAG24 | 300 pontos de 11/11 00:01 a 05:00 (372–417), mais 5 pontos isolados | **Set-point noturno** da pressão, que é um regime normal |
| TAG28 / TAG30 / TAG32 | 353 pontos de cada (10/11 20:43 → 11/11 00:42, 04:43 → 06:37, 12:48 → 12:53) | **B-02 parada como reserva** (valores 0–5 na corrente e 0–1 na frequência) |
| TAG28 / TAG30 | 21 / 146 pontos altos, em picos de carga ou vibração da B-02 como principal | Picos normais de carga |
| TAG27 / TAG29 / TAG31 | Nada | Como a B-01 ficou parada 25–35 % do tempo, Q1 = 0 e o IQR fica tão largo que nada sai |

### Conclusão em linguagem simples

- **A limpeza não está tirando falhas**, porque não havia nenhuma falha no treino (TAG73 = 0 o tempo todo). Pelo mesmo motivo, **também não está deixando falha passar**.
- **Ela está tirando dados normais**: 1.531 pontos no total, ou até 35 % de uma tag. Ela trata como "erro" dois regimes legítimos da planta: a **pressão noturna** (TAG24) e a **bomba B-02 parada como reserva** (TAG28/30/32).
- **Ela também age de forma desigual entre as bombas.** Nas tags da B-02, o perfil passou a descrever só a "B-02 ligada" (desvio 4 a 5 vezes menor). Nas tags da B-01, que ficou mais tempo parada, o perfil continua misturando "ligada" e "parada", com desvio enorme (TAG31: 254 ± 193).
- **Consequência possível (hipótese, não testada aqui):**
  - Com a B-01 como principal durante o preset 1, a TAG32 = 0 da reserva parada fica a cerca de 10 desvios da média limpa, e a TAG24 noturna a cerca de 15 desvios. Isso favorece **alarmes falsos** em operação normal.
  - Na B-01, a obstrução elevou a TAG31 só até 487, cerca de 1,2 desvio da média, e a TAG27 até cerca de 1,6 desvio. Isso dificulta **detectar a falha**.
  - Confirmar essas duas hipóteses exige cruzar com os limiares reais do detector e com as anomalias registradas pela IAApi.

*Scripts de análise (fora do repositório):* `an.py`, `iqr.py` e `rem.py`, no scratchpad da sessão. Nenhum código do projeto foi alterado.

## 2. Resultado da IA

**Fonte:** `dados/replay_preset1.csv`, gerado pela ferramenta `tools/Cp300OfflineReplay`. São 91 minutos (11/11 12:55 → 14:25), 13 tags (TAG22–TAG33 e TAG44), 6 detectores por leitura e limiar de 60.
**O que o replay reproduz** (confirmado no código da ferramenta): o `PredictionService` real, com o perfil treinado em 24 h e a decisão legada do ensemble. **Não reproduz:** Isolation Forest, multivariado, baseline por regime nem latência. Portanto, isto é o comportamento do **replay offline**, e não um log da IAApi em tempo real.
**Faixas usadas** (seção 1):
- **Falha:** 13:28–13:39 (TAG73 = 1).
- **Transitório:** 12:56–13:06, os primeiros ~10 min após o reset do nível.
- **Reserva B-02:** ligada de 13:37 a 13:41.

### 2.1 Tabela por tag

| Tag | O que mede | Detectou a obstrução? | Nota máx. na falha / fora dela | Alarmes na falha | Alarmes no transitório | Alarmes no resto | Por quê |
|---|---|---|---|---|---|---|---|
| **TAG27** | Corrente B-01 (principal) | **Não** | 57,7 / 72,5 | 0 | 0 | 15 (13:07–09, 13:12–14, 13:17, 13:22, 13:47, 13:56–58, 14:07–09) | Ver 2.2 (diluição sobre um sinal que não se destaca) |
| **TAG31** | Frequência B-01 | **Não** | 51,4 / 62,2 | 0 | 0 | 3 (13:08, 13:11–12) | Idem |
| **TAG33** | Potência da estação | **Não** | 40,0 / 45,6 | 0 | 0 | 0 | Idem; o pico de 151 às 13:37 fica abaixo do 154 normal das 13:58 |
| **TAG28** | Corrente B-02 (reserva) | **Não** (a partida às 13:37 teve nota 33) | 67,3 / 86,7 | 4 (13:28–29, 13:34–35)* | 6 (13:01–06) | 51 | Alarma sempre que a B-02 está **parada** (valor 0) |
| **TAG32** | Frequência B-02 | **Não** (nota 43 às 13:37) | 73,3 / 86,7 | 9 (13:28–36)* | 6 | 58 | Idem |
| **TAG30** | Vibração B-02 | **Não** | 81,4 / 91,2 | 9 (13:28–36)* | 6 | 58 | Idem |
| TAG29 | Vibração B-01 | Não (não era esperado reagir) | 51,1 / 73,0 | 0 | 0 | 5 (13:12, 13:18, 13:22–23, 14:08) | Mesmo efeito da TAG27 |
| TAG26 | Vazão de entrada | Não (não era esperado reagir) | 81,0 / 77,5 | 8 (13:28–35)* | 0 | 6 (13:22–27) | Válvula de entrada fechada de 13:17 a 13:35 (operação normal) |
| TAG22 | Nível | Não (não era esperado reagir) | 60,5 / 55,1 | 1 (13:35)* | 0 | 0 | Nível caindo por causa da válvula de entrada fechada |
| TAG23, TAG24, TAG25 | Sucção, recalque, vazão | Não | ≤ 58,8 | 0 | 0 | 0 | Nenhum alarme |
| TAG44 | Bomba principal (estado) | — | 0 | 0 | 0 | 0 | Classificada como `Discrete`; todos os detectores bloqueados (`BlockedByApplicabilityPolicy`). Essa tag não está na lista monitorada do PK2 (seção 2 do CENARIOS_CP300) |

\* **Coincidências, não detecções.** Esses alarmes caem dentro do intervalo da falha, mas a causa é outra:
- **TAG28/30/32:** a reserva estava parada, como desde 13:01.
- **TAG26/22:** a válvula de entrada fechou às 13:17, 11 min antes da falha.

Quando a reserva de fato ligou (13:37), que era o efeito esperado da obstrução, as notas **caíram** (23–43). Uma comparação automática minuto a minuto com a TAG73 contaria esses minutos como acertos falsos.

**Atraso de detecção:** não se aplica, porque nenhuma tag reagiu à obstrução.

### 2.2 O que impediu a detecção (TAG27, TAG31, TAG33)

| Mecanismo | Impediu? | Evidência no replay |
|---|---|---|
| Tipo da tag | **Não** | Tipo `Unknown` com confiança 0, então não houve roteamento por tipo. Os 6 detectores ficaram aplicáveis e participaram em todos os minutos. |
| Deadband | **Não** | Deadband de 11, 10 e 14, pequeno perto dos desvios (~120 na TAG27). Só zera o MAD quando o valor fica perto da mediana (~170, com as duas bombas ligadas). |
| Entrada inválida | **Não** | `entrada_invalida = 0` em 100 % das linhas, sem erros. |
| **Diluição** | **Sim, como mecanismo imediato** | Na TAG27 às 13:30, EWMA = 100 e Taxa de variação = 100, mas Z = 53, MAD = 69, Persistência = 0 e Mudança de patamar = 0, o que dá nota final **57,7 < 60**. Com Persistência e Mudança de patamar zeradas, a nota máxima possível é 73,3. |
| **Causa de fundo: o sinal não se destaca do normal** | **Sim** | Os valores da falha (TAG27 ≤ 258, TAG31 ≤ 487, TAG33 ≤ 151) se repetem em minutos normais. A nota máxima **fora** da falha é maior que a de **dentro** em todas as três tags. O EWMA fica em 100 o tempo todo porque o perfil (TAG27: média 117 ± 89) mistura "bomba ligada" e "bomba parada" (seção 1.5). Por isso ele não distingue nada. |

Baixar o limiar não resolveria. Na TAG27, por exemplo, um limiar de 57 acenderia primeiro em vários minutos normais (13:07–13:22, 13:56–58) e só então na falha.

### 2.3 Alarmes falsos fora da falha

| Grupo | Tags e minutos | Causa provável |
|---|---|---|
| **Transitório** (12:56–13:06) | TAG28, TAG30, TAG32: 13:01–13:06 | A B-02 desliga às 13:01 depois da partida. É a mesma causa do grupo seguinte; não é algo próprio do transitório. |
| **Reserva parada** | TAG28 (51), TAG30 (58), TAG32 (58): 13:07–13:27, 13:42–13:58, 14:06–14:25 | O perfil dessas tags foi treinado **sem** os zeros, porque o IQR os removeu (seção 1.5). Com isso, "B-02 parada" vira um desvio enorme (Z, MAD e EWMA = 100; Persistência = 100 depois de ~5 min). |
| **B-01 sozinha em carga alta** | TAG27 (15), TAG29 (5), TAG31 (3): 13:07–13:23, 13:47, 13:56–58, 14:07–09 | O perfil mistura bomba ligada e parada, então o EWMA fica sempre em 100. Picos de carga ligados ao liga/desliga da auxiliar somam Taxa de variação e Mudança de patamar e passam de 60. |
| **Válvula de entrada fechada** | TAG26: 13:22–13:27 (e 13:28–13:35, dentro da falha) | A válvula ficou fechada (valor 0) de 13:17 a 13:35. Persistência, MAD e Mudança de patamar subiram a 100. É operação normal: 18 % de zeros no treino. |
| — | TAG22: 13:35 | Consequência da TAG26, com o nível caindo sem entrada (nota 60,5) |

Total fora da falha e do transitório: **196 minutos-tag de alarme**, contra 0 detecções reais.

### 2.4 Conferência

| Item | Resultado |
|---|---|
| `conferencia` | **`ok` em todas as 7.098 linhas** (1.183 leituras): sem divergência de decisão, de nota nem da soma das contribuições |
| `decisao_final` vs. `decisao_ensemble` | Iguais em 100 % das leituras |
| `erro` | Nenhum |

### Resumo

1. A IA **não detectou** a obstrução do recalque em nenhuma tag.
2. A falha foi fraca: as tags da bomba B-01 subiram só até valores que a planta atinge normalmente.
3. Os detectores até reagiram a ela, mas com a mesma força que reagem em operação normal; a nota ficou abaixo do limite de 60.
4. A IA disparou **muitos alarmes falsos** (196), quase todos por três motivos de operação normal:
   - a bomba reserva parada;
   - a bomba principal trabalhando sozinha com carga alta;
   - a válvula de entrada fechada.
5. Os alarmes que caíram por acaso dentro do horário da falha **não eram a falha**.
6. A raiz está no treino: a limpeza retirou da bomba B-02 o estado "parada", e o perfil da B-01 mistura ligada e parada (seção 1).
7. Tipo da tag, deadband e entrada inválida **não** tiveram papel nisso.
8. A ferramenta de replay conferiu suas contas e não encontrou divergência.
9. Limitação: replay offline sem multivariado, Isolation Forest, baseline por regime nem latência.

*Script de análise (fora do repositório):* `rp.py`, no scratchpad da sessão. Nenhum código do projeto foi alterado.

## 3. Hipótese: baseline por regime

**Data:** 12/11/2026. **Nenhum código ou configuração foi alterado.**
**Legenda:** **[código]** = lido no repositório nesta sessão; **[dados]** = calculado sobre `dados/cp300_10a11nov.csv`; **[estimativa]** = cálculo aproximado com *proxies* (ver 3.3); **[a validar]** = depende do banco ou do Bridge, que não foram consultados.

### 3.1 Como funciona, em linguagem simples

**Ideia.** Uma mesma tag tem "normais" diferentes conforme o estado do equipamento: a corrente da B-02 é ~0 com a bomba parada e ~180 com ela ligada. Em vez de um perfil único que mistura os dois, o baseline por regime compara a leitura atual **só com o passado em que o equipamento estava no mesmo estado**.

**Tag de regime.** É uma tag de estado (número inteiro, como 0 = parada, 2 = operando) que diz "em que situação" a tag analisada está. Ela é cadastrada por tag analisada na tabela `TagsRegimesOperacionaisIa` (`Models/TagRegimeOperationalIa.cs`) [código]:

| Campo | Para que serve | Padrão |
|---|---|---|
| `ClienteId`, `TagName` | Tag analisada (ex.: PK2 / TAG27) | — |
| `TagRegime` | Tag de estado (ex.: TAG42) | — |
| `Ativo` | Liga/desliga | `true` |
| `LagMinutos` | Atraso entre o estado e o efeito na tag | 0 |
| `ToleranciaAlinhamentoMinutos` | Idade máxima da leitura de estado usada | 2 |
| `ToleranciaEstado` | Diferença máxima para considerar "mesmo estado" | 0,001 (na prática, igualdade exata) |
| `MinimoAmostrasPorRegime` | Mínimo de pontos do mesmo estado para aplicar | 30 |

Há um índice único em (`ClienteId`, `TagName`) (`Data/AppDbContext.cs`): **cada tag aceita uma única tag de regime** [código].

**Passo a passo, a cada leitura** (`PredictionDetectionPreparationService.PrepararAsync` → `OperationalRegimeBaselineProvider.Resolve` → `OperationalRegimeBaselineService.Resolve` → `OperationalRegimeHistoryResolver.Resolve`) [código]:

1. Busca a configuração da tag. Se não houver linha, ou se `Ativo = false`, segue com o perfil global sem aviso nenhum.
2. Busca no Bridge (`BuscarHistoricoPorPeriodo`) o histórico da tag de regime nas mesmas 24 h do histórico da tag analisada.
3. Descobre o **estado atual**: a última leitura da tag de regime até `agora − Lag`, desde que não seja mais antiga que `ToleranciaAlinhamentoMinutos`. Nunca usa leitura futura.
4. Para cada ponto das últimas 24 h da tag analisada, descobre o estado naquele minuto (mesma regra) e **guarda só os pontos no mesmo estado atual**.
5. Se sobrarem menos de `MinimoAmostrasPorRegime` (30) pontos, desiste e usa o perfil global.
6. Sobre esses pontos, calcula média, desvio e deadband com a **mesma matemática do treino** (`TagProfileStatisticsCalculator`, com limpeza IQR).

**Como a decisão usa isso** [código]:
- A média, o desvio, o deadband e **o próprio histórico entregue aos detectores** passam a ser os do regime. Os 6 detectores, os pesos e o limiar de 60 não mudam.
- **Não é um treino.** O baseline do regime é recalculado a cada leitura sobre as últimas 24 h, e a `TagPerfilIa` treinada não é usada naquela leitura. Isso acompanha mudanças, mas também absorve deriva lenta e falhas longas.
- O histórico filtrado tem **buracos** (os períodos em outro estado). A taxa de variação e o EWMA usam o tempo real entre os pontos; logo depois de uma troca de estado, o "ponto anterior" pode ser de horas atrás. A mudança de patamar usa janelas de 20 + 10 min e, logo após a troca, pode ficar sem pontos suficientes (não aplicável).
- Quando o regime é aplicado, o baseline comportamental não é tentado.
- Qualquer falha (Bridge, poucos pontos, estado desconhecido) volta ao perfil global (*fail-open*) e não bloqueia a detecção.

### 3.2 Por que não está ativo para o PK2 e o que seria preciso configurar

**O código está ligado para todos os clientes.** `AddOperationalRegimeServices` é chamado em `Services/DependencyInjection/IAApiApplicationServiceCollectionExtensions.cs:53` e o provedor é injetado na preparação. Não há chave no `appsettings.json` [código]. O que decide é **existir uma linha ativa na tabela** para a tag.

Motivos prováveis para não estar ativo:

| Motivo | Evidência |
|---|---|
| No **replay**, o provedor nem é instanciado (`new PredictionDetectionPreparationService(history)`, sem regime) | [código] `tools/Cp300OfflineReplay/Cp300Replay.cs`, linhas 153 e 348 |
| Na **produção**, provavelmente não há linhas do PK2 em `TagsRegimesOperacionaisIa`: não existe endpoint nem tela para cadastrar (nenhum controller usa a tabela), nem seed. Só haveria linhas se alguém as tivesse inserido direto no banco | [código] para a falta de endpoint; [a validar] no banco (tarefa 2 do BACKLOG, inventário) |
| As tags de estado (TAG42/43/45) **nunca foram exportadas** nos CSVs deste preset, e não sabemos se o Bridge as devolve em `/historico` | [dados]; [a validar] no Bridge |

**O que configurar (exige aprovação, porque é mudança de configuração em banco real):**

1. Confirmar que a migração `20260917195823_AddTagRegimeOperacional` está aplicada no `ia_config.db` do PK2.
2. Confirmar que o Bridge devolve histórico de TAG42, TAG43 e TAG45 **com o mesmo formato de nome** de `TagsIaConfig.TagName` (com ou sem o prefixo `PK2.`).
3. Inserir uma linha por tag analisada (8 linhas). Exemplo: `PK2 | TAG27 | TAG42 | Ativo=1 | Lag=0 | Tolerância=2 | ToleranciaEstado=0,5 | Mínimo=30`. Como os estados são inteiros, `ToleranciaEstado` de 0,5 evita problemas de arredondamento sem misturar estados.
4. Conferir no log `BASELINE POR REGIME APLICADO` (ou no PERF_DIAG, etapa `Preparation.RegimeBaseline`) se o regime foi de fato aplicado.
5. **Nunca** usar TAG60–TAG81 como `TagRegime`: seria vazamento direto do gabarito (já registrado em `AUDITORIA_STATUS.md`). **Hoje o código não bloqueia isso**; a proteção é só de procedimento.

### 3.3 O que a proposta provavelmente resolveria

**Proposta avaliada:** TAG42 (estado B-01) → TAG27/29/31; TAG43 (estado B-02) → TAG28/30/32; TAG45 (estado XV-01) → TAG26/TAG22.

**Como estimei** [estimativa]. TAG42/43/45 não estão no CSV, então usei *proxies*: B-01 "ligada" = TAG31 ≥ 50; B-02 "ligada" = TAG32 ≥ 50; válvula "aberta" = TAG26 ≥ 50. As três séries são claramente bimodais (~0 ou ≥ 300). Para cada minuto de 12:55 a 14:25, reproduzi a regra do código (24 h anteriores, mesmo estado, mínimo de 30, IQR, desvio populacional, deadband P95) e contei os minutos com **desvio ≥ 3σ** além do deadband. **Isso mede só a "distância da média" (Z, MAD e, em parte, EWMA); não é o ensemble.**

| Tag | Minutos ≥ 3σ, perfil atual (falha / transitório / resto) | Idem, com a proposta | Leitura |
|---|---|---|---|
| TAG28 | 9 / 6 / 58 | 0 / 0 / 5 | Reserva parada deixa de ser anomalia |
| TAG30 | 9 / 6 / 58 | 0 / 0 / 0 | Idem |
| TAG32 | 9 / 6 / 58 | 0 / 0 / 0 | Idem |
| TAG27 | 0 / 0 / 0 | **9 / 4 / 51** | **Piora**: B-01 sozinha vira anomalia |
| TAG29 | 0 / 0 / 0 | **9 / 4 / 51** | Idem |
| TAG31 | 0 / 0 / 0 | **10 / 3 / 51** | Idem |
| TAG26 | 0 / 0 / 0 | 1 / 2 / 7 | Válvula fechada vira normal; vazão alta com válvula aberta passa a se destacar |
| TAG22 | 0 / 0 / 0 | 0 / 0 / 0 | Sem mudança nesta medida |

No perfil atual, TAG27/29/31 e TAG26 não passam de 3σ porque o desvio está inflado. Os alarmes delas no replay vieram de EWMA, taxa de variação, persistência e mudança de patamar (ver 2.3).

**Por que a B-01 piora** [dados]. Nas 24 h de treino, os estados (B-01, B-02) foram: ambas ligadas em **560 min**, só a B-02 em **520 min** e só a B-01 em **353 min**. Com a B-01 ligada, a corrente TAG27 fica em ~179 em paralelo e em ~230–250 sozinha. O regime "B-01 operando" mistura os dois casos. Como o paralelo é maioria, o IQR corta a operação sozinha e o desvio cai para ~10. Às 13:24, **antes da falha**, a TAG27 já está a 5,7σ. É a mistura **principal/reserva**, que uma só tag de regime não separa — e a tabela aceita uma só.

**Teste com regime combinado** (B-01 × B-02, 4 estados; **o código hoje não suporta**) [estimativa]:
- Os alarmes da B-01 caem para 0–1 minuto por tag, e os da B-02 ficam entre 0 e 6.
- Mas, contra o regime certo ("B-01 sozinha": média 195 ± 32), a TAG27 na falha fica em **1,3–2,0σ**. A obstrução não se distingue da operação normal da B-01 sozinha.
- A única marca na falha é às **13:37**, quando a B-02 parte: TAG29 (vibração da B-01) = 202, contra 142 ± 9 em paralelo (6,9σ); TAG31, TAG30 e TAG32 ficam entre 4,6 e 5,5σ. Na partida normal das 14:00 isso não aparece.
- *Pode* ser efeito da falha (777 ‰), mas é um minuto só, e justamente um minuto de transição.

**Resumo por problema do preset 1:**

| Problema (seção 2) | Com a proposta | Confiança |
|---|---|---|
| Reserva B-02 parada (TAG28/30/32: 51–58 alarmes + 6 no transitório + 9 coincidências na falha) | **Provavelmente resolve.** Sobram poucos minutos com ruído de 3–4 na corrente parada (TAG28) | Média-alta |
| Válvula de entrada fechada (TAG26: 6 + 8 na falha) | **Provavelmente resolve** o período com a válvula fechada. Pode criar alarmes com vazão alta e válvula aberta (13:03–13:13, até 4,8σ) e na abertura (13:34) | Média |
| Nível caindo com a válvula fechada (TAG22, 13:35) | **Incerto.** O alarme veio da taxa de variação e da mudança de patamar, que esta estimativa não mede. Condicionar o nível à válvula faz sentido físico, mas o nível é acumulado e depende de quanto tempo a válvula ficou fechada | Baixa |
| B-01 sozinha com carga alta (TAG27/29/31: 15/5/3) | **Não resolve e provavelmente piora** (~51 min ≥ 3σ por tag) | Média-alta |
| Falta de detecção da obstrução (TAG27/31/33) | **Não resolve.** Com TAG42, a falha fica alta, mas igual aos minutos normais antes dela. Com o regime combinado, a falha fica dentro do normal. TAG33 (potência da estação) não está na proposta. No máximo, há um sinal isolado às 13:37 | Média-alta |

**Riscos do mecanismo que a estimativa não captura** [código + hipótese]:
- **Estados de transição** (Partindo/Parando nas bombas; Abrindo/Fechando na válvula) duram 1–2 min. Com igualdade exata, devem ter menos de 30 amostras em 24 h e cair no **perfil global** justamente nas partidas e paradas, que são os minutos de maior variação. Isso pode gerar alarmes em toda partida.
- **Estado 4 = Falha:** se o CLP marcar a bomba em falha, esse regime terá poucas amostras e cairá no perfil global. Se a falha for longa (mais de 30 min nas 24 h), passa a fazer parte do "normal" daquele estado.
- **A falha explicada pelo regime:** se a falha provocar a troca de estado (por exemplo, a partida da reserva), o regime trata o novo estado como normal. No preset 1 isso é desejável nas tags da B-02, mas esconde esse sintoma.

**Limitações da estimativa:**
- Os proxies saem das próprias tags: o alinhamento é perfeito por construção, sem estados de transição nem atraso.
- Mede só o desvio em σ: sem taxa de variação, persistência, mudança de patamar, EWMA completo nem ensemble.
- Cobre um único preset.

**Só o replay com as tags reais (3.4) confirma ou descarta.**

### 3.4 Esforço para incluir o modo no replay

**Código (só em `tools/Cp300OfflineReplay`, nada do runtime):** pequeno, cerca de 80–120 linhas e um teste, uns meio dia de trabalho.

1. Opção nova `--regime TAG27=TAG42,TAG29=TAG42,…`. Se for útil, também `--regime-minimo`, `--regime-lag` e `--regime-tolerancia-estado`.
2. **Recusar** qualquer `TagRegime` de TAG60–TAG81, com o `IsLabelTag` que já existe, e recusar uma tag como regime de si mesma.
3. Inserir as linhas em `TagsRegimesOperacionaisIa` no SQLite em memória. A tabela já é criada pelo `EnsureCreated`.
4. Montar `OperationalRegimeBaselineProvider(db, history, new OperationalRegimeBaselineService(new OperationalRegimeHistoryResolver(), new TagProfileStatisticsCalculator(new HistoricoLimpezaService())))` e passá-lo às **duas** instâncias de `PredictionDetectionPreparationService` (produção e detalhamento). Senão, a `conferencia` acusa divergência.
5. O histórico não precisa de mudança:
   - `CsvHistorySource.BuscarHistoricoPorPeriodo` já existe.
   - O CSV carrega todas as tags, inclusive as que não são reproduzidas.
   - A coluna `origem_baseline` já vem de `BaselineEfetivo.Kind`.
   - Vale acrescentar as colunas `valor_regime` e `amostras_regime`.
6. Atualizar o README (seções 3 e 4).

**Dados (o bloqueio real):** o CSV atual **não tem TAG42, TAG43 nem TAG45**. É preciso reexportar o mesmo período (10/11 12:30 → 11/11 17:39) com TAG41–TAG45. O script `Export-Cp300Historico.ps1` já inclui TAG22–TAG51 por padrão, mas **acessa o Bridge e exige autorização**. Sem isso, só dá para rodar com proxies: serve de ensaio, não de validação.

**Testes de aceite sugeridos:**
- Sem `--regime`, o replay continua idêntico ao atual (`replay_preset1.csv`).
- Com `--regime`, `conferencia = ok` em todas as linhas.
- `--regime TAG27=TAG73` é recusado.
- Comparar os alarmes com e sem regime, por tag e por faixa, na mesma tabela da seção 2.

### 3.5 Pressão noturna: TAG46 como regime da TAG24

**O que a TAG46 é** [código do simulador, `Docs/Simulador/SIM_ANOMALIAS_CP300.c`]:
- TAG46 = W45 = `PSP/10`, o set-point **em uso**, em bar × 100. É a **mesma unidade da TAG24**: 450 equivale a 4,50 bar.
- O alvo é `PSX = (SPO > 0 ? SPO : (noite e PSN > 0 ? PSN : PSD))`, com `PSD = 4500` mbar de dia e `PSN = 3800` mbar entre `PNH = 0` h e `PNE = 5` h.
- O set-point em uso sobe ou desce em rampa de **20 mbar/s**: a troca 450 ↔ 380 leva cerca de 35 s.
- Os cenários podem fixar outro set-point (`SPO = QSP×10`); o cenário 5, por exemplo, usa 380 e 500.

Isso descreve o **simulador**. Não confirma como o CLP real se comporta.

**O que os dados mostram** [dados, TAG24, janela de treino]:

| Regime | Pontos | Média ± desvio | Removidos pelo IQR dentro do regime |
|---|---|---|---|
| Noite (00:01–05:00) | 299 | **379,5 ± 3,1** | 0 |
| Dia | 1.134 | **449,6 ± 2,4** | 19 |
| Perfil global atual (IQR na série toda) | 1.128 usados | 449,6 ± 2,5 | **305**: a noite inteira |

A transição é limpa: 00:00 = 450, **00:01 = 388**, 00:02 = 380; 05:00 = 376, **05:01 = 447**. Só um minuto intermediário em cada troca.

**Problema atual (hipótese forte, fora da janela do preset 1).** O perfil global descreve só o dia. A noite fica a cerca de **28σ** (|379,5 − 449,6| / 2,5) e deve alarmar **nos 300 minutos de toda madrugada**. O replay do preset 1 (12:55–14:25) não mostrou isso porque foi todo de dia: a TAG24 não teve nenhum alarme ali. Para confirmar, basta um replay cobrindo 00:00–05:30.

**Com TAG46 como regime** [estimativa, horário como proxy do set-point]:
- **Provavelmente resolve** os alarmes noturnos. Cada regime fica com um baseline estreito (desvio de 2–3) e o IQR não remove nada dentro deles. A sensibilidade também melhora: um desvio de 10 na pressão passa a valer cerca de 3σ, tanto de dia quanto de noite.
- Esta é a tag em que a proposta tem **mais chance de funcionar bem**. O regime é discreto, tem poucos valores, os dois estados ficam muito separados e cada um tem muitos pontos.

**Riscos:**

| Risco | Efeito provável | Mitigação possível |
|---|---|---|
| Amostra da TAG46 no meio da rampa (por exemplo, 412) | Esse valor quase não ocorre e tem menos de 30 pontos, então volta ao perfil global. A TAG24 em transição (388) fica a cerca de 25σ: **1 alarme por troca** | `ToleranciaEstado` maior não resolve, porque misturaria os regimes. `LagMinutos = 1` alinha melhor, já que a pressão segue o set-point com atraso do PID. O resto precisa ser verificado no replay |
| Set-point novo, que não aparece nas 24 h (por exemplo, 500 num cenário) | Durante os **primeiros 30 min** volta ao perfil global (cerca de 20σ), ou seja, ~30 alarmes. Depois, o regime se forma com os próprios minutos novos | `MinimoAmostrasPorRegime` menor reduz a janela, mas deixa o baseline mais frágil |
| A falha mexe na pressão e o PID compensa | O regime não ajuda nem atrapalha. A TAG24 controlada tende a reagir pouco a falhas, porque o PID mantém a pressão | — |

**Alternativa melhor no futuro (não suportada hoje):** analisar o **erro de controle**, TAG24 − TAG46. As unidades são iguais, a série fica centrada em ~0 em qualquer set-point e não tem transições. Exigiria uma tag derivada ou um novo tipo de regime, ou seja, mudança de código.

### 3.6 A limpeza IQR dentro de cada regime

**Sim, a limpeza já é feita dentro de cada regime** [código]:
- `OperationalRegimeBaselineService.Resolve` passa ao `TagProfileStatisticsCalculator.Calculate` **somente os pontos do regime atual**, e o `Calculate` chama `HistoricoLimpezaService.RemoverOutliersPorIqr` sobre esse subconjunto.
- Média, desvio e deadband saem do subconjunto limpo.
- O **histórico entregue aos detectores não é limpo**: é o subconjunto do regime inteiro (comentário em `OperationalRegimeBaselineService.cs`, linhas 110–116). Então a mediana do MAD, o EWMA, a taxa de variação, a persistência e a mudança de patamar veem os pontos que o IQR removeria.

**Quanto o IQR remove dentro de cada regime** [estimativa, proxies da seção 3.3, janela de treino]:

| Tag / regime | Removidos | Comparação com o IQR global (seção 1.5) |
|---|---|---|
| TAG24 noite / dia | 0 de 299 / 19 de 1.134 | Global: 305 (a noite inteira) |
| TAG28 B-02 parada / ligada | 24 de 353 / 16 de 1.080 | Global: 374 |
| TAG30 parada / ligada | 1 de 353 / 109 de 1.080 | Global: 499 |
| TAG32 parada / ligada | 14 de 353 / 21 de 1.080 | Global: 353 |
| TAG26 válvula fechada / aberta | 17 de 476 / 39 de 957 | Global: 0 (desvio inflado) |
| **TAG27 B-01 ligada** | **142 de 913** | Global: 0 |
| **TAG31 B-01 ligada** | **167 de 913** | Global: 0 |

O resultado é que **resolve o problema da seção 1.5 nas tags com regimes bem definidos**: a noite e a reserva parada deixam de ser cortadas. Mas aparecem três problemas novos.

1. **Sub-regime dentro do regime (B-01).** Em "B-01 ligada", o IQR remove a operação da B-01 **sozinha** (142–167 pontos), porque o paralelo é maioria. O desvio cai para ~9–10 e a B-01 sozinha vira anomalia (seção 3.3). É o mesmo defeito da seção 1.5, um nível abaixo. **A limpeza não tem como corrigir isso; o que corrige é um regime mais completo.**
2. **Regime quase constante (bomba parada).** Na TAG32 e na TAG31 paradas, os valores são 0 (96 %) e 1 (4 %). Então Q1 = Q3 = 0 e o IQR vale 0: **todo valor diferente de zero é removido**. O desvio vira 0 e o deadband calculado também.
   - Hoje isso só não gera alarme por um detalhe do código: com deadband 0, o `BaselineProtectionProfile` usa o P95 das variações do histórico **não limpo**, que vale 1. Assim, leituras 0 e 1 ficam dentro do deadband.
   - Uma leitura de **2** com a bomba parada daria Z = 2 / (1/3) = 6σ, ou seja, nota 100 no Z-Score. Na TAG28 parada (valores de 0 a 5, deadband 2), leituras de 3–4 já passam de 4σ.
   - É uma proteção frágil, que existe por acaso [código + estimativa].
3. **O perfil global continua ruim e continua sendo usado.** Sempre que o regime não se aplica, volta a `TagPerfilIa`, treinada pelo `PerfilTagService.TreinarPerfil` com o IQR sobre a série **misturada**. Isso acontece em transições, com menos de 30 pontos, em set-point novo, em falha do Bridge e em tags sem configuração. O defeito da seção 1.5 continua exatamente nesses minutos, que são os de maior variação.

**O que precisaria mudar** (propostas; nada implementado, todas exigem aprovação):

| # | Mudança | Onde | Resolve | Risco |
|---|---|---|---|---|
| a | Nada para ter o IQR por regime: já é assim | — | Seção 1.5 nas tags com regime | — |
| b | Não remover nada quando o IQR for 0 (ou quase 0), ou usar um piso de escala absoluto (resolução do sensor) | `HistoricoLimpezaService` ou `TagProfileStatisticsCalculator`, de preferência **só no caminho do regime** (uma opção no `Calculate`) | Problema 2 | `TagProfileStatisticsCalculator` também é usado no treino. Mudar sem opção altera todos os perfis |
| c | Regime combinado (duas tags de estado, ou uma tag de estado derivada) | Modelo `TagRegimeOperacionalIa` (o índice único hoje é por tag) e o resolver | Problema 1 (B-01 sozinha × paralelo) | Mudança de esquema e de migração |
| d | No fallback, não usar o perfil global misturado. Por exemplo, treinar `TagPerfilIa` por regime, ou não aplicar IQR a séries multimodais/com maioria de zeros (opção (b) da seção 4 do BACKLOG) | `PerfilTagService` / `TagProfileStatisticsCalculator` | Problema 3 | Altera perfis de todos os clientes; precisa de replay em mais de um preset |
| e | Tratar os minutos de transição (Partindo/Parando, rampa de set-point): manter o regime anterior por N minutos ou suprimir o alarme ali | Resolver de regime | Alarmes nas trocas (3.3 e 3.5) | Pode esconder falhas que acontecem justamente na partida |

**Ordem sugerida para testar no replay, depois que ele tiver o modo regime (3.4):** primeiro (a) puro, só configurando; depois (b), que é pequeno e local; e só então (c), (d) e (e), conforme o que sobrar.

### Resumo

1. O baseline por regime compara cada leitura só com o passado no mesmo estado do equipamento. Ele é recalculado a cada leitura sobre as últimas 24 h. Mudam a média, o desvio, o deadband e o histórico dos detectores; os detectores e o limiar continuam iguais.
2. O código já está ligado; falta configuração: uma linha por tag em `TagsRegimesOperacionaisIa` (só pelo banco, não há endpoint) e as tags de estado com histórico no Bridge. Não verifiquei a produção.
3. Com TAG42/43/45 (estimativa por proxy, só desvio em σ):
   - **provavelmente resolve** a reserva B-02 parada (o maior grupo de alarmes falsos) e a válvula fechada na TAG26;
   - **não resolve, e piora,** a B-01, porque "B-01 operando" ainda mistura a bomba sozinha e em paralelo;
   - **não faz a obstrução aparecer**: contra o regime certo, ela continua dentro do normal.
4. Para a B-01 seria preciso um regime combinado (B-01 × B-02, ou bomba principal), e o código hoje só aceita uma tag de regime por tag.
5. Os estados de transição (partindo/parando) devem cair no perfil global e podem gerar alarmes nas partidas.
6. No replay, a mudança é pequena: cerca de meio dia, só na ferramenta. O bloqueio é reexportar o CSV com TAG41–TAG45, o que exige acesso autorizado ao Bridge.
7. **TAG46 → TAG24 é o melhor candidato.** Os dois regimes são limpos (noite 379,5 ± 3,1; dia 449,6 ± 2,4) e devem eliminar ~300 alarmes por madrugada, que o preset 1 não mostrou por ser diurno. Sobram cerca de 1 alarme por troca de set-point e ~30 min de perfil global quando aparece um set-point novo. A TAG46 também precisa entrar na reexportação.
8. **A limpeza IQR já é feita dentro de cada regime.** Isso corrige a noite e a reserva parada, mas:
   - corta a B-01 sozinha dentro de "B-01 ligada";
   - zera o desvio quando a bomba está parada (protegida hoje só por acaso, pelo deadband automático);
   - deixa o perfil global ruim em todo minuto em que o regime não se aplica.

   Mudanças possíveis, em ordem: tratar IQR = 0 só no caminho do regime, regime combinado e perfil de fallback por regime.

*Scripts de estimativa (fora do repositório):* `regime.py` e `regime3.py` a `regime6.py`, no scratchpad da sessão. Nenhum código do projeto foi alterado.

## 4. Teste do baseline por regime

**Data:** 12/11/2026. **Nível de evidência:** replay offline (`tools/Cp300OfflineReplay`) sobre dados do simulador. **Não é validação em planta** e não inclui Isolation Forest, multivariado nem latência.

### 4.1 O que foi feito

| Etapa | Resultado |
|---|---|
| Dados | `dados/estados_10a11nov.csv` (TAG41, 42, 43, 45 e 46; 1.742 pontos cada) juntado a `dados/cp300_10a11nov.csv`: **47.034 linhas, 0 duplicadas, 0 conflitantes**. Cópia do original guardada no scratchpad da sessão. Estados observados: TAG42/43 = 0, 2 ou 3 (Parando, 2 min); TAG45 = 0–3; TAG46 = 380 ou 450, sem valores intermediários. |
| Ferramenta | Nova opção `--regime TAGx=TAGy` (repetível). Recusa TAG60–TAG81, uma tag como regime de si mesma, tag analisada fora do replay e tag de regime ausente do CSV. Usa o `OperationalRegimeBaselineProvider` **real** nas duas instâncias da preparação, com os padrões do modelo (lag 0, tolerância 2 min, `ToleranciaEstado` 0,001, mínimo 30). Duas colunas novas no fim do CSV: `valor_regime` e `motivo_baseline`. **Nada do runtime foi alterado.** Arquivos: `Cp300Replay.cs`, `Program.cs`, `README.md`. |
| Testes | 2 testes novos em `IAApi.Tests/Cp300Replay/Cp300OfflineReplayTests.cs`: o regime é aplicado e conferido, e são recusados rótulo, tag de si mesma e formato inválido. `dotnet test --filter Cp300OfflineReplayTool`: **9 aprovados, 0 falhas** (7 antigos + 2 novos). A suíte completa não foi rodada. |
| Regressão | O replay do preset 1 **sem** `--regime` é **idêntico** ao `replay_preset1.csv` da seção 2 (1.183 leituras, 0 linhas diferentes nas colunas antigas). |
| Conferência | Nos 4 replays: `conferencia = ok`, **0 erros e 0 divergências**. |

**Configuração testada:** TAG28/30/32 ← TAG43; TAG27/29/31 ← TAG42; TAG26 ← TAG45; TAG24 ← TAG46. A **TAG22 ficou sem regime**, porque não estava na lista pedida. As demais tags rodaram iguais nos dois casos.

**Arquivos de saída:** `dados/replay_preset1_sem_regime.csv`, `dados/replay_preset1_com_regime.csv` e `dados/replay_preset1_regime_recomendado.csv`; e, para a madrugada, `dados/replay_madrugada_sem_regime.csv`, `dados/replay_madrugada_com_regime.csv` e `dados/replay_madrugada_regime_recomendado.csv`. Nos 6 replays: 0 erros e 0 divergências.

### 4.2 Preset 1 (11/11 12:55–14:25, treino até 12:55)

Minutos com alarme (`decisao_final = 1`). Mesmas faixas da seção 2: **falha** 13:28–13:39, **transitório** 12:56–13:06 e **resto**.

Três configurações:
- **Sem regime:** nenhuma tag de regime.
- **Regime completo:** as 8 tags, incluindo a B-01 com TAG42.
- **Regime recomendado:** só TAG28/30/32 ← TAG43, TAG26 ← TAG45 e TAG24 ← TAG46. A **B-01 fica sem regime**.

| Tag | Sem regime: falha / trans. / resto | Regime completo | Regime recomendado | Efeito do recomendado |
|---|---|---|---|---|
| TAG28 corrente B-02 (← TAG43) | 4 / 6 / 51 | **0 / 0 / 0** | **0 / 0 / 0** | ✅ Alarmes da reserva parada eliminados |
| TAG30 vibração B-02 (← TAG43) | 9 / 6 / 58 | **0 / 0 / 0** | **0 / 0 / 0** | ✅ Idem |
| TAG32 frequência B-02 (← TAG43) | 9 / 6 / 58 | **0 / 0 / 0** | **0 / 0 / 0** | ✅ Idem |
| TAG26 vazão de entrada (← TAG45) | 8 / 0 / 6 | 0 / 6 / 11 | 0 / 6 / 11 | ↔ A válvula fechada (13:17–13:35) deixou de alarmar, mas surgiram 16 alarmes com a válvula aberta e vazão alta (13:01–13:16) e 1 às 13:47 |
| TAG27 corrente B-01 | 0 / 0 / 15 | 9 / 5 / **56** | 0 / 0 / 15 | = Igual a sem regime |
| TAG29 vibração B-01 | 0 / 0 / 5 | 8 / 2 / **52** | 0 / 0 / 5 | = Igual a sem regime |
| TAG31 frequência B-01 | 0 / 0 / 3 | 10 / 4 / **54** | 0 / 0 / 3 | = Igual a sem regime |
| TAG24 pressão de recalque (← TAG46) | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 | = Sem mudança (de dia não havia problema) |
| TAG22, 23, 25, 33, 44 | 1 / 0 / 0 (TAG22) | igual | igual | = Sem regime |
| **Total (13 tags)** | **31 / 18 / 196 = 245** | **28 / 17 / 173 = 218** (−11 %) | **1 / 6 / 34 = 41 (−83 %)** | |

No regime recomendado, os 41 alarmes que sobram são:
- B-01 sozinha com carga alta: TAG27, 15; TAG29, 5; TAG31, 3;
- TAG26, 17;
- TAG22, 1 (13:35, nível caindo).

Nenhum deles é a obstrução.

O único minuto **sem** regime nas tags da B-02 é **13:41**, com TAG43 = 3 (Parando). Esse estado tem poucas amostras e caiu no perfil global; nesse minuto não houve alarme.

**A obstrução foi detectada? Não.** Os alarmes da B-01 dentro da falha (TAG27: 9, TAG29: 8, TAG31: 10) **não são detecção**:
- os alarmes da B-01 seguem exatamente a B-02: **sempre que a B-02 está desligada** (TAG43 = 0 em 13:01–13:36, 13:42–13:58 e 14:06–14:25), TAG27/29/31 alarmam quase todo minuto. Quando a B-02 liga (13:37–13:41, 13:59–14:05), os alarmes param;
- os alarmes começam às **13:02, 26 min antes da falha**, e continuam depois dela (13:42–14:25) do mesmo jeito.

Ou seja, o regime "B-01 operando" (TAG42 = 2) ainda mistura a bomba sozinha e em paralelo, exatamente como estimado na seção 3.3. Com isso, o alarme passou a significar **"B-01 está sozinha"**, e não falha. Nenhuma tag teve no intervalo da falha um comportamento diferente do resto do replay.

### 4.3 Madrugada (11/11 00:00–05:30, treino até 11/11 00:00)

Faixas: **noite** = 00:01–05:00 (set-point 380); **resto** = 00:00 e 05:01–05:30. TAG73 = 0 em todo o período: não houve falha.

| Tag | Sem regime: noite / resto | Regime completo | Regime recomendado | Efeito |
|---|---|---|---|---|
| **TAG24 pressão** (← TAG46) | **300 / 0** | **30 / 0** | **30 / 0** | ✅ **Alarme noturno confirmado e eliminado.** Os 30 que sobram são 00:01–00:30, ver abaixo |
| TAG26 vazão de entrada (← TAG45) | 210 / 5 | 5 / 0 | 5 / 0 | ✅ Sobram minutos de abertura/fechamento da válvula (01:28 = Abrindo, sem regime) |
| TAG27 corrente B-01 | 199 / 0 | 27 / 0 | **199 / 0** | Completo: melhora (sobram 00:02–00:38 e 04:44–04:56). **Recomendado: igual a sem regime** |
| TAG29 vibração B-01 | 131 / 0 | 0 / 0 | **131 / 0** | Completo: ✅. **Recomendado: igual a sem regime** |
| TAG31 frequência B-01 | 236 / 0 | 60 / 0 | **236 / 0** | Completo: melhora (sobram 00:01–00:42 e 04:43–05:00). **Recomendado: igual a sem regime** |
| TAG28 corrente B-02 (← TAG43) | 14 / 6 | 0 / 0 | 0 / 0 | ✅ |
| TAG30 vibração B-02 (← TAG43) | 15 / 30 | 0 / 0 | 0 / 0 | ✅ |
| TAG32 frequência B-02 (← TAG43) | 56 / 31 | 10 / 0 | 10 / 0 | ✅ Melhora; sobram 00:44–00:55 |
| TAG22, 23, 25, 33, 44 | 2, 42, 1, 4, 0 | iguais | iguais | = Sem regime (a TAG23, sucção, teve 40 alarmes noturnos; fora do escopo) |
| **Total (13 tags)** | **1.282** | **181** (−86 %) | **660** (−49 %) | |

**A B-01 se comporta de forma oposta nas duas janelas:**
- **De dia (preset 1):** sem regime, ela tem só 23 alarmes. Com TAG42, tem 190, porque o regime "B-01 ligada" fica dominado pela operação em paralelo.
- **De madrugada:** sem regime, ela tem **566 alarmes** (TAG27 199 + TAG29 131 + TAG31 236), porque o perfil global misturado não cobre a carga noturna. Com TAG42, tem 87.

Nenhuma das duas opções serve para a B-01: a correção certa é o regime combinado (BACKLOG, item 12). Os 566 alarmes noturnos da B-01 sem regime **não tinham sido medidos antes** (a seção 2 só cobriu o dia). Eles mudam a avaliação da seção 2: o problema da B-01 não é só "sozinha com carga alta", mas também o set-point noturno.

**Por que sobram alarmes:**
- **TAG24, 00:01–00:30.** O CSV começa às 12:30 do dia 10, então à 00:01 não existe nenhuma noite nas 24 h anteriores. O regime "380" só passa a ter as 30 amostras mínimas às 00:31; até lá vale o perfil global (diurno), e cada minuto alarma. **Em produção, com a noite anterior no histórico, isso provavelmente não aconteceria** [hipótese]. O mesmo vale para qualquer set-point novo (seção 3.5).
- **B-01 e B-02 sozinhas com set-point noturno.** Os alarmes da TAG27/31 caem exatamente onde a B-01 opera **sozinha à noite** (00:01–00:42 até o rodízio; 04:43–05:00 depois do rodízio de volta). Os da TAG32 caem logo depois de a B-02 assumir (00:43). É o mesmo problema do preset 1, agora com um terceiro fator: bomba × outra bomba × set-point.
- **Transição de set-point às 05:01 sem alarme.** Como a TAG46 não teve valores intermediários, a troca não caiu no perfil global.

**Limitação desta rodada:** o treino "até 00:00" teve só 11,5 h de dados (673 pontos usados na TAG24), porque o CSV começa às 12:30 do dia 10. O perfil global da TAG24 (449,6 ± 2,4) é praticamente o mesmo do treino de 24 h, porque na seção 1.5 o IQR já removia a noite inteira. Por isso, o alarme noturno sem regime é representativo do que aconteceria em produção.

### 4.4 Comparação com a estimativa da seção 3

| Previsão (§3.3/§3.5) | Replay |
|---|---|
| B-02 parada deixa de alarmar | ✅ Confirmado: 0 alarmes |
| Válvula fechada deixa de alarmar, mas a vazão alta com válvula aberta passa a alarmar | ✅ Confirmado |
| B-01 piora (~51 min por tag) | ✅ Confirmado: 52–56 min por tag |
| A obstrução não aparece | ✅ Confirmado |
| TAG24 alarma a noite inteira sem regime; com regime, sobram as trocas e o período sem histórico | ✅ Confirmado: 300 → 30. A troca em si não alarmou |

### Resumo

1. **Funciona onde o estado é bem definido.** A bomba reserva parada (TAG28/30/32), a válvula fechada (TAG26) e a pressão noturna (TAG24) deixaram de gerar alarme falso. Na madrugada, os alarmes caíram de **1.282 para 181**. Os **300 alarmes noturnos da TAG24** foram confirmados e eliminados, exceto nos primeiros 30 min, por falta de uma noite anterior no arquivo.
2. **Piora a bomba B-01.** Com uma só tag de estado, "B-01 ligada" mistura a bomba sozinha e acompanhada. Por isso a IA passou a alarmar **toda vez que a B-02 desliga** (52–56 minutos por tag no preset 1, contra 3–15 antes). No total do preset 1, os alarmes caíram só 11 % (245 → 218).
3. **A obstrução continua sem ser detectada.** Os alarmes que caem no horário da falha começam 26 min antes dela e seguem depois: são da "B-01 sozinha", não da falha.
4. **Próximos passos** (acrescentados ao BACKLOG, itens 11–13):
   - bloquear no código TAG60–TAG81 como tag de regime;
   - regime combinado para as bombas (qual bomba está ligada, e também o set-point);
   - analisar TAG24 − TAG46 como alternativa ao regime da pressão.
5. **Regime recomendado** (só TAG28/30/32 ← TAG43, TAG26 ← TAG45 e TAG24 ← TAG46; B-01 sem regime):
   - preset 1: **245 → 41 alarmes (−83 %)**;
   - madrugada: **1.282 → 660 (−49 %)**.

   Dos 660 noturnos, **566 são da B-01**, que fica como estava. Com TAG42, a B-01 melhora de noite (566 → 87), mas piora de dia (23 → 190). Nenhuma das duas opções resolve a B-01; só o regime combinado (BACKLOG, item 12) pode resolver.
6. **Não foi para produção.** Ativar o regime exige cadastro no banco do PK2 e aprovação. As cinco tags do regime recomendado melhoram nas duas janelas, sem nenhuma piora. A B-01 continua com alarmes falsos em qualquer opção atual.

*Scripts de comparação (fora do repositório):* `merge.py`, `cmp.py` e `tl.py`, no scratchpad da sessão.
