# Backlog: detecção univariada (pós-auditoria inicial)

Ordem: primeiro medição e diagnóstico, depois correções. Cada tarefa é independente. **Nenhuma está aprovada**: implementar só com autorização explícita (`CLAUDE.md`). As tarefas 1 a 4 não alteram a decisão de anomalia.

---

### 1. Recuperar a tabela de cenários CP300 e os logs dos testes
- **Objetivo:** obter o handoff ausente (`docs/IAAPI_HANDOFF_CLAUDE.md`) ou a tabela de cenários (tag, início/fim, esperado/observado) e logs/exports de diagnóstico desses intervalos, com dados redigidos.
- **Arquivos:** nenhum código; atualizar a seção 4 de `Docs/AUDITORIA_STATUS.md`.
- **Risco:** baixo. Atenção a dados industriais sensíveis.
- **Esforço:** P
- **Aceite:** cada cenário com a hipótese marcada como confirmada, descartada ou sem dados.

### 2. Replay offline de cada cenário com detalhamento por detector
- **Objetivo:** reproduzir, em `tools/IntelligenceLabReplay` (modo `--full-ensemble`), séries sintéticas equivalentes aos cenários com falha e registrar por leitura: score normalizado de cada detector, aplicabilidade, tipo comportamental, deadband e score final, para medir diluição, deadband e bloqueio por tipo.
- **Arquivos:** `tools/IntelligenceLabScenarioGenerator`, `tools/IntelligenceLabReplay` (novos cenários/fixtures); testes em `IAApi.Tests/IntelligenceLab/`.
- **Risco:** baixo (offline, sintético). Não substitui dados reais.
- **Esforço:** M
- **Aceite:** para cada cenário, uma tabela que mostra qual mecanismo impede `score ≥ 60` (ou que ele é atingido), com teste automatizado reproduzível.

### 3. Relatório de "por que não detectou" a partir do PERF_DIAG existente
- **Objetivo:** script/ferramenta offline que lê logs PERF_DIAG já capturados e resume por tag: intervalo real entre leituras, idade da leitura, duração por etapa, `DetectorResults`, casos de entrada inválida e de "nenhum detector aplicável".
- **Arquivos:** nova ferramenta em `tools/` (sem tocar no runtime).
- **Risco:** baixo.
- **Esforço:** M
- **Aceite:** executado sobre um log de exemplo redigido, gera o resumo; testes com log sintético.

### 4. Gravar o timestamp original da leitura junto do registro de detecção
- **Objetivo:** hoje `AnomaliaDetectada.DataDeteccao = DateTime.Now` e a hora da leitura se perde. Adicionar um campo opcional (ex.: `DataHoraLeitura`) para medir a latência leitura → detecção no LiveMonitor.
- **Arquivos:** `Models/AnomaliaDetectada.cs`, `Services/AnomaliaHistoricoService.cs` (`Registrar`), `PredictionFinalizationService.cs`, nova migration; opcionalmente exibir em `tools/IAApi.LiveMonitor`.
- **Risco:** médio (migration de banco; contrato de resposta precisa continuar compatível).
- **Esforço:** M
- **Aceite:** campo preenchido em novos registros; decisão idêntica (teste de equivalência existente passando); migration reversível.

### 5. Inventário do baseline e do tipo comportamental das tags PK2 (somente leitura)
- **Objetivo:** listar por tag ativa: data do treino do `TagPerfilIa`, média, desvio, deadband, outliers removidos, tipo comportamental resolvido e se o baseline por regime ou comportamento é aplicado. Detecta perfis contaminados ou desatualizados e tags bloqueadas por tipo.
- **Arquivos:** consulta/endpoint GET de diagnóstico ou script offline sobre uma cópia autorizada do `ia_config.db`.
- **Risco:** médio (exige autorização para acessar o banco real).
- **Esforço:** P
- **Aceite:** tabela por tag com marcação de "baseline suspeito" (por exemplo, treino fora da janela do teste, desvio > X × desvio recente, tipo ≠ Continuous).

### 6. Medir a duração do ciclo e a cadência real por tag no ambiente de teste
- **Objetivo:** com `IAAPI_PERF_DIAGNOSTICS` ligado em uma sessão de teste autorizada, medir o ciclo por cliente, o tempo por tag (histórico de 24 h × N tags) e a frequência efetiva de amostragem. Quantifica L1–L6 do mapa.
- **Arquivos:** nenhum código (só variável de ambiente e logs).
- **Risco:** médio (execução no ambiente de teste; exige aprovação).
- **Esforço:** P
- **Aceite:** números de p50/p95 por etapa e intervalo entre leituras por tag, anexados à auditoria.

### 7. (Correção, depois de 2 e 5) Ajustar a agregação do ensemble ou os pesos com base nas evidências
- **Objetivo:** se a diluição for confirmada, propor alternativa (ex.: regra "N detectores fortes" ou recalibração de pesos/limiar), validada antes no replay.
- **Arquivos:** `Services/AnomalyDetection/EnsembleDetectionService.cs`, `appsettings.json` `AnomalyDetection`.
- **Risco:** alto (muda a decisão operacional e o volume de falsos positivos).
- **Esforço:** G
- **Aceite:** no replay, os cenários antes perdidos passam a ser detectados sem aumentar os falsos positivos nas séries limpas acima de uma meta acordada; rollback por configuração.
