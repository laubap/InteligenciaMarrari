# Regime Shadow Mode

Modo estritamente observacional do Benchmark Clean Fast. O buffer candidato é separado do histórico ativo e todo estado é descartado entre Fast Windows.

Todos os parâmetros de `RegimeShadowOptions` são experimentais. Os defaults formam candidato após 5 pontos/4 minutos e avaliam estabilidade após 30 minutos e 20 amostras independentes. Dispersão máxima (10% do deslocamento), drift residual máximo (5%), retorno (3 pontos), capacidade (480 pontos) e horizonte (480 minutos) também são experimentais. Eles não reutilizam thresholds dos detectores. A configuração completa é gravada no summary.

Ativação: `--benchmark-clean-fast --regime-shadow --regime-shadow-tags=<tag>`. Opções `--shadow-*` permitem comparar configurações, incluindo `formation-points`, `formation-minutes`, `displacement-deadbands`, `displacement-scales`, `stability-samples`, `stability-minutes`, `max-dispersion-ratio`, `max-slope-ratio`, `return-points`, `buffer-capacity` e `buffer-minutes`.

## Lifecycle 2D.1

O `EpisodeId` nasce na primeira borda que abre o buffer e não muda quando o buffer rotaciona. O `CandidateId` nasce quando a máquina abre uma candidatura. Rejeição, retorno ao ativo, gap crítico e reset de Fast Window descartam o buffer; pontos de um episódio encerrado nunca entram no seguinte.

## Débito A2.1 — Revisão do TagBehaviorProfile

O benchmark usa `TipoTag="perfil-comportamental"`, que não é tipo explícito reconhecido. `TEMPERATURE` não determina Continuous pelo nome; sinais contínuos periódicos/quantizados podem ter `DistinctValueRatio` baixo; a tag auditada alternou entre Unknown/0, Continuous/0,86 e Continuous/0,94. Produção pode divergir quando recebe `TipoRepresentacao` explícito. Nenhuma correção desse débito pertence à Fase 2D.1.
