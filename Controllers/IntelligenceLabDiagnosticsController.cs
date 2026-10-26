using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IAApi.Controllers;

/// <summary>
/// Leitura de diagnosticos temporarios somente para ensaios locais.
/// A chave concede acesso global aos registros desta instancia: NAO publicar
/// em producao, nem usar via proxy externo, nem compartilhar entre tenants.
/// </summary>
[ApiController]
[Route("api/intelligence-lab/diagnostics")]
public sealed class IntelligenceLabDiagnosticsController : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;
    private const int MaximumFilterLength = 128;

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly BoundedDiagnosticBuffer? _buffer;

    public IntelligenceLabDiagnosticsController(
        IEnumerable<BoundedDiagnosticBuffer> buffers,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(buffers);
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _buffer = buffers.SingleOrDefault();
    }

    /// <summary>
    /// Ultimas leituras capturadas (ordem de armazenamento decrescente),
    /// com filtros exatos opcionais e limite obrigatorio entre 1 e 100.
    /// A listagem devolve resumos; detalhes apenas na consulta por ID.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IntelligenceLabDiagnosticSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<IntelligenceLabDiagnosticSummary>> List(
        [FromHeader(Name = IntelligenceLabLocalReadAccess.KeyHeader)] string? accessKey,
        [FromQuery] int limit = DefaultLimit,
        [FromQuery] string? clienteId = null,
        [FromQuery] string? tagName = null)
    {
        var denial = IntelligenceLabLocalReadAccess.DenyIfUnauthorized(
            this, _configuration, _environment, _buffer, accessKey);
        if (denial is not null)
        {
            return denial;
        }

        if (limit is < 1 or > MaximumLimit ||
            !ValidFilter(clienteId) || !ValidFilter(tagName))
        {
            return BadRequest("Limite deve estar entre 1 e 100 e filtros devem ter ate 128 caracteres nao vazios.");
        }

        // A autorizacao garante que o buffer nao e nulo nem desabilitado.
        var summaries = _buffer!.Snapshot()
            .Reverse()
            .Where(d => clienteId is null || string.Equals(d.ClienteId, clienteId, StringComparison.Ordinal))
            .Where(d => tagName is null || string.Equals(d.TagName, tagName, StringComparison.Ordinal))
            .Take(limit)
            .Select(d => new IntelligenceLabDiagnosticSummary(
                d.Context.DiagnosticId,
                d.ClienteId,
                d.TagName,
                d.ReadingTimestampUtc,
                d.Completeness.Status,
                d.Ensemble?.NormalizedScore,
                d.RuntimeDecision?.FinalAnomaly))
            .ToArray();

        return Ok(summaries);
    }

    /// <summary>Retorna o diagnostico original, se ainda existir no buffer limitado.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IntelligenceLabReadingDiagnostic), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IntelligenceLabReadingDiagnostic> GetById(
        [FromRoute] Guid id,
        [FromHeader(Name = IntelligenceLabLocalReadAccess.KeyHeader)] string? accessKey)
    {
        var denial = IntelligenceLabLocalReadAccess.DenyIfUnauthorized(
            this, _configuration, _environment, _buffer, accessKey);
        if (denial is not null)
        {
            return denial;
        }

        if (id == Guid.Empty)
        {
            return NotFound();
        }

        var diagnostic = _buffer!.Snapshot()
            .LastOrDefault(d => d.Context.DiagnosticId == id);
        return diagnostic is null ? NotFound() : Ok(diagnostic);
    }

    private static bool ValidFilter(string? value) =>
        value is null || (value.Length <= MaximumFilterLength &&
                          !string.IsNullOrWhiteSpace(value));
}

/// <summary>Resumo deliberadamente sem valor da leitura ou detalhes industriais.</summary>
public sealed record IntelligenceLabDiagnosticSummary(
    Guid DiagnosticId,
    string ClienteId,
    string TagName,
    DateTimeOffset ReadingTimestampUtc,
    DiagnosticCompletenessStatus Completeness,
    double? EnsembleScoreNormalized,
    bool? FinalAnomaly);
