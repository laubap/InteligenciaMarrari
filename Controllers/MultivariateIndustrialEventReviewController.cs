using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/grupos-analise")]
public sealed class MultivariateIndustrialEventReviewController
    : ControllerBase
{
    private readonly MultivariateIndustrialEventReviewStore
        _reviewStore;

    public MultivariateIndustrialEventReviewController(
        MultivariateIndustrialEventReviewStore reviewStore)
    {
        _reviewStore =
            reviewStore ??
            throw new ArgumentNullException(
                nameof(reviewStore));
    }

    /// <summary>
    /// B3.24.2 / MV08.4D2B
    ///
    /// Registra ou atualiza a revisão industrial humana de um evento
    /// multivariado.
    ///
    /// A identidade do evento continua vinculada ao cliente, grupo,
    /// minuteIndex, janela, versão/data do modelo e threshold.
    ///
    /// Este endpoint NÃO:
    /// - altera ApprovedGroups;
    /// - habilita promoção;
    /// - altera readiness;
    /// - altera RuntimeDecision.
    /// </summary>
    [HttpPost(
        "cliente/{clienteId}/grupo/{groupName}/industrial-reviews/{minuteIndex:long}")]
    [ProducesResponseType(
        typeof(MultivariateIndustrialEventReviewIa),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<
        MultivariateIndustrialEventReviewIa>>
        UpsertAsync(
            string clienteId,
            string groupName,
            long minuteIndex,
            [FromBody]
            MultivariateIndustrialEventReviewRequest request,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                clienteId))
        {
            return BadRequest(
                new
                {
                    erro =
                        "ClienteId é obrigatório."
                });
        }

        if (string.IsNullOrWhiteSpace(
                groupName))
        {
            return BadRequest(
                new
                {
                    erro =
                        "GroupName é obrigatório."
                });
        }

        if (minuteIndex < 0)
        {
            return BadRequest(
                new
                {
                    erro =
                        "MinuteIndex não pode ser negativo."
                });
        }

        if (request is null)
        {
            return BadRequest(
                new
                {
                    erro =
                        "O corpo da requisição é obrigatório."
                });
        }

        if (request.ModelVersion <= 0)
        {
            return BadRequest(
                new
                {
                    erro =
                        "ModelVersion deve ser maior que zero."
                });
        }

        if (request.ModelTrainedAtUtc == default)
        {
            return BadRequest(
                new
                {
                    erro =
                        "ModelTrainedAtUtc é obrigatório."
                });
        }

        var threshold =
            request.Threshold ??
            75.0;

        if (!double.IsFinite(
                threshold) ||
            threshold <= 0 ||
            threshold >= 100)
        {
            return BadRequest(
                new
                {
                    erro =
                        "Threshold deve estar no intervalo (0, 100)."
                });
        }

        if (string.IsNullOrWhiteSpace(
                request.SourceWindow))
        {
            return BadRequest(
                new
                {
                    erro =
                        "SourceWindow é obrigatório."
                });
        }

        if (!MultivariateIndustrialReviewStatuses
                .TryNormalize(
                    request.ReviewStatus,
                    out var normalizedStatus))
        {
            return BadRequest(
                new
                {
                    erro =
                        "ReviewStatus inválido. Use ConfirmedIndustrialEvent, " +
                        "NormalOperation ou Inconclusive."
                });
        }

        if (string.IsNullOrWhiteSpace(
                request.Reviewer))
        {
            return BadRequest(
                new
                {
                    erro =
                        "Reviewer é obrigatório."
                });
        }

        /*
         * Toda revisão persistida deve apontar para alguma evidência
         * auditável. Isso também vale para Inconclusive: precisamos saber
         * em que material o revisor se baseou para concluir que faltava
         * evidência.
         */
        if (string.IsNullOrWhiteSpace(
                request.EvidenceReference))
        {
            return BadRequest(
                new
                {
                    erro =
                        "EvidenceReference é obrigatório."
                });
        }

        try
        {
            var result =
                await _reviewStore
                    .UpsertAsync(
                        new MultivariateIndustrialEventReviewUpsert
                        {
                            ClienteId =
                                clienteId,

                            GroupName =
                                groupName,

                            MinuteIndex =
                                minuteIndex,

                            SourceWindow =
                                request.SourceWindow,

                            ModelVersion =
                                request.ModelVersion,

                            ModelTrainedAtUtc =
                                request.ModelTrainedAtUtc,

                            Threshold =
                                threshold,

                            ReviewStatus =
                                normalizedStatus,

                            Reviewer =
                                request.Reviewer,

                            Notes =
                                request.Notes,

                            EvidenceReference =
                                request.EvidenceReference,

                            ReviewedAtUtc =
                                request.ReviewedAtUtc
                        },
                        cancellationToken);

            return Ok(
                result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(
                new
                {
                    erro =
                        exception.Message
                });
        }
    }

    /// <summary>
    /// Lista as revisões persistidas para a identidade exata do
    /// modelo/threshold informada.
    /// </summary>
    [HttpGet(
        "cliente/{clienteId}/grupo/{groupName}/industrial-reviews")]
    [ProducesResponseType(
        typeof(IReadOnlyList<MultivariateIndustrialEventReviewIa>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<
        IReadOnlyList<MultivariateIndustrialEventReviewIa>>>
        ListAsync(
            string clienteId,
            string groupName,
            [FromQuery]
            int modelVersion,
            [FromQuery]
            DateTime modelTrainedAtUtc,
            [FromQuery]
            double threshold = 75.0,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                clienteId) ||
            string.IsNullOrWhiteSpace(
                groupName))
        {
            return BadRequest(
                new
                {
                    erro =
                        "ClienteId e GroupName são obrigatórios."
                });
        }

        try
        {
            var result =
                await _reviewStore
                    .ListAsync(
                        clienteId,
                        groupName,
                        modelVersion,
                        modelTrainedAtUtc,
                        threshold,
                        cancellationToken);

            return Ok(
                result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(
                new
                {
                    erro =
                        exception.Message
                });
        }
    }
}

public sealed class MultivariateIndustrialEventReviewRequest
{
    public string SourceWindow { get; set; } =
        "";

    public int ModelVersion { get; set; }

    public DateTime ModelTrainedAtUtc { get; set; }

    public double? Threshold { get; set; } =
        75.0;

    public string ReviewStatus { get; set; } =
        "";

    public string Reviewer { get; set; } =
        "";

    public string? Notes { get; set; }

    public string EvidenceReference { get; set; } =
        "";

    public DateTime? ReviewedAtUtc { get; set; }
}