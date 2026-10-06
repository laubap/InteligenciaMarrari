using IAApi.Services.AnomalyDetection.BehaviorProfiles;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Discovery;
using IAApi.Services.AnomalyDetection.BehaviorProfiles.Persistence;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/behavior-memory")]
public sealed class BehaviorMemoryDiscoveryController :
    ControllerBase
{
    private readonly BehaviorDiscoveryWorkflowService _workflow;
    private readonly BehaviorMemoryClassificationService _classificationService;

    public BehaviorMemoryDiscoveryController(
        BehaviorDiscoveryWorkflowService workflow,
        BehaviorMemoryClassificationService classificationService)
    {
        _workflow =
            workflow ??
            throw new ArgumentNullException(
                nameof(workflow));

        _classificationService =
            classificationService ??
            throw new ArgumentNullException(
                nameof(classificationService));
    }

    /// <summary>
    /// Executa, de forma explícita e controlada, a descoberta histórica
    /// de comportamentos de uma tag.
    ///
    /// Este endpoint:
    /// - consulta histórico por período;
    /// - descobre/agrupa episódios;
    /// - reconcilia com BEHs existentes;
    /// - consolida a memória persistente;
    /// - NÃO altera o runtime automático de detecção.
    /// </summary>
    [HttpPost("cliente/{clienteId}/tag/{tagName}/discover")]
    [ProducesResponseType(
        typeof(BehaviorMemoryDiscoveryResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BehaviorMemoryDiscoveryResponse>>
        DiscoverAsync(
            string clienteId,
            string tagName,
            [FromBody]
            BehaviorMemoryDiscoveryRequest request,
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
                tagName))
        {
            return BadRequest(
                new
                {
                    erro =
                        "TagName é obrigatório."
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

        if (request.Inicio == default ||
            request.Fim == default)
        {
            return BadRequest(
                new
                {
                    erro =
                        "Inicio e Fim são obrigatórios."
                });
        }

        if (request.Fim <=
            request.Inicio)
        {
            return BadRequest(
                new
                {
                    erro =
                        "Fim deve ser maior que Inicio."
                });
        }

        try
        {
            var result =
                await _workflow.ExecuteAsync(
                    clienteId,
                    tagName,
                    request.Inicio,
                    request.Fim,
                    cancellationToken);

            var response =
                BehaviorMemoryDiscoveryResponse.From(
                    result);

            return Ok(
                response);
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
        catch (InvalidOperationException exception)
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
    /// Altera explicitamente a classificação semântica de um comportamento Known.
    ///
    /// Valores aceitos:
    /// - Undefined
    /// - Normal
    /// - KnownAbnormal
    ///
    /// A operação não promove Candidate, não altera identidade BEH-xxx,
    /// confiança, origem, ocorrências ou snapshots.
    /// </summary>
    [HttpPatch(
        "cliente/{clienteId}/tag/{tagName}/behavior/{behaviorCode}/classification")]
    [ProducesResponseType(
        typeof(BehaviorMemoryClassificationResult),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BehaviorMemoryClassificationResult>>
        ClassifyAsync(
            string clienteId,
            string tagName,
            string behaviorCode,
            [FromBody]
            BehaviorMemoryClassificationRequest request,
            CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(
                new
                {
                    erro =
                        "O corpo da requisição é obrigatório."
                });
        }

        try
        {
            var result =
                await _classificationService.ClassifyAsync(
                    clienteId,
                    tagName,
                    behaviorCode,
                    request.Classification,
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
        catch (InvalidOperationException exception)
        {
            return BadRequest(
                new
                {
                    erro =
                        exception.Message
                });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(
                new
                {
                    erro =
                        exception.Message
                });
        }
    }
}

public sealed class BehaviorMemoryClassificationRequest
{
    public string Classification { get; set; } = "";
}

public sealed class BehaviorMemoryDiscoveryRequest
{
    /// <summary>
    /// Início do período no mesmo relógio fornecido pelo Bridge/PowerServer.
    /// Use ISO-8601 sem conversão de fuso.
    /// </summary>
    public DateTime Inicio { get; set; }

    /// <summary>
    /// Fim do período no mesmo relógio fornecido pelo Bridge/PowerServer.
    /// Use ISO-8601 sem conversão de fuso.
    /// </summary>
    public DateTime Fim { get; set; }
}

public sealed record BehaviorMemoryDiscoveryCandidateSummary
{
    public int GroupIndex { get; init; }

    public int OccurrenceCount { get; init; }

    public bool IsRecurrent { get; init; }

    public DateTime FirstOccurrenceStart { get; init; }

    public DateTime LastOccurrenceEnd { get; init; }

    public BehaviorCandidateRepresentativeProfile RepresentativeProfile { get; init; } =
        new();

    public string Reason { get; init; } = "";
}

public sealed record BehaviorMemoryDiscoveryResponse
{
    public string ClienteId { get; init; } = "";

    public string TagName { get; init; } = "";

    public DateTime PeriodoInicioUtc { get; init; }

    public DateTime PeriodoFimUtc { get; init; }

    public int SegmentCount { get; init; }

    public int TotalChangePoints { get; init; }

    public int TotalEpisodes { get; init; }

    public int TotalCharacterizableEpisodes { get; init; }

    public int GroupCount { get; init; }

    public IReadOnlyList<BehaviorMemoryDiscoveryCandidateSummary> Candidates { get; init; } =
        Array.Empty<BehaviorMemoryDiscoveryCandidateSummary>();

    public BehaviorMemoryConsolidationResult Consolidation { get; init; } =
        new();

    public static BehaviorMemoryDiscoveryResponse From(
        BehaviorDiscoveryWorkflowResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        return new BehaviorMemoryDiscoveryResponse
        {
            ClienteId =
                result.ClienteId,

            TagName =
                result.TagName,

            PeriodoInicioUtc =
                result.PeriodoInicio,

            PeriodoFimUtc =
                result.PeriodoFim,

            SegmentCount =
                result.Segments.Count,

            TotalChangePoints =
                result.TotalChangePoints,

            TotalEpisodes =
                result.TotalEpisodes,

            TotalCharacterizableEpisodes =
                result.TotalCharacterizableEpisodes,

            GroupCount =
                result.Grouping.Groups.Count,

            Candidates =
                result.Candidates
                    .Select(candidate =>
                        new BehaviorMemoryDiscoveryCandidateSummary
                        {
                            GroupIndex =
                                candidate.GroupIndex,

                            OccurrenceCount =
                                candidate.OccurrenceCount,

                            IsRecurrent =
                                candidate.IsRecurrent,

                            FirstOccurrenceStart =
                                candidate.FirstOccurrenceStart,

                            LastOccurrenceEnd =
                                candidate.LastOccurrenceEnd,

                            RepresentativeProfile =
                                candidate.RepresentativeProfile,

                            Reason =
                                candidate.Reason
                        })
                    .ToArray(),

            Consolidation =
                result.Consolidation
        };
    }
}
