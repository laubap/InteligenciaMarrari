using Microsoft.EntityFrameworkCore;

namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Localiza a configuração de regime operacional de uma tag,
/// busca no histórico a tag que representa o estado do processo
/// e tenta construir um baseline condicionado.
///
/// Este componente é a camada de integração entre:
///
/// - banco de configurações;
/// - IHistoricoService / Bridge;
/// - OperationalRegimeBaselineService.
///
/// Qualquer impossibilidade de construir o baseline condicionado
/// resulta em fallback seguro para o baseline global existente.
/// </summary>
public sealed class OperationalRegimeBaselineProvider
{
    private readonly AppDbContext _context;
    private readonly IHistoricoService _historicoService;
    private readonly OperationalRegimeBaselineService _baselineService;

    public OperationalRegimeBaselineProvider(
        AppDbContext context,
        IHistoricoService historicoService,
        OperationalRegimeBaselineService baselineService)
    {
        _context =
            context ??
            throw new ArgumentNullException(
                nameof(context));

        _historicoService =
            historicoService ??
            throw new ArgumentNullException(
                nameof(historicoService));

        _baselineService =
            baselineService ??
            throw new ArgumentNullException(
                nameof(baselineService));
    }

    public OperationalRegimeBaselineResolution Resolve(
        string clienteId,
        string tagName,
        IReadOnlyList<LeituraBruta> targetHistory,
        DateTime currentTimestamp)
    {
        if (string.IsNullOrWhiteSpace(
                clienteId))
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "Cliente não informado para resolução do regime operacional.");
        }

        if (string.IsNullOrWhiteSpace(
                tagName))
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "Tag não informada para resolução do regime operacional.");
        }

        ArgumentNullException.ThrowIfNull(
            targetHistory);

        if (currentTimestamp == default)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "Timestamp atual inválido para resolução do regime operacional.");
        }

        /*
         * A configuração é específica por cliente + tag.
         *
         * AsNoTracking evita colocar a configuração no change tracker,
         * pois esta operação é somente leitura.
         */
        TagRegimeOperacionalIa? configuration;

        try
        {
            configuration =
                _context
                    .TagsRegimesOperacionaisIa
                    .AsNoTracking()
                    .FirstOrDefault(
                        item =>
                            item.ClienteId == clienteId &&
                            item.TagName == tagName);
        }
        catch (Exception exception)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "Não foi possível consultar a configuração de regime " +
                $"operacional: {exception.Message}");
        }

        /*
         * Tags sem configuração continuam exatamente no fluxo global.
         */
        if (configuration is null)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "A tag não possui configuração de regime operacional.");
        }

        if (!configuration.Ativo)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "A configuração de regime operacional está desativada.");
        }

        if (string.IsNullOrWhiteSpace(
                configuration.TagRegime))
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "A configuração não possui uma TagRegime válida.");
        }

        if (configuration.LagMinutos < 0)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "LagMinutos não pode ser negativo.");
        }

        if (configuration.ToleranciaAlinhamentoMinutos < 0)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "ToleranciaAlinhamentoMinutos não pode ser negativa.");
        }

        var validTargetHistory =
            targetHistory
                .Where(IsValidReading)
                .OrderBy(reading =>
                    reading.DataHora)
                .ToArray();

        if (validTargetHistory.Length == 0)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "O histórico da tag analisada não possui leituras válidas.");
        }

        /*
         * Para alinhar um target em T:
         *
         * expectedRegimeTime = T - Lag
         *
         * Como o resolver aceita uma leitura causal até
         * ToleranciaAlinhamentoMinutos antes desse instante,
         * precisamos iniciar a consulta em:
         *
         * primeiroTarget - Lag - Tolerância.
         */
        DateTime regimeWindowStart;
        DateTime regimeWindowEnd;

        try
        {
            regimeWindowStart =
                validTargetHistory[0]
                    .DataHora
                    .AddMinutes(
                        -configuration.LagMinutos)
                    .AddMinutes(
                        -configuration.ToleranciaAlinhamentoMinutos);

            var latestTargetExpectedRegimeTime =
                validTargetHistory[^1]
                    .DataHora
                    .AddMinutes(
                        -configuration.LagMinutos);

            var currentExpectedRegimeTime =
                currentTimestamp
                    .AddMinutes(
                        -configuration.LagMinutos);

            /*
             * Normalmente currentTimestamp estará depois do histórico,
             * mas usamos o maior instante para o provider continuar
             * correto também em replays e outros cenários temporais.
             */
            regimeWindowEnd =
                currentExpectedRegimeTime >
                latestTargetExpectedRegimeTime
                    ? currentExpectedRegimeTime
                    : latestTargetExpectedRegimeTime;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "Não foi possível calcular a janela temporal do regime " +
                $"operacional: {exception.Message}");
        }

        if (regimeWindowEnd <=
            regimeWindowStart)
        {
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "A janela calculada para o histórico da tag de regime é inválida.");
        }

        List<LeituraBruta> regimeHistory;

        try
        {
            /*
             * Importante:
             *
             * NÃO utilizamos BridgeValorAtualService.
             *
             * O estado do regime deve pertencer à mesma linha temporal
             * da leitura analisada. Isso permite replay histórico e
             * impede que um estado atual explique uma leitura passada.
             */
            regimeHistory =
                _historicoService
                    .BuscarHistoricoPorPeriodo(
                        clienteId,
                        configuration.TagRegime,
                        regimeWindowStart,
                        regimeWindowEnd);
        }
        catch (Exception exception)
        {
            /*
             * Regime operacional é uma melhoria de baseline.
             *
             * Falha de Bridge, ausência de histórico ou outro problema
             * nesta consulta não deve impedir a detecção já existente.
             *
             * O chamador deverá manter o baseline global.
             */
            return OperationalRegimeBaselineResolution.CreateNotApplied(
                "Não foi possível obter o histórico da tag de regime. " +
                $"Fallback para baseline global. Motivo: {exception.Message}");
        }

        /*
         * A partir daqui delegamos:
         *
         * alinhamento temporal
         *     ↓
         * seleção do regime
         *     ↓
         * cálculo estatístico
         *
         * aos componentes já testados anteriormente.
         */
        return _baselineService.Resolve(
            configuration,
            validTargetHistory,
            regimeHistory,
            currentTimestamp);
    }

    private static bool IsValidReading(
        LeituraBruta reading)
    {
        return reading is not null &&
               reading.DataHora != default &&
               float.IsFinite(
                   reading.Valor);
    }
}