using System.Text.Json;
using IAApi.Services.AnomalyDetection.Runtime;

// Serviço responsável por registrar o histórico de anomalias detectadas.
// Ele grava a anomalia principal em AnomaliasDetectadas e, se houver análise
// de tags relacionadas, também grava as dependências detectadas.
public class AnomaliaHistoricoService
{
    private readonly AppDbContext _context;

    public AnomaliaHistoricoService(AppDbContext context)
    {
        _context = context;
    }

    // Registra uma leitura analisada e opcionalmente persiste os dados das tags
    // relacionadas juntamente com a decisão.
    //
    // A persistência continua INCONDICIONAL: normais, anômalas, continuations
    // e evidências funcionais continuam fazendo parte do histórico.
    public void Registrar(
        string clienteId,
        string tagName,
        string tipoTag,
        double valor,
        double score,
        bool ehAnomalia,
        string mensagem,
        string? criticidade,
        AnaliseRelacionadasResultado? analiseRelacionadas,
        int scoreRiscoProcesso,
        string? classificacaoRisco,
        string? tendenciaRisco,
        string? tendenciaValor,
        RuntimeDecisionAuditData? runtimeAudit = null)
    {
        var anomalia = new AnomaliaDetectada
        {
            ClienteId = clienteId,
            TagName = tagName,
            TipoTag = tipoTag,
            ValorRecebido = valor,
            Score = score,

            // Decisão operacional.
            EhAnomalia = ehAnomalia,

            // B3.19 — trilha de auditoria.
            //
            // Se algum chamador antigo ainda não fornecer runtimeAudit,
            // deixamos os novos campos null em vez de inventar informação.
            LegacyEhAnomalia =
                runtimeAudit?.LegacyFinalAnomaly,

            EmitNewEvidence =
                runtimeAudit?.EmitNewEvidence,

            RuntimeDecisionSource =
                runtimeAudit?.Source,

            RuntimeUsedLegacyFallback =
                runtimeAudit?.UsedLegacyFallback,

            RuntimeDecisionReasonsJson =
                runtimeAudit is null
                    ? null
                    : JsonSerializer.Serialize(
                        runtimeAudit.Reasons),

            RuntimeIncidentId =
                runtimeAudit?.IncidentId,

            RuntimeRegimeState =
                runtimeAudit?.RegimeState,

            RuntimeCandidateBaselineReady =
                runtimeAudit?.CandidateBaselineReady,

            Mensagem = mensagem,
            Criticidade = criticidade,
            ScoreRiscoProcesso = scoreRiscoProcesso,
            ClassificacaoRisco = classificacaoRisco,
            TendenciaRisco = tendenciaRisco,
            TendenciaValor = tendenciaValor,
            DataDeteccao = DateTime.Now
        };

        _context.AnomaliasDetectadas.Add(anomalia);
        _context.SaveChanges();

        // Se não houver análise de relacionamentos ou não houver resultados,
        // não grava dependências de tags relacionadas.
        if (analiseRelacionadas == null ||
            !analiseRelacionadas.Resultados.Any())
        {
            return;
        }

        foreach (var dep in analiseRelacionadas.Resultados)
        {
            var dependencia = new AnomaliaDependenciaDetectada
            {
                AnomaliaDetectadaId = anomalia.Id,
                TagDependente = dep.TagName,
                ValorAtual = dep.ValorAtual,
                Media = dep.Media,
                LimiteInferior = dep.LimiteInferior,
                LimiteSuperior = dep.LimiteSuperior,
                EhAnomalia = dep.EhAnomalia,
                Status = dep.EhAnomalia
                    ? "anomala"
                    : "normal",
                TipoRelacao = dep.TipoRelacao,
                Impacto = dep.Impacto,
                DescricaoRelacao = dep.DescricaoRelacao,
                DataDeteccao = DateTime.Now
            };

            _context.AnomaliaDependenciasDetectadas.Add(
                dependencia);
        }

        _context.SaveChanges();
    }
}