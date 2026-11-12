//==============================================================================
//
//                M A R R A R I  Automacao Industrial Ltda.
//
//     Projeto: Simulador de Anomalias - Estacao de Bombeamento (Booster)		xx00
//   Descricao: Planta simulada + controle real + injecao de falhas p/ teste de IA
//    Hardware: CPU300 + ESD (Posicao 1) + 3x ESA (Posicoes 2...4)
//    Software: SIM_ANOMALIAS_CP300.c
//    Customiz: ___
//     Cliente: Bancada de Laboratorio (saidas fisicas DESCONECTADAS)
//      Cidade: ___
//       Autor: Marrari - Simulador IA
//        Data: 29/10/26 - Versao 1.00 - Out/26 - Software SIM_ANOMALIAS_CP300.c
//
//
// ___________________________ IDENTIFICACAO DO PRODUTO ________________________
//
//				VERSAO ORIGINAL:
//					FML - Familia de Produtos (100=MR-100)
//					CTZ - ABCDD (A:_ / B:_ / C:Variacao (1/2/3) / DD:Aplicacao)
//					TBV - Versao da Tabela de Variaveis (0-Standard)
//					VSF - AABBC - 12.34c Versao do Software Instalado
//					DSF - ABBCC - A:_ / BB:Mes / CC:Ano do Software Instalado
//
//				CUSTOMIZACOES:
//					CTZ= --100		MR301-AAA_CP300.c	Chassi V1
//					CTZ= --200		MR301-BBB_CP300.c	Chassi V2
//					CTZ= --200		MR301-CCC_CP300.c	Chassi V3
//					CTZ= -----		---					---
//
// **************************** LISTA DE ALTERACOES ****************************
//
//	29/10/26	BASE: MR301_CP300.c				(Chassi - Versao 2.00 - Ago/26)
//				NOVO: SIM_ANOMALIAS_CP300.c		(Versao 1.00 - Out/26)
//				>> Simulador de anomalias e falhas para teste de software de deteccao (IA)
//				xx10		Planta: reservatorio, 2 bombas c/ inversor, rede, motores (MAQ 26)
//				xx11		Transmissores 4-20 mA / PT100 c/ ruido e falhas de sensor (MAQ 27)
//				xx12		Controle real: nivel, sequenciamento, PID de pressao, protecoes (MAQ 3...10)
//				xx13		Publicacao W21...W50 c/ falhas de comunicacao + rotulos W59...W80 (MAQ 11)
//				xx14		Cenarios, falhas em 3 slots e banco de eventos BDE (MAQ 12...14, 28...30)
//
// ____________________________ DESCRITIVO DO PROCESSO _________________________
//
//	Reservatorio de 50 m3 (altura util 4 m) alimentado pela valvula on/off XV-01 (90 m3/h).
//	Duas bombas centrifugas identicas (B-01/B-02, motores 18,5 kW / In=34,5 A) com inversor,
//	em paralelo, recalcam para uma rede de consumo (estatica 1,5 bar). O PID 0 regula a
//	pressao de recalque PT-02 variando a velocidade. A bomba principal (DTY) parte primeiro;
//	a auxiliar entra com velocidade >= SAS por TAS s e sai com velocidade < SAP por 2xTAS s.
//	Rodizio da principal a cada ROT horas de operacao; falha da principal -> reserva assume.
//	Consumo: perfil diario DPT[0...24] (permil) x passeio aleatorio, ou fixo pelo cenario.
//	Set-point noturno PSN entre PNH e PNE horas (gestao de pressao).
//
//	Modo simulador (SMLX=13579 + TST=1): MAQ 26 calcula a planta a cada 0,2 s e escreve as
//	ED's de campo; MAQ 27 gera as EA's com ruido, quantizacao de 12 bits e falhas de sensor.
//	O controle (MAQ 3...10) le EA/ED como num equipamento real. TST=2 congela a planta.
//
// ___________________________ CATALOGO DE FALHAS (Codigo) _____________________
//
//	Alvo (W04 / campo FTG): bomba 0=principal no instante da injecao, 1=B-01, 2=B-02;
//	sensor k=1...18 (ver EA's), +100 = sentido negativo (deriva, erro de ganho, saturacao).
//	Intensidade 0...1000 permil; Modo: 0=Subita 1=Rampa linear 2=Rampa acelerada 3=Intermitente.
//	Escalas de tempo p/ coleta de 1 amostra/min do PSi4: falhas e fases de minutos, nao de segundos.
//
//	PROCESSO	01 Parada inesperada da bomba (falha do inversor)			[bomba]
//				02 Obstrucao no manifold de recalque (ate +4 bar a 100 m3/h)
//				03 Obstrucao do filtro de succao (-> cavitacao)
//				04 Perda de rendimento / desgaste do rotor (-30% altura, -20 pts rend.)	[bomba]
//				05 Sobrecarga mecanica do motor (atrito, ate +6 kW)		[bomba]
//				06 Vazamento na linha de recalque (apos FT-01, ate 25% da cond. nominal)
//				07 Vazamento no reservatorio (ate ~9 m3/h)
//				08 Falha de ventilacao do motor (ate -80% da refrigeracao)	[bomba]
//				09 Falta de lubrificacao do mancal (+65 'C; dano > 50% eleva vibracao)	[bomba]
//				10 Desbalanceamento / desalinhamento (+7 mm/s a 100% rot.)	[bomba]
//				11 Valvula de entrada XV-01 travada (limite de abertura = 1000-intens.)
//				12 Oscilacao da malha de pressao (consumo oscilante 6 min, +-25%)
//	SENSOR		20 Travado		21 Deriva (ate 20% da faixa)	22 Ruido excessivo (ate 10x)
//				23 Intermitente (quedas 0/4095 de 20...120 s, ate 1 a cada 5 min)	24 Valor incorreto (erro de ganho ate 30%)
//				25 Sem atualizacao (retem 2 ate ~32 min)	26 Circuito aberto	27 Saturacao
//	COMUNICACAO	30 Interrupcao (W21...W50 + heartbeat W39 parados)
//				31 Atraso (ate 7,5 min)	32 Perda de amostras (blocos de 1...3 min, ate ~50%)
//				33 Dado congelado (W21...W50 parados, heartbeat ativo)
//	ROTULOS W59...W80 e o BDE NUNCA sao afetados pelas falhas de comunicacao.
//
// ******************************* CUSTOMIZACOES *******************************
//
//						VERSAO STANDARD - SEM CUSTOMIZACOES
//
// *****************************************************************************
//
//  ALARMES: 01 - Falha Bomba B-01
//			 02 - Falha Bomba B-02
//			 03 - Temperatura Alta Enrolamento M-01
//			 04 - SALVAR Dados no PC (Fim de Memoria do CLP)
//			 05 - Temperatura Alta Enrolamento M-02
//			 06 - Vibracao Alta B-01
//			 07 - Vibracao Alta B-02
//			 08 - Emergencia (ED3)
//			 09 - Nivel Muito Baixo no Reservatorio (Bloqueio das Bombas)
//			 10 - Pressao de Succao Baixa (Cavitacao)
//			 11 - Pressao de Recalque Baixa (Desvio > PDL por 60 s)
//			 12 - Pressao de Recalque Alta (> PDH)
//			 13 - Falha da Valvula de Entrada XV-01
//			 14 - Temperatura Alta Mancal B-01
//			 15 - Temperatura Alta Mancal B-02
//			 16 - Sobrecorrente M-01
//			 17 - Sobrecorrente M-02
//			 18 - Falha de Sensor (Fora de Faixa)
//			 19 - Nivel Alto no Reservatorio (LSH)
//			 20 - Livre
//			 21...99 - Livre
//
// _______________________________ TECLADO CP300 _______________________________
//
//                    ___________________________
//          TT1 (15) |                           |             TUP (21)
//                   |                           |
//   TESC   TT2 (16) |                           |    TRT<    TENT (23)     TLF>
//   (18)            |                           |    (19)                  (20)
//          TT3 (17) |                           |             TDW (22)
//                   |___________________________|
//
//             TF1 (10)    TF2 (11)    TF3 (12)    TF4 (13)     TF5 (14)
//
//             TN1 (1)     TN2 (2)     TN3 (3)     TN4 (4)      TN5 (5)
//
//             TN6 (6)     TN7 (7)     TN8 (8)     TN9 (9)      TN0 (0)
//
//   ___________________________________________________________________________
//
//       F1  - Supervisao
//       F2  - Parametros do Processo
//       F3  - Receitas
//       F4  - Servicos
//       F5  - Configuracao
//       F6  - Sistema
//       TKL - Variavel TECLA Temporizada
//
// ------------------------------------------------------------------------------
//
//								240 pixels
//                    ___________________________
//   				 |0,0                        |
//   				 |                           |
//   				 |                           | 160 pixels
//   				 |                           |
//   				 |                           |
//                   |___________________________|
//											  240,160
//
// 				Tam Fonte		Altura(px)		Largura(px)		Linhas		Digitos
//					0				11				 7			  14		  34
//					4				22				10			   7		  24
//					1				28				12			   5		  20
//					2				38				17			   4		  14
//					3				48				22			   3		  10
//
//  Comando:	MOSTRA ("tipo", inicio_x, inicio_y, largura, altura, fonte, "prefixo", &variável, "sufixo");
//
// 	Tipos de MOSTRA:
// 	T 		 - Limpa a tela
// 	D(N)P(n) - Alinha campo a direita, com 'N' caracteres e 'n' casas decimais
// 	Z 		 - Usado em conjunto com o com a opção 'D'. Alinha a direita e preenche com zeros a esquerda
// 	S(N) 	 - Mostra mensagem 'N' conforme valor da variável global WWW
// 	F 		 - Mostra e atualiza valor da variável na tela mas não permite editá-la
// 	M(L) 	 - Desenha uma moldura de largura 'L', em pixels, utilizando as coordenadas x, y, largura e altura
// 	I 		 - Inverte a caixa de texto para fundo para escuro e letra branca, utilizando as coordenadas x, y, larg e altura
// 	C 		 - Mostra na tela ao invés do valor da variável, uma mensagem codificada em ASCII até 20 caracteres ou até encontrar caracter NULL ('\0')
// 	B 		 - Caixa de seleção/botão
//
//==============================================================================

//=============================  HARDWARE INSTALADO ============================
// CLP=CP300 (EXH=0 ESD=1 ESA=3) - Posicao 1: ESD / Posicoes 2, 3 e 4: ESA

//=============== CANAIS DE COMUNICACAO RS232/RS485/USB/ETHERNET ===============
// INTERFACE1 232 (19200)=1 (PROTOCOLO=BCM2)
// INTERFACE2 485 (19200)=1 (PROTOCOLO=BCM2)
// INTERFACE3 485 (19200)=1 (PROTOCOLO=BCM2)
// INTERFACE4 USB (19200)=1 (PROTOCOLO=BCM2)       *** NAO UTILIZADA ***
// INTERFACE5 ETHERNET (PROTOCOLO=BCM2-TCP) IP=10.0.1.90 GT=10.0.1.1 NT=255.255.255.0

//======================  MAPA DE MEMORIA (16384 VARIAVEIS) ====================
//		> Sistema Operacional         	( 1024 Variaveis) - 0000h ate 0FFFh
//		> Variaveis Livres do Usuário 	(15360 Variaveis) - 1000h ate FFFFh
//							W00...W99 	(  100 Variaveis) - 1000h ate 118Fh
//			  Config. Portas de COMUN	(   60 Variaveis) - 1190h ate 127Fh
//					 Variaveis Livres	(15200 Variaveis) - 1280h ate FFFFh
//
// =================================  MAQUINAS  ================================

// ############################### ROTINAS BASICAS ##############################
// MAQUINA_0		// (VA) Pre-set de Partida + Geracao de DEC + Variavel TKL + Limpar Memoria do CLP
// MAQUINA_1		// (VB) Geracao de SEG + Relogio Calendario + Apagar Senha
// MAQUINA_2		// (VC) Geracao de MIN + Criar Bco Dados + Apagar Bco Dados + Turnos
// MAQUINA_3		// (VD) Leituras dos Sensores (Conversao + Falha por Faixa)

// ############################# ROTINAS DE CONTROLE ############################
// MAQUINA_4		// (VE) Reservatorio: Valvula de Entrada XV-01
// MAQUINA_5		// (VF) Sequenciamento da Estacao (Principal / Auxiliar / Rodizio / Reserva)
// MAQUINA_6		// (VG) Controle de Pressao de Recalque (PID 0) + Referencia de Velocidade
// MAQUINA_7		// (VH) Bomba B-01
// MAQUINA_8		// (VI) Bomba B-02
// MAQUINA_9		// (VJ) Intertravamentos + Comandos das Saidas + Disponibilidade
// MAQUINA_10		// (VK) Protecoes das Bombas + Temporizacoes de 1 s + Set-point
// MAQUINA_11		// (VL) Publicacao W21...W50 + Falhas de Comunicacao + Rotulos W59...W80
// MAQUINA_12		// (VM) Sequenciador de Cenarios (W00...W02)
// MAQUINA_13		// (VN) Banco de Eventos BDE (Verdade de Campo)
// MAQUINA_14		// (VO) Injecao Manual de Falhas (W03...W09)
// --- MAQUINA_15		// (VP) Livre
// --- MAQUINA_16		// (VQ) Livre
// --- MAQUINA_17		// (VR) Livre
// --- MAQUINA_18		// (VS) Livre
// --- MAQUINA_19		// (VT) Livre
// --- MAQUINA_20		// (VU) Livre

// ########################### ALARMES + ACIONAMENTOS ###########################
// MAQUINA_21		// (VV) Alarmes
// MAQUINA_22		// (VW) Funcoes Diversas - Geral
// MAQUINA_23		// (VX) Espelho de Acionamento das SD's e SA's

// ################################### DISPLAY ##################################
// MAQUINA_24		// (VY) Senha
// MAQUINA_25		// (VZ) Display

// ################################## SIMULADOR #################################
// MAQUINA_26		// (ZA) Planta: Hidraulica, Inversores, Motores, Temperaturas		XXX
// MAQUINA_27		// (ZB) Transmissores: Ruido, 4-20 mA / PT100, Falhas de Sensor	XXX
// MAQUINA_28		// (ZC) Slot de Falha A											XXX
// MAQUINA_29		// (ZD) Slot de Falha B											XXX
// MAQUINA_30		// (ZE) Slot de Falha C											XXX

//==============================================================================

//=============================  ENTRADAS DIGITAIS  ============================
// ...................................................... Modulo ESD (Posicao 1)
// ED1 Chave de Operacao da Estacao (0=Desligada / 1=Ligada)
// ED2 Desliga Alarme / Rearme das Bombas
// ED3 Emergencia (NF: 1=OK / 0=Emergencia)
// ED4 Inversor B-01 em Operacao
// ED5 Inversor B-01 em Falha
// ED6 Termistor (PTC) do Motor M-01 Atuado
// ED7 Inversor B-02 em Operacao
// ED8 Inversor B-02 em Falha
// ED9 Termistor (PTC) do Motor M-02 Atuado
// ED10 XV-01 Aberta (Fim de Curso)
// ED11 XV-01 Fechada (Fim de Curso)
// ED12 LSL Chave de Nivel Muito Baixo (NF: 1=Nivel OK)
// ED13 LSH Chave de Nivel Alto (1=Nivel Alto)

//==============================  SAIDAS DIGITAIS  =============================
// ...................................................... Modulo ESD (Posicao 1)
// SD1 Beep de Alarme
// SD2 Partida Inversor B-01
// SD3 Partida Inversor B-02
// SD4 Abrir XV-01 (1=Abrir / 0=Fechar)
// SD5 Reset Inversor B-01 (Pulso)
// SD6 Reset Inversor B-02 (Pulso)
// SD7 Sinaleiro Estacao em Operacao
// SD8 Sinaleiro de Alarme

//============================  ENTRADAS ANALOGICAS  ===========================
//  Canal k = 1...18 (indice de STV/RLO/RHI/SNA/EAO/MSF). Conversao no CLP (MAQUINA_3):
//  Valor = RLO[k] + EA*(RHI[k]-RLO[k])/4095. Faixas abaixo = valores de fabrica.
// ............................................................ ESA (Posicao 2)
// EA13 (PT100)  k=13 TT-01 Temperatura Enrolamento M-01 (0...200.0 'C)
// EA14 (PT100)  k=14 TT-02 Temperatura Enrolamento M-02 (0...200.0 'C)
// EA15 (PT100)  k=15 TT-03 Temperatura Mancal B-01 (0...150.0 'C)
// EA16 (PT100)  k=16 TT-04 Temperatura Mancal B-02 (0...150.0 'C)
// EA17 (PT100)  k=17 TT-05 Temperatura da Agua (0...100.0 'C)
// EA18 (PT100)  k=18 TT-06 Temperatura Ambiente (0...100.0 'C)
// EA21 (4-20mA) k=1  LT-01 Nivel do Reservatorio (0...100.0 %)
// EA22 (4-20mA) k=2  PT-01 Pressao de Succao (0...2500 mbar abs)
// EA23 (4-20mA) k=3  PT-02 Pressao de Recalque (0...10000 mbar)
// EA24 (4-20mA) k=4  FT-01 Vazao de Recalque (0...150.0 m3/h)
// ............................................................ ESA (Posicao 3)
// EA33 (4-20mA) k=5  FT-02 Vazao de Entrada (0...150.0 m3/h)
// EA34 (4-20mA) k=6  IT-01 Corrente M-01 (0...50.0 A)
// EA35 (4-20mA) k=7  IT-02 Corrente M-02 (0...50.0 A)
// EA36 (4-20mA) k=8  VT-01 Vibracao B-01 (0...20.00 mm/s RMS)
// ............................................................ ESA (Posicao 4)
// EA45 (4-20mA) k=9  VT-02 Vibracao B-02 (0...20.00 mm/s RMS)
// EA46 (4-20mA) k=10 ST-01 Frequencia Inversor B-01 (0...60.0 Hz)
// EA47 (4-20mA) k=11 ST-02 Frequencia Inversor B-02 (0...60.0 Hz)
// EA48 (4-20mA) k=12 JT-01 Potencia Ativa da Estacao (0...50.0 kW)
//  PT100: no equipamento o multiplexador renova cada canal a cada ~5,6 s; o simulador
//  renova um PT100 a cada 0,8 s (cada canal a cada 4,8 s).

//=============================  SAIDAS ANALOGICAS  ============================
// ............................................................ ESA (Posicao 2)
// SA5 (4-20mA) Referencia de Velocidade Inversor B-01 (0...4095 = 0...50 Hz)
// SA6 (4-20mA) Referencia de Velocidade Inversor B-02 (0...4095 = 0...50 Hz)

//===========================  Variaveis W00 ... W99  ==========================
//  Todos os campos lidos pelo PC ficam abaixo de 65535 (1 palavra de 16 bits util).
// 0 CSC PC - Comando do Cenario (1=Iniciar 2=Pausar 3=Retomar 4=Parar 5=Reiniciar) - CLP zera
// 1 PSC PC - Cenario a Iniciar (0=Tabela SCN do PC / 1...6=Presets)
// 2 SEED PC - Semente Aleatoria (0=Gerar; o CLP devolve a semente usada, 1...60000)
// 3 MFC PC - Falha Manual: Codigo (ver Catalogo)
// 4 MFT PC - Falha Manual: Alvo (bomba 0/1/2 ou sensor 1...18, +100=negativo)
// 5 MFI PC - Falha Manual: Intensidade (0...1000 permil)
// 6 MFM PC - Falha Manual: Modo (0=Subita 1=Linear 2=Acelerada 3=Intermitente)
// 7 MFR PC - Falha Manual: Tempo de Rampa (s) / Tempo de Recuperacao no comando 3
// 8 MFD PC - Falha Manual: Duracao Total (s, 0=ate cancelar)
// 9 MFX PC - Falha Manual: Comando (1=Injetar 2=Cancelar Todas 3=Recuperar Todas) - CLP zera
// 10 W10 ______________ CADASTRO DO PRODUTO ______________
// 11 FML PC - Familia de Produtos (111=MR-111)
// 12 CTZ PC - Customizacao
// 13 TBV PC - Versao da Tabela de Variaveis (0-Standard)
// 14 VSF PC - AABBC - 12.34c Versao do Software Instalado
// 15 DSF PC - _AABB - Mes/Ano do Software Instalado
// 16 W16 PC - Livre
// 17 W17 PC - Livre
// 18 W18 PC - Livre
// 19 W19 PC - Livre
// 20 W20 PC - Livre
// 21 W21 CLP - LT-01 Nivel do Reservatorio (% x10)
// 22 W22 CLP - PT-01 Pressao de Succao (mbar abs)
// 23 W23 CLP - PT-02 Pressao de Recalque (bar x100)
// 24 W24 CLP - FT-01 Vazao de Recalque (m3/h x10)
// 25 W25 CLP - FT-02 Vazao de Entrada (m3/h x10)
// 26 W26 CLP - IT-01 Corrente M-01 (A x10)
// 27 W27 CLP - IT-02 Corrente M-02 (A x10)
// 28 W28 CLP - VT-01 Vibracao B-01 (mm/s x100)
// 29 W29 CLP - VT-02 Vibracao B-02 (mm/s x100)
// 30 W30 CLP - ST-01 Frequencia Inversor B-01 (Hz x10)
// 31 W31 CLP - ST-02 Frequencia Inversor B-02 (Hz x10)
// 32 W32 CLP - JT-01 Potencia Ativa da Estacao (kW x10)
// 33 W33 CLP - TT-01 Temperatura Enrolamento M-01 ('C x10)
// 34 W34 CLP - TT-02 Temperatura Enrolamento M-02 ('C x10)
// 35 W35 CLP - TT-03 Temperatura Mancal B-01 ('C x10)
// 36 W36 CLP - TT-04 Temperatura Mancal B-02 ('C x10)
// 37 W37 CLP - TT-05 Temperatura da Agua ('C x10)
// 38 W38 CLP - TT-06 Temperatura Ambiente ('C x10)
// 39 HBT CLP - Heartbeat da Publicacao (0...59999, +1 por publicacao de 1 s)
// 40 W40 CLP - Estado da Estacao (0=Deslig 1=Partindo 2=1 Bomba 3=2 Bombas 4=Nivel Baixo 5=Falha 6=Manut 7=Emerg)
// 41 W41 CLP - Estado B-01 (0=Parada 1=Partindo 2=Operando 3=Parando 4=Falha 5=Manutencao)
// 42 W42 CLP - Estado B-02 (idem)
// 43 W43 CLP - Bomba Principal (1/2)
// 44 W44 CLP - Estado XV-01 (0=Fechada 1=Abrindo 2=Aberta 3=Fechando 4=Falha)
// 45 W45 CLP - Set-point de Pressao em Uso (bar x100)
// 46 W46 CLP - Referencia de Velocidade (% x10)
// 47 W47 CLP - Ultimo Alarme Ocorrido (01...20)
// 48 W48 CLP - Numero de Alarmes Ativos
// 49 W49 CLP - ED1...ED13 Empacotadas (bit0=ED1 ... bit12=ED13)
// 50 W50 CLP - SD1...SD8 Empacotadas (bit0=SD1 ... bit7=SD8)
// 51 DDE CLP - Ponteiro do Bco de Eventos BDE (0...99)
// 52 UDE PC  - Ultimo Registro do BDE Salvo pelo PC (0...99)
// 53 GEV CLP - Contador Continuo de Eventos (1...59999)
// 54 W54 Livre
// 55 W55 Livre
// 56 W56 Livre
// 57 W57 Livre
// 58 W58 Livre
// 59 HBR CLP - Heartbeat dos Rotulos (0...59999, +1 por segundo, imune as falhas de comunicacao)
//  ___ ROTULOS (verdade de campo, atualizados a cada 1 s, imunes as falhas de comunicacao) ___
// 60 W60 CLP - Slot A: Codigo da Falha (0=Livre)
// 61 W61 CLP - Slot A: Alvo
// 62 W62 CLP - Slot A: Severidade Instantanea (permil)
// 63 W63 CLP - Slot A: Fase (0=Livre 2=Rampa 3=Plena 4=Recuperacao)
// 64 W64 CLP - Slot B: Codigo da Falha
// 65 W65 CLP - Slot B: Alvo
// 66 W66 CLP - Slot B: Severidade (permil)
// 67 W67 CLP - Slot B: Fase
// 68 W68 CLP - Slot C: Codigo da Falha
// 69 W69 CLP - Slot C: Alvo
// 70 W70 CLP - Slot C: Severidade (permil)
// 71 W71 CLP - Slot C: Fase
// 72 W72 CLP - Anomalia Ativa (0=Normal / 1=Algum slot em fase 2...4)
// 73 W73 CLP - Cenario em Execucao (0=Tabela SCN / 1...6=Preset)
// 74 W74 CLP - Passo do Cenario (0...19)
// 75 W75 CLP - Estado do Cenario (0=Parado 1=Rodando 2=Pausado 3=Concluido)
// 76 W76 CLP - Tempo de Cenario: Horas
// 77 W77 CLP - Tempo de Cenario: Segundos na Hora (0...3599)
// 78 W78 CLP - Consumo Efetivo da Rede (permil da condutancia nominal)
// 79 W79 CLP - Ultimo Evento Operacional (1=Partida 2=Parada 3=Troca p/ Reserva 4=Set-point
//              5=Rodizio 6=Liga Auxiliar 7=Desliga Auxiliar 8=Mudanca de Carga)
// 80 W80 CLP - Contador de Eventos Operacionais (0...59999)
// 81 DD1 Ponteiro do Bco de Dados - Alarmes (0...99)
// 82 DD2                          - Minutos (0...99)
// 83 DD3 Ponteiro do Bco de Dados - Lotes   (0...9)
// 84 UDD1 Ultimo Registro Salvo pelo PC - Bco de Dados - Alarmes (0...99)
// 85 UDD2                                              - Minutos (0...99)
// 86 UDD3                                              - Lotes   (0...9)
// 87 W87 PC - Contador Continuo do Bco de Dados - Alarmes
// 88 W88 PC -                                   - Minutos
// 89 W89 PC -                                   - Lotes
// 90 W90 CLP - Contador de Partidas do CLP (Reinicio de CPU)
// 91 W91 CLP - Contador de Acionamentos da Tecla OK
// 92 W92 Selecao da Porta de Comunicacao com os Sensores (1/2/3)
// 93 BLQ Teclado Bloqueado - Acesso apenas via PC (0=Livre / 1=Bloqueado)
// 94 W94 1=Desl ALM / 11=Zerar Bco Dados: Alarmes+Minutos+Lotes / 12=AV ID
// 95 ALM Flag do Estado do Alarme (0=ok / 1=em Alarme)
// 96 W96 Utilizada para Zerar / Escrever a Memoria do CLP
// 97 SNH Senha Geral (0=Travado / 1 Aberto)
// 98 W98 Senha definivel pelo Operador Master
// 99 W99 Senha de Acesso ao Display (Definivel=W98)

// ==========================  VARIAVEIS MATRICIAIS  ==========================

// ======================================================== AUXILIARES

// _______________	Variaveis W00...W99
// DIMD DSP[100]	; Display 1

// _______________	Variavel de Referencia
// DIM VVV[1]		; Variavel de Referencia das Variaveis Matriciais

// _______________	Mapa de Telas
// DIM TLY[7]		; Ponteiros do Mapa de Telas

// ======================================================== FIFOS (Reserva - nao utilizadas)

// DIM FIF1[10] ... FIF8[10]	; Reserva (filtro antigo das EAN's)

// ======================================================== ALARMES

// ______________  Alarmes On-Line
// DIM AL1[101]    ; Motivo do Alarme
// DIM DT1[101]    ; DDmm - Dia/Mes da Ocorrencia do Alarme
// DIM HR1[101]    ; HHmm - Hor/Min da Ocorrencia do Alarme

// ======================================================== BANCO DE DADOS

// ______________  Banco de Dados - Alarmes
// DIM BDA[600]    ; Variaveis do Banco de Dados: Alarmes
//                 ;  1 - AAAAA - Contador Continuo para uso do PC
//                 ;  2 - _AABB - Dia/Mes do Alarme
//                 ;  3 - _AABB - Hor/Min do Alarme
//                 ;  4 - AAAAA - Motivo do Alarme (codigo x10)
//                 ;  5 - AAAAA - Livre
//                 ;  6 - AAAAA - Check-Sum

// ______________  Banco de Dados - Minutos
// DIM BDD[2000]   ; Variaveis do Banco de Dados: Minutos
//                 ;  1 - AAAAA - Contador Continuo para uso do PC
//                 ;  2 - ABBCC - Trn/Ano/Seg da Coleta
//                 ;  3 - _AABB - Dia/Mes da Coleta
//                 ;  4 - _AABB - Hor/Min da Coleta
//                 ;  5 - AAAAA - ID do Lote
//                 ;  6 - AAAAA - SMQ Status da Maquina
//                 ;  7 - AAAAA - PDT Produto
//                 ;  8 - AAAAA - CTZ
//                 ;  9 - AAAAA - Estado da Estacao (W40)
//                 ; 10 - AAAAA - Anomalia Ativa (W72)
//                 ; 11 - AAAAA - Nivel (% x10)
//                 ; 12 - AAAAA - Pressao de Succao (mbar abs)
//                 ; 13 - AAAAA - Pressao de Recalque (bar x100)
//                 ; 14 - AAAAA - Vazao de Recalque (m3/h x10)
//                 ; 15 - AAAAA - Corrente M-01 (A x10)
//                 ; 16 - AAAAA - Corrente M-02 (A x10)
//                 ; 17 - AAAAA - Temp. Enrolamento M-01 ('C x10)
//                 ; 18 - AAAAA - Temp. Enrolamento M-02 ('C x10)
//                 ; 19 - AAAAA - Potencia da Estacao (kW x10)
//                 ; 20 - AAAAA - Check-Sum

// ______________  Banco de Dados - Lotes
// DIM BDL[100]    ; Variaveis do Banco de Dados: Lotes
//                 ;  1 - AAAAA - Contador Continuo para uso do PC
//                 ;  2 - _AABB - Trn/Ano da Coleta
//                 ;  3 - _AABB - Dia/Mes da Coleta
//                 ;  4 - _AABB - Hor/Min da Coleta
//                 ;  5 - AAAAA - ID do Lote
//                 ;  6 - AAAAA - Livre
//                 ;  7 - AAAAA - Livre
//                 ;  8 - AAAAA - Livre
//                 ;  9 - AAAAA - Livre
//                 ; 10 - AAAAA - Livre

// ======================================================== SIMULADOR / CENARIOS (declaradas no fim)

// ______________  Transmissores (k = canal 1...18, indice 0 livre)
// DIM STV[20]     ; Valor verdadeiro do processo (unid. de engenharia do canal) - SIMULADOR
// DIM RLO[20]     ; Valor de engenharia em 4 mA / 0 contagens
// DIM RHI[20]     ; Valor de engenharia em 20 mA / 4095 contagens
// DIM SNA[20]     ; Ruido do transmissor: desvio padrao (0,1 unid. de engenharia)
// DIM EAO[20]     ; Ultima contagem gerada pelo transmissor - SIMULADOR
// DIM SFZ[20]     ; Contagem retida na falha "travado" - SIMULADOR
// DIM SFP[20]     ; Codigo de falha do canal no passo anterior - SIMULADOR
// DIM SHT[20]     ; Contador de retencao "sem atualizacao" (passos) - SIMULADOR
// DIM MSF[20]     ; Falha de faixa detectada pelo CLP (0=OK / 1=Falha)

// ______________  Consumo e Falhas
// DIM DPT[25]     ; Perfil de consumo por hora 0...24 (permil da condutancia nominal)
// DIM FSV[26]     ; Severidade das falhas de processo (permil): [cod] = comum/B-01, [cod+13] = B-02

// ______________  Cenario (PC pode escrever a tabela e iniciar com PSC=0)
// DIM SCN[240]    ; 20 passos x 12 campos. Passo n comeca em n*12:
//                 ;  0 - DUR - Duracao do passo (s); 0 = fim do cenario
//                 ;  1 - FCD - Falha a injetar (codigo; 98=recuperar todas; 99=cancelar todas)
//                 ;  2 - FTG - Alvo
//                 ;  3 - FIN - Intensidade (permil)
//                 ;  4 - FMD - Modo (0...3)
//                 ;  5 - FRT - Rampa (s)
//                 ;  6 - FDU - Duracao da falha (s; 0 = ate cancelar)
//                 ;  7 - FRC - Recuperacao (s) ao fim da falha / no codigo 98
//                 ;  8 - DMD - Consumo fixo (permil; 0 = perfil automatico)
//                 ;  9 - SPP - Set-point fixo (bar x100; 0 = automatico)
//                 ; 10 - OPC - Operacao (0=Nada 1=Parar Estacao 2=Ligar 3=Rodizio 4=Rearme)
//                 ; 11 - Reserva

// ______________  Publicacao com Atraso
// DIM RBF[1920]   ; Buffer circular: 30 tags x 32 s (tag t no bloco t*32) + 30 tags x 32 x 15 s (bloco 960+t*32)

// ______________  Banco de Eventos (verdade de campo)
// DIM BDE[1200]   ; 100 registros x 12 campos:
//                 ;  1 - AAAAA - Contador Continuo (GEV)
//                 ;  2 - _AABB - Dia/Mes
//                 ;  3 - _AABB - Hor/Min
//                 ;  4 - AAAAA - Segundo
//                 ;  5 - AAAAA - Tempo de Cenario: Horas
//                 ;  6 - AAAAA - Tempo de Cenario: Segundos na Hora
//                 ;  7 - AAAAA - Tipo (1/2/3=Slot A/B/C, 10=Cenario, 20=Operacional)
//                 ;  8 - AAAAA - Codigo (falha / passo+1 / evento operacional)
//                 ;  9 - AAAAA - Slot: Alvo*10+Fase / Cenario: Estado / Operacional: Bomba Principal
//                 ; 10 - AAAAA - Slot: Severidade / Cenario: Preset / Operacional: Bombas Operando
//                 ; 11 - AAAAA - Slot: Intensidade*10+Modo / Cenario: Semente / Operac.: SP (bar x100)
//                 ; 12 - AAAAA - Check-Sum (soma dos campos 1...11 & 0xFFFF)

// ========================  VARIAVEIS NAO MATRICIAIS  ========================

// ________________________________________________________  F1 - SUPERVISAO

// _______ Status de Operacao
// MNT Manutencao Ativada (0-Nao / 1-Sim)
// LGD Estacao Liberada (Chave ED1 + Habilita + sem Emergencia + sem Manutencao)
// HBL Habilita a Estacao (0-Nao/1-Sim) - o cenario pode desligar/ligar (OPC)
// EMG Emergencia (0-Nao / 1-Sim) = ED3 aberta
// XTT Status (0-Desligado / 1-Ligado / 2-Emergencia / 3-Manutencao)

// _______ Habilitar Alarmes
// ALX[101]		; Habilita/Desabilita Alarme 01...100 (seg)

// _______ Gerenciamento da Producao
// TRN - Turno Atual (0...4)
// PDT - Produto (1...10)
// SMQ - ___AB - Status da Maquina (A:Ligad/Deslig /B:Manutencao)

// _______ Lote Atual
// GR1 - ID do Lote (Dados do Lote Atual)
// GR2 - Trn/Ano do Inicio do Lote
// GR3 - Dia/Mes do Inicio do Lote
// GR4 - Hor/Min do Inicio do Lote

// _______ Reserva (chassi, nao utilizadas)
// TMP1...TMP5, UMD, PRS, VLC, AQC1, SPV1...SPV10

// _______ Medidas do CLP (MAQUINA_3)
// MLT Nivel (% x10)			MPS Pressao Succao (mbar abs)	MPD Pressao Recalque (mbar)
// MFQ Vazao Recalque (m3/h x10)	MQE Vazao Entrada (m3/h x10)
// MI1/MI2 Corrente (A x10)		MV1/MV2 Vibracao (mm/s x100)	MS1/MS2 Frequencia (Hz x10)
// MPW Potencia (kW x10)			MW1/MW2 Temp Enrolamento		MB1/MB2 Temp Mancal ('C x10)
// MTF Temp Agua				MTA Temp Ambiente ('C x10)		SNF Numero de Sensores em Falha

// _______ Estacao / Bombas
// ETP Estado da Estacao (=W40)	SB1/SB2 Estado das Bombas (=W41/W42)	DTY Bomba Principal (1/2)
// RQ1/RQ2 Solicitacao de Operacao (MAQ 5 -> MAQ 7/8)	CM1/CM2 Comando de Partida dos Inversores
// AV1/AV2 Bomba Disponivel		NRN Bombas em Operacao	SOK Estacao Liberada e Nivel OK
// FR1/FR2 Motivo da Falha (1=Inversor 2=PTC 3=Temp Enrol 4=Vibracao 5=Sobrecorrente
//         6=Temp Mancal 7=Sem Retorno de Operacao 8=Succao Baixa)
// TR1/TR2 Trip das Protecoes (MAQ 10, mesmo codigo de FR)	RV1/RV2 Pulso de Reset dos Inversores
// ARC1/ARC2 Rearmes Automaticos (zera apos 600 s em operacao)	RU1/RU2 Tempo em Operacao (s)
// RTH Tempo da Principal desde o ultimo rodizio (s)	FDS Pedido de Rodizio (cenario)
// CXV Comando XV-01	XVS Estado XV-01 (=W44)	XVF Falha XV-01	LLO Bloqueio por Nivel Baixo
// PSP Set-point em Uso (mbar, rampa 20 mbar/s)	PSX Set-point Alvo	PSXL Alvo Anterior
// SPR Saida do PID (permil)	SRF Referencia Aplicada (permil)	NGT Periodo Noturno	HRS Hora em Uso
// GOP Ultimo Evento Operacional (=W79)	GOC Contador de Eventos Operacionais (=W80)
// RSTQ Rearme em Curso (MAQUINA_21 no estado 5)	ALC Ultimo Alarme	ALN Alarmes Ativos

