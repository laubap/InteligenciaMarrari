using System.Collections.Concurrent;
using IAApi.IntelligenceLab.Observability;
using IAApi.IntelligenceLab.Observability.Application;

namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// Reúne leituras individuais das tags e monta vetores
/// multivariados por cliente, grupo e janela temporal.
/// </summary>
public sealed class MultivariateGroupBufferService
{
    private readonly GrupoAnaliseService _grupoAnaliseService;
    // B3.24.2: estado temporal compartilhado entre scopes HTTP.
    // A configuração dos grupos continua scoped no serviço.
    private readonly MultivariateRuntimeStateStore _stateStore;
    private readonly IIntelligenceLabMultivariateShadowCollector?
        _shadowDiagnosticCollector;

    private readonly ConcurrentDictionary<
        string,
        IReadOnlyList<GrupoAnaliseDto>> _gruposPorCliente = new(
            StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<
        string,
        Dictionary<string, List<GrupoAnaliseDto>>> _indicePorCliente =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _carregamentoLock =
        new(1, 1);

    public MultivariateGroupBufferService(
        GrupoAnaliseService grupoAnaliseService,
        MultivariateRuntimeStateStore stateStore,
        IIntelligenceLabMultivariateShadowCollector?
            shadowDiagnosticCollector = null)
    {
        _grupoAnaliseService =
            grupoAnaliseService ??
            throw new ArgumentNullException(
                nameof(grupoAnaliseService));

        _stateStore =
            stateStore ??
            throw new ArgumentNullException(
                nameof(stateStore));

        _shadowDiagnosticCollector = shadowDiagnosticCollector;
    }

    /// <summary>
    /// Adiciona uma leitura e devolve grupos que já podem ser analisados.
    /// A montagem respeita MaxLagMinutes e mantém uma rodada ativa por
    /// cliente/grupo. A repetição da mesma tag em outro minuto inicia
    /// uma nova rodada.
    /// </summary>
    public async Task<IReadOnlyList<BufferedGroupReading>>
        AdicionarLeituraAsync(
            string clienteId,
            string tagName,
            long minuteIndex,
            double valor,
            CancellationToken cancellationToken = default)
    {
        return await AdicionarLeituraAsync(
            clienteId,
            tagName,
            minuteIndex,
            valor,
            sourceTimestamp: null,
            sourceAnalysisCorrelationId: null,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<BufferedGroupReading>>
        AdicionarLeituraAsync(
            string clienteId,
            string tagName,
            long minuteIndex,
            double valor,
            DateTime? sourceTimestamp,
            string? sourceAnalysisCorrelationId,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        if (minuteIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minuteIndex),
                "O índice do minuto não pode ser negativo.");
        }

        if (double.IsNaN(valor) ||
            double.IsInfinity(valor))
        {
            return [];
        }

        var clienteNormalizado = clienteId.Trim();
        var tagNormalizada = tagName.Trim();

        var indice = await ObterIndiceAsync(
            clienteNormalizado,
            cancellationToken);

        var gruposFinalizados =
            FinalizarBuffersExpirados(
                    clienteNormalizado,
                    minuteIndex)
                .ToList();

        if (!indice.TryGetValue(
                tagNormalizada,
                out var gruposDaTag))
        {
            AtualizarUltimoMinuto(
                clienteNormalizado,
                minuteIndex);

            return gruposFinalizados;
        }

        foreach (var grupo in gruposDaTag)
        {
            var chave = CriarChave(
                clienteNormalizado,
                grupo.Nome);

            if (_stateStore.Buffers.TryGetValue(
                    chave,
                    out var bufferExistente))
            {
                if (bufferExistente.TryGetMinuteIndexDaTag(
                        tagNormalizada,
                        out var minutoExistenteDaTag))
                {
                    if (minutoExistenteDaTag == minuteIndex)
                    {
                        // Mesma tag no mesmo minuto: mantém a semântica
                        // anterior de atualização/substituição do valor.
                        bufferExistente.AdicionarValor(
                            tagNormalizada,
                            minuteIndex,
                            valor,
                            sourceTimestamp,
                            sourceAnalysisCorrelationId);

                        RegistrarLeituraAceita(bufferExistente.CriarCopia());

                        continue;
                    }

                    // A mesma tag reapareceu em outro minuto. Isso marca
                    // o início de uma nova rodada para o grupo.
                    FinalizarBuffer(
                        chave,
                        gruposFinalizados);

                    bufferExistente = null;
                }
                else if (!bufferExistente.PodeAceitarMinuteIndex(
                             minuteIndex))
                {
                    // A nova leitura ultrapassaria o MaxLagMinutes da
                    // rodada atual. Fecha a rodada e inicia outra.
                    FinalizarBuffer(
                        chave,
                        gruposFinalizados);

                    bufferExistente = null;
                }
            }

            var buffer =
                bufferExistente ??
                _stateStore.Buffers.GetOrAdd(
                    chave,
                    _ => CriarBuffer(
                        clienteNormalizado,
                        grupo,
                        minuteIndex));

            buffer.AdicionarValor(
                tagNormalizada,
                minuteIndex,
                valor,
                sourceTimestamp,
                sourceAnalysisCorrelationId);

            RegistrarLeituraAceita(buffer.CriarCopia());
        }

        AtualizarUltimoMinuto(
            clienteNormalizado,
            minuteIndex);

        return gruposFinalizados
            .OrderBy(x => x.MinuteIndex)
            .ThenBy(x => x.GroupName)
            .ToList();
    }

    /// <summary>
    /// Finaliza todos os buffers ainda pendentes.
    /// Deve ser chamado ao terminar o benchmark.
    /// </summary>
    public IReadOnlyList<BufferedGroupReading>
        FinalizarPendentes(
            string? clienteId = null)
    {
        var finalizados =
            new List<BufferedGroupReading>();

        foreach (var item in _stateStore.Buffers.ToArray())
        {
            if (!string.IsNullOrWhiteSpace(clienteId) &&
                !string.Equals(
                    item.Value.ClienteId,
                    clienteId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_stateStore.Buffers.TryRemove(
                    item.Key,
                    out var buffer))
            {
                continue;
            }

            if (buffer.PossuiMinimoDeTags())
            {
                var leitura = buffer.CriarCopia();
                finalizados.Add(leitura);
                RegistrarFechamento(leitura, descartado: false, reasonCode: null);
            }
            else
            {
                RegistrarFechamento(
                    buffer.CriarCopia(),
                    descartado: true,
                    reasonCode: "InsufficientTagsToFinalizeVector");
            }
        }

        return finalizados
            .OrderBy(x => x.MinuteIndex)
            .ThenBy(x => x.GroupName)
            .ToList();
    }

    public void LimparCliente(
        string clienteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clienteId);

        _stateStore.LimparCliente(clienteId);

        _gruposPorCliente.TryRemove(
            clienteId,
            out _);

        _indicePorCliente.TryRemove(
            clienteId,
            out _);
    }

    public void LimparTudo()
    {
        _stateStore.LimparTudo();
        _gruposPorCliente.Clear();
        _indicePorCliente.Clear();
    }

    public int ObterQuantidadeBuffers()
    {
        return _stateStore.ObterQuantidadeBuffers();
    }

    private IReadOnlyList<BufferedGroupReading>
        FinalizarBuffersExpirados(
            string clienteId,
            long minuteIndexAtual)
    {
        if (!_stateStore.UltimoMinutoPorCliente.TryGetValue(
                clienteId,
                out var ultimoMinuto))
        {
            return [];
        }

        if (minuteIndexAtual <= ultimoMinuto)
        {
            return [];
        }

        var finalizados =
            new List<BufferedGroupReading>();

        foreach (var item in _stateStore.Buffers.ToArray())
        {
            var buffer = item.Value;

            if (!string.Equals(
                    buffer.ClienteId,
                    clienteId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!buffer.ExpirouPara(minuteIndexAtual))
            {
                continue;
            }

            FinalizarBuffer(
                item.Key,
                finalizados);
        }

        return finalizados
            .OrderBy(x => x.MinuteIndex)
            .ThenBy(x => x.GroupName)
            .ToList();
    }

    private void FinalizarBuffer(
        string chave,
        ICollection<BufferedGroupReading> finalizados)
    {
        if (!_stateStore.Buffers.TryRemove(
                chave,
                out var removido))
        {
            return;
        }

        if (removido.PossuiMinimoDeTags())
        {
            var leitura = removido.CriarCopia();
            finalizados.Add(leitura);
            RegistrarFechamento(leitura, descartado: false, reasonCode: null);
        }
        else
        {
            RegistrarFechamento(
                removido.CriarCopia(),
                descartado: true,
                reasonCode: "InsufficientTagsToFinalizeVector");
        }
    }

    private static BufferedGroupReading CriarBuffer(
        string clienteId,
        GrupoAnaliseDto grupo,
        long minuteIndex)
    {
        return new BufferedGroupReading
        {
            ClienteId = clienteId,
            GroupName = grupo.Nome,
            MinuteIndex = minuteIndex,
            MinimumAvailableTags =
                Math.Max(
                    2,
                    grupo.MinimumAvailableTags),
            MaxLagMinutes =
                Math.Max(
                    0,
                    grupo.MaxLagMinutes),
            ExpectedTags =
                grupo.Tags
                    .Where(tag =>
                        !string.IsNullOrWhiteSpace(tag))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList()
        };
    }

    private void RegistrarLeituraAceita(BufferedGroupReading reading) =>
        RegistrarDiagnostico(
            reading,
            MultivariateShadowEventKind.ReadingAccepted,
            reasonCode: null,
            mahalanobisExecuted: false,
            anomaly: null,
            normalizedScore: null,
            threshold: null,
            mahalanobisSquared: null);

    private void RegistrarFechamento(
        BufferedGroupReading reading,
        bool descartado,
        string? reasonCode) =>
        RegistrarDiagnostico(
            reading,
            descartado
                ? MultivariateShadowEventKind.VectorDiscarded
                : MultivariateShadowEventKind.VectorFinalized,
            reasonCode,
            mahalanobisExecuted: false,
            anomaly: null,
            normalizedScore: null,
            threshold: null,
            mahalanobisSquared: null);

    private void RegistrarDiagnostico(
        BufferedGroupReading reading,
        MultivariateShadowEventKind eventKind,
        string? reasonCode,
        bool mahalanobisExecuted,
        bool? anomaly,
        double? normalizedScore,
        double? threshold,
        double? mahalanobisSquared)
    {
        if (_shadowDiagnosticCollector is null)
        {
            return;
        }

        try
        {
            var timestamps = reading.TimestampsPorTag;
            var correlationIds = reading.SourceAnalysisCorrelationIdsPorTag;
            var tags = reading.ExpectedTags
                .Concat(reading.Valores.Keys)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var readings = reading.Valores
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => new MultivariateShadowTagReading(
                    item.Key,
                    timestamps.TryGetValue(item.Key, out var timestamp)
                        ? timestamp
                        : null,
                    item.Value,
                    correlationIds.TryGetValue(item.Key, out var correlationId)
                        ? correlationId
                        : null))
                .ToArray();

            _shadowDiagnosticCollector.Record(
                new MultivariateShadowDiagnostic(
                    Guid.NewGuid(),
                    reading.VectorId,
                    eventKind,
                    reading.ClienteId,
                    reading.GroupName,
                    reading.MinuteIndex,
                    tags,
                    readings,
                    reading.MinimumAvailableTags,
                    reading.MaxLagMinutes,
                    reading.EstaCompleto(),
                    reasonCode,
                    mahalanobisExecuted,
                    anomaly,
                    normalizedScore,
                    threshold,
                    mahalanobisSquared,
                    DateTimeOffset.UtcNow));
        }
        catch (Exception)
        {
            // Telemetria é estritamente fail-open.
        }
    }

    private async Task<
        Dictionary<string, List<GrupoAnaliseDto>>>
        ObterIndiceAsync(
            string clienteId,
            CancellationToken cancellationToken)
    {
        if (_indicePorCliente.TryGetValue(
                clienteId,
                out var indiceExistente))
        {
            return indiceExistente;
        }

        await _carregamentoLock.WaitAsync(
            cancellationToken);

        try
        {
            if (_indicePorCliente.TryGetValue(
                    clienteId,
                    out indiceExistente))
            {
                return indiceExistente;
            }

            var grupos =
                await _grupoAnaliseService
                    .ObterGruposAsync(clienteId);

            var gruposAtivos = grupos
                .Where(grupo =>
                    grupo.Tags.Count >=
                    Math.Max(
                        2,
                        grupo.MinimumAvailableTags))
                .ToList();

            _gruposPorCliente[clienteId] =
                gruposAtivos;

            var indice =
                new Dictionary<
                    string,
                    List<GrupoAnaliseDto>>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var grupo in gruposAtivos)
            {
                foreach (var tag in grupo.Tags
                             .Where(tag =>
                                 !string.IsNullOrWhiteSpace(tag))
                             .Distinct(
                                 StringComparer.OrdinalIgnoreCase))
                {
                    if (!indice.TryGetValue(
                            tag,
                            out var lista))
                    {
                        lista = [];
                        indice[tag] = lista;
                    }

                    lista.Add(grupo);
                }
            }

            _indicePorCliente[clienteId] =
                indice;

            return indice;
        }
        finally
        {
            _carregamentoLock.Release();
        }
    }

    private void AtualizarUltimoMinuto(
        string clienteId,
        long minuteIndex)
    {
        _stateStore.UltimoMinutoPorCliente.AddOrUpdate(
            clienteId,
            minuteIndex,
            (_, atual) =>
                Math.Max(
                    atual,
                    minuteIndex));
    }

    private static string CriarChave(
        string clienteId,
        string groupName)
    {
        return string.Concat(
            clienteId.Trim(),
            "::",
            groupName.Trim());
    }
}
