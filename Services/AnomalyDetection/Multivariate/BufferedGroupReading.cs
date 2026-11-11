namespace IAApi.Services.AnomalyDetection.Multivariate;

/// <summary>
/// Representa as leituras reunidas para um grupo multivariado
/// dentro de uma mesma janela temporal.
/// </summary>
public sealed class BufferedGroupReading
{
    private readonly object _lock = new();

    private readonly Dictionary<string, long> _minuteIndexesPorTag =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, DateTime> _timestampsPorTag =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string?> _sourceAnalysisCorrelationIdsPorTag =
        new(StringComparer.OrdinalIgnoreCase);

    public string ClienteId { get; init; } = "";

    public string GroupName { get; init; } = "";

    /// <summary>
    /// Identidade estável da rodada, usada exclusivamente para correlacionar
    /// a telemetria shadow com as leituras que a compuseram.
    /// </summary>
    public Guid VectorId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Minuto de referência do vetor.
    /// Durante a montagem representa o minuto de abertura do buffer;
    /// na cópia finalizada representa o minuto mais recente utilizado.
    /// </summary>
    public long MinuteIndex { get; init; }

    public int MinimumAvailableTags { get; init; } = 2;

    /// <summary>
    /// Tempo máximo permitido entre a leitura mais antiga e a mais
    /// recente que compõem o mesmo vetor multivariado.
    /// </summary>
    public int MaxLagMinutes { get; init; }

    public IReadOnlyList<string> ExpectedTags { get; init; } = [];

    public Dictionary<string, double> Valores { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, DateTime> TimestampsPorTag
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, DateTime>(
                    _timestampsPorTag,
                    StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public IReadOnlyDictionary<string, string?> SourceAnalysisCorrelationIdsPorTag
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, string?>(
                    _sourceAnalysisCorrelationIdsPorTag,
                    StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Mantém compatibilidade com chamadas antigas que já entregam
    /// todas as tags no mesmo minuto de referência do buffer.
    /// </summary>
    public void AdicionarValor(
        string tagName,
        double valor)
    {
        AdicionarValor(
            tagName,
            MinuteIndex,
            valor);
    }

    public void AdicionarValor(
        string tagName,
        long minuteIndex,
        double valor)
    {
        AdicionarValor(
            tagName,
            minuteIndex,
            valor,
            sourceTimestamp: null,
            sourceAnalysisCorrelationId: null);
    }

    public void AdicionarValor(
        string tagName,
        long minuteIndex,
        double valor,
        DateTime? sourceTimestamp,
        string? sourceAnalysisCorrelationId)
    {
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
            return;
        }

        var tagNormalizada = tagName.Trim();

        lock (_lock)
        {
            Valores[tagNormalizada] = valor;
            _minuteIndexesPorTag[tagNormalizada] = minuteIndex;

            if (sourceTimestamp.HasValue && sourceTimestamp.Value != default)
            {
                _timestampsPorTag[tagNormalizada] = sourceTimestamp.Value;
            }

            _sourceAnalysisCorrelationIdsPorTag[tagNormalizada] =
                string.IsNullOrWhiteSpace(sourceAnalysisCorrelationId)
                    ? null
                    : sourceAnalysisCorrelationId.Trim();
        }
    }

    public bool PossuiTag(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        lock (_lock)
        {
            return Valores.ContainsKey(tagName.Trim());
        }
    }

    public bool TryGetMinuteIndexDaTag(
        string tagName,
        out long minuteIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        lock (_lock)
        {
            return _minuteIndexesPorTag.TryGetValue(
                tagName.Trim(),
                out minuteIndex);
        }
    }

    /// <summary>
    /// Indica se uma nova tag, ainda ausente no vetor, cabe na janela
    /// MaxLagMinutes sem ultrapassar o span temporal permitido.
    /// </summary>
    public bool PodeAceitarMinuteIndex(
        long minuteIndex)
    {
        if (minuteIndex < 0)
        {
            return false;
        }

        lock (_lock)
        {
            if (_minuteIndexesPorTag.Count == 0)
            {
                return true;
            }

            var primeiroMinuto =
                _minuteIndexesPorTag.Values.Min();

            var ultimoMinuto =
                _minuteIndexesPorTag.Values.Max();

            var novoPrimeiroMinuto =
                Math.Min(
                    primeiroMinuto,
                    minuteIndex);

            var novoUltimoMinuto =
                Math.Max(
                    ultimoMinuto,
                    minuteIndex);

            var span =
                novoUltimoMinuto - novoPrimeiroMinuto;

            return span <= Math.Max(0, MaxLagMinutes);
        }
    }

    /// <summary>
    /// Retorna true quando, considerando o minuto atual, a janela já
    /// ultrapassou MaxLagMinutes e não pode mais receber leituras novas.
    /// </summary>
    public bool ExpirouPara(
        long minuteIndexAtual)
    {
        if (minuteIndexAtual < 0)
        {
            return false;
        }

        lock (_lock)
        {
            if (_minuteIndexesPorTag.Count == 0)
            {
                return false;
            }

            var primeiroMinuto =
                _minuteIndexesPorTag.Values.Min();

            if (minuteIndexAtual <= primeiroMinuto)
            {
                return false;
            }

            return minuteIndexAtual - primeiroMinuto >
                   Math.Max(0, MaxLagMinutes);
        }
    }

    public bool PossuiMinimoDeTags()
    {
        lock (_lock)
        {
            return Valores.Count >= MinimumAvailableTags;
        }
    }

    public bool EstaCompleto()
    {
        lock (_lock)
        {
            if (ExpectedTags.Count == 0)
            {
                return Valores.Count >= MinimumAvailableTags;
            }

            return ExpectedTags.All(
                tag => Valores.ContainsKey(tag));
        }
    }

    public BufferedGroupReading CriarCopia()
    {
        lock (_lock)
        {
            var minuteIndexFinal =
                _minuteIndexesPorTag.Count == 0
                    ? MinuteIndex
                    : _minuteIndexesPorTag.Values.Max();

            var copia = new BufferedGroupReading
            {
                ClienteId = ClienteId,
                GroupName = GroupName,
                VectorId = VectorId,
                MinuteIndex = minuteIndexFinal,
                MinimumAvailableTags = MinimumAvailableTags,
                MaxLagMinutes = MaxLagMinutes,
                ExpectedTags = ExpectedTags.ToList()
            };

            foreach (var item in Valores)
            {
                copia.Valores[item.Key] = item.Value;
            }

            foreach (var item in _minuteIndexesPorTag)
            {
                copia._minuteIndexesPorTag[item.Key] = item.Value;
            }

            foreach (var item in _timestampsPorTag)
            {
                copia._timestampsPorTag[item.Key] = item.Value;
            }

            foreach (var item in _sourceAnalysisCorrelationIdsPorTag)
            {
                copia._sourceAnalysisCorrelationIdsPorTag[item.Key] = item.Value;
            }

            return copia;
        }
    }
}