// ________________________________________________________  F2 - PARAMETROS

// _______ Filtro das EDG's
// FED1...FED8 - Filtro da EDG1...EDG8 (seg). EDG9...EDG13 sem filtro.

// _______ Reserva (chassi, nao utilizadas)
// FEA1...FEA8, AQ11...AQ17, VLX1...VLX7, CM71...CMA7, VTP1...VTP5, UMMN, UMMX, PRM1...PRM10

// _______ Controle de Pressao
// PSD Set-point Diurno (mbar)		PSN Set-point Noturno (mbar, 0=sem reducao)
// PNH/PNE Hora de Inicio/Fim do Periodo Noturno (0...23)
// PKP/PKI/PKD Ganhos do PID 0 (/256)	SFB Velocidade Fixa com PT-02 em Falha (permil)

// _______ Bombas
// SMN Velocidade Minima (permil)	SAS/SAP Velocidade p/ Ligar/Desligar a Auxiliar (permil)
// TAS Atraso p/ Ligar a Auxiliar (s; desligar = 2xTAS)	ROT Rodizio (h de operacao, 0=Nao)
// ARS Rearme Automatico de Falha do Inversor (s, 0=Nao; max. 3 tentativas)

// _______ Protecoes
// TWA/TWT Temp. Enrolamento: Alarme/Trip ('C x10)	TBA/TBT Temp. Mancal: Alarme/Trip
// VBA/VBT Vibracao: Alarme (10 s)/Trip (5 s) (mm/s x100)	IAL/ITR Corrente: Alarme/Trip 30 s (A x10)
// PSL Pressao de Succao Minima: Trip 10 s (mbar abs; alarme em PSL+100)

// _______ Reservatorio / Pressao
// LON/LOF Nivel p/ Abrir/Fechar XV-01 (% x10)	LLL/LLR Nivel de Bloqueio/Liberacao (% x10)
// PDL Desvio de Pressao Baixa (mbar)	PDH Pressao Alta (mbar)

// _______ Diversos
// ASV			; SALVAR Dados no PC (0-Nao / 1-Sim)
// TNO1...TNO4 - (HHmm) Hora/Minuto do Inicio dos Turnos
// ABLT Abertura AUT de Lote por Dia ou por Turno (0-Nao / 1=Dia / 2=Turno)

// ________________________________________________________  F5 - CONFIGURACAO

//  _______ Reserva (chassi, nao utilizadas)
//  OFF1...OFF8, RNG1...RNG8, CFG1...CFG10, RZV1...RZV10

//  _______ Simulacao
//  HINI Hora Inicial da Planta Simulada (0...23; 99=Relogio do CLP)
//  SNG Ganho Global de Ruido dos Transmissores (permil, 1000=nominal)
//  FVS Assinatura da Memoria Programada (24680 = valores de fabrica carregados)

// ________________________________________________________  SIMULADOR			XXX

// _______ Variaveis do Simulador
// TST Modo de Simulacao (0=Desativada / 1=Ativada / 2=Congelada/Pausa)
// AAA Simulacao MAN de Alarmes (0,1,2,...)
// XLM1...XLM3 Livre

// _______ Planta (MAQUINA_26) - grandezas verdadeiras
// VOL Volume (mL)		LVT Nivel (0,01 %)	XVP Posicao XV-01 (permil)	QIN Vazao Entrada (0,1 m3/h)
// Q1/Q2 Vazao das Bombas	QOUT Vazao Total	QLK Vazamento Reservatorio (0,1 m3/h)
// PSA Succao (mbar abs)	PNA Rede (mbar abs)	PDG Recalque (mbar)	DPO Perda na Obstrucao (mbar)
// RCO/RTO Resistencia Consumidores/Total (mbar a 100 m3/h)	CAV Cavitacao (permil)
// NS1/NS2 Rotacao (permil)	HZ1/HZ2 Frequencia (Hz x10)	VFF1/VFF2 Falha Inversor	PR1/PR2 Reset Anterior
// HP1/HP2 Altura (mbar)	PH1/PH2 Pot. Hidraulica (W)	PW1/PW2 Pot. no Eixo (W)	ET1/ET2 Rendimento (permil)
// IM1/IM2 Corrente (0,1 A)	JTP Potencia Estacao (kW x10)	VB1/VB2 Vibracao (mm/s x100)
// TW1/TW2 Enrolamento, TB1/TB2 Mancal, TFL Agua (1e-5 'C)	TAM Ambiente ('C x10)	PC1/PC2 PTC Atuado
// TOD Hora do Dia Simulada (s)	TDK/TKC Contadores de Passo	DPF Perfil	DRW Passeio Aleatorio (x10000)
// DMT Consumo Alvo	DMA Consumo Filtrado	DMF Consumo Final (permil)	SUF Fator de Suprimento (x10000)
// RNA/RNB/RNC/RND Geradores Aleatorios (planta/transmissores/comunicacao/slots)
// PIN Planta Inicializada	RSTP Pedido de Condicoes Iniciais	ZTK Passo Pronto p/ Transmissores
// PTN/PTK PT100 em Renovacao / Contador

// _______ Cenario (MAQUINA_12)
// SCP Preset	SCE Passo	SCS Estado	SCT Tempo Total (s)	SST Tempo no Passo (s)
// DOV Consumo Fixo (permil)	SPO Set-point Fixo (mbar)	QDU...QOP Campos do Passo Atual

// _______ Slots de Falha A/B/C (MAQUINA_28/29/30) - sufixo A, B ou C
// XCx/XTx/XIx/XMx/XRx/XDx/XEx Comando: Codigo/Alvo/Intensidade/Modo/Rampa/Duracao/Recuperacao
// XGx Injetar	XKx Cancelar	XQx Recuperar (rampa de XEx s)
// GCx Codigo	TGx Alvo	INx Intensidade	MDx Modo	RTx Rampa	DUx Duracao	RCx Recuperacao
// SVx Severidade	PHx Fase	PTx Bomba Alvo	FIx Indice em FSV	SCx Canal de Sensor	SDx Sentido
// TEx Tempo na Fase	TTx Tempo Total	SRx Severidade no Inicio da Recuperacao	IOx/IWx Intermitencia

// _______ Publicacao / Eventos
// CMC/CMS Falha de Comunicacao Ativa: Codigo/Severidade	DLY Atraso (s)	RBI Indice do Buffer
// LPA/LPB/LPC/LSE/LSS/LGC Ultimos valores registrados no BDE	EVT/EVC/EVK/EVS/EVM Campos do Evento
//##############################################################################

#include "CLP/LingBCM_XC32.h"
#include "CLP/ESD_ESA.h"
#include "CLP/VARIAVEIS_CLP.h"

//						 > > >   Atualizar VSF + DSF   < < <					xx00

//cód equipto, versão FIRMWARE, versão SOFTWARE, data FIRMWARE, data SOFTWARE
const uint16_t PRES1[]={300, 0, 1000, 0, 1026};
//nome do software
const uint8_t PRES2[]={'S', 'I', 'M', ' ', 'A', 'N', 'O', 'M', 'A', 'L'};
//FML, CTZ, TBV, LIVRE-1, LIVRE-2
const uint16_t PRES3[]={100, 100, 0, 0, 0};
//versão software em string
const uint8_t PRES4[]={'1', '.', '0', '0', ' ', ' ', ' ', ' ', ' ', ' '};
//versão firmware em string
extern const uint8_t PRES5[];
//cód equipto, Reserva, versão FIRMWARE, Reserva, data FIRMWARE
extern const uint16_t PRES6[];

// =============================  ENTRADAS DIGITAIS  =============================
#define ED1 EDG.EDD1
#define ED2 EDG.EDD2
#define ED3 EDG.EDD3
#define ED4 EDG.EDD4
#define ED5 EDG.EDD5
#define ED6 EDG.EDD6
#define ED7 EDG.EDD7
#define ED8 EDG.EDD8
#define ED9 EDG.EDD9
#define ED10 EDG.EDD10
#define ED11 EDG.EDD11
#define ED12 EDG.EDD12
#define ED13 EDG.EDD13

// ==============================  SAIDAS DIGITAIS  ==============================
#define SD1 SDG.SDD1
#define SD2 SDG.SDD2
#define SD3 SDG.SDD3
#define SD4 SDG.SDD4
#define SD5 SDG.SDD5
#define SD6 SDG.SDD6
#define SD7 SDG.SDD7
#define SD8 SDG.SDD8

// ============================  ENTRADAS ANALOGICAS  ============================
#define EA1 EANA.EAN01
#define EA2 EANA.EAN02
#define EA3 EANA.EAN03
#define EA4 EANA.EAN04
#define EA5 EANA.EAN05
#define EA6 EANA.EAN06
#define EA7 EANA.EAN07
#define EA8 EANA.EAN08
#define EA13 EANA.EAN13
#define EA14 EANA.EAN14
#define EA15 EANA.EAN15
#define EA16 EANA.EAN16
#define EA17 EANA.EAN17
#define EA18 EANA.EAN18
#define EA21 EANA.EAN21
#define EA22 EANA.EAN22
#define EA23 EANA.EAN23
#define EA24 EANA.EAN24
#define EA33 EANA.EAN33
#define EA34 EANA.EAN34
#define EA35 EANA.EAN35
#define EA36 EANA.EAN36
#define EA45 EANA.EAN45
#define EA46 EANA.EAN46
#define EA47 EANA.EAN47
#define EA48 EANA.EAN48

// =============================  SAIDAS ANALOGICAS  =============================
#define SA1 SANA.SAN01
#define SA2 SANA.SAN02
#define SA3 SANA.SAN03
#define SA4 SANA.SAN04
#define SA5 SANA.SAN05
#define SA6 SANA.SAN06
#define SA7 SANA.SAN07
#define SA8 SANA.SAN08

// ===============================  VAVIAVEIS WWW  ===============================
typedef union
{
    struct
    {
        uint32_t WX00, WX01, WX02, WX03, WX04, WX05, WX06, WX07, WX08, WX09;
        uint32_t WX10, WX11, WX12, WX13, WX14, WX15, WX16, WX17, WX18, WX19;
        uint32_t WX20, WX21, WX22, WX23, WX24, WX25, WX26, WX27, WX28, WX29;
        uint32_t WX30, WX31, WX32, WX33, WX34, WX35, WX36, WX37, WX38, WX39;
        uint32_t WX40, WX41, WX42, WX43, WX44, WX45, WX46, WX47, WX48, WX49;
        uint32_t WX50, WX51, WX52, WX53, WX54, WX55, WX56, WX57, WX58, WX59;
        uint32_t WX60, WX61, WX62, WX63, WX64, WX65, WX66, WX67, WX68, WX69;
        uint32_t WX70, WX71, WX72, WX73, WX74, WX75, WX76, WX77, WX78, WX79;
        uint32_t WX80, WX81, WX82, WX83, WX84, WX85, WX86, WX87, WX88, WX89;
        uint32_t WX90, WX91, WX92, WX93, WX94, WX95, WX96, WX97, WX98, WX99;
    };
    struct
    {
        uint32_t WXXX[100];
    };

}VAR_W;
VAR_W var_wxx __attribute__  ((section ("user_var"), address(0x80051000)));

// ====================  MATRIZ DSP[100] - ESPELHO W00...W99  ====================
#define W00 var_wxx.WX00
#define W01 var_wxx.WX01
#define W02 var_wxx.WX02
#define W03 var_wxx.WX03
#define W04 var_wxx.WX04
#define W05 var_wxx.WX05
#define W06 var_wxx.WX06
#define W07 var_wxx.WX07
#define W08 var_wxx.WX08
#define W09 var_wxx.WX09
#define W10 var_wxx.WX10
#define W11 var_wxx.WX11
#define W12 var_wxx.WX12
#define W13 var_wxx.WX13
#define W14 var_wxx.WX14
#define W15 var_wxx.WX15
#define W16 var_wxx.WX16
#define W17 var_wxx.WX17
#define W18 var_wxx.WX18
#define W19 var_wxx.WX19
#define W20 var_wxx.WX20
#define W21 var_wxx.WX21
#define W22 var_wxx.WX22
#define W23 var_wxx.WX23
#define W24 var_wxx.WX24
#define W25 var_wxx.WX25
#define W26 var_wxx.WX26
#define W27 var_wxx.WX27
#define W28 var_wxx.WX28
#define W29 var_wxx.WX29
#define W30 var_wxx.WX30
#define W31 var_wxx.WX31
#define W32 var_wxx.WX32
#define W33 var_wxx.WX33
#define W34 var_wxx.WX34
#define W35 var_wxx.WX35
#define W36 var_wxx.WX36
#define W37 var_wxx.WX37
#define W38 var_wxx.WX38
#define W39 var_wxx.WX39
#define W40 var_wxx.WX40
#define W41 var_wxx.WX41
#define W42 var_wxx.WX42
#define W43 var_wxx.WX43
#define W44 var_wxx.WX44
#define W45 var_wxx.WX45
#define W46 var_wxx.WX46
#define W47 var_wxx.WX47
#define W48 var_wxx.WX48
#define W49 var_wxx.WX49
#define W50 var_wxx.WX50
#define W51 var_wxx.WX51
#define W52 var_wxx.WX52
#define W53 var_wxx.WX53
#define W54 var_wxx.WX54
#define W55 var_wxx.WX55
#define W56 var_wxx.WX56
#define W57 var_wxx.WX57
#define W58 var_wxx.WX58
#define W59 var_wxx.WX59
#define W60 var_wxx.WX60
#define W61 var_wxx.WX61
#define W62 var_wxx.WX62
#define W63 var_wxx.WX63
#define W64 var_wxx.WX64
#define W65 var_wxx.WX65
#define W66 var_wxx.WX66
#define W67 var_wxx.WX67
#define W68 var_wxx.WX68
#define W69 var_wxx.WX69
#define W70 var_wxx.WX70
#define W71 var_wxx.WX71
#define W72 var_wxx.WX72
#define W73 var_wxx.WX73
#define W74 var_wxx.WX74
#define W75 var_wxx.WX75
#define W76 var_wxx.WX76
#define W77 var_wxx.WX77
#define W78 var_wxx.WX78
#define W79 var_wxx.WX79
#define W80 var_wxx.WX80
#define W81 var_wxx.WX81
#define W82 var_wxx.WX82
#define W83 var_wxx.WX83
#define W84 var_wxx.WX84
#define W85 var_wxx.WX85
#define W86 var_wxx.WX86
#define W87 var_wxx.WX87
#define W88 var_wxx.WX88
#define W89 var_wxx.WX89
#define W90 var_wxx.WX90
#define W91 var_wxx.WX91
#define W92 var_wxx.WX92
#define W93 var_wxx.WX93
#define W94 var_wxx.WX94
#define W95 var_wxx.WX95
#define W96 var_wxx.WX96
#define W97 var_wxx.WX97
#define W98 var_wxx.WX98
#define W99 var_wxx.WX99
#define DSP var_wxx.WXXX

// ===========================  VARIAVEIS W00 ... W99  ===========================
#define FML W11
#define CTZ W12
#define TBV W13
#define VSF W14
#define DSF W15
#define DD1 W81
#define DD2 W82
#define DD3 W83
#define UDD1 W84
#define UDD2 W85
#define UDD3 W86
#define BLQ W93
#define ALM W95
#define SNH W97

#define CSC W00
#define PSC W01
#define SEED W02
#define MFC W03
#define MFT W04
#define MFI W05
#define MFM W06
#define MFR W07
#define MFD W08
#define MFX W09
#define HBT W39
#define HBR W59
#define DDE W51
#define UDE W52
#define GEV W53

// APPLICATION_ALIASES

// ============  PARAMETROS DE CONFIGURACAO DAS PORTAS DE COMUNICACAO ============
// ....... RS232
uint32_t XDTBS1 __attribute__((section ("user_var")));		// DATABIT
uint32_t XPARS1 __attribute__((section ("user_var")));		// PARIDADE
uint32_t XSTPS1 __attribute__((section ("user_var")));		// STOPBIT
uint32_t XPRTS1 __attribute__((section ("user_var")));		// PROTOCOLO
uint32_t XVLCN1 __attribute__((section ("user_var")));		// VELOCIDADE
uint32_t XCNL1 __attribute__((section ("user_var")));		// RESERVA 1
uint32_t XRV2S1 __attribute__((section ("user_var")));		// RESERVA 2

// ....... RS485_1
uint32_t XDTBS2 __attribute__((section ("user_var")));		// DATABIT
uint32_t XPARS2 __attribute__((section ("user_var")));		// PARIDADE
uint32_t XSTPS2 __attribute__((section ("user_var")));		// STOPBIT
uint32_t XPRTS2 __attribute__((section ("user_var")));		// PROTOCOLO
uint32_t XVLCN2 __attribute__((section ("user_var")));		// VELOCIDADE
uint32_t XCNL2 __attribute__((section ("user_var")));		// RESERVA 1
uint32_t XRV2S2 __attribute__((section ("user_var")));		// RESERVA 2

// ....... RS485_2
uint32_t XDTBS3 __attribute__((section ("user_var")));		// DATABIT
uint32_t XPARS3 __attribute__((section ("user_var")));		// PARIDADE
uint32_t XSTPS3 __attribute__((section ("user_var")));		// STOPBIT
uint32_t XPRTS3 __attribute__((section ("user_var")));		// PROTOCOLO
uint32_t XVLCN3 __attribute__((section ("user_var")));		// VELOCIDADE
uint32_t XCNL3 __attribute__((section ("user_var")));		// RESERVA 1
uint32_t XRV2S3 __attribute__((section ("user_var")));		// RESERVA 2

// ....... USB
uint32_t XDTBUB __attribute__((section ("user_var")));		// ???
uint32_t XPARUB __attribute__((section ("user_var")));		// ???
uint32_t XSTPUB __attribute__((section ("user_var")));		// ???
uint32_t XPRTUB __attribute__((section ("user_var")));		// ???
uint32_t XVLCNUB __attribute__((section ("user_var"))); 	// ???
uint32_t XRV1UB __attribute__((section ("user_var")));		// ???
uint32_t XRV2UB __attribute__((section ("user_var")));		// ???

// ....... ETHERNET
uint32_t XIP_B0 __attribute__((section ("user_var")));		// IP
uint32_t XIP_B1 __attribute__((section ("user_var")));		// IP
uint32_t XIP_B2 __attribute__((section ("user_var")));		// IP
uint32_t XIP_B3 __attribute__((section ("user_var")));		// IP
uint32_t XPORTET __attribute__((section ("user_var")));		// PORT
uint32_t XGT_B0 __attribute__((section ("user_var")));		// GATEWAY
uint32_t XGT_B1 __attribute__((section ("user_var")));		// GATEWAY
uint32_t XGT_B2 __attribute__((section ("user_var")));		// GATEWAY
uint32_t XGT_B3 __attribute__((section ("user_var")));		// GATEWAY
uint32_t XMSK_B0 __attribute__((section ("user_var")));		// MASCARA DE REDE
uint32_t XMSK_B1 __attribute__((section ("user_var")));		// MASCARA DE REDE
uint32_t XMSK_B2 __attribute__((section ("user_var")));		// MASCARA DE REDE
uint32_t XMSK_B3 __attribute__((section ("user_var")));		// MASCARA DE REDE
uint32_t XPRTET __attribute__((section ("user_var")));		// PROTOCOLO
uint32_t XRV1ET __attribute__((section ("user_var")));		// RESERVA 1
uint32_t XRV2ET  __attribute__((section ("user_var")));		// RESERVA 2X

// ....... ENCODER
uint32_t XDVEN1 __attribute__((section ("user_var")));		// DIVISOR ENCODER-1
uint32_t XDVEN2 __attribute__((section ("user_var")));		// DIVISOR ENCODER-2
uint32_t XDVEN3 __attribute__((section ("user_var")));		// DIVISOR ENCODER-3
uint32_t XDVEN4 __attribute__((section ("user_var")));		// DIVISOR ENCODER-4

// ....... RESERVA
uint32_t __attribute__((section ("user_var"))) RSV_BACKUP[12];

// ======================  VARIAVEIS MATRICIAIS ALOCADAS  ======================		XYZ

// ....... Geral
uint32_t __attribute__  ((section ("user_var"))) VVV[1];
uint32_t __attribute__  ((section ("user_var"))) TLY[7];
uint32_t __attribute__  ((section ("user_var"))) FIF1[10];
uint32_t __attribute__  ((section ("user_var"))) FIF2[10];
uint32_t __attribute__  ((section ("user_var"))) FIF3[10];
uint32_t __attribute__  ((section ("user_var"))) FIF4[10];
uint32_t __attribute__  ((section ("user_var"))) FIF5[10];
uint32_t __attribute__  ((section ("user_var"))) FIF6[10];
uint32_t __attribute__  ((section ("user_var"))) FIF7[10];
uint32_t __attribute__  ((section ("user_var"))) FIF8[10];
uint32_t __attribute__  ((section ("user_var"))) AL1[101];
uint32_t __attribute__  ((section ("user_var"))) DT1[101];
uint32_t __attribute__  ((section ("user_var"))) HR1[101];
uint32_t __attribute__  ((section ("user_var"))) ALX[101];

// ....... BAnco de Dados
uint32_t __attribute__  ((section ("user_var"))) BDA[600];
uint32_t __attribute__  ((section ("user_var"))) BDD[2000];
uint32_t __attribute__  ((section ("user_var"))) BDL[100];

// ========================  VARIAVEIS LIVRES ALOCADAS  ========================

// ....... Espelho das EDG´s/SDG´s
uint32_t __attribute__  ((section ("user_var"))) EDG1;
uint32_t __attribute__  ((section ("user_var"))) EDG2;
uint32_t __attribute__  ((section ("user_var"))) EDG3;
uint32_t __attribute__  ((section ("user_var"))) EDG4;
uint32_t __attribute__  ((section ("user_var"))) EDG5;
uint32_t __attribute__  ((section ("user_var"))) EDG6;
uint32_t __attribute__  ((section ("user_var"))) EDG7;
uint32_t __attribute__  ((section ("user_var"))) EDG8;
uint32_t __attribute__  ((section ("user_var"))) SDG1;
uint32_t __attribute__  ((section ("user_var"))) SDG2;
uint32_t __attribute__  ((section ("user_var"))) SDG3;
uint32_t __attribute__  ((section ("user_var"))) SDG4;
uint32_t __attribute__  ((section ("user_var"))) SDG5;
uint32_t __attribute__  ((section ("user_var"))) SDG6;
uint32_t __attribute__  ((section ("user_var"))) SDG7;
uint32_t __attribute__  ((section ("user_var"))) SDG8;

uint32_t __attribute__  ((section ("user_var"))) EAN1;
uint32_t __attribute__  ((section ("user_var"))) EAN2;
uint32_t __attribute__  ((section ("user_var"))) EAN3;
uint32_t __attribute__  ((section ("user_var"))) EAN4;
uint32_t __attribute__  ((section ("user_var"))) EAN5;
uint32_t __attribute__  ((section ("user_var"))) EAN6;
uint32_t __attribute__  ((section ("user_var"))) EAN7;
uint32_t __attribute__  ((section ("user_var"))) EAN8;
uint32_t __attribute__  ((section ("user_var"))) SAN1;
uint32_t __attribute__  ((section ("user_var"))) SAN2;
uint32_t __attribute__  ((section ("user_var"))) SAN3;
uint32_t __attribute__  ((section ("user_var"))) SAN4;
uint32_t __attribute__  ((section ("user_var"))) SAN5;
uint32_t __attribute__  ((section ("user_var"))) SAN6;
uint32_t __attribute__  ((section ("user_var"))) SAN7;
uint32_t __attribute__  ((section ("user_var"))) SAN8;

// .......  F1 - SUPERVISAO
uint32_t __attribute__  ((section ("user_var"))) MNT;
uint32_t __attribute__  ((section ("user_var"))) LGD;
uint32_t __attribute__  ((section ("user_var"))) HBL;
uint32_t __attribute__  ((section ("user_var"))) EMG;
uint32_t __attribute__  ((section ("user_var"))) XTT;
uint32_t __attribute__  ((section ("user_var"))) TRN;
uint32_t __attribute__  ((section ("user_var"))) PDT;
uint32_t __attribute__  ((section ("user_var"))) SMQ;
uint32_t __attribute__  ((section ("user_var"))) GR1;
uint32_t __attribute__  ((section ("user_var"))) GR2;
uint32_t __attribute__  ((section ("user_var"))) GR3;
uint32_t __attribute__  ((section ("user_var"))) GR4;

uint32_t __attribute__  ((section ("user_var"))) TMP1;
uint32_t __attribute__  ((section ("user_var"))) TMP2;
uint32_t __attribute__  ((section ("user_var"))) TMP3;
uint32_t __attribute__  ((section ("user_var"))) TMP4;
uint32_t __attribute__  ((section ("user_var"))) TMP5;
uint32_t __attribute__  ((section ("user_var"))) UMD;
uint32_t __attribute__  ((section ("user_var"))) PRS;
uint32_t __attribute__  ((section ("user_var"))) VLC;
uint32_t __attribute__  ((section ("user_var"))) AQC1;

uint32_t __attribute__  ((section ("user_var"))) SPV1;
uint32_t __attribute__  ((section ("user_var"))) SPV2;
uint32_t __attribute__  ((section ("user_var"))) SPV3;
uint32_t __attribute__  ((section ("user_var"))) SPV4;
uint32_t __attribute__  ((section ("user_var"))) SPV5;
uint32_t __attribute__  ((section ("user_var"))) SPV6;
uint32_t __attribute__  ((section ("user_var"))) SPV7;
uint32_t __attribute__  ((section ("user_var"))) SPV8;
uint32_t __attribute__  ((section ("user_var"))) SPV9;
uint32_t __attribute__  ((section ("user_var"))) SPV10;

// .......  F2 - PARAMETROS DO PROCESSO
uint32_t __attribute__  ((section ("user_var"))) FED1;
uint32_t __attribute__  ((section ("user_var"))) FED2;
uint32_t __attribute__  ((section ("user_var"))) FED3;
uint32_t __attribute__  ((section ("user_var"))) FED4;
uint32_t __attribute__  ((section ("user_var"))) FED5;
uint32_t __attribute__  ((section ("user_var"))) FED6;
uint32_t __attribute__  ((section ("user_var"))) FED7;
uint32_t __attribute__  ((section ("user_var"))) FED8;

uint32_t __attribute__  ((section ("user_var"))) FEA1;
uint32_t __attribute__  ((section ("user_var"))) FEA2;
uint32_t __attribute__  ((section ("user_var"))) FEA3;
uint32_t __attribute__  ((section ("user_var"))) FEA4;
uint32_t __attribute__  ((section ("user_var"))) FEA5;
uint32_t __attribute__  ((section ("user_var"))) FEA6;
uint32_t __attribute__  ((section ("user_var"))) FEA7;
uint32_t __attribute__  ((section ("user_var"))) FEA8;

uint32_t __attribute__  ((section ("user_var"))) AQ11;
uint32_t __attribute__  ((section ("user_var"))) AQ12;
uint32_t __attribute__  ((section ("user_var"))) AQ13;
uint32_t __attribute__  ((section ("user_var"))) AQ14;
uint32_t __attribute__  ((section ("user_var"))) AQ15;
uint32_t __attribute__  ((section ("user_var"))) AQ16;
uint32_t __attribute__  ((section ("user_var"))) AQ17;
uint32_t __attribute__  ((section ("user_var"))) VLX1;
uint32_t __attribute__  ((section ("user_var"))) VLX2;
uint32_t __attribute__  ((section ("user_var"))) VLX3;
uint32_t __attribute__  ((section ("user_var"))) VLX4;
uint32_t __attribute__  ((section ("user_var"))) VLX5;
uint32_t __attribute__  ((section ("user_var"))) VLX6;
uint32_t __attribute__  ((section ("user_var"))) VLX7;
uint32_t __attribute__  ((section ("user_var"))) CM71;
uint32_t __attribute__  ((section ("user_var"))) CM72;
uint32_t __attribute__  ((section ("user_var"))) CM73;
uint32_t __attribute__  ((section ("user_var"))) CM74;
uint32_t __attribute__  ((section ("user_var"))) CM75;
uint32_t __attribute__  ((section ("user_var"))) CM76;
uint32_t __attribute__  ((section ("user_var"))) CM77;
uint32_t __attribute__  ((section ("user_var"))) CM81;
uint32_t __attribute__  ((section ("user_var"))) CM82;
uint32_t __attribute__  ((section ("user_var"))) CM83;
uint32_t __attribute__  ((section ("user_var"))) CM84;
uint32_t __attribute__  ((section ("user_var"))) CM85;
uint32_t __attribute__  ((section ("user_var"))) CM86;
uint32_t __attribute__  ((section ("user_var"))) CM87;
uint32_t __attribute__  ((section ("user_var"))) CM91;
uint32_t __attribute__  ((section ("user_var"))) CM92;
uint32_t __attribute__  ((section ("user_var"))) CM93;
uint32_t __attribute__  ((section ("user_var"))) CM94;
uint32_t __attribute__  ((section ("user_var"))) CM95;
uint32_t __attribute__  ((section ("user_var"))) CM96;
uint32_t __attribute__  ((section ("user_var"))) CM97;
uint32_t __attribute__  ((section ("user_var"))) CMA1;
uint32_t __attribute__  ((section ("user_var"))) CMA2;
uint32_t __attribute__  ((section ("user_var"))) CMA3;
uint32_t __attribute__  ((section ("user_var"))) CMA4;
uint32_t __attribute__  ((section ("user_var"))) CMA5;
uint32_t __attribute__  ((section ("user_var"))) CMA6;
uint32_t __attribute__  ((section ("user_var"))) CMA7;

uint32_t __attribute__  ((section ("user_var"))) VTP1;
uint32_t __attribute__  ((section ("user_var"))) VTP2;
uint32_t __attribute__  ((section ("user_var"))) VTP3;
uint32_t __attribute__  ((section ("user_var"))) VTP4;
uint32_t __attribute__  ((section ("user_var"))) VTP5;
uint32_t __attribute__  ((section ("user_var"))) UMMN;
uint32_t __attribute__  ((section ("user_var"))) UMMX;
uint32_t __attribute__  ((section ("user_var"))) ASV;

uint32_t __attribute__  ((section ("user_var"))) TNO1;
uint32_t __attribute__  ((section ("user_var"))) TNO2;
uint32_t __attribute__  ((section ("user_var"))) TNO3;
uint32_t __attribute__  ((section ("user_var"))) TNO4;
uint32_t __attribute__  ((section ("user_var"))) ABLT;
uint32_t __attribute__  ((section ("user_var"))) PRM1;
uint32_t __attribute__  ((section ("user_var"))) PRM2;
uint32_t __attribute__  ((section ("user_var"))) PRM3;
uint32_t __attribute__  ((section ("user_var"))) PRM4;
uint32_t __attribute__  ((section ("user_var"))) PRM5;
uint32_t __attribute__  ((section ("user_var"))) PRM6;
uint32_t __attribute__  ((section ("user_var"))) PRM7;
uint32_t __attribute__  ((section ("user_var"))) PRM8;
uint32_t __attribute__  ((section ("user_var"))) PRM9;
uint32_t __attribute__  ((section ("user_var"))) PRM10;

// .......  F5 - CONFIGURACAO
uint32_t __attribute__  ((section ("user_var"))) OFF1;
uint32_t __attribute__  ((section ("user_var"))) OFF2;
uint32_t __attribute__  ((section ("user_var"))) OFF3;
uint32_t __attribute__  ((section ("user_var"))) OFF4;
uint32_t __attribute__  ((section ("user_var"))) OFF5;
uint32_t __attribute__  ((section ("user_var"))) OFF6;
uint32_t __attribute__  ((section ("user_var"))) OFF7;
uint32_t __attribute__  ((section ("user_var"))) OFF8;
uint32_t __attribute__  ((section ("user_var"))) RNG1;
uint32_t __attribute__  ((section ("user_var"))) RNG2;
uint32_t __attribute__  ((section ("user_var"))) RNG3;
uint32_t __attribute__  ((section ("user_var"))) RNG4;
uint32_t __attribute__  ((section ("user_var"))) RNG5;
uint32_t __attribute__  ((section ("user_var"))) RNG6;
uint32_t __attribute__  ((section ("user_var"))) RNG7;
uint32_t __attribute__  ((section ("user_var"))) RNG8;
uint32_t __attribute__  ((section ("user_var"))) CFG1;
uint32_t __attribute__  ((section ("user_var"))) CFG2;
uint32_t __attribute__  ((section ("user_var"))) CFG3;
uint32_t __attribute__  ((section ("user_var"))) CFG4;
uint32_t __attribute__  ((section ("user_var"))) CFG5;
uint32_t __attribute__  ((section ("user_var"))) CFG6;
uint32_t __attribute__  ((section ("user_var"))) CFG7;
uint32_t __attribute__  ((section ("user_var"))) CFG8;
uint32_t __attribute__  ((section ("user_var"))) CFG9;
uint32_t __attribute__  ((section ("user_var"))) CFG10;
uint32_t __attribute__  ((section ("user_var"))) RZV1;
uint32_t __attribute__  ((section ("user_var"))) RZV2;
uint32_t __attribute__  ((section ("user_var"))) RZV3;
uint32_t __attribute__  ((section ("user_var"))) RZV4;
uint32_t __attribute__  ((section ("user_var"))) RZV5;
uint32_t __attribute__  ((section ("user_var"))) RZV6;
uint32_t __attribute__  ((section ("user_var"))) RZV7;
uint32_t __attribute__  ((section ("user_var"))) RZV8;
uint32_t __attribute__  ((section ("user_var"))) RZV9;
uint32_t __attribute__  ((section ("user_var"))) RZV10;

// ....... Variaveis do SIMULADOR (COM endereco Fixo)
uint32_t __attribute__  ((section ("user_var"))) TST;
uint32_t __attribute__  ((section ("user_var"))) AAA;
uint32_t __attribute__  ((section ("user_var"))) XLM1;
uint32_t __attribute__  ((section ("user_var"))) XLM2;
uint32_t __attribute__  ((section ("user_var"))) XLM3;

// =====================  VARIAVEIS LIVRES SEM POSICAO FIXA  =====================

// ....... Variaveis Operacionais
uint32_t __attribute__  ((section ("user_var"))) ASG;
uint32_t __attribute__  ((section ("user_var"))) DTE;
uint32_t __attribute__  ((section ("user_var"))) RLG;
uint32_t __attribute__  ((section ("user_var"))) DPY;

// ....... Variaveis Mapa de Telas
uint32_t __attribute__  ((section ("user_var"))) TKL;
uint32_t __attribute__  ((section ("user_var"))) KEY;
uint32_t __attribute__  ((section ("user_var"))) MPT;
uint32_t __attribute__  ((section ("user_var"))) TLA;
uint32_t __attribute__  ((section ("user_var"))) XTLA;
uint32_t __attribute__  ((section ("user_var"))) XTLY;
uint32_t __attribute__  ((section ("user_var"))) ZTLA;
uint32_t __attribute__  ((section ("user_var"))) ZTLY;
uint32_t __attribute__  ((section ("user_var"))) ZY1;
uint32_t __attribute__  ((section ("user_var"))) ZY2;
uint32_t __attribute__  ((section ("user_var"))) ZY3;
uint32_t __attribute__  ((section ("user_var"))) ZY4;
uint32_t __attribute__  ((section ("user_var"))) ZY5;

