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
    private readonly OperationalRegimeForbiddenTagsOptions _forbiddenTags;
    private readonly bool _regimeCombinadoHabilitado;

    /// <param name="forbiddenTagsOptions">
    /// Tags proibidas como regime, por cliente (RC-7). Nulo ou vazio
    /// (padrão) = nenhuma tag bloqueada, comportamento anterior.
    /// </param>
    /// <param name="regimeCombinadoOptions">
    /// Chave do regime combinado (RC-8). Nula ou <c>Habilitado = false</c>
    /// (padrão) = a tabela de componentes não é lida e o caminho é o atual.
    /// </param>
    public OperationalRegimeBaselineProvider(
        AppDbContext context,
        IHistoricoService historicoService,
        OperationalRegimeBaselineService baselineService,
        Microsoft.Extensions.Options.IOptions<OperationalRegimeForbiddenTagsOptions>?
            forbiddenTagsOptions = null,
        Microsoft.Extensions.Options.IOptions<RegimeCombinadoOptions>?
            regimeCombinadoOptions = null)
    {
        _forbiddenTags =
            forbiddenTagsOptions?.Value ??
            new OperationalRegimeForbiddenTagsOptions();

        _regimeCombinadoHabilitado =
            regimeCombinadoOptions?.Value.Habilitado == true;

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

        /*
         * RC-7 (backlog 11): rótulos/gabarito do cliente nunca podem
         * escolher o baseline. Com a lista vazia (padrão), nada muda.
         * Recusa antes de qualquer consulta ao histórico.
         */
        if (_forbiddenTags.IsForbidden(
                clienteId,
                configuration.TagRegime))
        {
            var reason =
                $"TagRegime {configuration.TagRegime} está na lista de tags " +
                $"proibidas como regime do cliente {clienteId} " +
                "(AnomalyDetection:RegimeOperacional:TagsProibidasPorCliente). " +
                "Fallback para baseline global.";

            Console.WriteLine(
                $"BASELINE POR REGIME RECUSADO " +
                $"Cliente={clienteId} " +
                $"Tag={tagName} " +
                $"TagRegime={configuration.TagRegime} " +
                "Motivo=tag proibida como regime");

            return OperationalRegimeBaselineResolution.CreateNotApplied(
                reason);
        }

        /*
         * RC-8: regime combinado (componentes 2..N), só com
         * AnomalyDetection:RegimeCombinado:Habilitado = true. Com a chave
         * desligada, a tabela de componentes nem é lida e a lista fica
         * vazia: segue o caminho atual, inclusive ignorando MapaEstados.
         * Com a chave ligada e sem componentes cadastrados, idem.
         */
        IReadOnlyList<TagRegimeComponenteIa> components =
            Array.Empty<TagRegimeComponenteIa>();

        if (_regimeCombinadoHabilitado)
        {
            try
            {
                components =
                    _context
                        .TagsRegimesComponentesIa
                        .AsNoTracking()
                        .Where(item =>
                            item.ConfiguracaoId == configuration.Id)
                        .OrderBy(item =>
                            item.Ordem)
                        .ToList();
            }
            catch (Exception exception)
            {
                return OperationalRegimeBaselineResolution.CreateNotApplied(
                    "Não foi possível consultar os componentes do regime " +
                    $"combinado: {exception.Message}");
            }

            /*
             * Mesma recusa da RC-7 para cada componente, antes de
             * qualquer consulta ao histórico.
             */
            foreach (var component in components)
            {
                if (string.IsNullOrWhiteSpace(
                        component.TagRegime))
                {
                    return OperationalRegimeBaselineResolution.CreateNotApplied(
                        $"O componente {component.Ordem} do regime combinado " +
                        "não possui uma TagRegime válida.");
                }

                if (_forbiddenTags.IsForbidden(
                        clienteId,
                        component.TagRegime))
                {
                    Console.WriteLine(
                        $"BASELINE POR REGIME RECUSADO " +
                        $"Cliente={clienteId} " +
                        $"Tag={tagName} " +
                        $"TagRegime={component.TagRegime} " +
                        $"Componente={component.Ordem} " +
                        "Motivo=tag proibida como regime");

                    return OperationalRegimeBaselineResolution.CreateNotApplied(
                        $"Componente {component.Ordem} ({component.TagRegime}) " +
                        $"está na lista de tags proibidas como regime do cliente " +
                        $"{clienteId} " +
                        "(AnomalyDetection:RegimeOperacional:TagsProibidasPorCliente). " +
                        "Fallback para baseline global.");
                }
            }
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
        if (components.Count == 0)
        {
            return _baselineService.Resolve(
                configuration,
                validTargetHistory,
                regimeHistory,
                currentTimestamp);
        }

        /*
         * RC-8: uma consulta por componente adicional, com a mesma janela
         * do componente 1. Falha em qualquer uma ⇒ fallback global.
         */
        var regimeHistories =
            new List<IReadOnlyList<LeituraBruta>>(
                components.Count + 1)
            {
                regimeHistory
            };

        foreach (var component in components)
        {
            try
            {
                regimeHistories.Add(
                    _historicoService
                        .BuscarHistoricoPorPeriodo(
                            clienteId,
                            component.TagRegime,
                            regimeWindowStart,
                            regimeWindowEnd));
            }
            catch (Exception exception)
            {
                return OperationalRegimeBaselineResolution.CreateNotApplied(
                    $"Não foi possível obter o histórico do componente " +
                    $"{component.Ordem} ({component.TagRegime}) do regime " +
                    $"combinado. Fallback para baseline global. " +
                    $"Motivo: {exception.Message}");
            }
        }

        return _baselineService.Resolve(
            configuration,
            components,
            validTargetHistory,
            regimeHistories,
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