// ....... Variaveis Auxiliares das Maquinas (SEM endereco Fixo)
uint32_t __attribute__  ((section ("user_var"))) VA0;
uint32_t __attribute__  ((section ("user_var"))) VA1;
uint32_t __attribute__  ((section ("user_var"))) VA2;
uint32_t __attribute__  ((section ("user_var"))) VB0;
uint32_t __attribute__  ((section ("user_var"))) VB1;
uint32_t __attribute__  ((section ("user_var"))) VB2;
uint32_t __attribute__  ((section ("user_var"))) VC0;
uint32_t __attribute__  ((section ("user_var"))) VC1;
uint32_t __attribute__  ((section ("user_var"))) VC2;
uint32_t __attribute__  ((section ("user_var"))) VC3;
uint32_t __attribute__  ((section ("user_var"))) VC4;
uint32_t __attribute__  ((section ("user_var"))) VC5;
uint32_t __attribute__  ((section ("user_var"))) VC6;
uint32_t __attribute__  ((section ("user_var"))) VC7;
uint32_t __attribute__  ((section ("user_var"))) VC8;
uint32_t __attribute__  ((section ("user_var"))) VC9;
uint32_t __attribute__  ((section ("user_var"))) VC10;
uint32_t __attribute__  ((section ("user_var"))) VC11;
uint32_t __attribute__  ((section ("user_var"))) VC12;
uint32_t __attribute__  ((section ("user_var"))) VC13;
uint32_t __attribute__  ((section ("user_var"))) VC14;
uint32_t __attribute__  ((section ("user_var"))) VC15;
uint32_t __attribute__  ((section ("user_var"))) VC16;
uint32_t __attribute__  ((section ("user_var"))) VC17;
uint32_t __attribute__  ((section ("user_var"))) VC18;
uint32_t __attribute__  ((section ("user_var"))) VC19;
uint32_t __attribute__  ((section ("user_var"))) VC20;
uint32_t __attribute__  ((section ("user_var"))) VD0;
uint32_t __attribute__  ((section ("user_var"))) VD1;
uint32_t __attribute__  ((section ("user_var"))) VD2;
uint32_t __attribute__  ((section ("user_var"))) VE0;
uint32_t __attribute__  ((section ("user_var"))) VE1;
uint32_t __attribute__  ((section ("user_var"))) VE2;
uint32_t __attribute__  ((section ("user_var"))) VF0;
uint32_t __attribute__  ((section ("user_var"))) VF1;
uint32_t __attribute__  ((section ("user_var"))) VF2;
uint32_t __attribute__  ((section ("user_var"))) VG0;
uint32_t __attribute__  ((section ("user_var"))) VG1;
uint32_t __attribute__  ((section ("user_var"))) VG2;
uint32_t __attribute__  ((section ("user_var"))) VH0;
uint32_t __attribute__  ((section ("user_var"))) VH1;
uint32_t __attribute__  ((section ("user_var"))) VH2;
uint32_t __attribute__  ((section ("user_var"))) VI0;
uint32_t __attribute__  ((section ("user_var"))) VI1;
uint32_t __attribute__  ((section ("user_var"))) VI2;
uint32_t __attribute__  ((section ("user_var"))) VJ0;
uint32_t __attribute__  ((section ("user_var"))) VJ1;
uint32_t __attribute__  ((section ("user_var"))) VJ2;
uint32_t __attribute__  ((section ("user_var"))) VK0;
uint32_t __attribute__  ((section ("user_var"))) VK1;
uint32_t __attribute__  ((section ("user_var"))) VK2;
uint32_t __attribute__  ((section ("user_var"))) VL0;
uint32_t __attribute__  ((section ("user_var"))) VL1;
uint32_t __attribute__  ((section ("user_var"))) VL2;
uint32_t __attribute__  ((section ("user_var"))) VM0;
uint32_t __attribute__  ((section ("user_var"))) VM1;
uint32_t __attribute__  ((section ("user_var"))) VM2;
uint32_t __attribute__  ((section ("user_var"))) VN0;
uint32_t __attribute__  ((section ("user_var"))) VN1;
uint32_t __attribute__  ((section ("user_var"))) VN2;
uint32_t __attribute__  ((section ("user_var"))) VO0;
uint32_t __attribute__  ((section ("user_var"))) VO1;
uint32_t __attribute__  ((section ("user_var"))) VO2;
uint32_t __attribute__  ((section ("user_var"))) VP0;
uint32_t __attribute__  ((section ("user_var"))) VP1;
uint32_t __attribute__  ((section ("user_var"))) VP2;
uint32_t __attribute__  ((section ("user_var"))) VQ0;
uint32_t __attribute__  ((section ("user_var"))) VQ1;
uint32_t __attribute__  ((section ("user_var"))) VQ2;
uint32_t __attribute__  ((section ("user_var"))) VR0;
uint32_t __attribute__  ((section ("user_var"))) VR1;
uint32_t __attribute__  ((section ("user_var"))) VR2;
uint32_t __attribute__  ((section ("user_var"))) VS0;
uint32_t __attribute__  ((section ("user_var"))) VS1;
uint32_t __attribute__  ((section ("user_var"))) VS2;
uint32_t __attribute__  ((section ("user_var"))) VT0;
uint32_t __attribute__  ((section ("user_var"))) VT1;
uint32_t __attribute__  ((section ("user_var"))) VT2;
uint32_t __attribute__  ((section ("user_var"))) VU0;
uint32_t __attribute__  ((section ("user_var"))) VU1;
uint32_t __attribute__  ((section ("user_var"))) VU2;
uint32_t __attribute__  ((section ("user_var"))) VV0;
uint32_t __attribute__  ((section ("user_var"))) VV1;
uint32_t __attribute__  ((section ("user_var"))) VV2;
uint32_t __attribute__  ((section ("user_var"))) VV3;
uint32_t __attribute__  ((section ("user_var"))) VV4;
uint32_t __attribute__  ((section ("user_var"))) VV5;
uint32_t __attribute__  ((section ("user_var"))) VV6;
uint32_t __attribute__  ((section ("user_var"))) VV7;
uint32_t __attribute__  ((section ("user_var"))) VV8;
uint32_t __attribute__  ((section ("user_var"))) VV9;
uint32_t __attribute__  ((section ("user_var"))) VW0;
uint32_t __attribute__  ((section ("user_var"))) VW1;
uint32_t __attribute__  ((section ("user_var"))) VW2;
uint32_t __attribute__  ((section ("user_var"))) VX0;
uint32_t __attribute__  ((section ("user_var"))) VX1;
uint32_t __attribute__  ((section ("user_var"))) VX2;
uint32_t __attribute__  ((section ("user_var"))) VY0;
uint32_t __attribute__  ((section ("user_var"))) VY1;
uint32_t __attribute__  ((section ("user_var"))) VY2;
uint32_t __attribute__  ((section ("user_var"))) VZ0;
uint32_t __attribute__  ((section ("user_var"))) VZ1;
uint32_t __attribute__  ((section ("user_var"))) VZ2;
uint32_t __attribute__  ((section ("user_var"))) VZ3;
uint32_t __attribute__  ((section ("user_var"))) VZ4;
uint32_t __attribute__  ((section ("user_var"))) VZ5;
uint32_t __attribute__  ((section ("user_var"))) VZ6;
uint32_t __attribute__  ((section ("user_var"))) VZ7;
uint32_t __attribute__  ((section ("user_var"))) VZ8;
uint32_t __attribute__  ((section ("user_var"))) VZ9;
uint32_t __attribute__  ((section ("user_var"))) VZ10;
uint32_t __attribute__  ((section ("user_var"))) VZA;
uint32_t __attribute__  ((section ("user_var"))) VZB;
uint32_t __attribute__  ((section ("user_var"))) VZC;
uint32_t __attribute__  ((section ("user_var"))) VZD;
uint32_t __attribute__  ((section ("user_var"))) VZE;
uint32_t __attribute__  ((section ("user_var"))) VZF;
uint32_t __attribute__  ((section ("user_var"))) VZG;
uint32_t __attribute__  ((section ("user_var"))) VZH;
uint32_t __attribute__  ((section ("user_var"))) VZI;
uint32_t __attribute__  ((section ("user_var"))) VZJ;
uint32_t __attribute__  ((section ("user_var"))) VZK;
uint32_t __attribute__  ((section ("user_var"))) VZL;

uint32_t __attribute__  ((section ("user_var"))) ZA0;	// ....... >> SIMULADOR
uint32_t __attribute__  ((section ("user_var"))) ZA1;
uint32_t __attribute__  ((section ("user_var"))) ZA2;
uint32_t __attribute__  ((section ("user_var"))) ZA7;
uint32_t __attribute__  ((section ("user_var"))) ZB0;
uint32_t __attribute__  ((section ("user_var"))) ZB1;
uint32_t __attribute__  ((section ("user_var"))) ZB2;
uint32_t __attribute__  ((section ("user_var"))) ZC0;
uint32_t __attribute__  ((section ("user_var"))) ZC1;
uint32_t __attribute__  ((section ("user_var"))) ZC2;
uint32_t __attribute__  ((section ("user_var"))) ZD0;
uint32_t __attribute__  ((section ("user_var"))) ZD1;
uint32_t __attribute__  ((section ("user_var"))) ZD2;
uint32_t __attribute__  ((section ("user_var"))) ZE0;
uint32_t __attribute__  ((section ("user_var"))) ZE1;
uint32_t __attribute__  ((section ("user_var"))) ZE2;

// ....... Temporizadores (SEM endereco Fixo)
uint32_t __attribute__  ((section ("user_var"))) DC0;
uint32_t __attribute__  ((section ("user_var"))) DC1;
uint32_t __attribute__  ((section ("user_var"))) DC2;
uint32_t __attribute__  ((section ("user_var"))) DC3;
uint32_t __attribute__  ((section ("user_var"))) DC4;
uint32_t __attribute__  ((section ("user_var"))) DC5;

uint32_t __attribute__  ((section ("user_var"))) SG0;
uint32_t __attribute__  ((section ("user_var"))) SG1;
uint32_t __attribute__  ((section ("user_var"))) SG2;
uint32_t __attribute__  ((section ("user_var"))) SG3;
uint32_t __attribute__  ((section ("user_var"))) SG4;
uint32_t __attribute__  ((section ("user_var"))) SG5;
uint32_t __attribute__  ((section ("user_var"))) CG1;
uint32_t __attribute__  ((section ("user_var"))) CG2;
uint32_t __attribute__  ((section ("user_var"))) CG3;
uint32_t __attribute__  ((section ("user_var"))) CG4;
uint32_t __attribute__  ((section ("user_var"))) CG5;
uint32_t __attribute__  ((section ("user_var"))) CG6;
uint32_t __attribute__  ((section ("user_var"))) CG7;
uint32_t __attribute__  ((section ("user_var"))) CG8;
uint32_t __attribute__  ((section ("user_var"))) SC1;
uint32_t __attribute__  ((section ("user_var"))) SC2;
uint32_t __attribute__  ((section ("user_var"))) SC3;
uint32_t __attribute__  ((section ("user_var"))) SC4;
uint32_t __attribute__  ((section ("user_var"))) SC5;
uint32_t __attribute__  ((section ("user_var"))) SC6;
uint32_t __attribute__  ((section ("user_var"))) SC7;
uint32_t __attribute__  ((section ("user_var"))) SC8;
uint32_t __attribute__  ((section ("user_var"))) SC9;
uint32_t __attribute__  ((section ("user_var"))) SC10;
uint32_t __attribute__  ((section ("user_var"))) XC1;	// ....... >> SIMULADOR
uint32_t __attribute__  ((section ("user_var"))) XC2;
uint32_t __attribute__  ((section ("user_var"))) XC3;
uint32_t __attribute__  ((section ("user_var"))) XC4;
uint32_t __attribute__  ((section ("user_var"))) XC5;

uint32_t __attribute__  ((section ("user_var"))) MN1;
uint32_t __attribute__  ((section ("user_var"))) MN2;
uint32_t __attribute__  ((section ("user_var"))) MN3;

// ....... Ponteiros (SEM endereco Fixo)
uint32_t __attribute__  ((section ("user_var"))) ZZ1;
uint32_t __attribute__  ((section ("user_var"))) ZZ2;
uint32_t __attribute__  ((section ("user_var"))) ZZ3;
uint32_t __attribute__  ((section ("user_var"))) ZZ4;
uint32_t __attribute__  ((section ("user_var"))) ZZ5;
uint32_t __attribute__  ((section ("user_var"))) ZZ6;
uint32_t __attribute__  ((section ("user_var"))) ZZ7;
uint32_t __attribute__  ((section ("user_var"))) ZZ8;
uint32_t __attribute__  ((section ("user_var"))) ZZ9;
uint32_t __attribute__  ((section ("user_var"))) ZZA;

// ....... Contadores (SEM endereco Fixo)
uint32_t __attribute__  ((section ("user_var"))) KKA;
uint32_t __attribute__  ((section ("user_var"))) KKB;
uint32_t __attribute__  ((section ("user_var"))) KKC;
uint32_t __attribute__  ((section ("user_var"))) KKD;
uint32_t __attribute__  ((section ("user_var"))) KKE;

// ....... Acumuladores (SEM endereco Fixo)
uint32_t __attribute__  ((section ("user_var"))) YYA;
uint32_t __attribute__  ((section ("user_var"))) YYB;
uint32_t __attribute__  ((section ("user_var"))) YYC;
uint32_t __attribute__  ((section ("user_var"))) YYD;
uint32_t __attribute__  ((section ("user_var"))) YYE;

// ....... Diversos (SEM endereco Fixo)
uint32_t __attribute__  ((section ("user_var"))) SLD1;
uint32_t __attribute__  ((section ("user_var"))) SLD2;
uint32_t __attribute__  ((section ("user_var"))) SLD3;

uint32_t __attribute__  ((section ("user_var"))) BBB;						// KKK

// ======================  SIMULADOR DE ANOMALIAS (SEM endereco Fixo)  ======================	xx10

// ....... Espelho das ED's sem filtro
uint32_t __attribute__  ((section ("user_var"))) EDG9;
uint32_t __attribute__  ((section ("user_var"))) EDG10;
uint32_t __attribute__  ((section ("user_var"))) EDG11;
uint32_t __attribute__  ((section ("user_var"))) EDG12;
uint32_t __attribute__  ((section ("user_var"))) EDG13;

// ....... Parametros F2 / F5
uint32_t __attribute__  ((section ("user_var"))) PSD;
uint32_t __attribute__  ((section ("user_var"))) PSN;
uint32_t __attribute__  ((section ("user_var"))) PNH;
uint32_t __attribute__  ((section ("user_var"))) PNE;
uint32_t __attribute__  ((section ("user_var"))) PKP;
uint32_t __attribute__  ((section ("user_var"))) PKI;
uint32_t __attribute__  ((section ("user_var"))) PKD;
uint32_t __attribute__  ((section ("user_var"))) SFB;
uint32_t __attribute__  ((section ("user_var"))) SMN;
uint32_t __attribute__  ((section ("user_var"))) SAS;
uint32_t __attribute__  ((section ("user_var"))) SAP;
uint32_t __attribute__  ((section ("user_var"))) TAS;
uint32_t __attribute__  ((section ("user_var"))) ROT;
uint32_t __attribute__  ((section ("user_var"))) ARS;
uint32_t __attribute__  ((section ("user_var"))) TWA;
uint32_t __attribute__  ((section ("user_var"))) TWT;
uint32_t __attribute__  ((section ("user_var"))) TBA;
uint32_t __attribute__  ((section ("user_var"))) TBT;
uint32_t __attribute__  ((section ("user_var"))) VBA;
uint32_t __attribute__  ((section ("user_var"))) VBT;
uint32_t __attribute__  ((section ("user_var"))) IAL;
uint32_t __attribute__  ((section ("user_var"))) ITR;
uint32_t __attribute__  ((section ("user_var"))) PSL;
uint32_t __attribute__  ((section ("user_var"))) LON;
uint32_t __attribute__  ((section ("user_var"))) LOF;
uint32_t __attribute__  ((section ("user_var"))) LLL;
uint32_t __attribute__  ((section ("user_var"))) LLR;
uint32_t __attribute__  ((section ("user_var"))) PDL;
uint32_t __attribute__  ((section ("user_var"))) PDH;
uint32_t __attribute__  ((section ("user_var"))) HINI;
uint32_t __attribute__  ((section ("user_var"))) SNG;
uint32_t __attribute__  ((section ("user_var"))) FVS;

// ....... Medidas do CLP
uint32_t __attribute__  ((section ("user_var"))) MLT;
uint32_t __attribute__  ((section ("user_var"))) MPS;
uint32_t __attribute__  ((section ("user_var"))) MPD;
uint32_t __attribute__  ((section ("user_var"))) MFQ;
uint32_t __attribute__  ((section ("user_var"))) MQE;
uint32_t __attribute__  ((section ("user_var"))) MI1;
uint32_t __attribute__  ((section ("user_var"))) MI2;
uint32_t __attribute__  ((section ("user_var"))) MV1;
uint32_t __attribute__  ((section ("user_var"))) MV2;
uint32_t __attribute__  ((section ("user_var"))) MS1;
uint32_t __attribute__  ((section ("user_var"))) MS2;
uint32_t __attribute__  ((section ("user_var"))) MPW;
uint32_t __attribute__  ((section ("user_var"))) MW1;
uint32_t __attribute__  ((section ("user_var"))) MW2;
uint32_t __attribute__  ((section ("user_var"))) MB1;
uint32_t __attribute__  ((section ("user_var"))) MB2;
uint32_t __attribute__  ((section ("user_var"))) MTF;
uint32_t __attribute__  ((section ("user_var"))) MTA;
uint32_t __attribute__  ((section ("user_var"))) SNF;

// ....... Estacao / Bombas / Protecoes
uint32_t __attribute__  ((section ("user_var"))) ETP;
uint32_t __attribute__  ((section ("user_var"))) SB1;
uint32_t __attribute__  ((section ("user_var"))) SB2;
uint32_t __attribute__  ((section ("user_var"))) DTY;
uint32_t __attribute__  ((section ("user_var"))) RQ1;
uint32_t __attribute__  ((section ("user_var"))) RQ2;
uint32_t __attribute__  ((section ("user_var"))) CM1;
uint32_t __attribute__  ((section ("user_var"))) CM2;
uint32_t __attribute__  ((section ("user_var"))) CXV;
uint32_t __attribute__  ((section ("user_var"))) XVS;
uint32_t __attribute__  ((section ("user_var"))) XVF;
uint32_t __attribute__  ((section ("user_var"))) LLO;
uint32_t __attribute__  ((section ("user_var"))) SOK;
uint32_t __attribute__  ((section ("user_var"))) AV1;
uint32_t __attribute__  ((section ("user_var"))) AV2;
uint32_t __attribute__  ((section ("user_var"))) RV1;
uint32_t __attribute__  ((section ("user_var"))) RV2;
uint32_t __attribute__  ((section ("user_var"))) FR1;
uint32_t __attribute__  ((section ("user_var"))) FR2;
uint32_t __attribute__  ((section ("user_var"))) TR1;
uint32_t __attribute__  ((section ("user_var"))) TR2;
uint32_t __attribute__  ((section ("user_var"))) ARC1;
uint32_t __attribute__  ((section ("user_var"))) ARC2;
uint32_t __attribute__  ((section ("user_var"))) RU1;
uint32_t __attribute__  ((section ("user_var"))) RU2;
uint32_t __attribute__  ((section ("user_var"))) PSP;
uint32_t __attribute__  ((section ("user_var"))) PSX;
uint32_t __attribute__  ((section ("user_var"))) PSXL;
uint32_t __attribute__  ((section ("user_var"))) SPR;
uint32_t __attribute__  ((section ("user_var"))) SRF;
uint32_t __attribute__  ((section ("user_var"))) RTH;
uint32_t __attribute__  ((section ("user_var"))) NRN;
uint32_t __attribute__  ((section ("user_var"))) RSTQ;
uint32_t __attribute__  ((section ("user_var"))) GOP;
uint32_t __attribute__  ((section ("user_var"))) GOC;
uint32_t __attribute__  ((section ("user_var"))) FDS;
uint32_t __attribute__  ((section ("user_var"))) NGT;
uint32_t __attribute__  ((section ("user_var"))) HRS;
uint32_t __attribute__  ((section ("user_var"))) ALC;
uint32_t __attribute__  ((section ("user_var"))) ALN;
uint32_t __attribute__  ((section ("user_var"))) KW1;
uint32_t __attribute__  ((section ("user_var"))) KW2;
uint32_t __attribute__  ((section ("user_var"))) KB1;
uint32_t __attribute__  ((section ("user_var"))) KB2;
uint32_t __attribute__  ((section ("user_var"))) KV1;
uint32_t __attribute__  ((section ("user_var"))) KV2;
uint32_t __attribute__  ((section ("user_var"))) KI1;
uint32_t __attribute__  ((section ("user_var"))) KI2;
uint32_t __attribute__  ((section ("user_var"))) KSL;
uint32_t __attribute__  ((section ("user_var"))) SLD4;

// ....... Publicacao / Cenario / Eventos
uint32_t __attribute__  ((section ("user_var"))) RBI;
uint32_t __attribute__  ((section ("user_var"))) DLY;
uint32_t __attribute__  ((section ("user_var"))) CMC;
uint32_t __attribute__  ((section ("user_var"))) CMS;
uint32_t __attribute__  ((section ("user_var"))) VL3;
uint32_t __attribute__  ((section ("user_var"))) VL4;
uint32_t __attribute__  ((section ("user_var"))) VL5;
uint32_t __attribute__  ((section ("user_var"))) VL6;
uint32_t __attribute__  ((section ("user_var"))) VL7;
uint32_t __attribute__  ((section ("user_var"))) RSI;
uint32_t __attribute__  ((section ("user_var"))) RSC;
uint32_t __attribute__  ((section ("user_var"))) LOS;
uint32_t __attribute__  ((section ("user_var"))) SCP;
uint32_t __attribute__  ((section ("user_var"))) SCE;
uint32_t __attribute__  ((section ("user_var"))) SCS;
uint32_t __attribute__  ((section ("user_var"))) SCT;
uint32_t __attribute__  ((section ("user_var"))) SST;
uint32_t __attribute__  ((section ("user_var"))) DOV;
uint32_t __attribute__  ((section ("user_var"))) SPO;
uint32_t __attribute__  ((section ("user_var"))) QDU;
uint32_t __attribute__  ((section ("user_var"))) QFC;
uint32_t __attribute__  ((section ("user_var"))) QFT;
uint32_t __attribute__  ((section ("user_var"))) QFI;
uint32_t __attribute__  ((section ("user_var"))) QFM;
uint32_t __attribute__  ((section ("user_var"))) QFR;
uint32_t __attribute__  ((section ("user_var"))) QFD;
uint32_t __attribute__  ((section ("user_var"))) QRC;
uint32_t __attribute__  ((section ("user_var"))) QDM;
uint32_t __attribute__  ((section ("user_var"))) QSP;
uint32_t __attribute__  ((section ("user_var"))) QOP;
uint32_t __attribute__  ((section ("user_var"))) VM3;
uint32_t __attribute__  ((section ("user_var"))) VM4;
uint32_t __attribute__  ((section ("user_var"))) VM5;
uint32_t __attribute__  ((section ("user_var"))) VM6;
uint32_t __attribute__  ((section ("user_var"))) VM7;
uint32_t __attribute__  ((section ("user_var"))) VM8;
uint32_t __attribute__  ((section ("user_var"))) VM9;
uint32_t __attribute__  ((section ("user_var"))) VMA;
uint32_t __attribute__  ((section ("user_var"))) VMB;
uint32_t __attribute__  ((section ("user_var"))) LPA;
uint32_t __attribute__  ((section ("user_var"))) LPB;
uint32_t __attribute__  ((section ("user_var"))) LPC;
uint32_t __attribute__  ((section ("user_var"))) LSE;
uint32_t __attribute__  ((section ("user_var"))) LSS;
uint32_t __attribute__  ((section ("user_var"))) LGC;
uint32_t __attribute__  ((section ("user_var"))) EVT;
uint32_t __attribute__  ((section ("user_var"))) EVC;
uint32_t __attribute__  ((section ("user_var"))) EVK;
uint32_t __attribute__  ((section ("user_var"))) EVS;
uint32_t __attribute__  ((section ("user_var"))) EVM;
uint32_t __attribute__  ((section ("user_var"))) VN3;
uint32_t __attribute__  ((section ("user_var"))) VN4;
uint32_t __attribute__  ((section ("user_var"))) VN5;
uint32_t __attribute__  ((section ("user_var"))) VN6;
uint32_t __attribute__  ((section ("user_var"))) VN7;
uint32_t __attribute__  ((section ("user_var"))) VN8;
uint32_t __attribute__  ((section ("user_var"))) VN9;
uint32_t __attribute__  ((section ("user_var"))) VNA;
uint32_t __attribute__  ((section ("user_var"))) VNB;
uint32_t __attribute__  ((section ("user_var"))) VNC;

// ....... Slots de Falha A/B/C
uint32_t __attribute__  ((section ("user_var"))) XCA;
uint32_t __attribute__  ((section ("user_var"))) XTA;
uint32_t __attribute__  ((section ("user_var"))) XIA;
uint32_t __attribute__  ((section ("user_var"))) XMA;
uint32_t __attribute__  ((section ("user_var"))) XRA;
uint32_t __attribute__  ((section ("user_var"))) XDA;
uint32_t __attribute__  ((section ("user_var"))) XEA;
uint32_t __attribute__  ((section ("user_var"))) XGA;
uint32_t __attribute__  ((section ("user_var"))) XKA;
uint32_t __attribute__  ((section ("user_var"))) XQA;
uint32_t __attribute__  ((section ("user_var"))) GCA;
uint32_t __attribute__  ((section ("user_var"))) TGA;
uint32_t __attribute__  ((section ("user_var"))) INA;
uint32_t __attribute__  ((section ("user_var"))) MDA;
uint32_t __attribute__  ((section ("user_var"))) RTA;
uint32_t __attribute__  ((section ("user_var"))) DUA;
uint32_t __attribute__  ((section ("user_var"))) RCA;
uint32_t __attribute__  ((section ("user_var"))) SVA;
uint32_t __attribute__  ((section ("user_var"))) PHA;
uint32_t __attribute__  ((section ("user_var"))) PTA;
uint32_t __attribute__  ((section ("user_var"))) FIA;
uint32_t __attribute__  ((section ("user_var"))) SCA;
uint32_t __attribute__  ((section ("user_var"))) SDA;
uint32_t __attribute__  ((section ("user_var"))) TEA;
uint32_t __attribute__  ((section ("user_var"))) TTA;
uint32_t __attribute__  ((section ("user_var"))) SRA;
uint32_t __attribute__  ((section ("user_var"))) IOA;
uint32_t __attribute__  ((section ("user_var"))) IWA;
uint32_t __attribute__  ((section ("user_var"))) XCB;
uint32_t __attribute__  ((section ("user_var"))) XTB;
uint32_t __attribute__  ((section ("user_var"))) XIB;
uint32_t __attribute__  ((section ("user_var"))) XMB;
uint32_t __attribute__  ((section ("user_var"))) XRB;
uint32_t __attribute__  ((section ("user_var"))) XDB;
uint32_t __attribute__  ((section ("user_var"))) XEB;
uint32_t __attribute__  ((section ("user_var"))) XGB;
uint32_t __attribute__  ((section ("user_var"))) XKB;
uint32_t __attribute__  ((section ("user_var"))) XQB;
uint32_t __attribute__  ((section ("user_var"))) GCB;
uint32_t __attribute__  ((section ("user_var"))) TGB;
uint32_t __attribute__  ((section ("user_var"))) INB;
uint32_t __attribute__  ((section ("user_var"))) MDB;
uint32_t __attribute__  ((section ("user_var"))) RTB;
uint32_t __attribute__  ((section ("user_var"))) DUB;
uint32_t __attribute__  ((section ("user_var"))) RCB;
uint32_t __attribute__  ((section ("user_var"))) SVB;
uint32_t __attribute__  ((section ("user_var"))) PHB;
uint32_t __attribute__  ((section ("user_var"))) PTB;
uint32_t __attribute__  ((section ("user_var"))) FIB;
uint32_t __attribute__  ((section ("user_var"))) SCB;
uint32_t __attribute__  ((section ("user_var"))) SDB;
uint32_t __attribute__  ((section ("user_var"))) TEB;
uint32_t __attribute__  ((section ("user_var"))) TTB;
uint32_t __attribute__  ((section ("user_var"))) SRB;
uint32_t __attribute__  ((section ("user_var"))) IOB;
uint32_t __attribute__  ((section ("user_var"))) IWB;
uint32_t __attribute__  ((section ("user_var"))) XCC;
uint32_t __attribute__  ((section ("user_var"))) XTC;
uint32_t __attribute__  ((section ("user_var"))) XIC;
uint32_t __attribute__  ((section ("user_var"))) XMC;
uint32_t __attribute__  ((section ("user_var"))) XRC;
uint32_t __attribute__  ((section ("user_var"))) XDC;
uint32_t __attribute__  ((section ("user_var"))) XEC;
uint32_t __attribute__  ((section ("user_var"))) XGC;
uint32_t __attribute__  ((section ("user_var"))) XKC;
uint32_t __attribute__  ((section ("user_var"))) XQC;
uint32_t __attribute__  ((section ("user_var"))) GCC;
uint32_t __attribute__  ((section ("user_var"))) TGC;
uint32_t __attribute__  ((section ("user_var"))) INC;
uint32_t __attribute__  ((section ("user_var"))) MDC;
uint32_t __attribute__  ((section ("user_var"))) RTC;
uint32_t __attribute__  ((section ("user_var"))) DUC;
uint32_t __attribute__  ((section ("user_var"))) RCC;
uint32_t __attribute__  ((section ("user_var"))) SVC;
uint32_t __attribute__  ((section ("user_var"))) PHC;
uint32_t __attribute__  ((section ("user_var"))) PTC;
uint32_t __attribute__  ((section ("user_var"))) FIC;
uint32_t __attribute__  ((section ("user_var"))) SCC;
uint32_t __attribute__  ((section ("user_var"))) SDC;
uint32_t __attribute__  ((section ("user_var"))) TEC;
uint32_t __attribute__  ((section ("user_var"))) TTC;
uint32_t __attribute__  ((section ("user_var"))) SRC;
uint32_t __attribute__  ((section ("user_var"))) IOC;
uint32_t __attribute__  ((section ("user_var"))) IWC;

// ....... Planta Simulada (MAQUINA_26)
uint32_t __attribute__  ((section ("user_var"))) RNA;
uint32_t __attribute__  ((section ("user_var"))) RNB;
uint32_t __attribute__  ((section ("user_var"))) RNC;
uint32_t __attribute__  ((section ("user_var"))) RND;
uint32_t __attribute__  ((section ("user_var"))) TOD;
uint32_t __attribute__  ((section ("user_var"))) TDK;
uint32_t __attribute__  ((section ("user_var"))) TKC;
uint32_t __attribute__  ((section ("user_var"))) DPF;
uint32_t __attribute__  ((section ("user_var"))) DRW;
uint32_t __attribute__  ((section ("user_var"))) DMT;
uint32_t __attribute__  ((section ("user_var"))) DMA;
uint32_t __attribute__  ((section ("user_var"))) DMF;
uint32_t __attribute__  ((section ("user_var"))) SUF;
uint32_t __attribute__  ((section ("user_var"))) XVP;
uint32_t __attribute__  ((section ("user_var"))) QIN;
uint32_t __attribute__  ((section ("user_var"))) QOUT;
uint32_t __attribute__  ((section ("user_var"))) Q1;
uint32_t __attribute__  ((section ("user_var"))) Q2;
uint32_t __attribute__  ((section ("user_var"))) QLK;
uint32_t __attribute__  ((section ("user_var"))) VOL;
uint32_t __attribute__  ((section ("user_var"))) LVT;
uint32_t __attribute__  ((section ("user_var"))) PSA;
uint32_t __attribute__  ((section ("user_var"))) PNA;
uint32_t __attribute__  ((section ("user_var"))) PDG;
uint32_t __attribute__  ((section ("user_var"))) DPO;
uint32_t __attribute__  ((section ("user_var"))) RCO;
uint32_t __attribute__  ((section ("user_var"))) RTO;
uint32_t __attribute__  ((section ("user_var"))) CAV;
uint32_t __attribute__  ((section ("user_var"))) NS1;
uint32_t __attribute__  ((section ("user_var"))) NS2;
uint32_t __attribute__  ((section ("user_var"))) VFF1;
uint32_t __attribute__  ((section ("user_var"))) VFF2;
uint32_t __attribute__  ((section ("user_var"))) PR1;
uint32_t __attribute__  ((section ("user_var"))) PR2;
uint32_t __attribute__  ((section ("user_var"))) HZ1;
uint32_t __attribute__  ((section ("user_var"))) HZ2;
uint32_t __attribute__  ((section ("user_var"))) HP1;
uint32_t __attribute__  ((section ("user_var"))) HP2;
uint32_t __attribute__  ((section ("user_var"))) PH1;
uint32_t __attribute__  ((section ("user_var"))) PH2;
uint32_t __attribute__  ((section ("user_var"))) PW1;
uint32_t __attribute__  ((section ("user_var"))) PW2;
uint32_t __attribute__  ((section ("user_var"))) ET1;
uint32_t __attribute__  ((section ("user_var"))) ET2;
uint32_t __attribute__  ((section ("user_var"))) IM1;
uint32_t __attribute__  ((section ("user_var"))) IM2;
uint32_t __attribute__  ((section ("user_var"))) JTP;
uint32_t __attribute__  ((section ("user_var"))) TW1;
uint32_t __attribute__  ((section ("user_var"))) TW2;
uint32_t __attribute__  ((section ("user_var"))) TB1;
uint32_t __attribute__  ((section ("user_var"))) TB2;
uint32_t __attribute__  ((section ("user_var"))) TFL;
uint32_t __attribute__  ((section ("user_var"))) TAM;
uint32_t __attribute__  ((section ("user_var"))) PC1;
uint32_t __attribute__  ((section ("user_var"))) PC2;
uint32_t __attribute__  ((section ("user_var"))) PIN;
uint32_t __attribute__  ((section ("user_var"))) RSTP;
uint32_t __attribute__  ((section ("user_var"))) ZTK;
uint32_t __attribute__  ((section ("user_var"))) PTN;
uint32_t __attribute__  ((section ("user_var"))) PTK;

// ....... Auxiliares do Simulador
uint32_t __attribute__  ((section ("user_var"))) ZA3;
uint32_t __attribute__  ((section ("user_var"))) ZA4;
uint32_t __attribute__  ((section ("user_var"))) ZA5;
uint32_t __attribute__  ((section ("user_var"))) ZA6;
uint32_t __attribute__  ((section ("user_var"))) ZA8;
uint32_t __attribute__  ((section ("user_var"))) ZA9;
uint32_t __attribute__  ((section ("user_var"))) ZAA;
uint32_t __attribute__  ((section ("user_var"))) ZAB;
uint32_t __attribute__  ((section ("user_var"))) ZAC;
uint32_t __attribute__  ((section ("user_var"))) ZAD;
uint32_t __attribute__  ((section ("user_var"))) ZAE;
uint32_t __attribute__  ((section ("user_var"))) ZAF;
uint32_t __attribute__  ((section ("user_var"))) ZAG;
uint32_t __attribute__  ((section ("user_var"))) ZAH;
uint32_t __attribute__  ((section ("user_var"))) ZB3;
uint32_t __attribute__  ((section ("user_var"))) ZB4;
uint32_t __attribute__  ((section ("user_var"))) ZB5;
uint32_t __attribute__  ((section ("user_var"))) ZB6;
uint32_t __attribute__  ((section ("user_var"))) ZB7;
uint32_t __attribute__  ((section ("user_var"))) ZB8;
uint32_t __attribute__  ((section ("user_var"))) ZB9;
uint32_t __attribute__  ((section ("user_var"))) ZBA;
uint32_t __attribute__  ((section ("user_var"))) ZBB;
uint32_t __attribute__  ((section ("user_var"))) ZBC;
uint32_t __attribute__  ((section ("user_var"))) ZBD;
uint32_t __attribute__  ((section ("user_var"))) ZBE;
uint32_t __attribute__  ((section ("user_var"))) ZBF;
uint32_t __attribute__  ((section ("user_var"))) ZBG;
uint32_t __attribute__  ((section ("user_var"))) ZBH;

// ....... Temporizadores (SEM endereco Fixo)
uint32_t __attribute__  ((section ("user_var"))) SG6;
uint32_t __attribute__  ((section ("user_var"))) SG7;
uint32_t __attribute__  ((section ("user_var"))) SG8;
uint32_t __attribute__  ((section ("user_var"))) SG9;
uint32_t __attribute__  ((section ("user_var"))) SC11;
uint32_t __attribute__  ((section ("user_var"))) SC12;
uint32_t __attribute__  ((section ("user_var"))) SC13;
uint32_t __attribute__  ((section ("user_var"))) SC14;
uint32_t __attribute__  ((section ("user_var"))) SC15;
uint32_t __attribute__  ((section ("user_var"))) SC16;
uint32_t __attribute__  ((section ("user_var"))) SC17;
uint32_t __attribute__  ((section ("user_var"))) SC18;
uint32_t __attribute__  ((section ("user_var"))) SC19;
uint32_t __attribute__  ((section ("user_var"))) SC20;

// ....... Matrizes do Simulador / Cenarios / Eventos
uint32_t __attribute__  ((section ("user_var"))) STV[20];
uint32_t __attribute__  ((section ("user_var"))) RLO[20];
uint32_t __attribute__  ((section ("user_var"))) RHI[20];
uint32_t __attribute__  ((section ("user_var"))) SNA[20];
uint32_t __attribute__  ((section ("user_var"))) EAO[20];
uint32_t __attribute__  ((section ("user_var"))) SFZ[20];
uint32_t __attribute__  ((section ("user_var"))) SFP[20];
uint32_t __attribute__  ((section ("user_var"))) SHT[20];
uint32_t __attribute__  ((section ("user_var"))) SDO[20];
uint32_t __attribute__  ((section ("user_var"))) SDV[20];
uint32_t __attribute__  ((section ("user_var"))) MSF[20];
uint32_t __attribute__  ((section ("user_var"))) DPT[25];
uint32_t __attribute__  ((section ("user_var"))) FSV[26];
uint32_t __attribute__  ((section ("user_var"))) SCN[240];
uint32_t __attribute__  ((section ("user_var"))) RBF[1920];
uint32_t __attribute__  ((section ("user_var"))) BDE[1200];

// ....... Preenchimento do fim da area: ao declarar variaveis acima, tire a mesma quantidade
//         de VVV2[N]. Fechar exatamente 15360 nao e necessario; passar de 15360 e erro.
//         Chassi: VVV2[9000] - Simulador de Anomalias: 3984 = 5016
uint32_t __attribute__  ((section ("user_var"))) VVV2[5016];
uint32_t __attribute__  ((section ("user_var"))) FIM_USER_VAR;

void Main_Prog(void)
{

//=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#	XYZ

// ############################### ROTINAS BASICAS ##############################

MAQUINA_0		// (VA) Pre-set de Partida + Geracao de DEC + Variavel TKL + Limpar Memoria do CLP

	ESTADOS
	{
		ESTADO 0:		// Zeramentos de Variaveis + Limpeza dos Ponteiros
			INCREMENTA (1,&W90);	// Contador de Partidas do CLP (Reinicio de CPU)
			RESETA (8,&SDG1,&SDG2,&SDG3,&SDG4,&SDG5,&SDG6,&SDG7,&SDG8);
			RESETA (8,&SAN1,&SAN2,&SAN3,&SAN4,&SAN5,&SAN6,&SAN7,&SAN8);
			RESETA (3,&MNT,&KEY,&ZZ7);
			SETA (1,&MIN);			// Forcar atualizacao do Dia/Turno no Bco Dados ao Ligar CLP
			SETA (2,&ED1,&ED3);		// Modo Simulador: Chave Ligada + Emergencia OK, senao entram Zeradas
			FACA {DD1=(DD1>99?0:DD1) E_ DD2=(DD2>99?0:DD2) E_ DD3=(DD3>9?0:DD3);}		// Filtros Ponteiros Bco Dados
			VAPARA (5);
// .......

		ESTADO 5:
			SE (TECLA<24) ENTAO (6);
			SE (DEC>0) ENTAO (7);

		ESTADO 6:
			SETA (2,&SG0,&ZZ7);
			VAPARA (5);

		ESTADO 7:		// Incrementa Decimos
			RESETA (1,&DEC);
			INCREMENTA (6,&DC0,&DC1,&DC2,&DC3,&DC4,&DC5);
			FACA {DC0=(TECLA>23?0:DC0) E_ TKL=(TECLA*100+DC0);}
			VAPARA (5);

// DC0 - Filtro para gerar a Variavel TKL p/ uso no Mapa de Telas
// DC1 - Passo de 0,2 s da Planta Simulada (MAQUINA_26)
// DC2 - Livre
// DC3 - Livre
// DC4 - Livre
// DC5 - Livre

// _______ Zerar a Memoria do CLP

		ESTADO 99:
			RESETA (1,&VA0);
			SE (VA0!=0) ENTAO (99);
			VAPARA (100);

		ESTADO 100:		// W00...W99
			RESETA (1,&DSP[VA0]);
			INCREMENTA (1,&VA0);
			SE (VA0>99) ENTAO (101);
			VAPARA (100);

		ESTADO 101:		// VVV[0]...VVV[15000]
			FACA {VA0=W96+5000 E_ VA1=W96+10000;}
			RESETA (3,&VVV[W96],&VVV[VA0],&VVV[VA1]);
			INCREMENTA (1,&W96);
			SE (W96>5000) ENTAO (0);
			VAPARA (101);
	}

// _____________________

MAQUINA_1		// (VB) Geracao de SEG + Relogio Calendario + Apagar Senha

	ESTADOS
	{
		ESTADO 0:
			SE (SEC>0) ENTAO (5);
// _______

		ESTADO 5:		// Incrementa Segundos
			RESETA (1,&SEC);
			INCREMENTA (10,&SG0,&SG1,&SG2,&SG3,&SG4,&SG5,&SG6,&SG7,&SG8,&SG9);
			INCREMENTA (8,&CG1,&CG2,&CG3,&CG4,&CG5,&CG6,&CG7,&CG8);
			INCREMENTA (10,&SC1,&SC2,&SC3,&SC4,&SC5,&SC6,&SC7,&SC8,&SC9,&SC10);
			INCREMENTA (10,&SC11,&SC12,&SC13,&SC14,&SC15,&SC16,&SC17,&SC18,&SC19,&SC20);
			INCREMENTA (5,&XC1,&XC2,&XC3,&XC4,&XC5);							// XXX
			FACA {ASG=ANO*100+RSEC E_ DTE=DIA*100+MES E_ RLG=RHOR*100+RMIN;}
			SE (SG0>300 && MNT==0 && SMLX!=13579) ENTAO (6);					// Simulador OFF
			VAPARA (10);

		ESTADO 6:		// Apagar a SENHA
			RESETA (2,&SG0,&W99);
			INCREMENTA (1,&ZZ7);
			FACA {SNH=(W98==0?1:0) E_ E25(55);}
			SE (ZZ7>6) ENTAO (7);
			VAPARA (10);

		ESTADO 7:
			RESETA (1,&MPT);
			VAPARA (10);

//       Filtros Diversos
// SG0 - Tempo para Apagar a Senha
// SG1 - Tempo de Beep de Alarme Ligado
// SG2 - Livre (era o Filtro das EAN's)
// SG3 - Temporizador da XV-01 (MAQUINA_4)
// SG4 - Temporizador da Estacao (MAQUINA_5)
// SG5 - Condicao Liga/Desliga Auxiliar (MAQUINA_5)
// SG6 - Temporizador da Bomba B-01 (MAQUINA_7)
// SG7 - Temporizador da Bomba B-02 (MAQUINA_8)
// SG8 - Pulso de 1 s das Protecoes (MAQUINA_10, consumido)
// SG9 - Pulso de 1 s da Publicacao (MAQUINA_11, consumido)

//       Filtros das EDG's
// CG1 - Filtro da ED1
// CG2 - Filtro da ED2
// CG3 - Filtro da ED3
// CG4 - Filtro da ED4
// CG5 - Filtro da ED5
// CG6 - Filtro da ED6
// CG7 - Filtro da ED7
// CG8 - Filtro da ED8

//       Filtros dos Alarmes
// SC1 ... SC20 - Filtros dos Alarmes 01...20

//       Temporizadores do Simulador (consumidos: 1 s exato)						XXX
// XC1 - Slot de Falha A (MAQUINA_28)
// XC2 - Slot de Falha B (MAQUINA_29)
// XC3 - Slot de Falha C (MAQUINA_30)
// XC4 - Sequenciador de Cenarios (MAQUINA_12)
// XC5 - Livre

// _______

		ESTADO 10:
			VAPARA (0);
	}

// _____________________

MAQUINA_2		// (VC) Geracao de MIN + Geracao Bco Dados + Apagar Bco Dados + Turnos

	ESTADOS
	{
		ESTADO 0:
			SE (W94>10) ENTAO (20);		// Apagar Bco de Alarmes - Alarmes + Minutos + Lotes
			SE (MIN>0) ENTAO (5);
// _______

		ESTADO 5:		// Incrementa Minutos
			RESETA (1,&MIN);
			INCREMENTA (3,&MN1,&MN2,&MN3);
			VAPARA (10);

// MN1 - Livre
// MN2 - Livre
// MN3 - Livre

// ______________  Banco de Dados - Minutos
// DIM BDD[2000]   ; Variaveis do Banco de Dados: Minutos
//                 ;  1 - AAAAA - Contador Continuo para uso do PC
//                 ;  2 - ABBCC - Trn/Ano/Seg da Coleta
//                 ;  3 - _AABB - Dia/Mes da Coleta
//                 ;  4 - _AABB - Hor/Min da Coleta
//                 ;  5 - AAAAA - ID do Lote
//                 ;  6 - AAAAA - SMQ Status da Maquina
//                 ;  7 - AAAAA - PDT Produto
//                 ;  8 - AAAAA - CTZ
//                 ;  9 - AAAAA - Livre
//                 ; 10 - AAAAA - Livre
//                 ; 11 - ABCDE - Temperatura-1 ('C)
//                 ; 12 - AAAAA - Temperatura-2 ('C)
//                 ; 13 - AAAAA - Temperatura-3 ('C)
//                 ; 14 - AAAAA - Temperatura-4 ('C)
//                 ; 15 - AAAAA - Temperatura-5 ('C)
//                 ; 16 - AAAAA - Umidade da Lamina (%)
//                 ; 17 - ABBCC - Pressao da Caldeira (kg/cm2)
//                 ; 18 - AAAAA - Velocidade - Esteira (m/min)
//                 ; 19 - AAAAA - Livre
//                 ; 20 - AAAAA - Check-Sum

		ESTADO 10:		// Carregamento do Banco de Dados de Minutos
			INCREMENTA (2,&DD2,&W88);
			FACA {DD2=(DD2>99?0:DD2) E_ VC1=DD2*20 E_ VC2=VC1+1 E_ VC3=VC1+2 E_ VC4=VC1+3 E_ VC5=VC1+4
				 E_ VC6=VC1+5 E_ VC7=VC1+6 E_ VC8=VC1+7 E_ VC9=VC1+8 E_ VC10=VC1+9
				 E_ VC11=VC1+10 E_ VC12=VC1+11 E_ VC13=VC1+12 E_ VC14=VC1+13 E_ VC15=VC1+14
				 E_ VC16=VC1+15 E_ VC17=VC1+16 E_ VC18=VC1+17 E_ VC19=VC1+18 E_ VC20=VC1+19;}
			VAPARA (11);

		ESTADO 11:
			FACA {BDD[VC1]=W88 E_ BDD[VC2]=TRN*10000+ASG E_ BDD[VC3]=DTE E_ BDD[VC4]=RLG
				 E_ BDD[VC5]=GR1 E_ BDD[VC6]=SMQ E_ BDD[VC7]=PDT E_ BDD[VC8]=CTZ E_ BDD[VC9]=ETP E_ BDD[VC10]=W72
				 E_ BDD[VC11]=MLT E_ BDD[VC12]=MPS E_ BDD[VC13]=MPD/10 E_ BDD[VC14]=MFQ E_ BDD[VC15]=MI1
				 E_ BDD[VC16]=MI2 E_ BDD[VC17]=MW1 E_ BDD[VC18]=MW2 E_ BDD[VC19]=MPW
				 E_ VC0=BDD[VC1]+BDD[VC2]+BDD[VC3]+BDD[VC4]+BDD[VC5]+BDD[VC6]+BDD[VC7]+BDD[VC8]+BDD[VC9]+BDD[VC10]
				 E_ VC0=VC0+BDD[VC11]+BDD[VC12]+BDD[VC13]+BDD[VC14]+BDD[VC15]+BDD[VC16]+BDD[VC17]+BDD[VC18]+BDD[VC19] E_ BDD[VC20]=VC0;}
			VAPARA (12);

// _______ Identificacao do Turno de Trabalho

		ESTADO 12:
			FACA {DTE=DIA*100+MES E_ RLG=RHOR*100+RMIN
				E_ VC1=TNO1 E_ VC2=(TNO2<TNO1?TNO2+2400:TNO2) E_ VC3=(TNO3<TNO1?TNO3+2400:TNO3) E_ VC4=(TNO4<TNO1?TNO4+2400:TNO4)
				E_ VC7=(RLG<TNO1?RLG+2401:RLG+1);}
			SE (VC7>VC1) ENTAO (13);
			SE (VC7>VC2 && TNO2>0) ENTAO (14);
			SE (VC7>VC3 && TNO3>0) ENTAO (15);
			SE (VC7>VC4 && TNO4>0) ENTAO (16);
			SE (TNO1==0) ENTAO (13);       // Se 1o Turno=0 NAO tem Turnos, Entao Turno=1
			VAPARA (13);

		ESTADO 13:		// 1o Turno
			SETA (1,&TRN);
			VAPARA (17);

		ESTADO 14:		// 2o Turno
			FACA {TRN=2;}
			VAPARA (17);

		ESTADO 15:		// 3o Turno
			FACA {TRN=3;}
			VAPARA (17);

		ESTADO 16:		// 4o Turno
			FACA {TRN=4;}
			VAPARA (17);

		ESTADO 17:		// Abertura AUT de Lote por Dia ou por Turno? (0-Nao / 1=Dia / 2=Turno)
			SE (ABLT==1) ENTAO (18);
			SE (ABLT==2) ENTAO (19);
			VAPARA (0);

		ESTADO 18:		// Abertura AUT de Lote por Dia
			SE (DTE!=GR3) ENTAO (30);
			VAPARA (0);

		ESTADO 19:		// Abertura AUT de Lote por Turno
			FACA {VC7=GR2/100;}
			SE (TRN!=VC7) ENTAO (30);
			VAPARA (0);

// _______ Apagar Bco de Dados

		ESTADO 20:
			RESETA (2,&VC1,&VC2);
			SE (W94==11) ENTAO (25);		// Apagar Bco de Alarmes - Alarmes + Minutos + Lotes
			SE (W94==12) ENTAO (30);		// Avancar ID MAN
			VAPARA (35);
// .......

		ESTADO 25:		// Apagar Banco de Dados - Alarmes + Minutos
			RESETA (6,&DD1,&DD2,&UDD1,&UDD2,&BDA[VC1],&BDD[VC1]);
			INCREMENTA (1,&VC1);
			SE (VC1>1999) ENTAO (27);
			VAPARA (25);

		ESTADO 27:		// Apagar Banco de Dados - Lotes
			RESETA (3,&DD3,&UDD3,&BDL[VC2]);
			INCREMENTA (1,&VC2);
			SE (VC2>99) ENTAO (35);
			VAPARA (27);
// .......

		ESTADO 30:		// Avancar ID MAN/AUT
			INCREMENTA (1,&GR1);
			VAPARA (32);

// ______________  Banco de Dados - Lotes
// DIM BDL[100]    ; Variaveis do Banco de Dados: Lotes
//                 ;  1 - AAAAA - Contador Continuo para uso do PC
//                 ;  2 - _AABB - Trn/Ano da Coleta
//                 ;  3 - _AABB - Dia/Mes da Coleta
//                 ;  4 - _AABB - Hor/Min da Coleta
//                 ;  5 - AAAAA - ID do Lote
//                 ;  6 - AAAAA - Livre
//                 ;  7 - AAAAA - Livre
//                 ;  8 - AAAAA - Livre
//                 ;  9 - AAAAA - Livre
//                 ; 10 - AAAAA - Livre

		ESTADO 32:
			INCREMENTA (2,&DD3,&W89);
			FACA {DD3=(DD3>9?0:DD3) E_ GR2=TRN*100+ANO E_ GR3=DTE E_ GR4=RLG
				 E_ VC1=DD3*10 E_ VC2=VC1+1 E_ VC3=VC1+2 E_ VC4=VC1+3 E_ VC5=VC1+4
				 E_ VC6=VC1+5 E_ VC7=VC1+6 E_ VC8=VC1+7 E_ VC9=VC1+8 E_ VC10=VC1+9
				 E_ BDL[VC1]=W89 E_ BDL[VC2]=GR2 E_ BDL[VC3]=GR3 E_ BDL[VC4]=GR4 E_ BDL[VC5]=GR1
				 E_ BDL[VC6]=0 E_ BDL[VC7]=0 E_ BDL[VC8]=0 E_ BDL[VC9]=0 E_ BDL[VC10]=0;}
			VAPARA (35);
// .......

		ESTADO 35:
			RESETA (1,&W94);
			VAPARA (0);
	}

// _____________________

MAQUINA_3		// (VD) Leituras dos Sensores (Conversao + Falha por Faixa)

	ESTADOS
	{
		ESTADO 0:
			SE (ATRASO>=10) ENTAO (10);
// .......

		ESTADO 10:		// Valor = RLO + EA*(RHI-RLO)/4095
			FACA {  MLT=RLO[1]+EA21*(RHI[1]-RLO[1])/4095
				 E_ MPS=RLO[2]+EA22*(RHI[2]-RLO[2])/4095
				 E_ MPD=RLO[3]+EA23*(RHI[3]-RLO[3])/4095
				 E_ MFQ=RLO[4]+EA24*(RHI[4]-RLO[4])/4095
				 E_ MQE=RLO[5]+EA33*(RHI[5]-RLO[5])/4095
				 E_ MI1=RLO[6]+EA34*(RHI[6]-RLO[6])/4095
				 E_ MI2=RLO[7]+EA35*(RHI[7]-RLO[7])/4095
				 E_ MV1=RLO[8]+EA36*(RHI[8]-RLO[8])/4095
				 E_ MV2=RLO[9]+EA45*(RHI[9]-RLO[9])/4095
				 E_ MS1=RLO[10]+EA46*(RHI[10]-RLO[10])/4095
				 E_ MS2=RLO[11]+EA47*(RHI[11]-RLO[11])/4095
				 E_ MPW=RLO[12]+EA48*(RHI[12]-RLO[12])/4095
				 E_ MW1=RLO[13]+EA13*(RHI[13]-RLO[13])/4095
				 E_ MW2=RLO[14]+EA14*(RHI[14]-RLO[14])/4095
				 E_ MB1=RLO[15]+EA15*(RHI[15]-RLO[15])/4095
				 E_ MB2=RLO[16]+EA16*(RHI[16]-RLO[16])/4095
				 E_ MTF=RLO[17]+EA17*(RHI[17]-RLO[17])/4095
				 E_ MTA=RLO[18]+EA18*(RHI[18]-RLO[18])/4095;}
			VAPARA (11);

		ESTADO 11:		// Falha por faixa: 4095 = acima de 20 mA ou PT100 aberto; 0 so e falha onde o processo nunca le zero
			FACA {  MSF[1]=(EA21==0 || EA21>=4095?1:0) E_ MSF[2]=(EA22==0 || EA22>=4095?1:0) E_ MSF[3]=(EA23==0 || EA23>=4095?1:0)
				 E_ MSF[4]=(EA24>=4095?1:0) E_ MSF[5]=(EA33>=4095?1:0) E_ MSF[6]=(EA34>=4095?1:0) E_ MSF[7]=(EA35>=4095?1:0)
				 E_ MSF[8]=(EA36>=4095?1:0) E_ MSF[9]=(EA45>=4095?1:0) E_ MSF[10]=(EA46>=4095?1:0) E_ MSF[11]=(EA47>=4095?1:0)
				 E_ MSF[12]=(EA48>=4095?1:0)
				 E_ MSF[13]=(EA13==0 || EA13>=4095?1:0) E_ MSF[14]=(EA14==0 || EA14>=4095?1:0) E_ MSF[15]=(EA15==0 || EA15>=4095?1:0)
				 E_ MSF[16]=(EA16==0 || EA16>=4095?1:0) E_ MSF[17]=(EA17==0 || EA17>=4095?1:0) E_ MSF[18]=(EA18==0 || EA18>=4095?1:0)
				 E_ SNF=MSF[1]+MSF[2]+MSF[3]+MSF[4]+MSF[5]+MSF[6]+MSF[7]+MSF[8]+MSF[9]
				 E_ SNF=SNF+MSF[10]+MSF[11]+MSF[12]+MSF[13]+MSF[14]+MSF[15]+MSF[16]+MSF[17]+MSF[18];}
			VAPARA (12);

		ESTADO 12:
			SE (ATRASO>=2) ENTAO (10);
	}

// _____________________

// ############################# ROTINAS DE CONTROLE ############################

MAQUINA_4		// (VE) Reservatorio: Valvula de Entrada XV-01
	SE (MNT==1) ENTAO (1);							// Manutencao

	ESTADOS
	{
		ESTADO 0:		// XV-01 fechada
			RESETA (2,&CXV,&XVF);
			FACA {XVS=0;}
			SE (MLT<LON && MSF[1]==0 && ATRASO>=50) ENTAO (10);
			SE (EDG12==0 && ATRASO>=50) ENTAO (10);			// LT-01 em falha: abre pela chave LSL

		ESTADO 1:		// Manutencao
			RESETA (1,&CXV);
			FACA {XVS=0;}
			VAPARA (0);

		ESTADO 10:		// Abrindo
			SETA (1,&CXV);
			RESETA (1,&SG3);
			FACA {XVS=1;}
			SE (SG3>30) ENTAO (15);
			SE (EDG10==1) ENTAO (20);

		ESTADO 15:		// Nao abriu em 30 s: mantem o comando e repete a verificacao
			SETA (2,&CXV,&XVF);
			RESETA (1,&SG3);
			FACA {XVS=4;}
			SE (SG3>120) ENTAO (10);
			SE ((MLT>LOF && MSF[1]==0) || EDG13==1) ENTAO (30);
			SE (EDG10==1) ENTAO (20);

		ESTADO 20:		// Aberta
			RESETA (1,&XVF);
			FACA {XVS=2;}
			SE ((MLT>LOF && MSF[1]==0) || EDG13==1) ENTAO (30);

		ESTADO 30:		// Fechando
			RESETA (2,&CXV,&SG3);
			FACA {XVS=3;}
			SE (SG3>30) ENTAO (35);
			SE (EDG11==1) ENTAO (0);

		ESTADO 35:		// Nao fechou em 30 s
			SETA (1,&XVF);
			FACA {XVS=4;}
			SE (MLT<LON && MSF[1]==0 && ATRASO>=50) ENTAO (10);
			SE (EDG11==1) ENTAO (0);
	}

// _____________________

MAQUINA_5		// (VF) Sequenciamento da Estacao (Principal / Auxiliar / Rodizio / Reserva)

	ESTADOS
	{
		ESTADO 0:		// Estacao parada
			RESETA (2,&RQ1,&RQ2);
			SE (SOK==1 && (AV1==1 || AV2==1)) ENTAO (5);

		ESTADO 5:		// Partida: escolhe a principal disponivel e pre-carrega o PID na velocidade minima
			FACA {DTY=(DTY==2?2:1) E_ DTY=(DTY==1 && AV1==0?2:DTY) E_ DTY=(DTY==2 && AV2==0?1:DTY)
				 E_ GOP=1 E_ GOC=(GOC>59998?0:GOC+1) E_ IniciaPID() E_ J00=(PKI==0?0:SMN*256/PKI);}
			VAPARA (10);

		ESTADO 10:		// Aguarda a principal em operacao
			FACA {RQ1=(DTY==1?1:0) E_ RQ2=(DTY==2?1:0);}
			SE ((DTY==1 && AV1==0) || (DTY==2 && AV2==0)) ENTAO (25);
			SE ((DTY==1 && SB1==2) || (DTY==2 && SB2==2)) ENTAO (20);
			SE (SOK==0) ENTAO (90);

		ESTADO 20:		// Uma bomba em operacao
			FACA {RQ1=(DTY==1?1:0) E_ RQ2=(DTY==2?1:0) E_ FDS=(FDS==1 && ((DTY==1 && AV2==1) || (DTY==2 && AV1==1))?1:0);}
			RESETA (1,&SG5);
			FACA_ {SG5=(SPR>=SAS && ((DTY==1 && AV2==1) || (DTY==2 && AV1==1))?SG5:0);}
			SE (SG5>=TAS && TAS>0) ENTAO (30);
			SE ((RTH>=ROT*3600 && ROT>0 && ((DTY==1 && AV2==1) || (DTY==2 && AV1==1))) || FDS==1) ENTAO (40);
			SE ((DTY==1 && SB1!=1 && SB1!=2) || (DTY==2 && SB2!=1 && SB2!=2)) ENTAO (25);
			SE (SOK==0) ENTAO (90);

		ESTADO 25:		// Principal falhou: a reserva assume
			FACA {DTY=(DTY==1?2:1) E_ RQ1=0 E_ RQ2=0 E_ GOP=3 E_ GOC=(GOC>59998?0:GOC+1) E_ RTH=0;}
			SE ((DTY==1 && AV1==1) || (DTY==2 && AV2==1)) ENTAO (10);
			VAPARA (0);

		ESTADO 30:		// Partida da auxiliar
			FACA {RQ1=1 E_ RQ2=1 E_ GOP=6 E_ GOC=(GOC>59998?0:GOC+1);}
			RESETA (1,&SG4);
			SE (SG4>30 || (DTY==1 && AV2==0) || (DTY==2 && AV1==0)) ENTAO (55);
			SE (SB1==2 && SB2==2) ENTAO (50);
			SE (SOK==0) ENTAO (90);

		ESTADO 40:		// Rodizio: parte a reserva antes de parar a principal
			FACA {RQ1=1 E_ RQ2=1 E_ FDS=0 E_ GOP=5 E_ GOC=(GOC>59998?0:GOC+1);}
			RESETA (1,&SG4);
			SE (SG4>30) ENTAO (55);
			SE (SB1==2 && SB2==2) ENTAO (42);
			SE (SOK==0) ENTAO (90);

		ESTADO 42:
			FACA {DTY=(DTY==1?2:1) E_ RQ1=(DTY==1?1:0) E_ RQ2=(DTY==2?1:0) E_ RTH=0;}
			VAPARA (20);

		ESTADO 50:		// Duas bombas em operacao
			RESETA (1,&SG5);
			FACA_ {SG5=(SPR<SAP?SG5:0);}
			SE (SG5>=2*TAS) ENTAO (55);
			SE ((DTY==1 && SB2!=2) || (DTY==2 && SB1!=2)) ENTAO (55);		// auxiliar parou
			SE ((DTY==1 && SB1!=2) || (DTY==2 && SB2!=2)) ENTAO (52);		// principal parou
			SE (SOK==0) ENTAO (90);

		ESTADO 52:		// Principal parou com duas em operacao: a auxiliar vira principal
			FACA {DTY=(DTY==1?2:1) E_ RQ1=(DTY==1?1:0) E_ RQ2=(DTY==2?1:0) E_ GOP=3 E_ GOC=(GOC>59998?0:GOC+1) E_ RTH=0;}
			VAPARA (20);

		ESTADO 55:		// Desliga a auxiliar
			FACA {RQ1=(DTY==1?1:0) E_ RQ2=(DTY==2?1:0) E_ GOP=7 E_ GOC=(GOC>59998?0:GOC+1);}
			VAPARA (20);

		ESTADO 90:		// Parada da estacao
			FACA {RQ1=0 E_ RQ2=0 E_ GOP=2 E_ GOC=(GOC>59998?0:GOC+1);}
			SE (SB1!=1 && SB1!=2 && SB1!=3 && SB2!=1 && SB2!=2 && SB2!=3) ENTAO (0);
	}

// _____________________

MAQUINA_6		// (VG) Controle de Pressao de Recalque (PID 0) + Referencia de Velocidade
	PID(0); SPR=VS[0]; RE[0]=PSP; MD[0]=MPD; KP[0]=PKP; KI[0]=PKI; KD[0]=PKD; TA[0]=1; LS[0]=1000; LI[0]=0;

	ESTADOS
	{
		ESTADO 0:
			SE (ATRASO>=5) ENTAO (10);

		ESTADO 10:		// PT-02 em falha: velocidade fixa SFB. Referencia 0...1000 permil -> 0...4095
			FACA_ {SRF=(SPR>SMN?SPR:SMN) E_ SRF=(MSF[3]==1?SFB:SRF) E_ SRF=(SRF>1000?1000:SRF)
				 E_ SAN5=(CM1==1?SRF*4095/1000:0) E_ SAN6=(CM2==1?SRF*4095/1000:0);}
			FIMCOMANDO();
	}

// _____________________

MAQUINA_7		// (VH) Bomba B-01
	SE (MNT==1) ENTAO (1);							// Manutencao

	ESTADOS
	{
		ESTADO 0:		// Parada
			RESETA (2,&CM1,&RV1);
			FACA {SB1=0;}
			SE (RQ1==1 && AV1==1) ENTAO (10);
			SE (EDG5==1) ENTAO (82);
			SE (EDG6==1) ENTAO (83);

		ESTADO 1:		// Manutencao
			RESETA (2,&CM1,&RV1);
			FACA {SB1=5;}
			VAPARA (0);

		ESTADO 10:		// Partindo: comando ao inversor e espera do retorno de operacao
			SETA (1,&CM1);
			RESETA (1,&SG6);
			FACA {SB1=1;}
			SE (RQ1==0) ENTAO (30);
			SE (EDG4==1 && SG6>=2) ENTAO (20);
			SE (SG6>10) ENTAO (81);
			SE (EDG5==1) ENTAO (82);
			SE (EDG6==1) ENTAO (83);

		ESTADO 20:		// Em operacao
			RESETA (1,&SG6);
			FACA {SB1=2;}
			FACA_ {SG6=(EDG4==1?0:SG6);}
			SE (RQ1==0) ENTAO (30);
			SE (SG6>2) ENTAO (81);
			SE (TR1>0) ENTAO (84);
			SE (EDG6==1) ENTAO (83);
			SE (EDG5==1) ENTAO (82);

		ESTADO 30:		// Parando (rampa do inversor)
			RESETA (2,&CM1,&SG6);
			FACA {SB1=3;}
			SE (EDG4==0 || SG6>20) ENTAO (0);
			SE (RQ1==1 && AV1==1) ENTAO (10);

		ESTADO 81:		// Sem retorno de operacao
			FACA {FR1=7;}
			VAPARA (80);

		ESTADO 82:		// Falha do inversor
			FACA {FR1=1;}
			VAPARA (80);

		ESTADO 83:		// Termistor do motor
			FACA {FR1=2;}
			VAPARA (80);

		ESTADO 84:		// Protecao (MAQUINA_10)
			FACA {FR1=TR1;}
			VAPARA (80);

		ESTADO 80:		// Falha: travada ate o rearme
			RESETA (2,&CM1,&SG6);
			FACA {SB1=4;}
			SE (RSTQ==1 && EDG6==0 && TR1==0) ENTAO (85);
			SE (SG6>=ARS && ARS>0 && ARC1<3 && (FR1==1 || FR1==7)) ENTAO (86);

		ESTADO 85:		// Rearme: pulso de reset no inversor (2 s)
			SETA (1,&RV1);
			FACA {FR1=0;}
			SE (ATRASO>=20) ENTAO (0);

		ESTADO 86:		// Rearme automatico do inversor
			FACA {ARC1=ARC1+1;}
			VAPARA (85);
	}

// _____________________

MAQUINA_8		// (VI) Bomba B-02
	SE (MNT==1) ENTAO (1);							// Manutencao

	ESTADOS
	{
		ESTADO 0:		// Parada
			RESETA (2,&CM2,&RV2);
			FACA {SB2=0;}
			SE (RQ2==1 && AV2==1) ENTAO (10);
			SE (EDG8==1) ENTAO (82);
			SE (EDG9==1) ENTAO (83);

		ESTADO 1:		// Manutencao
			RESETA (2,&CM2,&RV2);
			FACA {SB2=5;}
			VAPARA (0);

		ESTADO 10:		// Partindo
			SETA (1,&CM2);
			RESETA (1,&SG7);
			FACA {SB2=1;}
			SE (RQ2==0) ENTAO (30);
			SE (EDG7==1 && SG7>=2) ENTAO (20);
			SE (SG7>10) ENTAO (81);
			SE (EDG8==1) ENTAO (82);
			SE (EDG9==1) ENTAO (83);

		ESTADO 20:		// Em operacao
			RESETA (1,&SG7);
			FACA {SB2=2;}
			FACA_ {SG7=(EDG7==1?0:SG7);}
			SE (RQ2==0) ENTAO (30);
			SE (SG7>2) ENTAO (81);
			SE (TR2>0) ENTAO (84);
			SE (EDG9==1) ENTAO (83);
			SE (EDG8==1) ENTAO (82);

		ESTADO 30:		// Parando
			RESETA (2,&CM2,&SG7);
			FACA {SB2=3;}
			SE (EDG7==0 || SG7>20) ENTAO (0);
			SE (RQ2==1 && AV2==1) ENTAO (10);

		ESTADO 81:
			FACA {FR2=7;}
			VAPARA (80);

		ESTADO 82:
			FACA {FR2=1;}
			VAPARA (80);

		ESTADO 83:
			FACA {FR2=2;}
			VAPARA (80);

		ESTADO 84:
			FACA {FR2=TR2;}
			VAPARA (80);

		ESTADO 80:		// Falha: travada ate o rearme
			RESETA (2,&CM2,&SG7);
			FACA {SB2=4;}
			SE (RSTQ==1 && EDG9==0 && TR2==0) ENTAO (85);
			SE (SG7>=ARS && ARS>0 && ARC2<3 && (FR2==1 || FR2==7)) ENTAO (86);

		ESTADO 85:		// Rearme: pulso de reset no inversor (2 s)
			SETA (1,&RV2);
			FACA {FR2=0;}
			SE (ATRASO>=20) ENTAO (0);

		ESTADO 86:
			FACA {ARC2=ARC2+1;}
			VAPARA (85);
	}

// _____________________

MAQUINA_9		// (VJ) Intertravamentos + Comandos das Saidas + Disponibilidade

	ESTADOS
	{
		ESTADO 0:		// Unico dono de EMG, LGD, LLO, SOK, AV1/AV2 e das SDG2...SDG8
			FACA_ { EMG=(EDG3==0?1:0)
				 E_ LGD=(EDG1==1 && HBL==1 && EMG==0 && MNT==0?1:0)
				 E_ LLO=((MLT<LLL && MSF[1]==0) || EDG12==0?1:LLO)
				 E_ LLO=(LLO==1 && (MLT>LLR || MSF[1]==1) && EDG12==1?0:LLO)
				 E_ SOK=(LGD==1 && LLO==0?1:0)
				 E_ AV1=(SB1<4?1:0) E_ AV2=(SB2<4?1:0)
				 E_ NRN=(SB1==2?1:0)+(SB2==2?1:0)
				 E_ RSTQ=(EST21==5?1:0)
				 E_ SDG2=(CM1==1 && EMG==0?1:0)
				 E_ SDG3=(CM2==1 && EMG==0?1:0)
				 E_ SDG4=CXV
				 E_ SDG5=RV1 E_ SDG6=RV2
				 E_ SDG7=(NRN>0?1:0)
				 E_ SDG8=ALM
				 E_ XTT=(EMG==1?2:(MNT==1?3:LGD))
				 E_ ETP=(SB1==1 || SB2==1?1:0)
				 E_ ETP=((SB1==4 || SB2==4) && NRN==0?5:ETP)
				 E_ ETP=(NRN==1?2:ETP) E_ ETP=(NRN==2?3:ETP)
				 E_ ETP=(LLO==1?4:ETP) E_ ETP=(MNT==1?6:ETP) E_ ETP=(EMG==1?7:ETP);}
			FIMCOMANDO();
	}

// _____________________

MAQUINA_10		// (VK) Protecoes das Bombas + Temporizacoes de 1 s + Set-point

	ESTADOS
	{
		ESTADO 0:
			SE (SG8>=1) ENTAO (5);

		ESTADO 5:		// Contadores de condicao (s). Sensor em falha (MSF) inibe a protecao correspondente
			FACA {SG8=SG8-1 E_ SG8=(SG8>5?0:SG8)
				 E_ KW1=(MW1>TWT && MSF[13]==0 && SB1==2?KW1+1:0) E_ KW2=(MW2>TWT && MSF[14]==0 && SB2==2?KW2+1:0)
				 E_ KB1=(MB1>TBT && MSF[15]==0 && SB1==2?KB1+1:0) E_ KB2=(MB2>TBT && MSF[16]==0 && SB2==2?KB2+1:0)
				 E_ KV1=(MV1>VBT && MSF[8]==0 && SB1==2?KV1+1:0) E_ KV2=(MV2>VBT && MSF[9]==0 && SB2==2?KV2+1:0)
				 E_ KI1=(MI1>ITR && MSF[6]==0 && SB1==2?KI1+1:0) E_ KI2=(MI2>ITR && MSF[7]==0 && SB2==2?KI2+1:0)
				 E_ KSL=(MPS<PSL && MSF[2]==0 && NRN>0?KSL+1:0)
				 E_ TR1=(EST7==85 || RSTQ==1?0:TR1) E_ TR2=(EST8==85 || RSTQ==1?0:TR2)
				 E_ TR1=(TR1==0 && KSL>=10 && SB1==2?8:TR1) E_ TR1=(TR1==0 && KB1>=3?6:TR1) E_ TR1=(TR1==0 && KI1>=30?5:TR1)
				 E_ TR1=(TR1==0 && KV1>=5?4:TR1) E_ TR1=(TR1==0 && KW1>=3?3:TR1)
				 E_ TR2=(TR2==0 && KSL>=10 && SB2==2?8:TR2) E_ TR2=(TR2==0 && KB2>=3?6:TR2) E_ TR2=(TR2==0 && KI2>=30?5:TR2)
				 E_ TR2=(TR2==0 && KV2>=5?4:TR2) E_ TR2=(TR2==0 && KW2>=3?3:TR2)
				 E_ RTH=(NRN>0?RTH+1:RTH)
				 E_ RU1=(SB1==2?RU1+1:0) E_ ARC1=(RU1>600?0:ARC1) E_ RU2=(SB2==2?RU2+1:0) E_ ARC2=(RU2>600?0:ARC2)
				 E_ HRS=(SMLX==13579 && TST>0?TOD/3600:RHOR)
				 E_ NGT=(PNH<=PNE?(HRS>=PNH && HRS<PNE?1:0):(HRS>=PNH || HRS<PNE?1:0))
				 E_ PSX=(SPO>0?SPO:(NGT==1 && PSN>0?PSN:PSD))
				 E_ GOP=(PSX!=PSXL && PSXL>0?4:GOP) E_ GOC=(PSX!=PSXL && PSXL>0?(GOC>59998?0:GOC+1):GOC) E_ PSXL=PSX
				 E_ PSP=(PSP==0?PSX:PSP) E_ PSP=(PSP+20<PSX?PSP+20:(PSP>PSX+20?PSP-20:PSX));}
			VAPARA (0);
	}

// _____________________

MAQUINA_11		// (VL) Publicacao W21...W50 + Falhas de Comunicacao + Rotulos W59...W80

	ESTADOS
	{
		ESTADO 0:
			SE (SG9>=1) ENTAO (5);

		ESTADO 5:		// Rotulos de verdade de campo: publicados sempre, antes de qualquer falha de comunicacao
			FACA {SG9=SG9-1 E_ SG9=(SG9>5?0:SG9)
				 E_ W60=GCA E_ W61=TGA E_ W62=SVA E_ W63=PHA
				 E_ W64=GCB E_ W65=TGB E_ W66=SVB E_ W67=PHB
				 E_ W68=GCC E_ W69=TGC E_ W70=SVC E_ W71=PHC
				 E_ W72=(PHA>=2 || PHB>=2 || PHC>=2?1:0)
				 E_ W73=SCP E_ W74=SCE E_ W75=SCS E_ W76=SCT/3600 E_ W77=SCT%3600
				 E_ W78=DMF E_ W79=GOP E_ W80=GOC E_ HBR=(HBR>59998?0:HBR+1)
				 E_ CMC=0 E_ CMS=0
				 E_ CMS=(GCA>=30 && GCA<=33 && SVA>0?SVA:CMS) E_ CMC=(GCA>=30 && GCA<=33 && SVA>0?GCA:CMC)
				 E_ CMS=(GCB>=30 && GCB<=33 && SVB>0?SVB:CMS) E_ CMC=(GCB>=30 && GCB<=33 && SVB>0?GCB:CMC)
				 E_ CMS=(GCC>=30 && GCC<=33 && SVC>0?SVC:CMS) E_ CMC=(GCC>=30 && GCC<=33 && SVC>0?GCC:CMC)
				 E_ DLY=(CMC==31?CMS*45/100:0) E_ LOS=(CMC==32?LOS:0)
				 E_ RNC=RNC*1664525+1013904223 E_ VL3=(RNC>>16)%1000 E_ VL5=(RNC>>4)%121;}
			VAPARA (10);

		ESTADO 10:		// Historico de 32 s de cada tag (tag t no bloco t*32 do RBF)
			FACA {RBI=(RBI>30?0:RBI+1) E_ VL1=RBI
				 E_ RBF[VL1]=MLT E_ VL1=VL1+32 E_ RBF[VL1]=MPS E_ VL1=VL1+32 E_ RBF[VL1]=MPD/10
				 E_ VL1=VL1+32 E_ RBF[VL1]=MFQ E_ VL1=VL1+32 E_ RBF[VL1]=MQE E_ VL1=VL1+32 E_ RBF[VL1]=MI1
				 E_ VL1=VL1+32 E_ RBF[VL1]=MI2 E_ VL1=VL1+32 E_ RBF[VL1]=MV1 E_ VL1=VL1+32 E_ RBF[VL1]=MV2
				 E_ VL1=VL1+32 E_ RBF[VL1]=MS1 E_ VL1=VL1+32 E_ RBF[VL1]=MS2 E_ VL1=VL1+32 E_ RBF[VL1]=MPW
				 E_ VL1=VL1+32 E_ RBF[VL1]=MW1 E_ VL1=VL1+32 E_ RBF[VL1]=MW2 E_ VL1=VL1+32 E_ RBF[VL1]=MB1
				 E_ VL1=VL1+32 E_ RBF[VL1]=MB2 E_ VL1=VL1+32 E_ RBF[VL1]=MTF E_ VL1=VL1+32 E_ RBF[VL1]=MTA
				 E_ VL1=VL1+32 E_ RBF[VL1]=ETP E_ VL1=VL1+32 E_ RBF[VL1]=SB1 E_ VL1=VL1+32 E_ RBF[VL1]=SB2
				 E_ VL1=VL1+32 E_ RBF[VL1]=DTY E_ VL1=VL1+32 E_ RBF[VL1]=XVS E_ VL1=VL1+32 E_ RBF[VL1]=PSP/10
				 E_ VL1=VL1+32 E_ RBF[VL1]=SRF E_ VL1=VL1+32 E_ RBF[VL1]=ALC E_ VL1=VL1+32 E_ RBF[VL1]=ALN
				 E_ VL1=VL1+32 E_ RBF[VL1]=EDG1+EDG2*2+EDG3*4+EDG4*8+EDG5*16+EDG6*32+EDG7*64+EDG8*128+EDG9*256+EDG10*512+EDG11*1024+EDG12*2048+EDG13*4096
				 E_ VL1=VL1+32 E_ RBF[VL1]=SDG1+SDG2*2+SDG3*4+SDG4*8+SDG5*16+SDG6*32+SDG7*64+SDG8*128;}
			VAPARA (11);

		ESTADO 11:		// Historico lento: 32 amostras a cada 15 s (8 min) no bloco 960...1919 do RBF
			FACA {RSC=RSC+1;}
			SE (RSC>=15) ENTAO (12);
			VAPARA (15);

		ESTADO 12:
			FACA {RSC=0 E_ RSI=(RSI>30?0:RSI+1) E_ VL4=0;}
			VAPARA (13);

		ESTADO 13:
			FACA {VL6=VL4*32+RBI E_ VL7=VL4*32+960+RSI E_ RBF[VL7]=RBF[VL6] E_ VL4=VL4+1;}
			SE (VL4>=30) ENTAO (15);
			VAPARA (13);

		ESTADO 15:		// Falha de comunicacao ativa
			SE (CMC==30) ENTAO (0);						// interrupcao: nada e publicado, heartbeat parado
			SE (CMC==32 && LOS>0) ENTAO (16);				// perda de amostras em curso
			SE (CMC==32 && LOS==0 && VL3*125<CMS) ENTAO (17);	// nova perda (probabilidade CMS/125 permil por s)
			SE (CMC==33) ENTAO (25);						// dado congelado: so o heartbeat avanca
			VAPARA (20);

		ESTADO 16:
			FACA {LOS=LOS-1;}
			VAPARA (0);

		ESTADO 17:		// Perda de 60...180 s de publicacoes (heartbeat parado)
			FACA {LOS=60+VL5;}
			VAPARA (0);

		ESTADO 20:		// Publica com atraso DLY (0 = tempo real; ate 31 s no historico rapido, acima no lento)
			FACA {VL2=(DLY<32?(RBI+32-DLY)%32:960+(RSI+32-DLY/15)%32)
				 E_ W21=RBF[VL2] E_ VL2=VL2+32 E_ W22=RBF[VL2] E_ VL2=VL2+32 E_ W23=RBF[VL2]
				 E_ VL2=VL2+32 E_ W24=RBF[VL2] E_ VL2=VL2+32 E_ W25=RBF[VL2] E_ VL2=VL2+32 E_ W26=RBF[VL2]
				 E_ VL2=VL2+32 E_ W27=RBF[VL2] E_ VL2=VL2+32 E_ W28=RBF[VL2] E_ VL2=VL2+32 E_ W29=RBF[VL2]
				 E_ VL2=VL2+32 E_ W30=RBF[VL2] E_ VL2=VL2+32 E_ W31=RBF[VL2] E_ VL2=VL2+32 E_ W32=RBF[VL2]
				 E_ VL2=VL2+32 E_ W33=RBF[VL2] E_ VL2=VL2+32 E_ W34=RBF[VL2] E_ VL2=VL2+32 E_ W35=RBF[VL2]
				 E_ VL2=VL2+32 E_ W36=RBF[VL2] E_ VL2=VL2+32 E_ W37=RBF[VL2] E_ VL2=VL2+32 E_ W38=RBF[VL2]
				 E_ VL2=VL2+32 E_ W40=RBF[VL2] E_ VL2=VL2+32 E_ W41=RBF[VL2] E_ VL2=VL2+32 E_ W42=RBF[VL2]
				 E_ VL2=VL2+32 E_ W43=RBF[VL2] E_ VL2=VL2+32 E_ W44=RBF[VL2] E_ VL2=VL2+32 E_ W45=RBF[VL2]
				 E_ VL2=VL2+32 E_ W46=RBF[VL2] E_ VL2=VL2+32 E_ W47=RBF[VL2] E_ VL2=VL2+32 E_ W48=RBF[VL2]
				 E_ VL2=VL2+32 E_ W49=RBF[VL2] E_ VL2=VL2+32 E_ W50=RBF[VL2];}
			VAPARA (25);

		ESTADO 25:		// Heartbeat
			FACA {HBT=(HBT>59998?0:HBT+1);}
			VAPARA (0);
	}

// _____________________

MAQUINA_12		// (VM) Sequenciador de Cenarios (W00 Comando / W01 Preset / W02 Semente)
	SE (CSC==4 && SCS>0) ENTAO (80);					// Parar
	SE (CSC==1 || CSC==5) ENTAO (5);					// Iniciar / Reiniciar com a mesma semente

	ESTADOS
	{
		ESTADO 0:		// Sem cenario em execucao
			FACA_ {CSC=(CSC>=2 && CSC<=4?0:CSC);}
			FIMCOMANDO();

		ESTADO 5:		// Inicio: semente, condicoes iniciais da planta, rearme e estacao habilitada
			FACA {CSC=0 E_ SCP=(PSC>6?0:PSC) E_ SEED=(SEED==0 || SEED>60000?(CEN*2654435761+CIC)%60000+1:SEED)
				 E_ RNA=SEED*7919+1 E_ RNB=SEED*104729+3 E_ RNC=SEED*1299709+5 E_ RND=SEED*15485863+7
				 E_ XKA=1 E_ XKB=1 E_ XKC=1 E_ DOV=0 E_ SPO=0 E_ FDS=0 E_ HBL=1 E_ W94=1 E_ RSTP=1
				 E_ TST=(TST==2?1:TST) E_ SCE=0 E_ SCT=0 E_ SST=0 E_ XC4=0 E_ SCS=1;}
			SE (SCP==0) ENTAO (10);						// Tabela SCN escrita pelo PC
			VAPARA (6);

		ESTADO 6:		// Zera a tabela antes de carregar o preset
			RESETA (1,&VM0);
			VAPARA (7);

		ESTADO 7:
			RESETA (1,&SCN[VM0]);
			INCREMENTA (1,&VM0);
			SE (VM0>239) ENTAO (8);
			VAPARA (7);

		ESTADO 8:
			SE (SCP==1) ENTAO (61);
			SE (SCP==2) ENTAO (62);
			SE (SCP==3) ENTAO (63);
			SE (SCP==4) ENTAO (64);
			SE (SCP==5) ENTAO (65);
			SE (SCP==6) ENTAO (66);
			VAPARA (10);

// _______ Inicio de cada passo

		ESTADO 10:
			FACA {VM1=(SCE>19?228:SCE*12) E_ VM2=VM1+1 E_ VM3=VM1+2 E_ VM4=VM1+3 E_ VM5=VM1+4 E_ VM6=VM1+5
				 E_ VM7=VM1+6 E_ VM8=VM1+7 E_ VM9=VM1+8 E_ VMA=VM1+9 E_ VMB=VM1+10
				 E_ QDU=SCN[VM1] E_ QFC=SCN[VM2] E_ QFT=SCN[VM3] E_ QFI=SCN[VM4] E_ QFM=SCN[VM5] E_ QFR=SCN[VM6]
				 E_ QFD=SCN[VM7] E_ QRC=SCN[VM8] E_ QDM=SCN[VM9] E_ QSP=SCN[VMA] E_ QOP=SCN[VMB] E_ SST=0;}
			SE (QDU==0 || SCE>19) ENTAO (90);
			VAPARA (11);

		ESTADO 11:		// Consumo, set-point e operacao do passo (0 = automatico)
			FACA {GOP=(QDM!=DOV?8:GOP) E_ GOC=(QDM!=DOV?(GOC>59998?0:GOC+1):GOC)
				 E_ DOV=(QDM>1000?1000:QDM) E_ DOV=(DOV>0 && DOV<50?50:DOV) E_ SPO=QSP*10
				 E_ FDS=(QOP==3?1:0) E_ HBL=(QOP==1?0:(QOP==2?1:HBL)) E_ W94=(QOP==4?1:W94);}
			SE (QFC>0 && QFC<98) ENTAO (14);
			SE (QFC==98) ENTAO (13);
			SE (QFC==99) ENTAO (12);
			VAPARA (15);

		ESTADO 12:		// Cancela todas as falhas
			FACA {XKA=1 E_ XKB=1 E_ XKC=1;}
			VAPARA (15);

		ESTADO 13:		// Recuperacao gradual de todas as falhas em QRC s
			FACA {XEA=QRC E_ XEB=QRC E_ XEC=QRC E_ XQA=1 E_ XQB=1 E_ XQC=1;}
			VAPARA (15);

		ESTADO 14:		// Injeta no primeiro slot livre (C e sobrescrito se todos ocupados)
			SE (EST28==0) ENTAO (16);
			SE (EST28!=0 && EST29==0) ENTAO (17);
			SE (EST28!=0 && EST29!=0) ENTAO (18);

		ESTADO 16:
			FACA {XCA=QFC E_ XTA=QFT E_ XIA=QFI E_ XMA=QFM E_ XRA=QFR E_ XDA=QFD E_ XEA=QRC E_ XGA=1;}
			VAPARA (15);

		ESTADO 17:
			FACA {XCB=QFC E_ XTB=QFT E_ XIB=QFI E_ XMB=QFM E_ XRB=QFR E_ XDB=QFD E_ XEB=QRC E_ XGB=1;}
			VAPARA (15);

		ESTADO 18:
			FACA {XCC=QFC E_ XTC=QFT E_ XIC=QFI E_ XMC=QFM E_ XRC=QFR E_ XDC=QFD E_ XEC=QRC E_ XGC=1;}
			VAPARA (15);

// _______ Passo em execucao (XC4 = segundos exatos, consumidos)

		ESTADO 15:
			SE (XC4>=1) ENTAO (19);
			SE (CSC==2) ENTAO (20);

		ESTADO 19:
			FACA {XC4=XC4-1 E_ XC4=(XC4>5?0:XC4) E_ SST=SST+1 E_ SCT=SCT+1;}
			SE (SST>=QDU) ENTAO (25);
			VAPARA (15);

		ESTADO 25:		// Proximo passo
			INCREMENTA (1,&SCE);
			VAPARA (10);

		ESTADO 20:		// Pausa: congela a planta e os slots (TST=2)
			FACA {CSC=0 E_ SCS=2 E_ TST=(TST==1?2:TST);}
			SE (CSC==3) ENTAO (21);

		ESTADO 21:		// Retomar
			FACA {CSC=0 E_ SCS=1 E_ TST=(TST==2?1:TST) E_ XC4=0;}
			VAPARA (15);

		ESTADO 80:		// Parar
			FACA {CSC=0 E_ XKA=1 E_ XKB=1 E_ XKC=1 E_ DOV=0 E_ SPO=0 E_ FDS=0 E_ SCS=0 E_ TST=(TST==2?1:TST);}
			VAPARA (0);

		ESTADO 90:		// Fim do cenario
			FACA {XKA=1 E_ XKB=1 E_ XKC=1 E_ DOV=0 E_ SPO=0 E_ FDS=0 E_ SCS=3;}
			VAPARA (0);

// _______ Presets. Campos: [n*12+] 0 DUR / 1 FCD / 2 FTG / 3 FIN / 4 FMD / 5 FRT / 6 FDU / 7 FRC / 8 DMD / 9 SPP / 10 OPC

		ESTADO 61:		// 1 - Especificacao: 30 min normal, 10 min obstrucao progressiva, 15 min recuperacao, 30 min normal
			FACA {SCN[0]=1800
				 E_ SCN[12]=600 E_ SCN[13]=2 E_ SCN[15]=800 E_ SCN[16]=1 E_ SCN[17]=600
				 E_ SCN[24]=900 E_ SCN[25]=98 E_ SCN[31]=120
				 E_ SCN[36]=1800;}
			VAPARA (10);

		ESTADO 62:		// 2 - Bateria de falhas de processo intercaladas com operacao normal (7,5 h)
			FACA {SCN[0]=1800
				 E_ SCN[12]=1200 E_ SCN[13]=1 E_ SCN[15]=1000 E_ SCN[18]=300
				 E_ SCN[24]=1800 E_ SCN[34]=4
				 E_ SCN[36]=2400 E_ SCN[37]=3 E_ SCN[39]=900 E_ SCN[40]=1 E_ SCN[41]=1800 E_ SCN[42]=2400
				 E_ SCN[48]=1800 E_ SCN[58]=4
				 E_ SCN[60]=2400 E_ SCN[61]=4 E_ SCN[63]=700 E_ SCN[64]=1 E_ SCN[65]=2400 E_ SCN[66]=2400
				 E_ SCN[72]=1200
				 E_ SCN[84]=2400 E_ SCN[85]=6 E_ SCN[87]=600 E_ SCN[90]=2400
				 E_ SCN[96]=1200
				 E_ SCN[108]=2400 E_ SCN[109]=8 E_ SCN[111]=1000 E_ SCN[114]=2400
				 E_ SCN[120]=1200
				 E_ SCN[132]=1800 E_ SCN[133]=12 E_ SCN[135]=700 E_ SCN[138]=1800
				 E_ SCN[144]=1200
				 E_ SCN[156]=2400 E_ SCN[157]=7 E_ SCN[159]=1000 E_ SCN[162]=2400
				 E_ SCN[168]=1800 E_ SCN[178]=4;}
			VAPARA (10);

		ESTADO 63:		// 3 - Bateria de falhas de sensor (7 h)
			FACA {SCN[0]=1200
				 E_ SCN[12]=1800 E_ SCN[13]=20 E_ SCN[14]=3 E_ SCN[15]=1000 E_ SCN[18]=1800
				 E_ SCN[24]=1200
				 E_ SCN[36]=2400 E_ SCN[37]=21 E_ SCN[38]=4 E_ SCN[39]=600 E_ SCN[40]=1 E_ SCN[41]=2400 E_ SCN[42]=2400
				 E_ SCN[48]=1200
				 E_ SCN[60]=1800 E_ SCN[61]=22 E_ SCN[62]=6 E_ SCN[63]=800 E_ SCN[66]=1800
				 E_ SCN[72]=1200
				 E_ SCN[84]=2400 E_ SCN[85]=23 E_ SCN[86]=1 E_ SCN[87]=1000 E_ SCN[90]=2400
				 E_ SCN[96]=1200
				 E_ SCN[108]=1800 E_ SCN[109]=24 E_ SCN[110]=103 E_ SCN[111]=300 E_ SCN[114]=1800
				 E_ SCN[120]=1200
				 E_ SCN[132]=2400 E_ SCN[133]=25 E_ SCN[134]=13 E_ SCN[135]=500 E_ SCN[138]=2400
				 E_ SCN[144]=1200
				 E_ SCN[156]=1200 E_ SCN[157]=26 E_ SCN[158]=8 E_ SCN[159]=1000 E_ SCN[162]=1200
				 E_ SCN[168]=1200
				 E_ SCN[180]=1200 E_ SCN[181]=27 E_ SCN[182]=2 E_ SCN[183]=1000 E_ SCN[186]=1200
				 E_ SCN[192]=1200 E_ SCN[202]=4;}
			VAPARA (10);

		ESTADO 64:		// 4 - Bateria de falhas de comunicacao (3,5 h)
			FACA {SCN[0]=1200
				 E_ SCN[12]=1500 E_ SCN[13]=30 E_ SCN[15]=1000 E_ SCN[18]=900
				 E_ SCN[24]=1200
				 E_ SCN[36]=1800 E_ SCN[37]=31 E_ SCN[39]=800 E_ SCN[42]=1800
				 E_ SCN[48]=1200
				 E_ SCN[60]=1800 E_ SCN[61]=32 E_ SCN[63]=1000 E_ SCN[66]=1800
				 E_ SCN[72]=1200
				 E_ SCN[84]=1200 E_ SCN[85]=33 E_ SCN[87]=1000 E_ SCN[90]=1200
				 E_ SCN[96]=1200;}
			VAPARA (10);

		ESTADO 65:		// 5 - Somente operacao normal: carga, set-point, parada/partida e rodizio (sem rotulo de falha) (5,5 h)
			FACA {SCN[0]=3600
				 E_ SCN[12]=1800 E_ SCN[20]=900
				 E_ SCN[24]=1800 E_ SCN[32]=300
				 E_ SCN[36]=1200 E_ SCN[45]=380
				 E_ SCN[48]=1200 E_ SCN[57]=500
				 E_ SCN[60]=600 E_ SCN[70]=1
				 E_ SCN[72]=1200 E_ SCN[82]=2
				 E_ SCN[84]=1200 E_ SCN[94]=3
				 E_ SCN[96]=7200;}
			VAPARA (10);

		ESTADO 66:		// 6 - Degradacao encadeada: desbalanceamento -> falta de lubrificacao -> trip -> reserva
			FACA {SCN[0]=1200
				 E_ SCN[12]=2400 E_ SCN[13]=10 E_ SCN[15]=900 E_ SCN[16]=1 E_ SCN[17]=2400
				 E_ SCN[24]=2400 E_ SCN[25]=9 E_ SCN[27]=1000 E_ SCN[28]=2 E_ SCN[29]=2400
				 E_ SCN[36]=1800
				 E_ SCN[48]=1200 E_ SCN[49]=99 E_ SCN[58]=4;}
			VAPARA (10);
	}

// _____________________

MAQUINA_13		// (VN) Banco de Eventos BDE (Verdade de Campo)

	ESTADOS
	{
		ESTADO 0:		// Um evento por passagem; alteracoes simultaneas sao gravadas nas passagens seguintes
			SE (GOC!=LGC) ENTAO (14);
			SE (SCE!=LSE || SCS!=LSS) ENTAO (13);
			SE (PHC!=LPC) ENTAO (12);
			SE (PHB!=LPB) ENTAO (11);
			SE (PHA!=LPA) ENTAO (10);

		ESTADO 10:		// Slot A
			FACA {LPA=PHA E_ EVT=1 E_ EVC=GCA E_ EVK=TGA*10+PHA E_ EVS=SVA E_ EVM=INA*10+MDA;}
			VAPARA (20);

		ESTADO 11:		// Slot B
			FACA {LPB=PHB E_ EVT=2 E_ EVC=GCB E_ EVK=TGB*10+PHB E_ EVS=SVB E_ EVM=INB*10+MDB;}
			VAPARA (20);

		ESTADO 12:		// Slot C
			FACA {LPC=PHC E_ EVT=3 E_ EVC=GCC E_ EVK=TGC*10+PHC E_ EVS=SVC E_ EVM=INC*10+MDC;}
			VAPARA (20);

		ESTADO 13:		// Cenario
			FACA {LSE=SCE E_ LSS=SCS E_ EVT=10 E_ EVC=SCE+1 E_ EVK=SCS E_ EVS=SCP E_ EVM=SEED;}
			VAPARA (20);

		ESTADO 14:		// Evento operacional
			FACA {LGC=GOC E_ EVT=20 E_ EVC=GOP E_ EVK=DTY E_ EVS=NRN E_ EVM=PSP/10;}
			VAPARA (20);

		ESTADO 20:		// Grava registro de 12 campos
			INCREMENTA (2,&DDE,&GEV);
			FACA {DDE=(DDE>99?0:DDE) E_ GEV=(GEV>59999?1:GEV)
				 E_ VN1=DDE*12 E_ VN2=VN1+1 E_ VN3=VN1+2 E_ VN4=VN1+3 E_ VN5=VN1+4 E_ VN6=VN1+5
				 E_ VN7=VN1+6 E_ VN8=VN1+7 E_ VN9=VN1+8 E_ VNA=VN1+9 E_ VNB=VN1+10 E_ VNC=VN1+11
				 E_ BDE[VN1]=GEV E_ BDE[VN2]=DTE E_ BDE[VN3]=RLG E_ BDE[VN4]=RSEC E_ BDE[VN5]=SCT/3600 E_ BDE[VN6]=SCT%3600
				 E_ BDE[VN7]=EVT E_ BDE[VN8]=EVC E_ BDE[VN9]=EVK E_ BDE[VNA]=EVS E_ BDE[VNB]=EVM
				 E_ VN0=GEV+DTE+RLG+RSEC+BDE[VN5]+BDE[VN6]+EVT+EVC+EVK+EVS+EVM E_ BDE[VNC]=VN0&0xFFFF;}
			VAPARA (0);
	}

// _____________________

MAQUINA_14		// (VO) Injecao Manual de Falhas (W03...W09)

	ESTADOS
	{
		ESTADO 0:
			SE (MFX>3 || (MFX==1 && MFC==0)) ENTAO (40);
			SE (MFX==1 && MFC>0) ENTAO (10);
			SE (MFX==2) ENTAO (20);
			SE (MFX==3) ENTAO (30);

		ESTADO 10:		// Primeiro slot livre (C e sobrescrito se todos ocupados)
			SE (EST28==0) ENTAO (11);
			SE (EST28!=0 && EST29==0) ENTAO (12);
			SE (EST28!=0 && EST29!=0) ENTAO (13);

		ESTADO 11:
			FACA {XCA=MFC E_ XTA=MFT E_ XIA=MFI E_ XMA=MFM E_ XRA=MFR E_ XDA=MFD E_ XEA=0 E_ XGA=1 E_ MFX=0;}
			VAPARA (0);

		ESTADO 12:
			FACA {XCB=MFC E_ XTB=MFT E_ XIB=MFI E_ XMB=MFM E_ XRB=MFR E_ XDB=MFD E_ XEB=0 E_ XGB=1 E_ MFX=0;}
			VAPARA (0);

		ESTADO 13:
			FACA {XCC=MFC E_ XTC=MFT E_ XIC=MFI E_ XMC=MFM E_ XRC=MFR E_ XDC=MFD E_ XEC=0 E_ XGC=1 E_ MFX=0;}
			VAPARA (0);

		ESTADO 20:		// Cancela todas
			FACA {XKA=1 E_ XKB=1 E_ XKC=1 E_ MFX=0;}
			VAPARA (0);

		ESTADO 30:		// Recuperacao gradual de todas em MFR s
			FACA {XEA=MFR E_ XEB=MFR E_ XEC=MFR E_ XQA=1 E_ XQB=1 E_ XQC=1 E_ MFX=0;}
			VAPARA (0);

		ESTADO 40:		// Comando invalido
			FACA {MFX=0;}
			VAPARA (0);
	}

// --- MAQUINA_15		// (VP) Livre
// --- MAQUINA_16		// (VQ) Livre
// --- MAQUINA_17		// (VR) Livre
// --- MAQUINA_18		// (VS) Livre
// --- MAQUINA_19		// (VT) Livre
// --- MAQUINA_20		// (VU) Livre

// APPLICATION_MACHINES

// _____________________

// ########################### ALARMES + ACIONAMENTOS ###########################

// __________________________________ ALARMES __________________________________
//  ALARMES: 01 - Falha Bomba B-01
//			 02 - Falha Bomba B-02
//			 03 - Temperatura Alta Enrolamento M-01
//			 04 - SALVAR Dados no PC (Fim de Memoria do CLP)
//			 05 - Temperatura Alta Enrolamento M-02
//			 06 - Vibracao Alta B-01
//			 07 - Vibracao Alta B-02
//			 08 - Emergencia (ED3)
//			 09 - Nivel Muito Baixo no Reservatorio
//			 10 - Pressao de Succao Baixa
//			 11 - Pressao de Recalque Baixa
//			 12 - Pressao de Recalque Alta
//			 13 - Falha da Valvula XV-01
//			 14 - Temperatura Alta Mancal B-01
//			 15 - Temperatura Alta Mancal B-02
//			 16 - Sobrecorrente M-01
//			 17 - Sobrecorrente M-02
//			 18 - Falha de Sensor (Fora de Faixa)
//			 19 - Nivel Alto no Reservatorio (LSH)
//			 20 - Livre
//			 21...99 - Livre

MAQUINA_21		// (VV) Alarmes
	SE (ED2==1 || W94==1 || TECLA==0) ENTAO (5);	// Desliga Alarme
	SE (MNT==1) ENTAO (1);							// Manutencao

	ESTADOS
	{
		ESTADO 0:
			RESETA (14,&SDG1,&SC1,&SC2,&SC3,&SC4,&SC5,&SC6,&SC7,&SC8,&SC9,&SC10,&AAA,&ALM,&AL1[0]);
			RESETA (10,&SC11,&SC12,&SC13,&SC14,&SC15,&SC16,&SC17,&SC18,&SC19,&SC20);
			RESETA (10,&AL1[1],&AL1[2],&AL1[3],&AL1[4],&AL1[5],&AL1[6],&AL1[7],&AL1[8],&AL1[9],&AL1[10]);
			RESETA (10,&AL1[11],&AL1[12],&AL1[13],&AL1[14],&AL1[15],&AL1[16],&AL1[17],&AL1[18],&AL1[19],&AL1[20]);
			RESETA (1,&ALN);
			SE (ATRASO>=99) ENTAO (10);		// Atraso p/ Estabilizacao do Sistema

		ESTADO 1:		// Manutencao
			VAPARA (0);

		ESTADO 5:		// Desliga Alarme
			RESETA (2,&SDG1,&W94);
			SE (ATRASO>=30) ENTAO (10);
// .......

		ESTADO 10:		// Alarme-01 - Falha Bomba B-01
			FACA {VV8=AL1[1] E_ VV9=10;}
			SE (SB1==4) ENTAO (12);
			SE ((AAA==1 || AAA==99) && SMLX==13579) ENTAO (12);			// Simulador ON
			VAPARA (17);

		ESTADO 12:
			SE (SC1>5 && AL1[1]==0) ENTAO (300);
			VAPARA (20);

		ESTADO 17:
			RESETA (2,&SC1,&AL1[1]);
			VAPARA (20);
// .......

		ESTADO 20:		// Alarme-02 - Falha Bomba B-02
			FACA {VV8=VV8+AL1[2] E_ VV9=20;}
			SE (SB2==4) ENTAO (22);
			SE ((AAA==2 || AAA==99) && SMLX==13579) ENTAO (22);			// Simulador ON
			VAPARA (27);

		ESTADO 22:
			SE (SC2>5 && AL1[2]==0) ENTAO (300);
			VAPARA (30);

		ESTADO 27:
			RESETA (2,&SC2,&AL1[2]);
			VAPARA (30);
// .......

		ESTADO 30:		// Alarme-03 - Temperatura Alta Enrolamento M-01
			FACA {VV8=VV8+AL1[3] E_ VV9=30;}
			SE (MW1>TWA && MSF[13]==0) ENTAO (32);
			SE ((AAA==3 || AAA==99) && SMLX==13579) ENTAO (32);			// Simulador ON
			VAPARA (37);

		ESTADO 32:
			SE (SC3>5 && AL1[3]==0) ENTAO (300);
			VAPARA (40);

		ESTADO 37:
			RESETA (2,&SC3,&AL1[3]);
			VAPARA (40);
// .......

		ESTADO 40:		// Alarme-04 - SALVAR Dados no PC (Fim de Memoria do CLP)
			FACA {VV8=VV8+AL1[4] E_ VV9=40;}
			SE ((SLD1<11 || SLD2<11 || SLD3<3 || SLD4<11) && ASV>0) ENTAO (42);
			SE ((AAA==4 || AAA==99) && SMLX==13579) ENTAO (42);			// Simulador ON
			VAPARA (47);

		ESTADO 42:
			SE (SC4>5 && AL1[4]==0) ENTAO (300);
			VAPARA (50);

		ESTADO 47:
			RESETA (2,&SC4,&AL1[4]);
			VAPARA (50);
// .......

		ESTADO 50:		// Alarme-05 - Temperatura Alta Enrolamento M-02
			FACA {VV8=VV8+AL1[5] E_ VV9=50;}
			SE (MW2>TWA && MSF[14]==0) ENTAO (52);
			SE ((AAA==5 || AAA==99) && SMLX==13579) ENTAO (52);			// Simulador ON
			VAPARA (57);

		ESTADO 52:
			SE (SC5>5 && AL1[5]==0) ENTAO (300);
			VAPARA (60);

		ESTADO 57:
			RESETA (2,&SC5,&AL1[5]);
			VAPARA (60);
// .......

		ESTADO 60:		// Alarme-06 - Vibracao Alta B-01
			FACA {VV8=VV8+AL1[6] E_ VV9=60;}
			SE (MV1>VBA && MSF[8]==0 && SB1==2) ENTAO (62);
			SE ((AAA==6 || AAA==99) && SMLX==13579) ENTAO (62);			// Simulador ON
			VAPARA (67);

		ESTADO 62:
			SE (SC6>10 && AL1[6]==0) ENTAO (300);
			VAPARA (70);

		ESTADO 67:
			RESETA (2,&SC6,&AL1[6]);
			VAPARA (70);
// .......

		ESTADO 70:		// Alarme-07 - Vibracao Alta B-02
			FACA {VV8=VV8+AL1[7] E_ VV9=70;}
			SE (MV2>VBA && MSF[9]==0 && SB2==2) ENTAO (72);
			SE ((AAA==7 || AAA==99) && SMLX==13579) ENTAO (72);			// Simulador ON
			VAPARA (77);

		ESTADO 72:
			SE (SC7>10 && AL1[7]==0) ENTAO (300);
			VAPARA (80);

		ESTADO 77:
			RESETA (2,&SC7,&AL1[7]);
			VAPARA (80);
// .......

		ESTADO 80:		// Alarme-08 - Emergencia (ED3)
			FACA {VV8=VV8+AL1[8] E_ VV9=80;}
			SE (EMG==1) ENTAO (82);
			SE ((AAA==8 || AAA==99) && SMLX==13579) ENTAO (82);			// Simulador ON
			VAPARA (87);

		ESTADO 82:
			SE (SC8>1 && AL1[8]==0) ENTAO (300);
			VAPARA (90);

		ESTADO 87:
			RESETA (2,&SC8,&AL1[8]);
			VAPARA (90);
// .......

		ESTADO 90:		// Alarme-09 - Nivel Muito Baixo no Reservatorio
			FACA {VV8=VV8+AL1[9] E_ VV9=90;}
			SE (LLO==1) ENTAO (92);
			SE ((AAA==9 || AAA==99) && SMLX==13579) ENTAO (92);			// Simulador ON
			VAPARA (97);

		ESTADO 92:
			SE (SC9>5 && AL1[9]==0) ENTAO (300);
			VAPARA (100);

		ESTADO 97:
			RESETA (2,&SC9,&AL1[9]);
			VAPARA (100);
// .......

		ESTADO 100:		// Alarme-10 - Pressao de Succao Baixa
			FACA {VV8=VV8+AL1[10] E_ VV9=100;}
			SE (MPS<PSL+100 && MSF[2]==0 && NRN>0) ENTAO (102);
			SE ((AAA==10 || AAA==99) && SMLX==13579) ENTAO (102);			// Simulador ON
			VAPARA (107);

		ESTADO 102:
			SE (SC10>5 && AL1[10]==0) ENTAO (300);
			VAPARA (110);

		ESTADO 107:
			RESETA (2,&SC10,&AL1[10]);
			VAPARA (110);
// .......

		ESTADO 110:		// Alarme-11 - Pressao de Recalque Baixa
			FACA {VV8=VV8+AL1[11] E_ VV9=110;}
			SE (NRN>0 && MSF[3]==0 && MPD+PDL<PSP) ENTAO (112);
			SE ((AAA==11 || AAA==99) && SMLX==13579) ENTAO (112);			// Simulador ON
			VAPARA (117);

		ESTADO 112:
			SE (SC11>60 && AL1[11]==0) ENTAO (300);
			VAPARA (120);

		ESTADO 117:
			RESETA (2,&SC11,&AL1[11]);
			VAPARA (120);
// .......

		ESTADO 120:		// Alarme-12 - Pressao de Recalque Alta
			FACA {VV8=VV8+AL1[12] E_ VV9=120;}
			SE (MPD>PDH && MSF[3]==0) ENTAO (122);
			SE ((AAA==12 || AAA==99) && SMLX==13579) ENTAO (122);			// Simulador ON
			VAPARA (127);

		ESTADO 122:
			SE (SC12>5 && AL1[12]==0) ENTAO (300);
			VAPARA (130);

		ESTADO 127:
			RESETA (2,&SC12,&AL1[12]);
			VAPARA (130);
// .......

		ESTADO 130:		// Alarme-13 - Falha da Valvula XV-01
			FACA {VV8=VV8+AL1[13] E_ VV9=130;}
			SE (XVF==1) ENTAO (132);
			SE ((AAA==13 || AAA==99) && SMLX==13579) ENTAO (132);			// Simulador ON
			VAPARA (137);

		ESTADO 132:
			SE (SC13>5 && AL1[13]==0) ENTAO (300);
			VAPARA (140);

		ESTADO 137:
			RESETA (2,&SC13,&AL1[13]);
			VAPARA (140);
// .......

		ESTADO 140:		// Alarme-14 - Temperatura Alta Mancal B-01
			FACA {VV8=VV8+AL1[14] E_ VV9=140;}
			SE (MB1>TBA && MSF[15]==0) ENTAO (142);
			SE ((AAA==14 || AAA==99) && SMLX==13579) ENTAO (142);			// Simulador ON
			VAPARA (147);

		ESTADO 142:
			SE (SC14>5 && AL1[14]==0) ENTAO (300);
			VAPARA (150);

		ESTADO 147:
			RESETA (2,&SC14,&AL1[14]);
			VAPARA (150);
// .......

		ESTADO 150:		// Alarme-15 - Temperatura Alta Mancal B-02
			FACA {VV8=VV8+AL1[15] E_ VV9=150;}
			SE (MB2>TBA && MSF[16]==0) ENTAO (152);
			SE ((AAA==15 || AAA==99) && SMLX==13579) ENTAO (152);			// Simulador ON
			VAPARA (157);

		ESTADO 152:
			SE (SC15>5 && AL1[15]==0) ENTAO (300);
			VAPARA (160);

		ESTADO 157:
			RESETA (2,&SC15,&AL1[15]);
			VAPARA (160);
// .......

		ESTADO 160:		// Alarme-16 - Sobrecorrente M-01
			FACA {VV8=VV8+AL1[16] E_ VV9=160;}
			SE (MI1>IAL && MSF[6]==0 && SB1==2) ENTAO (162);
			SE ((AAA==16 || AAA==99) && SMLX==13579) ENTAO (162);			// Simulador ON
			VAPARA (167);

		ESTADO 162:
			SE (SC16>5 && AL1[16]==0) ENTAO (300);
			VAPARA (170);

		ESTADO 167:
			RESETA (2,&SC16,&AL1[16]);
			VAPARA (170);
// .......

		ESTADO 170:		// Alarme-17 - Sobrecorrente M-02
			FACA {VV8=VV8+AL1[17] E_ VV9=170;}
			SE (MI2>IAL && MSF[7]==0 && SB2==2) ENTAO (172);
			SE ((AAA==17 || AAA==99) && SMLX==13579) ENTAO (172);			// Simulador ON
			VAPARA (177);

		ESTADO 172:
			SE (SC17>5 && AL1[17]==0) ENTAO (300);
			VAPARA (180);

		ESTADO 177:
			RESETA (2,&SC17,&AL1[17]);
			VAPARA (180);
// .......

		ESTADO 180:		// Alarme-18 - Falha de Sensor (Fora de Faixa)
			FACA {VV8=VV8+AL1[18] E_ VV9=180;}
			SE (SNF>0) ENTAO (182);
			SE ((AAA==18 || AAA==99) && SMLX==13579) ENTAO (182);			// Simulador ON
			VAPARA (187);

		ESTADO 182:
			SE (SC18>5 && AL1[18]==0) ENTAO (300);
			VAPARA (190);

		ESTADO 187:
			RESETA (2,&SC18,&AL1[18]);
			VAPARA (190);
// .......

		ESTADO 190:		// Alarme-19 - Nivel Alto no Reservatorio (LSH)
			FACA {VV8=VV8+AL1[19] E_ VV9=190;}
			SE (EDG13==1) ENTAO (192);
			SE ((AAA==19 || AAA==99) && SMLX==13579) ENTAO (192);			// Simulador ON
			VAPARA (197);

		ESTADO 192:
			SE (SC19>5 && AL1[19]==0) ENTAO (300);
			VAPARA (200);

		ESTADO 197:
			RESETA (2,&SC19,&AL1[19]);
			VAPARA (200);
// .......

		ESTADO 200:		// Alarme-20 - Livre
			FACA {VV8=VV8+AL1[20] E_ VV9=200;}
			SE (BBB==1) ENTAO (202);
			SE ((AAA==20 || AAA==99) && SMLX==13579) ENTAO (202);			// Simulador ON
			VAPARA (207);

		ESTADO 202:
			SE (SC20>5 && AL1[20]==0) ENTAO (300);
			VAPARA (305);

		ESTADO 207:
			RESETA (2,&SC20,&AL1[20]);
			VAPARA (305);
// .......

// ______________  Banco de Dados - Alarmes
// DIM BDA[600]    ; Variaveis do Banco de Dados: Alarmes
//                 ;  1 - AAAAA - Contador Continuo para uso do PC
//                 ;  2 - _AABB - Dia/Mes do Alarme
//                 ;  3 - _AABB - Hor/Min do Alarme
//                 ;  4 - AAAAA - Motivo do Alarme
//                 ;  5 - AAAAA - Segundo do Dia do Relogio do Simulador / 2 (TOD/2)
//                 ;  6 - AAAAA - Check-Sum

		ESTADO 300:		// Alarme Novo
			SETA (3,&SDG1,&SG1,&ALM);
			INCREMENTA (2,&DD1,&W87);
			FACA {DD1=(DD1>99?0:DD1) E_ VV7=TOD/2
				E_ VV1=DD1*6 E_ VV2=VV1+1 E_ VV3=VV1+2 E_ VV4=VV1+3 E_ VV5=VV1+4 E_ VV6=VV1+5
				E_ BDA[VV1]=W87 E_ BDA[VV2]=DTE E_ BDA[VV3]=RLG E_ BDA[VV4]=VV9 E_ BDA[VV5]=VV7 E_ BDA[VV6]=W87+DTE+RLG+VV9+VV7
				E_ VV1=VV9/10 E_ AL1[VV1]=VV9 E_ DT1[VV1]=DTE E_ HR1[VV1]=RLG E_ ALC=VV9/10 E_ E25(210);}
			VAPARA (10);
// .......

		ESTADO 305:		// Verificacao do Estado do Alarme
			FACA {ALN=(AL1[1]>0)+(AL1[2]>0)+(AL1[3]>0)+(AL1[4]>0)+(AL1[5]>0)+(AL1[6]>0)+(AL1[7]>0)+(AL1[8]>0)+(AL1[9]>0)+(AL1[10]>0)
				 E_ ALN=ALN+(AL1[11]>0)+(AL1[12]>0)+(AL1[13]>0)+(AL1[14]>0)+(AL1[15]>0)+(AL1[16]>0)+(AL1[17]>0)+(AL1[18]>0)+(AL1[19]>0)+(AL1[20]>0)
				 E_ ALC=(ALN==0?0:ALC);}
			SE (SG1>300) ENTAO (306);	// Tempo Esgotado de Beep
			SE (VV8==0) ENTAO (307);	// Alarme OK
			VAPARA (10);				// em Alarme

		ESTADO 306:		// Tempo Esgotado de Beep
			RESETA (2,&SDG1,&SG1);
			VAPARA (10);

		ESTADO 307:		// Alarme OK
			RESETA (2,&SDG1,&ALM);
			VAPARA (10);
	}

// _____________________

MAQUINA_22		// (VW) Funcoes Diversas - Geral


	ESTADOS
	{
		ESTADO 0:		// 1a energizacao (FVS sem assinatura): carrega os Valores de Fabrica
			FACA {FML=PRES3[0] E_ CTZ=PRES3[1] E_ TBV=PRES3[2] E_ VSF=PRES1[2] E_ DSF=PRES1[4];}
			SE (FVS!=24680 && EST25!=93) {E25(93);}
			VAPARA (20);

// ========== Espelho das ED's com Filtros (ED9...ED13 sem filtro)

		ESTADO 20:
			FACA {  CG1=(ED1==EDG1?0:CG1) E_ CG2=(ED2==EDG2?0:CG2) E_ CG3=(ED3==EDG3?0:CG3) E_ CG4=(ED4==EDG4?0:CG4)
				 E_ CG5=(ED5==EDG5?0:CG5) E_ CG6=(ED6==EDG6?0:CG6) E_ CG7=(ED7==EDG7?0:CG7) E_ CG8=(ED8==EDG8?0:CG8)
				 E_ EDG9=ED9 E_ EDG10=ED10 E_ EDG11=ED11 E_ EDG12=ED12 E_ EDG13=ED13;}
			if (CG1>FED1) {EDG1=(EDG1==0?1:0);}
			if (CG2>FED2) {EDG2=(EDG2==0?1:0);}
			if (CG3>FED3) {EDG3=(EDG3==0?1:0);}
			if (CG4>FED4) {EDG4=(EDG4==0?1:0);}
			if (CG5>FED5) {EDG5=(EDG5==0?1:0);}
			if (CG6>FED6) {EDG6=(EDG6==0?1:0);}
			if (CG7>FED7) {EDG7=(EDG7==0?1:0);}
			if (CG8>FED8) {EDG8=(EDG8==0?1:0);}
			VAPARA (30);

// ========== Calculo dos Saldos para Alarme de Salvar Dados no PC

		ESTADO 30:
			FACA {  SLD1=(DD1>=UDD1?(99+UDD1-DD1):(UDD1-DD1))
				 E_ SLD2=(DD2>=UDD2?(99+UDD2-DD2):(UDD2-DD2))
				 E_ SLD3=(DD3>=UDD3?(9+UDD3-DD3):(UDD3-DD3))
				 E_ SLD4=(DDE>=UDE?(99+UDE-DDE):(UDE-DDE));}
			VAPARA (40);

// ========== Livre

		ESTADO 40:
			VAPARA (50);

// ========== Livre

		ESTADO 50:
			VAPARA (0);
	}

// _____________________

MAQUINA_23		// (VX) Espelho de Acionamento das SD's
	SE (EST23>1) ENTAO (0);

	ESTADOS
	{
		ESTADO 0:
			SE (SDG1!=1) {SD1=0;}
			SE (SDG1==1) {SD1=1;}
			SE (SDG2!=1) {SD2=0;}
			SE (SDG2==1) {SD2=1;}
			SE (SDG3!=1) {SD3=0;}
			SE (SDG3==1) {SD3=1;}
			SE (SDG4!=1) {SD4=0;}
			SE (SDG4==1) {SD4=1;}
			VAPARA (1);

		ESTADO 1:		// Saidas analogicas: SAN5/SAN6 -> imagem fisica (Referencia de Velocidade dos Inversores)
			FACA_ {SA5=(SAN5>4095?4095:SAN5) E_ SA6=(SAN6>4095?4095:SAN6);}
			SE (SDG5!=1) {SD5=0;}
			SE (SDG5==1) {SD5=1;}
			SE (SDG6!=1) {SD6=0;}
			SE (SDG6==1) {SD6=1;}
			SE (SDG7!=1) {SD7=0;}
			SE (SDG7==1) {SD7=1;}
			SE (SDG8!=1) {SD8=0;}
			SE (SDG8==1) {SD8=1;}
			VAPARA (0);
	}

// _____________________

// ################################### DISPLAY ##################################

MAQUINA_24		// (VY) Senha

	ESTADOS
	{
		ESTADO 0:
			SE (TECLA==23) ENTAO (2);
			SE (TECLA==19 || TECLA==20) ENTAO (3);

		ESTADO 2:
			INCREMENTA (1,&W91);		// Contador de Acionamentos da Tecla E/V
			SE (TECLA!=23) ENTAO (0);
// .......

		ESTADO 3:		// Checar Estado do Simulador
			SE (SMLX==13579) ENTAO (5);		// Simulador ON
			VAPARA (4);       				// Simulador OFF

		ESTADO 4:       // Simulador OFF
			SE (KEY==1) ENTAO (6);				// Apenas Leitura ...
			SE (KEY==2 && SNH!=1) ENTAO (7);	// Senha ???
			SE (KEY==3 && MNT==0) ENTAO (8);	// Manutencao ???
			SE (BLQ==1) ENTAO (9);
			VAPARA (0);

		ESTADO 5:       // Simulador ON
			SE (KEY==1) ENTAO (6);							// Apenas Leitura ...
			SE ((KEY==2 || KEY==3) && SNH==0) ENTAO (7);	// Senha ???
			VAPARA (0);
// .......

		ESTADO 6:		// Apenas Leitura ...
			FACA {E25(21);}
			SE (TECLA!=19 && TECLA!=20) ENTAO (0);

		ESTADO 7:		// Senha ???
			FACA {E25(22);}
			SE (TECLA!=19 && TECLA!=20) ENTAO (0);

		ESTADO 8:		// Manutencao ???
			FACA {E25(23);}
			SE (TECLA!=19 && TECLA!=20) ENTAO (0);

		ESTADO 9:		// Teclado Bloqueado - Acesso via PC"
			FACA {E25(24);}
			SE (TECLA!=19 && TECLA!=20) ENTAO (0);
	}

// _____________________

MAQUINA_25		// (VZ) Display													XYZ
	SE (TECLA==21 || TECLA==22) ENTAO (12);				// Retornar / Avancar Tela
	SE (TLA==6) ENTAO (9);								// Estacionar Mapa de Telas F1+F2+F3+F4+F5
    SE (TF1==1 && EST25!=50) ENTAO (50);				// F1 - Supervisao
    SE (TF2==1 && EST25!=100) ENTAO (100);				// F2 - Parametros
    SE (TF3==1 && EST25!=150) ENTAO (150);				// F3 - Receitas
    SE (TF4==1 && EST25!=200) ENTAO (200);				// F4 - Servicos
    SE (TF5==1 && EST25!=250) ENTAO (250);				// F5 - Configuracao
	SE (TESC==1 && EST25!=10) ENTAO (10);				// ESC Tela

	ESTADOS
	{
		ESTADO 0:
			MOSTRA ("T",0,28,240,38,2,"M A R R A R I ",0,"");
			SE (RHOR>11) ENTAO (2);
			SE (RHOR>17) ENTAO (3);
			SE (RHOR>23) ENTAO (1);
			VAPARA (1);

		ESTADO 1:
			MOSTRA ("",0,84,240,38,2,"   Bom  Dia   ",0,"");
			VAPARA (4);

		ESTADO 2:
			MOSTRA ("",0,84,240,38,2,"  Boa  Tarde  ",0,"");
			VAPARA (4);

		ESTADO 3:
			MOSTRA ("",0,84,240,38,2,"  Boa  Noite  ",0,"");
			VAPARA (4);

		ESTADO 4:
			SE (ATRASO>=30) ENTAO (10);
			SE (TT2==1 && TENT==1) ENTAO (5);		// Tecla B + Tecla OK
// .......

		ESTADO 5:		// Definir Senha
			RESETA (2,&W99,&BLQ);
			FACA_ {SNH=(W98==0?1:0);}
			MOSTRA ("T",0,0,240,28,1,"Definir SENHA: ",&W98,"");
			MOSTRA ("D1",0,28,240,28,1,"Bloqueio Teclado: ",&BLQ,"");
			FIMCOMANDO();
// _______

        ESTADO 9:		// Mapa de Telas F1+F2+F3+F4+F5 Estacionado
            FIMCOMANDO();
// _______

		ESTADO 10:
			RESETA (6,&TLA,&XTLA,&XTLY,&ZTLA,&ZTLY,&TLY[0]);
			SETA (6,&TLY[1],&TLY[2],&TLY[3],&TLY[4],&TLY[5],&TLY[6]);
			FACA {ZZA=DD3;}
			MOSTRA ("T",0,0,240,38,2,"M A R R A R I ",0,"");
			MOSTRA ("D2Z",0,38,36,28,1,"",&DIA,"/");
			MOSTRA ("D2Z",36,38,36,28,1,"",&MES,"/");
			MOSTRA ("D2Z",72,38,24,28,1,"",&ANO,"");
			MOSTRA ("D2Z",144,38,36,28,1,"",&RHOR,":");
			MOSTRA ("D2Z",180,38,36,28,1,"",&RMIN,":");
			MOSTRA ("D2Z",216,38,24,28,1,"",&RSEC,"");
			MOSTRA ("D1",0,66,84,28,1,"Turno:",&TRN,"");
			if (SMLX==13579) {MOSTRA ("I",0,122,240,28,1," SIMULADOR ATIVADO  ",0,"");}
			SE (ATRASO>=35 && MPT==1) ENTAO (11);

		ESTADO 11:
			MOSTRA ("T",0,0,240,28,1,"   F1-Supervisao    ",0,"");
			MOSTRA ("",0,28,240,28,1,"   F2-Parametros    ",0,"");
			MOSTRA ("",0,56,240,28,1,"   F3-Receitas      ",0,"");
			MOSTRA ("",0,84,240,28,1,"   F4-Servicos      ",0,"");
			MOSTRA ("",0,112,240,28,1,"   F5-Configuracao  ",0,"");
			SE (ATRASO>=35) ENTAO (10);
// _______

		ESTADO 12:
			FACA {ZY1=TECLA;}
			SE (ZY1==21 || ZY1==22) ENTAO (13);		// Filtro de Seguranca
			VAPARA (20);

		ESTADO 13:		// AVANCAR/RETORNAR Telas
			FACA_ {ZY2=(ZY1==21?0xFFFFFFFF:1) E_ ZY3=(SMLX==13579?1:0) E_ MPT=(MPT==1?1:0);}
			if (TLA==1) {FACA ZY4=0*MPT+5+ZY3 E_ ZY5=TLY[1]+ZY2 E_ ZY5=(ZY5==0?ZY4:ZY5) E_ ZY5=(ZY5>ZY4?1:ZY5) E_ TLY[1]=ZY5;}
			if (TLA==2) {FACA ZY4=8*MPT+0 E_ ZY5=TLY[2]+ZY2 E_ ZY5=(ZY5==0?ZY4:ZY5) E_ ZY5=(ZY5>ZY4?1:ZY5) E_ TLY[2]=ZY5;}
			if (TLA==3) {FACA ZY4=3*MPT+0 E_ ZY5=TLY[3]+ZY2 E_ ZY5=(ZY5==0?ZY4:ZY5) E_ ZY5=(ZY5>ZY4?1:ZY5) E_ TLY[3]=ZY5;}
			if (TLA==4) {FACA ZY4=5*MPT+5 E_ ZY5=TLY[4]+ZY2 E_ ZY5=(ZY5==0?ZY4:ZY5) E_ ZY5=(ZY5>ZY4?1:ZY5) E_ TLY[4]=ZY5;}
			if (TLA==5) {FACA ZY4=3*MPT+0 E_ ZY5=TLY[5]+ZY2 E_ ZY5=(ZY5==0?ZY4:ZY5) E_ ZY5=(ZY5>ZY4?1:ZY5) E_ TLY[5]=ZY5;}
			VAPARA (20);
// _______

		ESTADO 20:
			SE (TLA==1) ENTAO (51);
			SE (TLA==2) ENTAO (101);
			SE (TLA==3) ENTAO (151);
			SE (TLA==4) ENTAO (201);
			SE (TLA==5) ENTAO (251);
			VAPARA (10);

// ______________________ M E N S A G E N S ______________________

		ESTADO 21:
			MOSTRA ("T",0,56,240,28,1," Apenas Leitura ... ",0,"");
			SE (ATRASO>=7 && TECLA==128) ENTAO (20);

		ESTADO 22:
			FACA {XTLA=TLA E_ XTLY=TLY[TLA];}
			MOSTRA ("T",0,56,240,28,1,"     Senha ???      ",0,"");
			SE (ATRASO>=7 && TECLA==128) ENTAO (205);

		ESTADO 23:
			FACA {ZTLA=TLA E_ ZTLY=TLY[TLA];}
			MOSTRA ("T",0,56,240,28,1,"   Manutencao ???   ",0,"");
			SE (ATRASO>=7 && TECLA==128) ENTAO (238);

		ESTADO 24:
			MOSTRA ("T",0,56,240,28,1," Teclado Bloqueado  ",0,"");
			MOSTRA ("",0,84,240,28,1,"   Acesso via PC    ",0,"");
			SE (ATRASO>=7 && TECLA==128) ENTAO (20);

		ESTADO 25:
			MOSTRA ("T",0,56,240,28,1,"    Cancelado ...   ",0,"");
			SE (ATRASO>=15) ENTAO (20);

		ESTADO 26:
			MOSTRA ("T",0,56,240,28,1,"       * ok *       ",0,"");
			SE (ATRASO>=15) ENTAO (20);

// ================================  SUPERVISAO  ===============================

		ESTADO 50:
//										12345678901234567890
			MOSTRA ("TI",0,56,240,28,1,"  F1 - Supervisao   ",0,"");
            SETA (1,&TLA);
			SE (TF1==0) ENTAO (51);

		ESTADO 51:
			SETA (2,&TLA,&KEY);
			SE (TLY[1]==2) ENTAO (60);
			SE (TLY[1]==3) ENTAO (61);
			SE (TLY[1]==4) ENTAO (62);
			SE (TLY[1]==5) ENTAO (65);

			SE (TLY[1]==6) ENTAO (90);			// Simulador ON
			VAPARA (55);
// _______

// 12345678901234567890
//  STATUS DA ESTACAO
//   1 BOMBA OPERANDO
// Chave:1   Habilita:1
// B1:2 B2:0  Princ:1
// Alarmes: 2   Emg:0

		ESTADO 55:						// TLY1=1
			SETA (2,&TLA,&TLY[1]);
			FACA {KEY=2 E_ HBL=(HBL==1?1:0) E_ VZ1=ETP E_ WWW=ETP;}
			MOSTRA ("T",0,0,240,28,1," STATUS DA ESTACAO  ",0,"");
			MOSTRA ("S0I",0,28,240,28,1,"     DESLIGADA      ",0,"");
			MOSTRA ("S1I",0,28,240,28,1,"   PARTINDO BOMBA   ",0,"");
			MOSTRA ("S2I",0,28,240,28,1,"  1 BOMBA OPERANDO  ",0,"");
			MOSTRA ("S3I",0,28,240,28,1," 2 BOMBAS OPERANDO  ",0,"");
			MOSTRA ("S4I",0,28,240,28,1," NIVEL BAIXO - BLOQ ",0,"");
			MOSTRA ("S5I",0,28,240,28,1,"  FALHA DAS BOMBAS  ",0,"");
			MOSTRA ("S6I",0,28,240,28,1,"     MANUTENCAO     ",0,"");
			MOSTRA ("S7I",0,28,240,28,1,"     EMERGENCIA     ",0,"");

			MOSTRA ("FD1",0,56,84,28,1,"Chave:",&EDG1,"");
			MOSTRA ("D1",120,56,120,28,1,"Habilita:",&HBL,"");
			MOSTRA ("FD1",0,84,48,28,1,"B1:",&SB1,"");
			MOSTRA ("FD1",60,84,48,28,1,"B2:",&SB2,"");
			MOSTRA ("FD1",132,84,108,28,1,"Princ:",&DTY,"");
			MOSTRA ("FD2",0,112,132,28,1,"Alarmes:",&ALN,"");
			MOSTRA ("FD1",156,112,84,28,1,"Emg:",&EMG,"");

			SE (ETP!=VZ1 && ATRASO>=10) ENTAO (55);
// _______

// 123456789012345678901234
// Nivel: 60.0%Entr: 52.0
// Succ: 1163mbVaz:  52.7
// Rec: 4.50barSP:  4.50
// Ref: 85.3%  Pot: 25.6kW
// Tagua: 22.5 Tamb: 25.3
// Hora: 14    Noite:0
// Dem:  950   Alarmes: 0

		ESTADO 60:						// TLY1=2
			FACA_ {VZ1=MPD/10 E_ VZ2=PSP/10;}
			MOSTRA ("TFD4P1",0,0,120,22,4,"Nivel:",&MLT,"%");
			MOSTRA ("FD4P1",130,0,110,22,4,"Entr:",&MQE,"");
			MOSTRA ("FD4",0,22,120,22,4,"Succ:",&MPS,"mb");
			MOSTRA ("FD4P1",130,22,110,22,4,"Vaz: ",&MFQ,"");
			MOSTRA ("FD4P2",0,44,120,22,4,"Rec:",&VZ1,"bar");
			MOSTRA ("FD4P2",130,44,110,22,4,"SP: ",&VZ2,"");
			MOSTRA ("FD4P1",0,66,120,22,4,"Ref:",&SRF,"%");
			MOSTRA ("FD4P1",130,66,110,22,4,"Pot:",&MPW,"kW");
			MOSTRA ("FD4P1",0,88,120,22,4,"Tagua:",&MTF,"");
			MOSTRA ("FD4P1",130,88,110,22,4,"Tamb:",&MTA,"");
			MOSTRA ("FD2",0,110,120,22,4,"Hora: ",&HRS,"");
			MOSTRA ("FD1",130,110,110,22,4,"Noite:",&NGT,"");
			MOSTRA ("FD4",0,132,120,22,4,"Dem:  ",&DMF,"");
			MOSTRA ("FD2",130,132,110,22,4,"Alarmes:",&ALN,"");
			FIMCOMANDO();
// _______

// 123456789012345678901234
//       B-01     B-02
// Est:  2        0
// Freq: 42.6Hz   0.0Hz
// Corr: 34.5A    0.0A
// Vib:  2.35mm   0.20mm
// Enrol:85.2'C   22.5'C
// Manc: 45.0'C   23.0'C

		ESTADO 61:						// TLY1=3
			MOSTRA ("TF",0,0,240,22,4,"       B-01     B-02",0,"");
			MOSTRA ("F",0,22,60,22,4,"Est:",0,"");
			MOSTRA ("FD5",60,22,90,22,4,"",&SB1,"");
			MOSTRA ("FD5",150,22,90,22,4,"",&SB2,"");
			MOSTRA ("F",0,44,60,22,4,"Freq:",0,"");
			MOSTRA ("FD5P1",60,44,90,22,4,"",&MS1,"Hz");
			MOSTRA ("FD5P1",150,44,90,22,4,"",&MS2,"Hz");
			MOSTRA ("F",0,66,60,22,4,"Corr:",0,"");
			MOSTRA ("FD5P1",60,66,90,22,4,"",&MI1,"A");
			MOSTRA ("FD5P1",150,66,90,22,4,"",&MI2,"A");
			MOSTRA ("F",0,88,60,22,4,"Vib:",0,"");
			MOSTRA ("FD5P2",60,88,90,22,4,"",&MV1,"mm");
			MOSTRA ("FD5P2",150,88,90,22,4,"",&MV2,"mm");
			MOSTRA ("F",0,110,60,22,4,"Enrol:",0,"");
			MOSTRA ("FD5P1",60,110,90,22,4,"",&MW1,"'C");
			MOSTRA ("FD5P1",150,110,90,22,4,"",&MW2,"'C");
			MOSTRA ("F",0,132,60,22,4,"Manc:",0,"");
			MOSTRA ("FD5P1",60,132,90,22,4,"",&MB1,"'C");
			MOSTRA ("FD5P1",150,132,90,22,4,"",&MB2,"'C");
			FIMCOMANDO();
// _______

// 123456789012345678901234
// Preset:1  Comando:0
// Passo: 3      RODANDO
// Tempo:  1h 0234s
// A: 2   1  800p F3
// B: 0   0    0p F0
// C: 0   0    0p F0
// Semente:12345  Anom:1

		ESTADO 62:						// TLY1=4
			FACA {KEY=2 E_ VZ1=SCS E_ WWW=SCS;}
			MOSTRA ("TD1",0,0,100,22,4,"Preset:",&PSC,"");
			MOSTRA ("D1",120,0,100,22,4,"Comando:",&CSC,"");
			MOSTRA ("FD2",0,22,90,22,4,"Passo:",&SCE,"");
			MOSTRA ("S0",130,22,110,22,4,"PARADO",0,"");
			MOSTRA ("S1",130,22,110,22,4,"RODANDO",0,"");
			MOSTRA ("S2",130,22,110,22,4,"PAUSADO",0,"");
			MOSTRA ("S3",130,22,110,22,4,"CONCLUIDO",0,"");
			MOSTRA ("FD3",0,44,110,22,4,"Tempo:",&W76,"h");
			MOSTRA ("FD4Z",110,44,60,22,4,"",&W77,"s");
			MOSTRA ("FD2",0,66,50,22,4,"A:",&GCA,"");
			MOSTRA ("FD3",60,66,40,22,4,"",&TGA,"");
			MOSTRA ("FD4",110,66,60,22,4,"",&SVA,"p");
			MOSTRA ("FD1",180,66,30,22,4,"F",&PHA,"");
			MOSTRA ("FD2",0,88,50,22,4,"B:",&GCB,"");
			MOSTRA ("FD3",60,88,40,22,4,"",&TGB,"");
			MOSTRA ("FD4",110,88,60,22,4,"",&SVB,"p");
			MOSTRA ("FD1",180,88,30,22,4,"F",&PHB,"");
			MOSTRA ("FD2",0,110,50,22,4,"C:",&GCC,"");
			MOSTRA ("FD3",60,110,40,22,4,"",&TGC,"");
			MOSTRA ("FD4",110,110,60,22,4,"",&SVC,"p");
			MOSTRA ("FD1",180,110,30,22,4,"F",&PHC,"");
			MOSTRA ("FD5",0,132,140,22,4,"Semente:",&SEED,"");
			MOSTRA ("FD1",150,132,90,22,4,"Anom:",&W72,"");
			SE (SCS!=VZ1 && ATRASO>=10) ENTAO (62);
// _______

//		12345678901234567890
//	    ID Atual:1234567890
//		12/05/23 de 14:35
//		Turno: 2
//
//		Tecle A p Novo Lote

		ESTADO 65:						// TLY1=3
			FACA {  VZ1=GR2/100 E_ VZ2=GR2%100
				 E_ VZ3=GR3/100 E_ VZ4=GR3%100
				 E_ VZ5=GR4/100 E_ VZ6=GR4%100 E_ VZA=GR1;}
			MOSTRA ("T",0,0,240,28,1,"ID Atual:",&VZA,"");
			MOSTRA ("D2Z",0,28,36,28,1,"",&VZ3,"/");
			MOSTRA ("D2Z",36,28,36,28,1,"",&VZ4,"/");
			MOSTRA ("D2Z",72,28,60,28,1,"",&VZ2," de");
			MOSTRA ("D2Z",144,28,36,28,1,"",&VZ5,":");
			MOSTRA ("D2Z",180,28,24,28,1,"",&VZ6,"");
			MOSTRA ("D1",0,56,240,28,1,"Turno: ",&VZ1,"");
			MOSTRA ("",0,112,240,28,1,"Tecle A p Novo Lote ",0,"");
			SE (TKL==1507) ENTAO (66);           // Tecla 'A' por 7 decimos de segundo

//		12345678901234567890
//	    ID Atual:1234567890
//
//	     Novo ID:1234567891
//
//		Tecle OK p CONFIRMAR

		ESTADO 66:
			RESETA (1,&KEY);
			FACA {VZB=VZA+1;}
			MOSTRA ("T",0,0,240,28,1,"ID Atual:",&VZA,"");
			MOSTRA ("",0,56,240,28,1," Novo ID:",&VZB,"");
			MOSTRA ("",0,112,240,28,1,"Tecle OK p CONFIRMAR",0,"");
			SE (TKL==2307) ENTAO (67);           // Tecla 'OK' por 7 decimos de segundo
			SE (SNH==0) ENTAO (22);

		ESTADO 67:
			MOSTRA ("T",0,56,240,28,1,"     Aguarde ...    ",0,"");
			FACA {GR1=VZB E_ W94=12;}
			SE (ATRASO>=15 && W94==0) ENTAO (26);

// _______ SIMULADOR ON															XYZ

// TST Modo de Simulacao (0=Desativada / 1=Ativada / 2=Congelada)
// AAA Simulacao MAN de Alarmes (0,1,2,...)
// XLM1 Livre
// XLM2 Livre
// XLM3 Livre

		ESTADO 90:						// TLY1=6
			RESETA (1,&KEY);
			MOSTRA ("TI",0,0,240,28,1,"   Tecle OK para    ",0,"");
			MOSTRA ("I",0,28,240,28,1," Programar Memoria  ",0,"");
			MOSTRA ("D1",36,56,156,28,1,"Simulador >>",&TST,"");	// Modo de Simulacao
			MOSTRA ("",0,84,120,28,1,"AL:",&AAA,"");				// Simula Alarme
			MOSTRA ("",120,84,120,28,1,"X1:",&XLM1,"");				// Livre
			MOSTRA ("",0,112,120,28,1,"X2:",&XLM2,"");				// Livre
			MOSTRA ("",120,112,120,28,1,"X3:",&XLM3,"");			// Livre
			SE (TKL==2307) ENTAO (91);								// Tecla 'OK' por 7 decimos de segundo

		ESTADO 91:
			MOSTRA ("TI",0,0,240,28,1," A T E N C A O  !!! ",0,"");
			MOSTRA ("",0,56,240,28,1," Esta Operacao ira  ",0,"");
			MOSTRA ("",0,84,240,28,1,"APAGAR toda Memoria ",0,"");
			MOSTRA ("",0,112,240,28,1,"Tecle A p/ Confirmar",0,"");
			SE (ATRASO>=50) ENTAO (25);
			SE (TT1==1) ENTAO (92);		// Tecla A

		ESTADO 92:
			FACA {E00(99);}
			MOSTRA ("T",54,0,132,28,1,"Aguarde ...",0,"");
			MOSTRA ("",0,28,240,28,1,"Limpand Memoria ",&W96,"");
			SE (ATRASO>=40 && EST0<10) ENTAO (93);

		ESTADO 93:
			MOSTRA ("T",54,0,132,28,1,"Aguarde ...",0,"");
			MOSTRA ("",54,28,132,28,1,"Programando",0,"");

// ....... ED's / SD's / Encoder's / Sinaleiros
			RESETA (8,&ED1,&ED2,&ED3,&ED4,&ED5,&ED6,&ED7,&ED8);
			RESETA (5,&ED9,&ED10,&ED11,&ED12,&ED13);
			RESETA (8,&SDG1,&SDG2,&SDG3,&SDG4,&SDG5,&SDG6,&SDG7,&SDG8);
			RESETA (8,&SAN1,&SAN2,&SAN3,&SAN4,&SAN5,&SAN6,&SAN7,&SAN8);
			SETA (3,&ED1,&ED3,&ED12);

// ....... Banco de Dados
			RESETA (8,&DD1,&DD2,&DD3,&UDD1,&UDD2,&UDD3,&DDE,&UDE);

// ....... Diversos
			FACA_ {TLA=1 E_ TLY[1]=6;}

// ....... W00 ... W99
			FACA_ { W87=0 E_ W88=0 E_ W89=0 E_ W90=0 E_ W91=0 E_ W92=0
				 E_ BLQ=0 E_ SNH=1 E_ W98=111 E_ W99=111 E_ MPT=1
				 E_ CSC=0 E_ PSC=1 E_ SEED=0 E_ MFC=0 E_ MFT=0 E_ MFI=0 E_ MFM=0 E_ MFR=0 E_ MFD=0 E_ MFX=0 E_ GEV=0;}

// ....... F1 - SUPERVISAO
			FACA_ {MNT=0 E_ HBL=1
				 E_  PDT=123 E_ GR1=12345 E_ GR2=TRN*100+ANO E_ GR3=DTE E_ GR4=RLG
				 E_ SPV1=0 E_ SPV2=0 E_ SPV3=0 E_ SPV4=0 E_ SPV5=0 E_ SPV6=0 E_ SPV7=10 E_ SPV8=0 E_ SPV9=0 E_ SPV10=0;}

// ....... F2 - PARAMETROS DO PROCESSO
			FACA_ { FED1=1 E_ FED2=1 E_ FED3=0 E_ FED4=0 E_ FED5=0 E_ FED6=0 E_ FED7=0 E_ FED8=0
				 E_ ASV=1 E_ TNO1=800 E_ TNO2=1400 E_ TNO3=2000 E_ TNO4=200 E_ ABLT=2
				 E_ PSD=4500 E_ PSN=3800 E_ PNH=0 E_ PNE=5 E_ PKP=20 E_ PKI=8 E_ PKD=0
				 E_ SMN=450 E_ SAS=950 E_ SAP=780 E_ TAS=60 E_ ROT=4 E_ ARS=30 E_ SFB=850
				 E_ TWA=1300 E_ TWT=1500 E_ TBA=900 E_ TBT=1000 E_ VBA=710 E_ VBT=1100 E_ IAL=362 E_ ITR=397 E_ PSL=350
				 E_ LON=400 E_ LOF=800 E_ LLL=100 E_ LLR=250 E_ PDL=800 E_ PDH=7000;}

// ....... F3 - PRODUCAO
			FACA_ { BDL[0]=12345 E_ BDL[1]=GR2 E_ BDL[2]=GR3 E_ BDL[3]=GR4 E_ BDL[4]=GR1
				 E_ BDL[5]=0 E_ BDL[6]=0 E_ BDL[7]=0 E_  BDL[8]=0 E_  BDL[9]=0;}

// ....... F5 - CONFIGURACAO: Faixas (RLO/RHI) e Ruido 1 sigma (SNA, unidade de engenharia) dos Transmissores
			FACA_ { RLO[0]=0 E_ RLO[1]=0 E_ RLO[2]=0 E_ RLO[3]=0 E_ RLO[4]=0 E_ RLO[5]=0 E_ RLO[6]=0 E_ RLO[7]=0 E_ RLO[8]=0 E_ RLO[9]=0
				 E_ RLO[10]=0 E_ RLO[11]=0 E_ RLO[12]=0 E_ RLO[13]=0 E_ RLO[14]=0 E_ RLO[15]=0 E_ RLO[16]=0 E_ RLO[17]=0 E_ RLO[18]=0
				 E_ RHI[0]=0 E_ RHI[1]=1000 E_ RHI[2]=2500 E_ RHI[3]=10000 E_ RHI[4]=1500 E_ RHI[5]=1500 E_ RHI[6]=500 E_ RHI[7]=500
				 E_ RHI[8]=2000 E_ RHI[9]=2000 E_ RHI[10]=600 E_ RHI[11]=600 E_ RHI[12]=500 E_ RHI[13]=2000 E_ RHI[14]=2000
				 E_ RHI[15]=1500 E_ RHI[16]=1500 E_ RHI[17]=1000 E_ RHI[18]=1000
				 E_ SNA[0]=0 E_ SNA[1]=25 E_ SNA[2]=40 E_ SNA[3]=150 E_ SNA[4]=60 E_ SNA[5]=60 E_ SNA[6]=20 E_ SNA[7]=20
				 E_ SNA[8]=60 E_ SNA[9]=60 E_ SNA[10]=5 E_ SNA[11]=5 E_ SNA[12]=10 E_ SNA[13]=10 E_ SNA[14]=10
				 E_ SNA[15]=10 E_ SNA[16]=10 E_ SNA[17]=5 E_ SNA[18]=10;}

// ....... SIMULADOR: Perfil de Consumo Diario (permil da condutancia nominal, DPT[h] = hora h, DPT[24] = DPT[0])
			FACA_ { DPT[0]=300 E_ DPT[1]=260 E_ DPT[2]=240 E_ DPT[3]=230 E_ DPT[4]=240 E_ DPT[5]=320 E_ DPT[6]=520
				 E_ DPT[7]=750 E_ DPT[8]=800 E_ DPT[9]=720 E_ DPT[10]=650 E_ DPT[11]=640 E_ DPT[12]=700 E_ DPT[13]=660
				 E_ DPT[14]=600 E_ DPT[15]=580 E_ DPT[16]=600 E_ DPT[17]=680 E_ DPT[18]=780 E_ DPT[19]=820 E_ DPT[20]=760
				 E_ DPT[21]=640 E_ DPT[22]=500 E_ DPT[23]=380 E_ DPT[24]=300;}

// ....... SIMULADOR
			FACA {TST=1 E_ AAA=0 E_ XLM1=0 E_ XLM2=0 E_ XLM3=0 E_ HINI=99 E_ SNG=1000 E_ FVS=24680 E_ RSTP=1;}
			SE (ATRASO>=10) ENTAO (26);

// ==========================  PARAMETROS DO PROCESSO  =========================

		ESTADO 100:
//									    12345678901234567890
			MOSTRA ("TI",0,56,240,28,1,"  F2 - Parametros   ",0,"");
			FACA {TLA=2;}
			SE (TF2==0) ENTAO (101);

		ESTADO 101:
			FACA {TLA=2 E_ KEY=2;}
			SE (TLY[2]==2) ENTAO (110);
			SE (TLY[2]==3) ENTAO (112);
			SE (TLY[2]==4) ENTAO (115);
			SE (TLY[2]==5) ENTAO (117);
			SE (TLY[2]==6) ENTAO (118);
			SE (TLY[2]==7) ENTAO (120);
			SE (TLY[2]==8) ENTAO (140);
			SE (MPT==0) ENTAO (102);
			VAPARA (105);

		ESTADO 102:                             // Telas Ocultas
			MOSTRA ("T",0,0,240,28,1,"       _  _  _      ",0,"");
			FIMCOMANDO();
// _______

// 12345678901234567890
// Filt das EDG's (seg)
// 01:1s     05:1s
// 02:1s     06:1s
// 03:1s     07:1s
// 04:1s     08:1s

		ESTADO 105:						// TLY2=1
			SETA (1,&TLY[2]);
			MOSTRA ("T",0,0,240,28,1,"Filt das EDG's (seg)",0,"");
			MOSTRA ("",0,28,120,28,1,"01:",&FED1,"");
			MOSTRA ("",0,56,120,28,1,"02:",&FED2,"");
			MOSTRA ("",0,84,120,28,1,"03:",&FED3,"");
			MOSTRA ("",0,112,120,28,1,"04:",&FED4,"");
			MOSTRA ("",120,28,120,28,1,"05:",&FED5,"");
			MOSTRA ("",120,56,120,28,1,"06:",&FED6,"");
			MOSTRA ("",120,84,120,28,1,"07:",&FED7,"");
			MOSTRA ("",120,112,120,28,1,"08:",&FED8,"");
			FIMCOMANDO();
// _______

// 12345678901234567890
// PRESSAO DE RECALQUE
// SP Dia:   4.500bar
// SP Noite: 3.800bar
// Noite:00h  ate 05h
// P 20    I  8    D  0

		ESTADO 110:						// TLY2=2
			MOSTRA ("TI",0,0,240,28,1,"PRESSAO DE RECALQUE ",0,"");
			MOSTRA ("D5P3",0,28,240,28,1,"SP Dia:   ",&PSD,"bar");
			MOSTRA ("D5P3",0,56,240,28,1,"SP Noite: ",&PSN,"bar");
			MOSTRA ("D2Z",0,84,120,28,1,"Noite:",&PNH,"h");
			MOSTRA ("D2Z",132,84,108,28,1,"ate ",&PNE,"h");
			MOSTRA ("D3",0,112,72,28,1,"P",&PKP,"");
			MOSTRA ("D3",84,112,72,28,1,"I",&PKI,"");
			MOSTRA ("D3",168,112,72,28,1,"D",&PKD,"");
			FIMCOMANDO();
// _______

// 12345678901234567890
// BOMBAS - SEQUENCIA
// Vel Minima:  45.0%
// Aux Liga:    95.0%
// Aux Desliga: 76.0%
// T Aux: 60s Rod: 4h

		ESTADO 112:                    // TLY2=3
			MOSTRA ("TI",0,0,240,28,1," BOMBAS - SEQUENCIA ",0,"");
			MOSTRA ("D4P1",0,28,240,28,1,"Vel Minima:  ",&SMN,"%");
			MOSTRA ("D4P1",0,56,240,28,1,"Aux Liga:    ",&SAS,"%");
			MOSTRA ("D4P1",0,84,240,28,1,"Aux Desliga: ",&SAP,"%");
			MOSTRA ("D3",0,112,120,28,1,"T Aux:",&TAS,"s");
			MOSTRA ("D2",132,112,108,28,1,"Rod:",&ROT,"h");
			FIMCOMANDO();
// _______

// 12345678901234567890
// PROTECAO TEMPERATURA
// Enrol Alm: 130.0'C
// Enrol Trip:150.0'C
// Manc Alm:   90.0'C
// Manc Trip: 100.0'C

		ESTADO 115:                    // TLY2=4
			MOSTRA ("TI",0,0,240,28,1,"PROTECAO TEMPERATURA",0,"");
			MOSTRA ("D4P1",0,28,240,28,1,"Enrol Alm: ",&TWA,"'C");
			MOSTRA ("D4P1",0,56,240,28,1,"Enrol Trip:",&TWT,"'C");
			MOSTRA ("D4P1",0,84,240,28,1,"Manc Alm:  ",&TBA,"'C");
			MOSTRA ("D4P1",0,112,240,28,1,"Manc Trip: ",&TBT,"'C");
			FIMCOMANDO();
// _______

// 123456789012345678901234
// PROTECAO VIB/CORR/SUCC
// Vib Alarme:   7.10mm/s
// Vib Trip:    11.00mm/s
// Corr Alarme:  36.2A
// Corr Trip:    39.7A
// Succao Min:  350mbar

		ESTADO 117:                    // TLY2=5
			MOSTRA ("TI",0,0,240,22,4,"PROTECAO VIB/CORR/SUCC  ",0,"");
			MOSTRA ("D5P2",0,22,240,22,4,"Vib Alarme:  ",&VBA,"mm/s");
			MOSTRA ("D5P2",0,44,240,22,4,"Vib Trip:    ",&VBT,"mm/s");
			MOSTRA ("D4P1",0,66,240,22,4,"Corr Alarme:  ",&IAL,"A");
			MOSTRA ("D4P1",0,88,240,22,4,"Corr Trip:    ",&ITR,"A");
			MOSTRA ("D4",0,110,240,22,4,"Succao Min:  ",&PSL,"mbar");
			FIMCOMANDO();
// _______

// 123456789012345678901234
// RESERVATORIO E LIMITES
// XV Abre:40.0% Fecha:80.0%
// LL Bloq:10.0% Lib:  25.0%
// Desv Baixo:  800mbar
// Press Alta: 7000mbar
// Rearme Aut:   30s
// Vel s/PT-02: 85.0%

		ESTADO 118:                    // TLY2=6
			MOSTRA ("TI",0,0,240,22,4,"RESERVATORIO E LIMITES  ",0,"");
			MOSTRA ("D4P1",0,22,130,22,4,"XV Abre:",&LON,"%");
			MOSTRA ("D4P1",130,22,110,22,4,"Fecha:",&LOF,"%");
			MOSTRA ("D4P1",0,44,130,22,4,"LL Bloq:",&LLL,"%");
			MOSTRA ("D4P1",130,44,110,22,4,"Lib:  ",&LLR,"%");
			MOSTRA ("D5",0,66,240,22,4,"Desv Baixo: ",&PDL,"mbar");
			MOSTRA ("D5",0,88,240,22,4,"Press Alta: ",&PDH,"mbar");
			MOSTRA ("D4",0,110,240,22,4,"Rearme Aut: ",&ARS,"s");
			MOSTRA ("D4P1",0,132,240,22,4,"Vel s/PT-02:",&SFB,"%");
			FIMCOMANDO();
// _______

// 12345678901234567890
// ALM Salvar Dados PC
//      1 (sim)

		ESTADO 120:						// TLY2=6
			FACA {ASV=(ASV>1?0:ASV) E_ WWW=ASV;}
			MOSTRA ("T",0,0,240,28,1,"ALM Salvar Dados PC",0,"");
			MOSTRA ("D1",60,28,12,28,1,"",&ASV,"");
			MOSTRA ("S0",84,28,156,28,1,"(nao)",0,"");
			MOSTRA ("S1",84,28,156,28,1,"(sim)",0,"");
			SE (TKL==2301) ENTAO (120);		// Tecla 'OK' + Atraso para Atualizacao das Variaveis
// _______

		ESTADO 140:						// TLY2=7
			FACA_ {TNO1=(TNO1>2359?0:TNO1) E_ TNO2=(TNO2>2359?0:TNO2) E_ TNO3=(TNO3>2359?0:TNO3) E_ TNO4=(TNO4>2359?0:TNO4)
				E_ ABLT=(ABLT>2?0:ABLT) E_ WWW=ABLT;}
			MOSTRA ("T",0,0,240,28,1," Horario dos Turnos",0,"");
			MOSTRA ("D4Z",12,28,84,28,1,"T1:",&TNO1,"");
			MOSTRA ("D4Z",144,28,84,28,1,"T2:",&TNO2,"");
			MOSTRA ("D4Z",12,56,84,28,1,"T3:",&TNO3,"");
			MOSTRA ("D4Z",144,56,84,28,1,"T4:",&TNO4,"");
			MOSTRA ("D1",0,84,192,28,1,"Abert AUT Lote:",&ABLT,"");
			MOSTRA ("S0",192,84,48,28,1,"-Nao",0,"");
			MOSTRA ("S1",192,84,48,28,1,"-Dia",0,"");
			MOSTRA ("S2",192,84,48,28,1,"-Trn",0,"");
			SE (TKL==2301) ENTAO (140);		// Tecla 'OK' + Atraso para Atualizacao das Variaveis

// ================================== PRODUCAO =================================

		ESTADO 150:
//									    12345678901234567890
			MOSTRA ("TI",0,56,240,28,1,"   F3 - Producao    ",0,"");
            FACA {TLA=3;}
			SE (TF3==0) ENTAO (151);

		ESTADO 151:
			FACA {TLA=3 E_ KEY=2;}
			SE (TLY[3]==2) ENTAO (180);
			SE (TLY[3]==3) ENTAO (185);
			VAPARA (155);
// _______

// 12345678901234567890
// 9) ID:12345
// 11/08/26 11:35 Trn:1
// 12345 12345 12345
// 12345 12345
//
//                  >AB

// DIM BDL[100]    ; Variaveis do Banco de Dados: Lotes
//                 ;  1 - AAAAA - Contador Continuo para uso do PC
//                 ;  2 - _AABB - Trn/Ano da Coleta
//                 ;  3 - _AABB - Dia/Mes da Coleta
//                 ;  4 - _AABB - Hor/Min da Coleta
//                 ;  5 - AAAAA - ID do Lote
//                 ;  6 - AAAAA - Livre
//                 ;  7 - AAAAA - Livre
//                 ;  8 - AAAAA - Livre
//                 ;  9 - AAAAA - Livre
//                 ; 10 - AAAAA - Livre

		ESTADO 155:						// TLY3=1
			SETA (2,&TLY[3],&KEY);
			FACA {ZZA=(ZZA>9?0:ZZA) E_ VZ1=ZZA*10 E_ VZ2=VZ1+1 E_ VZ3=VZ1+2 E_ VZ4=VZ1+3 E_ VZ5=VZ1+4
				 E_ VZ6=VZ1+5 E_ VZ7=VZ1+6 E_ VZ8=VZ1+7 E_ VZ9=VZ1+8 E_ VZ10=VZ1+9
				 E_ VZA=BDL[VZ2]/100 E_ VZB=BDL[VZ2]%100
				 E_ VZC=BDL[VZ3]/100 E_ VZD=BDL[VZ3]%100
				 E_ VZE=BDL[VZ4]/100 E_ VZF=BDL[VZ4]%100
				 E_ VZG=BDL[VZ5] E_ VZH=BDL[VZ6] E_ VZI=BDL[VZ7] E_ VZJ=BDL[VZ8] E_ VZK=BDL[VZ9] E_ VZL=BDL[VZ10];}
			MOSTRA ("TD1",0,0,36,28,1,"",&ZZA,")");
			MOSTRA ("",36,0,204,28,1,"ID:",&VZG,"");
			MOSTRA ("D2Z",0,28,36,28,1,"",&VZC,"/");
			MOSTRA ("D2Z",36,28,36,28,1,"",&VZD,"/");
			MOSTRA ("D2Z",72,28,24,28,1,"",&VZB,"");
			MOSTRA ("D2Z",108,28,36,28,1,"",&VZE,":");
			MOSTRA ("D2Z",144,28,24,28,1,"",&VZF,"");
			MOSTRA ("D1",180,28,60,28,1,"Trn:",&VZA,"");
			MOSTRA ("",0,56,80,28,1,"",&VZH,"");
			MOSTRA ("",80,56,80,28,1,"",&VZI,"");
			MOSTRA ("",160,56,80,28,1,"",&VZJ,"");
			MOSTRA ("",0,84,80,28,1,"",&VZK,"");
			MOSTRA ("",80,84,80,28,1,"",&VZL,"");
			MOSTRA ("",204,112,36,28,1,">AB",0,"");
			SE (ATRASO>=30) ENTAO (155);
			SE (TT1==1) ENTAO (156);
			SE (TT2==1) ENTAO (157);

		ESTADO 156:
			INCREMENTA (1,&ZZA);
			FACA {ZZA=(ZZA>9?0:ZZA);}
			SE (TT1==0) ENTAO (155);

		ESTADO 157:
			DECREMENTA (1,&ZZA);
			FACA {ZZA=(ZZA>999?9:ZZA);}
			SE (TT2==0) ENTAO (155);
// _______

// 12345678901234567890
// Alarme:123/123  >123
// Minuto:123/123  >123
//   Lote: 12/12   >12

		ESTADO 180:						// TLY3=2
			FACA {KEY=(SMLX==13579?0:1);}
			MOSTRA ("TD3Z",0,0,120,28,1,"Alarme:",&DD1,"");
			MOSTRA ("D3Z",120,0,48,28,1,"/",&UDD1,"");
			MOSTRA ("D3Z",192,0,48,28,1,">",&SLD1,"");
			MOSTRA ("D3Z",0,28,120,28,1,"Minuto:",&DD2,"");
			MOSTRA ("D3Z",120,28,48,28,1,"/",&UDD2,"");
			MOSTRA ("D3Z",192,28,48,28,1,">",&SLD2,"");
			MOSTRA ("D2Z",0,56,120,28,1,"  Lote: ",&DD3,"");
			MOSTRA ("D2Z",120,56,36,28,1,"/",&UDD3,"");
			MOSTRA ("D2Z",192,56,36,28,1,">",&SLD3,"");
			FIMCOMANDO();

// _______ Apagar Bco de Dados - Minutos + Alarmes

		ESTADO 185:						// TLY3=3
			MOSTRA ("T",0,0,240,28,1,"Tecle OK para Apagar",0,"");
			MOSTRA ("",36,28,168,28,1,"Banco de Dados",0,"");
			SE (TKL==2307) ENTAO (186);           // Tecla 'OK' por 7 decimos de segundo

		ESTADO 186:
			MOSTRA ("T",0,56,240,28,1,"     Aguarde ...    ",0,"");
			SE (ATRASO>=15) ENTAO (187);
			SE (TECLA!=23) ENTAO (25);
			SE (SNH==0) ENTAO (22);

		ESTADO 187:
			MOSTRA ("T",0,56,240,28,1,"     Apagando ...   ",0,"");
			FACA {W94=11;}
			SE (ATRASO>=7 && W94==0) ENTAO (26);

// =================================  SERVICOS  ================================

		ESTADO 200:
//									    12345678901234567890
			MOSTRA ("TI",0,56,240,28,1,"   F4 - Servicos    ",0,"");
            FACA {TLA=4;}
			SE (TF4==0) ENTAO (201);

		ESTADO 201:
			FACA {TLA=4 E_ KEY=3;}
			SE (TLY[4]==2) ENTAO (210);
			SE (TLY[4]==3) ENTAO (215);
			SE (TLY[4]==4) ENTAO (230);
			SE (TLY[4]==5) ENTAO (235);

			SE (TLY[4]==6) ENTAO (238);
			SE (TLY[4]==7) ENTAO (241);
			SE (TLY[4]==8) ENTAO (242);
			SE (TLY[4]==9) ENTAO (243);
			SE (TLY[4]==10) ENTAO (244);
			VAPARA (205);

// ____________________________ Senha ____________________________

		ESTADO 205:                    	// TLY4=1
			RESETA (1,&KEY);
			SETA (1,&TLY[4]);
			FACA {TLA=4;}
			SE (W99==W98) ENTAO (207);
			SE (W98==0) ENTAO (208);
			VAPARA (206);

		ESTADO 206:
			RESETA (2,&SNH,&W99);
			MOSTRA ("TI",0,0,240,28,1,"     S E N H A      ",0,"");
			MOSTRA ("",108,28,60,28,1,"",&W99,"");
			SE (W99>0) ENTAO (205);

		ESTADO 207:
			SETA (1,&SNH);
			MOSTRA ("TI",0,0,240,28,1,"     S E N H A      ",0,"");
			MOSTRA ("D1",108,28,60,28,1,"",&W99,"");
			SE (W99!=W98) ENTAO (206);
			SE (XTLA!=0) ENTAO (209);

		ESTADO 208:
			SETA (1,&SNH);
			MOSTRA ("T",18,0,222,28,1,"NAO protegido por",0,"");
			MOSTRA ("",66,28,108,28,1,"S E N H A",0,"");
			FIMCOMANDO();

		ESTADO 209:
			FACA {TLA=XTLA E_ TLY[TLA]=XTLY;}
			RESETA (2,&XTLA,&XTLY);
			VAPARA (20);

// ___________________________ Alarmes ___________________________

// VZ0 - Auxiliar

// VZ1 - Ponteiro do Alarme (On-Line / Bco de Dados)
// VZ2 - Motivo do Alarme
// VZ3 - Data
// VZ4 - Hora

// VZ5 - Motivo da Parada
// VZ6 - Variacao do Alarme

// VZA - Dia
// VZB - Mes
// VZC - Hora
// VZD - Minuto

		ESTADO 210:						// TLY4=2
			SETA (1,&KEY);
			FACA {TLA=4 E_ TLY[4]=2;}
			VAPARA (211);

		ESTADO 211:
			RESETA (1,&VZ1);
			SE (ALM==0) ENTAO (212);		// Alarme OK
			VAPARA (213);               	// Em Alarme

		ESTADO 212:
			MOSTRA ("TI",0,0,240,28,1,"     A L A R M E    ",0,"");
			MOSTRA ("",0,28,240,28,1, "      *  ok  *      ",0,"");
			SE (ALM>0) ENTAO (213);

		ESTADO 213:		// Alarmes
			MOSTRA ("T",0,0,72,28,1,"ALARME",0,"");
			INCREMENTA (1,&VZ1);
			FACA {VZ2=AL1[VZ1] E_ VZ3=DT1[VZ1] E_ VZ4=HR1[VZ1] E_ VZ5=AL1[VZ1];}
			SE (AL1[VZ1]>0) ENTAO (218);
			SE (VZ1>20) ENTAO (211);
			VAPARA (213);

// ___________________ Bco de Dados de Alarmes ___________________

		ESTADO 215:						// TLY4=3
			FACA {VZ9=DD1+1;}
			VAPARA (216);

		ESTADO 216:
			SE (TENT==1 || VZ9>0) ENTAO (217);

		ESTADO 217:
			DECREMENTA (1,&VZ9);
			FACA {VZ9=(VZ9>99?99:VZ9) E_ VZA=VZ9*6+1 E_ VZB=VZA+1
				 E_ VZC=VZA+2 E_ VZD=VZA+3 E_ VZ1=BDA[VZC]/10 E_ VZ2=BDA[VZC] E_ VZ3=BDA[VZA] E_ VZ4=BDA[VZB] E_ VZ5=BDA[VZD];}
			MOSTRA ("TD2Z",0,0,72,28,1,"ALM_",&VZ9,"");
			VAPARA (218);

		ESTADO 218:
			FACA {VZA=VZ3/100 E_ VZB=VZ3%100 E_ VZC=VZ4/100 E_ VZD=VZ4%100;}
			MOSTRA ("D2Z",84,0,36,28,1,"",&VZA,"/");
			MOSTRA ("D2Z",120,0,36,28,1,"",&VZB,"");
			MOSTRA ("D2Z",156,0,36,28,1,"",&VZC,":");
			MOSTRA ("D2Z",192,0,24,28,1,"",&VZD,"");
			SE (VZ2==0) ENTAO (225);
			VAPARA (220);

		ESTADO 220:
			FACA {WWW=VZ1;}
			MOSTRA ("S1",0,28,240,28,1,"  Falha Bomba B-01  ",0,"");
			MOSTRA ("S2",0,28,240,28,1,"  Falha Bomba B-02  ",0,"");
			MOSTRA ("S3",0,28,240,28,1,"Temp Alta Enrol M-01",0,"");
			MOSTRA ("S4",0,28,240,28,1," SALVAR Dados no PC ",0,"");
			MOSTRA ("S5",0,28,240,28,1,"Temp Alta Enrol M-02",0,"");
			MOSTRA ("S6",0,28,240,28,1," Vibracao Alta B-01 ",0,"");
			MOSTRA ("S7",0,28,240,28,1," Vibracao Alta B-02 ",0,"");
			MOSTRA ("S8",0,28,240,28,1,"     EMERGENCIA     ",0,"");
			MOSTRA ("S9",0,28,240,28,1,"Nivel Muito Baixo RV",0,"");
			MOSTRA ("S10",0,28,240,28,1,"Press Succao Baixa  ",0,"");
			MOSTRA ("S11",0,28,240,28,1,"Press Recalque Baixa",0,"");
			MOSTRA ("S12",0,28,240,28,1,"Press Recalque Alta ",0,"");
			MOSTRA ("S13",0,28,240,28,1,"Falha Valvula XV-01 ",0,"");
			MOSTRA ("S14",0,28,240,28,1,"Temp Alta Mancal B01",0,"");
			MOSTRA ("S15",0,28,240,28,1,"Temp Alta Mancal B02",0,"");
			MOSTRA ("S16",0,28,240,28,1," Sobrecorrente M-01 ",0,"");
			MOSTRA ("S17",0,28,240,28,1," Sobrecorrente M-02 ",0,"");
			MOSTRA ("S18",0,28,240,28,1,"Sensor Fora de Faixa",0,"");
			MOSTRA ("S19",0,28,240,28,1,"Nivel Alto RV (LSH) ",0,"");
			MOSTRA ("S20",0,28,240,28,1,"     Alarme 20      ",0,"");
			SE (ATRASO>=20) ENTAO (225);

		ESTADO 225:
			SE (TLY[4]==2) ENTAO (213);		// Alarme Atual
			VAPARA (216);                 	// Bco de Alarmes
// _______

		ESTADO 230:                    	// TLY4=4
			FACA {KEY=2 E_ WWW=MPT;}
			MOSTRA ("TI",0,0,240,28,1,"    Mapa de Telas   ",0,"");
			MOSTRA ("",24,28,72,28,1,"Basico",0,"");
			MOSTRA ("",168,28,60,28,1,"Total",0,"");
			MOSTRA ("S0",12,28,12,28,1,">",0,"");
			MOSTRA ("S1",156,28,12,28,1,">",0,"");
			MOSTRA ("",0,112,240,28,1,"<OK> para selecionar",0,"");
			SE (TENT==1) ENTAO (231);
			SE (TENT==1 && SNH==0) ENTAO (22);

		ESTADO 231:
			FACA {MPT=(MPT==1?0:1);}
			SE (TENT==0) ENTAO (230);
// _______

		ESTADO 235:                    	// TLY4=5
			FACA {VZ1=DSF/100 E_ VZ2=DSF%100 E_ VZ3=DTFMW/100 E_ VZ4=DTFMW%100;}
			MOSTRA ("TI",0,0,108,28,1,"Software ", 0,"");
			MOSTRA ("CI",108,0,132,28,1,"", (uint32_t *)NOMESOFT,"");
			MOSTRA ("",0,28,84,28,1,"Versao ", 0,"");
			MOSTRA ("C",84,28,84,28,1,"", (uint32_t *)NOMEVSOFT,"");
			MOSTRA ("D2Z",168,28,36,28,1,"", &VZ1,"/");
			MOSTRA ("D2Z",204,28,24,28,1,"", &VZ2,"");
			MOSTRA ("I",0,84,240,28,1,"Sistema Operacional", 0,"");
			MOSTRA ("",0,112,84,28,1,"Versao ", 0,"");
			MOSTRA ("C",84,112,84,28,1,"", (uint32_t *)NOMEVFMW,"");
			MOSTRA ("D2Z",168,112,36,28,1,"", &VZ3,"/");
			MOSTRA ("D2Z",204,112,24,28,1,"", &VZ4,"");
			SE (TKL==1515 && TN0==1) ENTAO (236);           // Tecla 'A' por 15 decimos de segundo + Tecla 0

		ESTADO 236:
			MOSTRA ("T",24,0,192,28,1,"ATIVAR SIMULADOR",0,"");
			MOSTRA ("",90,28,60,28,1,"",&SMLX,"");
			SE (TKL==1707 && MNT==4321) ENTAO(91);			// Tecla 'C'  por 7 decimos de segundo - Atalho para Programar Memoria
			SE (TKL==2307) ENTAO(237);           			// Tecla 'OK' por 7 decimos de segundo

		ESTADO 237:		// Parar Simulador + Mostrar Simulador Ativado
			FACA {SMLX=(SMLX==0?13579:0);}
			RESETA (2,&TST,&TLA);
			SE (EST126==100) ENTAO (245);

		ESTADO 245:
			FACA {E126(50);}
			VAPARA (26);

// _________________________ Manutencao __________________________				XYZ

		ESTADO 238:                    	// TLY4=6
 			FACA {TLA=4 E_ TLY[4]=6 E_ KEY=2;}
			MOSTRA ("TI",0,0,240,28,1,"     Manutencao     ",0,"");
			MOSTRA ("",108,28,60,28,1,"",&MNT,"");

			MOSTRA ("",0,112,240,28,1,"Partidas:",&W90,"x");
			SE (MNT==1 && ZTLA!=0) ENTAO (239);

		ESTADO 239:
			FACA {TLA=ZTLA E_ TLY[TLA]=ZTLY;}
			RESETA (2,&ZTLA,&ZTLY);
			VAPARA (20);
// _______

// 12345678901234567890
//  ENTRADAS DIGITAIS
// 01:11     05:11
// 02:11     06:11
// 03:11     07:11
// 04:11     08:11

		ESTADO 241:						// TLY4=7
			FACA {KEY=(SMLX==13579?0:1);}
			MOSTRA ("TI",0,0,240,28,1," ENTRADAS DIGITAIS  ",0,"");
			MOSTRA ("D1",0,28,48,28,1,"01:",&ED1,"");
			MOSTRA ("D1",48,28,12,28,1,"",&EDG1,"");
			MOSTRA ("D1",0,56,48,28,1,"02:",&ED2,"");
			MOSTRA ("D1",48,56,12,28,1,"",&EDG2,"");
			MOSTRA ("D1",0,84,48,28,1,"03:",&ED3,"");
			MOSTRA ("D1",48,84,12,28,1,"",&EDG3,"");
			MOSTRA ("D1",0,112,48,28,1,"04:",&ED4,"");
			MOSTRA ("D1",48,112,12,28,1,"",&EDG4,"");
			MOSTRA ("D1",120,28,48,28,1,"05:",&ED5,"");
			MOSTRA ("D1",168,28,12,28,1,"",&EDG5,"");
			MOSTRA ("D1",120,56,48,28,1,"06:",&ED6,"");
			MOSTRA ("D1",168,56,12,28,1,"",&EDG6,"");
			MOSTRA ("D1",120,84,48,28,1,"07:",&ED7,"");
			MOSTRA ("D1",168,84,12,28,1,"",&EDG7,"");
			MOSTRA ("D1",120,112,48,28,1,"08:",&ED8,"");
			MOSTRA ("D1",168,112,12,28,1,"",&EDG8,"");
			FIMCOMANDO();
// _______

// 12345678901234567890
//   SAIDAS DIGITAIS
// 01:1      05:1
// 02:1      06:1
// 03:1      07:1
// 04:1      08:1

		ESTADO 242:						// TLY4=8
			MOSTRA ("TI",0,0,240,28,1,"  SAIDAS DIGITAIS   ",0,"");
			MOSTRA ("D1",0,28,48,28,1,"01:",&SDG1,"");
			MOSTRA ("D1",0,56,48,28,1,"02:",&SDG2,"");
			MOSTRA ("D1",0,84,48,28,1,"03:",&SDG3,"");
			MOSTRA ("D1",0,112,48,28,1,"04:",&SDG4,"");
			MOSTRA ("D1",120,28,48,28,1,"05:",&SDG5,"");
			MOSTRA ("D1",120,56,48,28,1,"06:",&SDG6,"");
			MOSTRA ("D1",120,84,48,28,1,"07:",&SDG7,"");
			MOSTRA ("D1",120,112,48,28,1,"08:",&SDG8,"");
			FIMCOMANDO();
// _______

// 123456789012345678901234
// ENTRADAS ANALOGICAS
// 21:1234 22:1234 23:1234
// 24:1234 33:1234 34:1234
// 35:1234 36:1234 45:1234
// 46:1234 47:1234 48:1234
// 13:1234 14:1234 15:1234
// 16:1234 17:1234 18:1234

		ESTADO 243:                    	// TLY4=9
			FACA {KEY=(SMLX==13579?0:1);}
			MOSTRA ("TI",0,0,240,22,4,"  ENTRADAS ANALOGICAS   ",0,"");
			MOSTRA ("D4",0,22,70,22,4,"21:",&EA21,"");
			MOSTRA ("D4",80,22,70,22,4,"22:",&EA22,"");
			MOSTRA ("D4",160,22,70,22,4,"23:",&EA23,"");
			MOSTRA ("D4",0,44,70,22,4,"24:",&EA24,"");
			MOSTRA ("D4",80,44,70,22,4,"33:",&EA33,"");
			MOSTRA ("D4",160,44,70,22,4,"34:",&EA34,"");
			MOSTRA ("D4",0,66,70,22,4,"35:",&EA35,"");
			MOSTRA ("D4",80,66,70,22,4,"36:",&EA36,"");
			MOSTRA ("D4",160,66,70,22,4,"45:",&EA45,"");
			MOSTRA ("D4",0,88,70,22,4,"46:",&EA46,"");
			MOSTRA ("D4",80,88,70,22,4,"47:",&EA47,"");
			MOSTRA ("D4",160,88,70,22,4,"48:",&EA48,"");
			MOSTRA ("D4",0,110,70,22,4,"13:",&EA13,"");
			MOSTRA ("D4",80,110,70,22,4,"14:",&EA14,"");
			MOSTRA ("D4",160,110,70,22,4,"15:",&EA15,"");
			MOSTRA ("D4",0,132,70,22,4,"16:",&EA16,"");
			MOSTRA ("D4",80,132,70,22,4,"17:",&EA17,"");
			MOSTRA ("D4",160,132,70,22,4,"18:",&EA18,"");
			FIMCOMANDO();
// _______

// 12345678901234567890
//  SAIDAS ANALOGICAS
// SA5: 3493 SA6:    0
// SAN5:3493 SAN6:   0
// Ref Vel:  85.3%

		ESTADO 244:                    	// TLY4=10
			MOSTRA ("TI",0,0,240,28,1," SAIDAS ANALOGICAS  ",0,"");
			MOSTRA ("FD5",0,28,108,28,1,"SA5:",&SA5,"");
			MOSTRA ("FD5",120,28,120,28,1,"SA6:",&SA6,"");
			MOSTRA ("FD4",0,56,108,28,1,"SAN5:",&SAN5,"");
			MOSTRA ("FD4",120,56,120,28,1,"SAN6:",&SAN6,"");
			MOSTRA ("FD4P1",0,84,240,28,1,"Ref Vel:  ",&SRF,"%");
			FIMCOMANDO();

// ===============================  CONFIGURACAO  ==============================

		ESTADO 250:
//									    12345678901234567890
			MOSTRA ("TI",0,56,240,28,1," F5 - Configuracao  ",0,"");
			FACA {TLA=5;}
			SE (TF5==0) ENTAO (251);

		ESTADO 251:
			FACA {TLA=5 E_ KEY=3;}
			SE (TLY[5]==2) ENTAO (290);
			SE (TLY[5]==3) ENTAO (295);
			SE (MPT==0) ENTAO (252);
			VAPARA (255);

		ESTADO 252:                             // Telas Ocultas
			MOSTRA ("T",0,0,240,28,1,"       _  _  _      ",0,"");
			FIMCOMANDO();
// _______

// 12345678901234567890
//  CONFIG. SIMULACAO
// Hora Inicial:99h
// Ruido: 1000 permil
// Semente:12345
// Eventos:  123

		ESTADO 255:						// TLY5=1
			SETA (1,&TLY[5]);
			MOSTRA ("TI",0,0,240,28,1," CONFIG. SIMULACAO  ",0,"");
			MOSTRA ("D2",0,28,240,28,1,"Hora Inicial:",&HINI,"h");
			MOSTRA ("D4",0,56,240,28,1,"Ruido:",&SNG," permil");
			MOSTRA ("D5",0,84,240,28,1,"Semente:",&SEED,"");
			MOSTRA ("FD5",0,112,240,28,1,"Eventos:",&GEV,"");
			FIMCOMANDO();
// _______

		ESTADO 290:						// TLY5=3
			MOSTRA ("TI",0,0,240,28,1,"   CANAIS SERIAIS   ",0,"");
			MOSTRA ("D2Z",0,28,180,28,1,"Canal 232_1:",&CNL1,"");
			MOSTRA ("D2Z",0,56,180,28,1,"Canal 485_1:",&CNL2,"");
			MOSTRA ("D2Z",0,84,180,28,1,"Canal 485_2:",&CNL3,"");
			MOSTRA ("",24,132,192,28,1,"'A' -> Programar",0,"");
            SE (TT1 && EST126==100) ENTAO ( 291);

        ESTADO 291:
            FACA{ E126(30);}
            SE (!TT1) ENTAO (26);
// _______

		ESTADO 295:						// TLY5=4
			MOSTRA ("TI",0,0,240,28,1,"   CANAIS ETHERNET  ",0,"");
			MOSTRA ("",0,28,36,28,1,"IP:",0,"");
			MOSTRA ("D3Z",36,28,48,28,1,"",&IPET0,".");
			MOSTRA ("D3Z",84,28,48,28,1,"",&IPET1,".");
			MOSTRA ("D3Z",132,28,48,28,1,"",&IPET2,".");
			MOSTRA ("D3Z",180,28,48,28,1,"",&IPET3,"");
			MOSTRA ("",0,56,36,28,1,"MK:",0,"");
			MOSTRA ("D3Z",36,56,48,28,1,"",&MSKET0,".");
			MOSTRA ("D3Z",84,56,48,28,1,"",&MSKET1,".");
			MOSTRA ("D3Z",132,56,48,28,1,"",&MSKET2,".");
			MOSTRA ("D3Z",180,56,48,28,1,"",&MSKET3,"");
			MOSTRA ("",0,84,36,56,1,"GT:",0,"");
			MOSTRA ("D3Z",36,84,48,28,1,"",&GTET0,".");
			MOSTRA ("D3Z",84,84,48,28,1,"",&GTET1,".");
			MOSTRA ("D3Z",132,84,48,28,1,"",&GTET2,".");
			MOSTRA ("D3Z",180,84,48,28,1,"",&GTET3,"");
			MOSTRA ("D5",0,112,180,16,0,"PORT:",&PORTETA,"");
			MOSTRA ("",24,132,192,28,1,"'A' -> Programar",0,"");
            SE (TT1 && EST126==100) ENTAO ( 296);

        ESTADO 296:
            FACA{ E126(20);}
            SE (!TT1) ENTAO (26);
	}

// ################################## SIMULADOR #################################
//  Passo da planta: 0,2 s (DC1). Unidades: vazao 0,1 m3/h; pressao mbar; rotacao permil;
//  temperaturas internas 1e-5 'C. Toda subtracao e protegida (uint32).

MAQUINA_26		// (ZA) SIMULADOR - Planta: Reservatorio, XV-01, Bombas/Inversores, Rede, Motores
	SE (RSTP==1) ENTAO (2);						// Condicoes iniciais (inicio de cenario)
	SE (TST==0 || SMLX!=13579) ENTAO (0);		// Simulador Desativado

	ESTADOS
	{
		ESTADO 0:
			SE (ATRASO>=20) ENTAO (1);

		ESTADO 1:
			RESETA (1,&DC1);
			SE (PIN==1) ENTAO (5);
			VAPARA (2);

		ESTADO 2:		// Condicoes iniciais: reservatorio a 60 %, bombas paradas, temperaturas = ambiente
			RESETA (2,&RSTP,&ED2);
			SETA (3,&ED1,&ED3,&ED12);
			FACA {PIN=1 E_ VOL=30000000 E_ LVT=6000 E_ XVP=0 E_ QIN=0 E_ Q1=0 E_ Q2=0 E_ QOUT=0 E_ QLK=0
				 E_ NS1=0 E_ NS2=0 E_ HZ1=0 E_ HZ2=0 E_ VFF1=0 E_ VFF2=0 E_ PR1=0 E_ PR2=0 E_ PC1=0 E_ PC2=0
				 E_ IM1=0 E_ IM2=0 E_ PW1=0 E_ PW2=0 E_ CAV=0 E_ DPO=0
				 E_ RNA=(RNA==0?CEN*2654435761+CIC+1:RNA)
				 E_ TOD=(HINI<24?HINI*3600:RHOR*3600+RMIN*60+RSEC) E_ TOD=(TOD>=86400?0:TOD) E_ TDK=0 E_ TKC=0
				 E_ DRW=10000 E_ SUF=10000 E_ ZA0=TOD/3600 E_ DMA=DPT[ZA0] E_ DMA=(DMA<50?500:DMA) E_ DMF=DMA
				 E_ TAM=220 E_ TFL=2250000 E_ TW1=2200000 E_ TW2=2200000 E_ TB1=2225000 E_ TB2=2225000
				 E_ PSA=1403 E_ PNA=2513 E_ PDG=1500;}
			VAPARA (5);

		ESTADO 5:		// Aguarda o passo de 0,2 s (TST=2 congela)
			SE (DC1>=2 && TST==1) ENTAO (10);

// _______ Relogio, consumo e severidades das falhas

		ESTADO 10:
			FACA {DC1=DC1-2 E_ DC1=(DC1>10?0:DC1)
				 E_ TDK=TDK+1 E_ TKC=(TKC>=1799?0:TKC+1)
				 E_ ZA9=(TDK%5==0?1:0) E_ TOD=TOD+ZA9 E_ TOD=(TOD>=86400?0:TOD)
				 E_ RNA=RNA*1664525+1013904223 E_ ZA1=RNA>>24 E_ ZA2=(RNA>>16)&255
				 E_ ZA0=TOD/3600 E_ ZAB=DPT[ZA0] E_ ZA0=ZA0+1 E_ ZAC=DPT[ZA0] E_ ZA0=TOD%3600
				 E_ DPF=(ZAC>=ZAB?ZAB+(ZAC-ZAB)*ZA0/3600:ZAB-(ZAB-ZAC)*ZA0/3600)
				 E_ DRW=(ZA9==1?DRW+ZA1-128:DRW)
				 E_ DRW=(ZA9==1?(DRW>10000?DRW-(DRW-10000)/300:DRW+(10000-DRW)/300):DRW)
				 E_ DRW=(DRW<7000?7000:(DRW>13000?13000:DRW))
				 E_ DMT=(DOV>0?DOV:DPF*DRW/10000)
				 E_ DMA=(DMT>DMA?DMA+(DMT-DMA+99)/100:DMA-(DMA-DMT+99)/100)
				 E_ ZAD=(TKC>900?TKC-900:900-TKC) E_ ZAE=FSV[12]/4
				 E_ ZAF=1000-ZAE+2*ZAE*ZAD/900
				 E_ DMF=DMA*ZAF/1000 E_ DMF=(DMF<50?50:(DMF>1000?1000:DMF))
				 E_ FSV[1]=0 E_ FSV[2]=0 E_ FSV[3]=0 E_ FSV[4]=0 E_ FSV[5]=0 E_ FSV[6]=0 E_ FSV[7]=0 E_ FSV[8]=0
				 E_ FSV[9]=0 E_ FSV[10]=0 E_ FSV[11]=0 E_ FSV[12]=0 E_ FSV[14]=0 E_ FSV[17]=0 E_ FSV[18]=0
				 E_ FSV[21]=0 E_ FSV[22]=0 E_ FSV[23]=0
				 E_ FSV[FIA]=(SVA>FSV[FIA]?SVA:FSV[FIA]) E_ FSV[FIB]=(SVB>FSV[FIB]?SVB:FSV[FIB])
				 E_ FSV[FIC]=(SVC>FSV[FIC]?SVC:FSV[FIC]) E_ FSV[0]=0;}
			VAPARA (11);

// _______ XV-01, entrada, vazamento e balanco de volume (1 m3/h = 55,56 mL por passo)

		ESTADO 11:		// Falha 11: abertura limitada a 1000-SV (fim de curso "aberta" nunca e atingido)
			FACA {ZA3=1000-FSV[11]
				 E_ XVP=(SD4==1?(XVP+20>ZA3?(XVP>ZA3?XVP:ZA3):XVP+20):(XVP>20?XVP-20:0))
				 E_ SUF=(ZA9==1?SUF+ZA2/4-32:SUF)
				 E_ SUF=(ZA9==1?(SUF>10000?SUF-(SUF-10000)/600:SUF+(10000-SUF)/600):SUF)
				 E_ SUF=(SUF<8500?8500:(SUF>11500?11500:SUF))
				 E_ QIN=900*XVP/1000*SUF/10000
				 E_ QLK=FSV[7]*(LVT/100+20)/800
				 E_ ZA4=QIN*5556/1000 E_ ZA5=(QOUT+QLK)*5556/1000
				 E_ VOL=VOL+ZA4 E_ VOL=(VOL>ZA5?VOL-ZA5:0) E_ VOL=(VOL>52000000?52000000:VOL)
				 E_ LVT=VOL/5000;}
			VAPARA (12);

// _______ Inversores: rampa 100 permil/s; falha ou emergencia = parada por inercia

		ESTADO 12:
			FACA {VFF1=(FSV[1]>0 || IM1>700?1:VFF1)
				 E_ VFF1=(SD5==1 && PR1==0 && FSV[1]==0 && IM1<=700?0:VFF1) E_ PR1=SD5
				 E_ ZA3=(SD2==1 && VFF1==0 && ED3==1?SA5*1000/4095:0)
				 E_ NS1=(VFF1==1 || ED3==0?(NS1>NS1/15+1?NS1-NS1/15-1:0):(ZA3>NS1+20?NS1+20:(NS1>ZA3+20?NS1-20:ZA3)))
				 E_ HZ1=NS1/2
				 E_ VFF2=(FSV[14]>0 || IM2>700?1:VFF2)
				 E_ VFF2=(SD6==1 && PR2==0 && FSV[14]==0 && IM2<=700?0:VFF2) E_ PR2=SD6
				 E_ ZA3=(SD3==1 && VFF2==0 && ED3==1?SA6*1000/4095:0)
				 E_ NS2=(VFF2==1 || ED3==0?(NS2>NS2/15+1?NS2-NS2/15-1:0):(ZA3>NS2+20?NS2+20:(NS2>ZA3+20?NS2-20:ZA3)))
				 E_ HZ2=NS2/2;}
			VAPARA (13);

// _______ Hidraulica: succao, rede (estatica 1,5 bar + consumidores), obstrucao, curvas das bombas

		ESTADO 13:
			FACA {ZA3=QOUT*QOUT/1000
				 E_ ZA4=150*(1000+FSV[3]*15)/1000 E_ ZA5=ZA3*ZA4/1000 E_ ZA6=1163+LVT*400/10000
				 E_ PSA=(ZA6>ZA5+60?ZA6-ZA5:60)
				 E_ CAV=(PSA<450?(450-PSA)*1000/350:0) E_ CAV=(CAV>1000?1000:CAV)
				 E_ CAV=(LVT<300 && (300-LVT)*1000/300>CAV?(300-LVT)*1000/300:CAV)
				 E_ ZA8=DMF+FSV[6]/4 E_ RCO=(ZA8>0?2600000/ZA8*1000/ZA8:1000000) E_ RTO=400+RCO
				 E_ PNA=2513+ZA3*(RTO/10)/100
				 E_ ZAA=FSV[2]*4 E_ DPO=ZA3*ZAA/1000
				 E_ PDG=(PNA>1013?PNA-1013:0);}
			VAPARA (131);

		ESTADO 131:		// Vazao de cada bomba converge para o equilibrio (Newton amortecido; retencao: q >= 0)
			FACA {ZAB=8000*(1000-FSV[4]*3/10)/1000 E_ ZAB=ZAB*NS1/1000*NS1/1000
				 E_ ZAC=Q1*Q1*6/1000 E_ HP1=(ZAB>ZAC?ZAB-ZAC:0) E_ HP1=HP1*(1000-CAV*6/10)/1000
				 E_ ZA4=PSA+HP1 E_ ZA5=PNA+DPO
				 E_ ZA6=(RTO/10+ZAA/10)*2*QOUT/100000+12*Q1/1000+5
				 E_ ZA7=(ZA4>ZA5?ZA4-ZA5:ZA5-ZA4)
				 E_ ZAD=(ZA7*5/100<(ZA7+ZA6)/(2*ZA6)?ZA7*5/100:(ZA7+ZA6)/(2*ZA6))
				 E_ Q1=(ZA4>ZA5?Q1+ZAD:(Q1>ZAD?Q1-ZAD:0)) E_ Q1=(Q1>1200?1200:Q1)
				 E_ ZAB=8000*(1000-FSV[17]*3/10)/1000 E_ ZAB=ZAB*NS2/1000*NS2/1000
				 E_ ZAC=Q2*Q2*6/1000 E_ HP2=(ZAB>ZAC?ZAB-ZAC:0) E_ HP2=HP2*(1000-CAV*6/10)/1000
				 E_ ZA4=PSA+HP2 E_ ZA5=PNA+DPO
				 E_ ZA6=(RTO/10+ZAA/10)*2*QOUT/100000+12*Q2/1000+5
				 E_ ZA7=(ZA4>ZA5?ZA4-ZA5:ZA5-ZA4)
				 E_ ZAD=(ZA7*5/100<(ZA7+ZA6)/(2*ZA6)?ZA7*5/100:(ZA7+ZA6)/(2*ZA6))
				 E_ Q2=(ZA4>ZA5?Q2+ZAD:(Q2>ZAD?Q2-ZAD:0)) E_ Q2=(Q2>1200?1200:Q2)
				 E_ QOUT=Q1+Q2 E_ QOUT=(QOUT>1800?1800:QOUT);}
			VAPARA (14);

// _______ Rendimento (BEP em 50 m3/h a 100 %), potencia no eixo, corrente (magnetizacao 11 A)

		ESTADO 14:
			FACA {ZAB=500*NS1/1000+10
				 E_ ZAC=(Q1>ZAB?(Q1-ZAB)*1000/ZAB:(ZAB-Q1)*1000/ZAB) E_ ZAC=(ZAC>1000?1000:ZAC)
				 E_ ET1=(780-FSV[4]/5)*(1000-ZAC*ZAC/1500)/1000 E_ ET1=(ET1<150?150:ET1)
				 E_ PH1=Q1*HP1/360
				 E_ PW1=(NS1>0 && VFF1==0 && ED3==1?PH1*1000/ET1+400*NS1/1000+FSV[5]*6*NS1/1000:0)
				 E_ ZAE=(NS1>50?PW1*1000/NS1/56:PW1*20/56)
				 E_ ZAD=(ZAE>110?ZAE+12100/(2*ZAE):110+ZAE*ZAE/220)
				 E_ RNA=RNA*1664525+1013904223 E_ ZA1=RNA>>24
				 E_ ZAD=ZAD+ZA1*CAV*60/255000 E_ ZAF=CAV*30/1000 E_ ZAD=(ZAD>ZAF?ZAD-ZAF:0)
				 E_ IM1=(PW1>0?ZAD:0)
				 E_ ZAB=500*NS2/1000+10
				 E_ ZAC=(Q2>ZAB?(Q2-ZAB)*1000/ZAB:(ZAB-Q2)*1000/ZAB) E_ ZAC=(ZAC>1000?1000:ZAC)
				 E_ ET2=(780-FSV[17]/5)*(1000-ZAC*ZAC/1500)/1000 E_ ET2=(ET2<150?150:ET2)
				 E_ PH2=Q2*HP2/360
				 E_ PW2=(NS2>0 && VFF2==0 && ED3==1?PH2*1000/ET2+400*NS2/1000+FSV[18]*6*NS2/1000:0)
				 E_ ZAE=(NS2>50?PW2*1000/NS2/56:PW2*20/56)
				 E_ ZAD=(ZAE>110?ZAE+12100/(2*ZAE):110+ZAE*ZAE/220)
				 E_ RNA=RNA*1664525+1013904223 E_ ZA1=RNA>>24
				 E_ ZAD=ZAD+ZA1*CAV*60/255000 E_ ZAF=CAV*30/1000 E_ ZAD=(ZAD>ZAF?ZAD-ZAF:0)
				 E_ IM2=(PW2>0?ZAD:0)
				 E_ JTP=(PW1+PW2)*100/92/100+3;}
			VAPARA (15);

// _______ Termica: ambiente (min 05h / max 15h), agua, enrolamentos (I2 / ventilacao), mancais

		ESTADO 15:
			FACA {ZA0=(TOD>=18000 && TOD<54000?(TOD-18000)/36:(TOD>=54000?(TOD-54000)*10/504:(TOD+32400)*10/504))
				 E_ ZA0=(ZA0>1000?1000:ZA0) E_ ZA7=(3*ZA0*ZA0-2*ZA0*ZA0/1000*ZA0)/1000
				 E_ ZA7=(TOD>=18000 && TOD<54000?ZA7:1000-ZA7)
				 E_ TAM=180+130*ZA7/1000
				 E_ ZAF=220+15*ZA7/1000 E_ ZAF=(8*ZAF+2*TAM)*1000
				 E_ TFL=(ZAF>TFL?TFL+(ZAF-TFL)/36000:TFL-(TFL-ZAF)/36000)
				 E_ ZAB=(300+NS1*7/10)*(1000-FSV[8]*8/10)/1000 E_ ZAB=(ZAB<100?100:ZAB)
				 E_ ZAC=IM1*1000/345 E_ ZAD=TAM*10000+80*ZAC*ZAC/ZAB*100 E_ ZAE=(NS1>0?4500:13500)
				 E_ TW1=(ZAD>TW1?TW1+(ZAD-TW1)/ZAE:TW1-(TW1-ZAD)/ZAE)
				 E_ PC1=(TW1>=16000000?1:(TW1<=14000000?0:PC1))
				 E_ ZAD=(TAM*10000+TFL)/2+1500*NS1/1000*NS1+(FSV[9]*6500+FSV[10]*800+FSV[5]*1000)/1000*NS1
				 E_ ZAE=(NS1>0?3000:9000)
				 E_ TB1=(ZAD>TB1?TB1+(ZAD-TB1)/ZAE:TB1-(TB1-ZAD)/ZAE)
				 E_ ZAB=(300+NS2*7/10)*(1000-FSV[21]*8/10)/1000 E_ ZAB=(ZAB<100?100:ZAB)
				 E_ ZAC=IM2*1000/345 E_ ZAD=TAM*10000+80*ZAC*ZAC/ZAB*100 E_ ZAE=(NS2>0?4500:13500)
				 E_ TW2=(ZAD>TW2?TW2+(ZAD-TW2)/ZAE:TW2-(TW2-ZAD)/ZAE)
				 E_ PC2=(TW2>=16000000?1:(TW2<=14000000?0:PC2))
				 E_ ZAD=(TAM*10000+TFL)/2+1500*NS2/1000*NS2+(FSV[22]*6500+FSV[23]*800+FSV[18]*1000)/1000*NS2
				 E_ ZAE=(NS2>0?3000:9000)
				 E_ TB2=(ZAD>TB2?TB2+(ZAD-TB2)/ZAE:TB2-(TB2-ZAD)/ZAE);}
			VAPARA (16);

// _______ Vibracao (mm/s x100): rotacao, afastamento do BEP, cavitacao, desbalanceamento, mancal

		ESTADO 16:
			FACA {ZAB=NS1*NS1/1000 E_ ZA3=500*NS1/1000+10
				 E_ ZAC=(Q1>ZA3?(Q1-ZA3)*1000/ZA3:(ZA3-Q1)*1000/ZA3) E_ ZAC=(ZAC>1000?1000:ZAC)
				 E_ VB1=(NS1>0?40+150*ZAB/1000+100*ZAC/1000+CAV*350/1000+FSV[10]*7/10*ZAB/1000+(FSV[9]>500?(FSV[9]-500)*2:0)*4/10+FSV[5]*8/100:20+NS2*15/1000)
				 E_ RNA=RNA*1664525+1013904223 E_ ZA1=RNA>>24 E_ VB1=VB1*(950+ZA1*100/255)/1000
				 E_ ZAB=NS2*NS2/1000 E_ ZA3=500*NS2/1000+10
				 E_ ZAC=(Q2>ZA3?(Q2-ZA3)*1000/ZA3:(ZA3-Q2)*1000/ZA3) E_ ZAC=(ZAC>1000?1000:ZAC)
				 E_ VB2=(NS2>0?40+150*ZAB/1000+100*ZAC/1000+CAV*350/1000+FSV[23]*7/10*ZAB/1000+(FSV[22]>500?(FSV[22]-500)*2:0)*4/10+FSV[18]*8/100:20+NS1*15/1000)
				 E_ RNA=RNA*1664525+1013904223 E_ ZA1=RNA>>24 E_ VB2=VB2*(950+ZA1*100/255)/1000;}
			VAPARA (17);

// _______ Sinais de campo (ED) e valores verdadeiros dos transmissores (STV, unidade de engenharia)

		ESTADO 17:
			FACA {ED4=(VFF1==0 && (SD2==1 || NS1>0)?1:0) E_ ED5=VFF1 E_ ED6=PC1
				 E_ ED7=(VFF2==0 && (SD3==1 || NS2>0)?1:0) E_ ED8=VFF2 E_ ED9=PC2
				 E_ ED10=(XVP>=980?1:0) E_ ED11=(XVP<=20?1:0) E_ ED12=(LVT>500?1:0) E_ ED13=(LVT>9500?1:0)
				 E_ STV[1]=LVT/10 E_ STV[2]=PSA E_ STV[3]=PDG E_ STV[4]=QOUT E_ STV[5]=QIN E_ STV[6]=IM1
				 E_ STV[7]=IM2 E_ STV[8]=VB1 E_ STV[9]=VB2 E_ STV[10]=HZ1 E_ STV[11]=HZ2 E_ STV[12]=JTP
				 E_ STV[13]=TW1/10000 E_ STV[14]=TW2/10000 E_ STV[15]=TB1/10000 E_ STV[16]=TB2/10000
				 E_ STV[17]=TFL/10000 E_ STV[18]=TAM E_ ZTK=1;}
			VAPARA (5);
	}

// _____________________

MAQUINA_27		// (ZB) SIMULADOR - Transmissores: Ruido, Quantizacao 12 bits, Multiplex PT100 e Falhas de Sensor
	SE (TST==0 || SMLX!=13579) ENTAO (0);		// Simulador Desativado

	ESTADOS
	{
		ESTADO 0:
			SE (ZTK==1) ENTAO (5);

		ESTADO 5:		// Um PT100 (13...18) renovado a cada 4 passos (0,8 s)
			FACA {ZTK=0 E_ ZB1=1 E_ PTK=PTK+1 E_ PTK=(PTK>=4?0:PTK)
				 E_ PTN=(PTK==0?(PTN>=18 || PTN<13?13:PTN+1):PTN);}
			VAPARA (10);

		ESTADO 10:		// Canal k = ZB1. Falha ativa no canal: ZB2 codigo / ZB3 severidade / ZB4 sentido (1=negativo)
			FACA {ZB2=0 E_ ZB3=0 E_ ZB4=0
				 E_ ZB2=(SCA==ZB1 && SVA>0?GCA:ZB2) E_ ZB3=(SCA==ZB1 && SVA>0?SVA:ZB3) E_ ZB4=(SCA==ZB1 && SVA>0?SDA:ZB4)
				 E_ ZB2=(SCB==ZB1 && SVB>0?GCB:ZB2) E_ ZB3=(SCB==ZB1 && SVB>0?SVB:ZB3) E_ ZB4=(SCB==ZB1 && SVB>0?SDB:ZB4)
				 E_ ZB2=(SCC==ZB1 && SVC>0?GCC:ZB2) E_ ZB3=(SCC==ZB1 && SVC>0?SVC:ZB3) E_ ZB4=(SCC==ZB1 && SVC>0?SDC:ZB4)
				 E_ ZB5=STV[ZB1] E_ ZB6=(RHI[ZB1]>RLO[ZB1]?RHI[ZB1]-RLO[ZB1]:1)
				 E_ ZB7=(ZB2==21?ZB6*ZB3/5000:0) E_ ZB5=(ZB4==1?(ZB5>ZB7?ZB5-ZB7:0):ZB5+ZB7)
				 E_ ZB7=(ZB2==24?ZB5*ZB3*3/10000:0) E_ ZB5=(ZB4==1?(ZB5>ZB7?ZB5-ZB7:0):ZB5+ZB7)
				 E_ RNB=RNB*1664525+1013904223 E_ ZB8=(RNB>>24)+((RNB>>16)&255)
				 E_ RNB=RNB*1664525+1013904223 E_ ZB8=ZB8+(RNB>>24)+((RNB>>16)&255)
				 E_ ZB9=SNA[ZB1]*SNG/1000 E_ ZB9=(ZB2==22?ZB9*(1000+ZB3*9)/1000:ZB9)
				 E_ ZBA=ZB5*1480+ZB8*ZB9 E_ ZB7=510*ZB9+RLO[ZB1]*1480 E_ ZBA=(ZBA>ZB7?ZBA-ZB7:0)
				 E_ ZBC=((ZBA/ZB6)*4095+(ZBA%ZB6)*4095/ZB6)/1480 E_ ZBC=(ZBC>4095?4095:ZBC)
				 E_ ZBD=(ZB1>=13 && ZB1!=PTN && EAO[ZB1]>0?1:0) E_ ZBC=(ZBD==1?EAO[ZB1]:ZBC)
				 E_ SHT[ZB1]=(ZB2==25?(SFP[ZB1]!=25?0:SHT[ZB1]+1):0)
				 E_ ZBE=(ZB2==25 && SHT[ZB1]<600+ZB3*9?1:0) E_ SHT[ZB1]=(ZB2==25 && ZBE==0?0:SHT[ZB1])
				 E_ ZBC=(ZBE==1 && EAO[ZB1]>0?EAO[ZB1]:ZBC)
				 E_ SFZ[ZB1]=(ZB2==20 && SFP[ZB1]!=20?EAO[ZB1]:SFZ[ZB1]) E_ ZBC=(ZB2==20?SFZ[ZB1]:ZBC)
				 E_ RNB=RNB*1664525+1013904223 E_ ZBF=(RNB>>16)%1000
				 E_ RNB=RNB*1664525+1013904223 E_ ZBG=(RNB>>8)%60000
				 E_ ZBH=(ZB2==23 && SDO[ZB1]==0 && ZBG*25<ZB3?1:0) E_ SDV[ZB1]=(ZBH==1?((RNB>>4)&1)*4095:SDV[ZB1])
				 E_ SDO[ZB1]=(ZBH==1?100+ZBF/2:(SDO[ZB1]>0?SDO[ZB1]-1:0)) E_ SDO[ZB1]=(ZB2==23?SDO[ZB1]:0)
				 E_ ZBC=(ZB2==23 && SDO[ZB1]>0?SDV[ZB1]:ZBC)
				 E_ ZBC=(ZB2==26?(ZB1>=13?4095:0):ZBC)
				 E_ ZBC=(ZB2==27?(ZB4==1?0:4095):ZBC)
				 E_ EAO[ZB1]=ZBC E_ SFP[ZB1]=ZB2 E_ ZB1=ZB1+1;}
			SE (ZB1>18) ENTAO (20);
			VAPARA (10);

		ESTADO 20:		// Imagem das entradas analogicas
			FACA {EA21=EAO[1] E_ EA22=EAO[2] E_ EA23=EAO[3] E_ EA24=EAO[4] E_ EA33=EAO[5] E_ EA34=EAO[6]
				 E_ EA35=EAO[7] E_ EA36=EAO[8] E_ EA45=EAO[9] E_ EA46=EAO[10] E_ EA47=EAO[11] E_ EA48=EAO[12]
				 E_ EA13=EAO[13] E_ EA14=EAO[14] E_ EA15=EAO[15] E_ EA16=EAO[16] E_ EA17=EAO[17] E_ EA18=EAO[18];}
			VAPARA (0);
	}

// _____________________
//  Slots de Falha A/B/C: fase 1 injetada / 2 rampa / 3 plena / 4 recuperacao. Tempo em s exatos (XC1...XC3).
//  Codigos por bomba (01/04/05/08/09/10) usam FSV[cod] p/ B-01 e FSV[cod+13] p/ B-02.

MAQUINA_28		// (ZC) SIMULADOR - Slot de Falha A
	SE (XGA==1) ENTAO (5);								// Injetar
	SE (XQA==1 && EST28>=10 && EST28<40) ENTAO (40);	// Recuperar
	SE (XKA==1) ENTAO (0);								// Cancelar
	SE (TST==0 || SMLX!=13579) ENTAO (0);				// Simulador Desativado

	ESTADOS
	{
		ESTADO 0:		// Livre
			RESETA (8,&GCA,&SVA,&PHA,&FIA,&SCA,&XKA,&XQA,&TGA);
			FIMCOMANDO();

		ESTADO 5:		// Injecao: copia o comando; alvo 0 = bomba principal neste instante
			RESETA (3,&XGA,&XQA,&XKA);
			FACA {GCA=XCA E_ TGA=XTA E_ INA=(XIA>1000?1000:XIA) E_ MDA=(XMA>3?0:XMA)
				 E_ RTA=XRA E_ DUA=XDA E_ RCA=XEA
				 E_ PTA=(TGA==1 || TGA==2?TGA:DTY) E_ PTA=(PTA==2?2:1)
				 E_ ZC0=(GCA==1 || GCA==4 || GCA==5 || GCA==8 || GCA==9 || GCA==10?1:0)
				 E_ TGA=(ZC0==1?PTA:TGA)
				 E_ FIA=(GCA>=1 && GCA<=12?GCA:0) E_ FIA=(ZC0==1 && PTA==2?GCA+13:FIA)
				 E_ SCA=(GCA>=20 && GCA<=27?TGA%100:0) E_ SCA=(SCA>18?0:SCA) E_ SDA=(TGA>=100?1:0)
				 E_ TEA=0 E_ TTA=0 E_ SVA=0 E_ PHA=1 E_ XC1=0 E_ IOA=1 E_ IWA=0 E_ SRA=0;}
			SE (GCA==0 || (GCA>12 && GCA<20) || (GCA>27 && GCA<30) || GCA>33) ENTAO (0);
			SE (GCA>=20 && GCA<=27 && SCA==0) ENTAO (0);
			VAPARA (20);

		ESTADO 20:		// Rampa (modo 1 linear / 2 acelerada)
			FACA {PHA=2;}
			SE (XC1>=1 && TST==1) ENTAO (21);
			SE (MDA==0 || MDA==3 || RTA==0) ENTAO (25);

		ESTADO 21:
			FACA {XC1=XC1-1 E_ XC1=(XC1>5?0:XC1) E_ TEA=TEA+1 E_ TTA=TTA+1
				 E_ ZC0=(TEA>RTA?RTA:TEA)
				 E_ SVA=(MDA==2?INA*ZC0/RTA*ZC0/RTA:INA*ZC0/RTA);}
			SE (TEA>=RTA) ENTAO (25);
			SE (DUA>0 && TTA>=DUA) ENTAO (40);
			VAPARA (20);

		ESTADO 25:		// Severidade plena
			FACA {PHA=3 E_ SVA=INA E_ TEA=0 E_ IOA=1 E_ IWA=300;}
			VAPARA (30);

		ESTADO 30:
			SE (XC1>=1 && TST==1) ENTAO (31);

		ESTADO 31:		// Modo 3 intermitente: ativa 2...15 min / inativa 3...30 min (aleatorio)
			FACA {XC1=XC1-1 E_ XC1=(XC1>5?0:XC1) E_ TTA=TTA+1 E_ TEA=TEA+1
				 E_ RND=RND*1664525+1013904223 E_ ZC1=(RND>>16)%1000
				 E_ ZC0=(MDA==3 && TEA>=IWA?1:0)
				 E_ IOA=(ZC0==1?(IOA==1?0:1):IOA)
				 E_ IWA=(ZC0==1?(IOA==1?120+ZC1*78/100:180+ZC1*162/100):IWA)
				 E_ TEA=(ZC0==1?0:TEA) E_ SVA=(IOA==1?INA:0);}
			SE (DUA>0 && TTA>=DUA) ENTAO (40);
			VAPARA (30);

		ESTADO 40:		// Fim da falha: recuperacao linear em RC s (0 = imediata)
			FACA {PHA=4 E_ RCA=(XQA==1?XEA:RCA) E_ XQA=0 E_ SRA=SVA E_ TEA=0 E_ XC1=0;}
			SE (RCA==0 || SVA==0) ENTAO (0);
			VAPARA (41);

		ESTADO 41:
			SE (XC1>=1 && TST==1) ENTAO (42);

		ESTADO 42:
			FACA {XC1=XC1-1 E_ XC1=(XC1>5?0:XC1) E_ TEA=TEA+1 E_ TTA=TTA+1
				 E_ ZC0=(TEA>RCA?RCA:TEA) E_ SVA=SRA-SRA*ZC0/RCA;}
			SE (TEA>=RCA) ENTAO (0);
			VAPARA (41);
	}

MAQUINA_29		// (ZD) SIMULADOR - Slot de Falha B
	SE (XGB==1) ENTAO (5);								// Injetar
	SE (XQB==1 && EST29>=10 && EST29<40) ENTAO (40);	// Recuperar
	SE (XKB==1) ENTAO (0);								// Cancelar
	SE (TST==0 || SMLX!=13579) ENTAO (0);				// Simulador Desativado

	ESTADOS
	{
		ESTADO 0:		// Livre
			RESETA (8,&GCB,&SVB,&PHB,&FIB,&SCB,&XKB,&XQB,&TGB);
			FIMCOMANDO();

		ESTADO 5:		// Injecao: copia o comando; alvo 0 = bomba principal neste instante
			RESETA (3,&XGB,&XQB,&XKB);
			FACA {GCB=XCB E_ TGB=XTB E_ INB=(XIB>1000?1000:XIB) E_ MDB=(XMB>3?0:XMB)
				 E_ RTB=XRB E_ DUB=XDB E_ RCB=XEB
				 E_ PTB=(TGB==1 || TGB==2?TGB:DTY) E_ PTB=(PTB==2?2:1)
				 E_ ZD0=(GCB==1 || GCB==4 || GCB==5 || GCB==8 || GCB==9 || GCB==10?1:0)
				 E_ TGB=(ZD0==1?PTB:TGB)
				 E_ FIB=(GCB>=1 && GCB<=12?GCB:0) E_ FIB=(ZD0==1 && PTB==2?GCB+13:FIB)
				 E_ SCB=(GCB>=20 && GCB<=27?TGB%100:0) E_ SCB=(SCB>18?0:SCB) E_ SDB=(TGB>=100?1:0)
				 E_ TEB=0 E_ TTB=0 E_ SVB=0 E_ PHB=1 E_ XC2=0 E_ IOB=1 E_ IWB=0 E_ SRB=0;}
			SE (GCB==0 || (GCB>12 && GCB<20) || (GCB>27 && GCB<30) || GCB>33) ENTAO (0);
			SE (GCB>=20 && GCB<=27 && SCB==0) ENTAO (0);
			VAPARA (20);

		ESTADO 20:		// Rampa (modo 1 linear / 2 acelerada)
			FACA {PHB=2;}
			SE (XC2>=1 && TST==1) ENTAO (21);
			SE (MDB==0 || MDB==3 || RTB==0) ENTAO (25);

		ESTADO 21:
			FACA {XC2=XC2-1 E_ XC2=(XC2>5?0:XC2) E_ TEB=TEB+1 E_ TTB=TTB+1
				 E_ ZD0=(TEB>RTB?RTB:TEB)
				 E_ SVB=(MDB==2?INB*ZD0/RTB*ZD0/RTB:INB*ZD0/RTB);}
			SE (TEB>=RTB) ENTAO (25);
			SE (DUB>0 && TTB>=DUB) ENTAO (40);
			VAPARA (20);

		ESTADO 25:		// Severidade plena
			FACA {PHB=3 E_ SVB=INB E_ TEB=0 E_ IOB=1 E_ IWB=300;}
			VAPARA (30);

		ESTADO 30:
			SE (XC2>=1 && TST==1) ENTAO (31);

		ESTADO 31:		// Modo 3 intermitente: ativa 2...15 min / inativa 3...30 min (aleatorio)
			FACA {XC2=XC2-1 E_ XC2=(XC2>5?0:XC2) E_ TTB=TTB+1 E_ TEB=TEB+1
				 E_ RND=RND*1664525+1013904223 E_ ZD1=(RND>>16)%1000
				 E_ ZD0=(MDB==3 && TEB>=IWB?1:0)
				 E_ IOB=(ZD0==1?(IOB==1?0:1):IOB)
				 E_ IWB=(ZD0==1?(IOB==1?120+ZD1*78/100:180+ZD1*162/100):IWB)
				 E_ TEB=(ZD0==1?0:TEB) E_ SVB=(IOB==1?INB:0);}
			SE (DUB>0 && TTB>=DUB) ENTAO (40);
			VAPARA (30);

		ESTADO 40:		// Fim da falha: recuperacao linear em RC s (0 = imediata)
			FACA {PHB=4 E_ RCB=(XQB==1?XEB:RCB) E_ XQB=0 E_ SRB=SVB E_ TEB=0 E_ XC2=0;}
			SE (RCB==0 || SVB==0) ENTAO (0);
			VAPARA (41);

		ESTADO 41:
			SE (XC2>=1 && TST==1) ENTAO (42);

		ESTADO 42:
			FACA {XC2=XC2-1 E_ XC2=(XC2>5?0:XC2) E_ TEB=TEB+1 E_ TTB=TTB+1
				 E_ ZD0=(TEB>RCB?RCB:TEB) E_ SVB=SRB-SRB*ZD0/RCB;}
			SE (TEB>=RCB) ENTAO (0);
			VAPARA (41);
	}

MAQUINA_30		// (ZE) SIMULADOR - Slot de Falha C
	SE (XGC==1) ENTAO (5);								// Injetar
	SE (XQC==1 && EST30>=10 && EST30<40) ENTAO (40);	// Recuperar
	SE (XKC==1) ENTAO (0);								// Cancelar
	SE (TST==0 || SMLX!=13579) ENTAO (0);				// Simulador Desativado

	ESTADOS
	{
		ESTADO 0:		// Livre
			RESETA (8,&GCC,&SVC,&PHC,&FIC,&SCC,&XKC,&XQC,&TGC);
			FIMCOMANDO();

		ESTADO 5:		// Injecao: copia o comando; alvo 0 = bomba principal neste instante
			RESETA (3,&XGC,&XQC,&XKC);
			FACA {GCC=XCC E_ TGC=XTC E_ INC=(XIC>1000?1000:XIC) E_ MDC=(XMC>3?0:XMC)
				 E_ RTC=XRC E_ DUC=XDC E_ RCC=XEC
				 E_ PTC=(TGC==1 || TGC==2?TGC:DTY) E_ PTC=(PTC==2?2:1)
				 E_ ZE0=(GCC==1 || GCC==4 || GCC==5 || GCC==8 || GCC==9 || GCC==10?1:0)
				 E_ TGC=(ZE0==1?PTC:TGC)
				 E_ FIC=(GCC>=1 && GCC<=12?GCC:0) E_ FIC=(ZE0==1 && PTC==2?GCC+13:FIC)
				 E_ SCC=(GCC>=20 && GCC<=27?TGC%100:0) E_ SCC=(SCC>18?0:SCC) E_ SDC=(TGC>=100?1:0)
				 E_ TEC=0 E_ TTC=0 E_ SVC=0 E_ PHC=1 E_ XC3=0 E_ IOC=1 E_ IWC=0 E_ SRC=0;}
			SE (GCC==0 || (GCC>12 && GCC<20) || (GCC>27 && GCC<30) || GCC>33) ENTAO (0);
			SE (GCC>=20 && GCC<=27 && SCC==0) ENTAO (0);
			VAPARA (20);

		ESTADO 20:		// Rampa (modo 1 linear / 2 acelerada)
			FACA {PHC=2;}
			SE (XC3>=1 && TST==1) ENTAO (21);
			SE (MDC==0 || MDC==3 || RTC==0) ENTAO (25);

		ESTADO 21:
			FACA {XC3=XC3-1 E_ XC3=(XC3>5?0:XC3) E_ TEC=TEC+1 E_ TTC=TTC+1
				 E_ ZE0=(TEC>RTC?RTC:TEC)
				 E_ SVC=(MDC==2?INC*ZE0/RTC*ZE0/RTC:INC*ZE0/RTC);}
			SE (TEC>=RTC) ENTAO (25);
			SE (DUC>0 && TTC>=DUC) ENTAO (40);
			VAPARA (20);

		ESTADO 25:		// Severidade plena
			FACA {PHC=3 E_ SVC=INC E_ TEC=0 E_ IOC=1 E_ IWC=300;}
			VAPARA (30);

		ESTADO 30:
			SE (XC3>=1 && TST==1) ENTAO (31);

		ESTADO 31:		// Modo 3 intermitente: ativa 2...15 min / inativa 3...30 min (aleatorio)
			FACA {XC3=XC3-1 E_ XC3=(XC3>5?0:XC3) E_ TTC=TTC+1 E_ TEC=TEC+1
				 E_ RND=RND*1664525+1013904223 E_ ZE1=(RND>>16)%1000
				 E_ ZE0=(MDC==3 && TEC>=IWC?1:0)
				 E_ IOC=(ZE0==1?(IOC==1?0:1):IOC)
				 E_ IWC=(ZE0==1?(IOC==1?120+ZE1*78/100:180+ZE1*162/100):IWC)
				 E_ TEC=(ZE0==1?0:TEC) E_ SVC=(IOC==1?INC:0);}
			SE (DUC>0 && TTC>=DUC) ENTAO (40);
			VAPARA (30);

		ESTADO 40:		// Fim da falha: recuperacao linear em RC s (0 = imediata)
			FACA {PHC=4 E_ RCC=(XQC==1?XEC:RCC) E_ XQC=0 E_ SRC=SVC E_ TEC=0 E_ XC3=0;}
			SE (RCC==0 || SVC==0) ENTAO (0);
			VAPARA (41);

		ESTADO 41:
			SE (XC3>=1 && TST==1) ENTAO (42);

		ESTADO 42:
			FACA {XC3=XC3-1 E_ XC3=(XC3>5?0:XC3) E_ TEC=TEC+1 E_ TTC=TTC+1
				 E_ ZE0=(TEC>RCC?RCC:TEC) E_ SVC=SRC-SRC*ZE0/RCC;}
			SE (TEC>=RCC) ENTAO (0);
			VAPARA (41);
	}

}

//=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#=#

//===============================================================================
//
// ___________________________ LIMPEZA DOS COMENTARIOS __________________________
//			 Localizar: //.*						   * Marcar Expressao Regular
//			Substituir:
//
// _______________________________ TRECHO COMENTADO _____________________________
//			/*
//			Trecho comentado
//			*/
//
//===============================================================================
