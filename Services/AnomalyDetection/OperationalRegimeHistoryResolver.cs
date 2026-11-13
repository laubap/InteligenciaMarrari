namespace IAApi.Services.AnomalyDetection;

/// <summary>
/// Resolve o subconjunto do histórico de uma tag que pertence ao mesmo
/// regime operacional da leitura atualmente analisada.
///
/// Exemplo:
///
/// INLET_FLOW_LPS em 10:01
/// LagMinutos = 1
///
/// O estado operacional esperado é consultado em:
///
/// PUMP_RUNNING em 10:00
///
/// Se o estado encontrado for 0, somente pontos históricos de
/// INLET_FLOW_LPS cujo estado operacional correspondente também seja
/// aproximadamente 0 participam do baseline condicionado.
///
/// O alinhamento é sempre causal:
/// uma leitura futura da tag de regime nunca é utilizada para explicar
/// uma leitura passada da tag analisada.
/// </summary>
public sealed class OperationalRegimeHistoryResolver
{
    public OperationalRegimeHistoryResolution Resolve(
        TagRegimeOperacionalIa configuration,
        IReadOnlyList<HistoricoDetectorPoint> targetHistory,
        IReadOnlyList<HistoricoDetectorPoint> regimeHistory,
        DateTime currentTimestamp)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(targetHistory);
        ArgumentNullException.ThrowIfNull(regimeHistory);

        var configurationError =
            ValidateConfiguration(configuration);

        if (configurationError is not null)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                configurationError);
        }

        var targetOrdered =
            targetHistory
                .Where(IsValidPoint)
                .OrderBy(x => x.DataHora)
                .ToArray();

        var regimeOrdered =
            regimeHistory
                .Where(IsValidPoint)
                .OrderBy(x => x.DataHora)
                .ToArray();

        if (targetOrdered.Length == 0)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "O histórico da tag analisada não possui pontos válidos.");
        }

        if (regimeOrdered.Length == 0)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "O histórico da tag de regime não possui pontos válidos.");
        }

        var expectedCurrentRegimeTimestamp =
            currentTimestamp.AddMinutes(
                -configuration.LagMinutos);

        var currentRegimePoint =
            FindCausalRegimePoint(
                regimeOrdered,
                expectedCurrentRegimeTimestamp,
                configuration.ToleranciaAlinhamentoMinutos);

        if (currentRegimePoint is null)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "Não foi possível determinar o regime operacional atual " +
                "dentro da tolerância temporal configurada.");
        }

        var currentRegimeValue =
            currentRegimePoint.Valor;

        var selectedHistory =
            new List<HistoricoDetectorPoint>(
                targetOrdered.Length);

        var alignedPoints = 0;

        /*
         * Busca com dois ponteiros (RC-3).
         *
         * targetOrdered está em ordem crescente e LagMinutos é fixo,
         * então os instantes esperados também são crescentes. O ponteiro
         * da série de regime, portanto, só avança.
         *
         * regimeCursor = quantidade de pontos de regime com
         * DataHora <= instante esperado. O candidato é o último deles
         * (regimeOrdered[regimeCursor - 1]), exatamente o mesmo ponto que a
         * busca linear de FindCausalRegimePoint retorna, inclusive com
         * timestamps repetidos (OrderBy é estável).
         *
         * Custo: O(n + m) em vez de O(n · m).
         */
        var regimeCursor = 0;

        foreach (var targetPoint in targetOrdered)
        {
            /*
             * Para cada ponto histórico da tag analisada, buscamos
             * o estado operacional causal correspondente.
             *
             * Exemplo:
             *
             * INLET em 10:01
             * Lag = 1 minuto
             *
             * estado esperado:
             * PUMP_RUNNING em 10:00.
             */
            var expectedRegimeTimestamp =
                targetPoint.DataHora.AddMinutes(
                    -configuration.LagMinutos);

            while (regimeCursor < regimeOrdered.Length &&
                   regimeOrdered[regimeCursor].DataHora <=
                   expectedRegimeTimestamp)
            {
                regimeCursor++;
            }

            var regimePoint =
                regimeCursor == 0
                    ? null
                    : AcceptWithinTolerance(
                        regimeOrdered[regimeCursor - 1],
                        expectedRegimeTimestamp,
                        configuration.ToleranciaAlinhamentoMinutos);

            if (regimePoint is null)
            {
                continue;
            }

            alignedPoints++;

            if (BelongsToSameState(
                    regimePoint.Valor,
                    currentRegimeValue,
                    configuration.ToleranciaEstado))
            {
                selectedHistory.Add(
                    targetPoint);
            }
        }

        if (selectedHistory.Count <
            configuration.MinimoAmostrasPorRegime)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "Histórico insuficiente para o regime operacional atual.",
                currentRegimeValue,
                targetOrdered.Length,
                alignedPoints,
                selectedHistory.Count);
        }

        return OperationalRegimeHistoryResolution.CreateApplied(
            currentRegimeValue,
            targetOrdered.Length,
            alignedPoints,
            selectedHistory);
    }

    /// <summary>
    /// Regime combinado com N componentes (RC-6, PLANO_REGIME_COMBINADO.md §6.3).
    ///
    /// Componente 1 = <see cref="TagRegimeOperacionalIa.TagRegime"/>, com o mapa
    /// <see cref="TagRegimeOperacionalIa.MapaEstados"/> e a
    /// <see cref="TagRegimeOperacionalIa.ToleranciaEstado"/> da configuração.
    /// Componentes 2..N = <paramref name="additionalComponents"/>, em ordem
    /// crescente de <see cref="TagRegimeComponenteIa.Ordem"/>.
    /// <paramref name="regimeHistories"/>[0] é a série do componente 1 e
    /// <paramref name="regimeHistories"/>[k] a do componente k + 1.
    ///
    /// Regras:
    /// - LagMinutos e ToleranciaAlinhamentoMinutos da configuração valem para
    ///   todos os componentes (recomendação de §6.2, aprovada em 13/11/2026);
    /// - cada componente tem um ponteiro que só avança (técnica da RC-3) e usa
    ///   a mesma regra causal do caminho atual;
    /// - ponto histórico só é alinhado se <b>todos</b> os componentes tiverem
    ///   leitura dentro da tolerância; senão é descartado;
    /// - estado atual: todos os componentes precisam existir, senão "não aplicado";
    /// - componente com mapa: a leitura é mapeada (entrada casa com a
    ///   ToleranciaEstado do componente); dois valores mapeados (entrada ou
    ///   <c>*</c>) são comparados exatos (decisões de §6.2);
    /// - valor fora do mapa e sem <c>*</c> passa sem mudança e, se algum dos
    ///   lados estiver fora do mapa, compara com |Δ| ≤ ToleranciaEstado
    ///   (decisão de 13/11/2026, RC-6);
    /// - componente sem mapa: compara a leitura com |Δ| ≤ ToleranciaEstado,
    ///   como o caminho atual.
    ///
    /// Com 1 componente sem mapa, o resultado é igual ao de
    /// <see cref="Resolve(TagRegimeOperacionalIa, IReadOnlyList{HistoricoDetectorPoint}, IReadOnlyList{HistoricoDetectorPoint}, DateTime)"/>
    /// (provado por teste contra o oráculo da RC-3); a única diferença é
    /// <see cref="OperationalRegimeHistoryResolution.CurrentRegimeKey"/>, que
    /// aqui é preenchida quando o estado atual é determinado.
    ///
    /// <see cref="OperationalRegimeHistoryResolution.CurrentRegimeValue"/> é a
    /// leitura (sem mapa) do componente 1; a chave leva os valores mapeados.
    ///
    /// Nada no runtime chama esta sobrecarga ainda (só a partir da RC-8,
    /// atrás de AnomalyDetection:RegimeCombinado:Habilitado).
    /// </summary>
    public OperationalRegimeHistoryResolution Resolve(
        TagRegimeOperacionalIa configuration,
        IReadOnlyList<TagRegimeComponenteIa> additionalComponents,
        IReadOnlyList<HistoricoDetectorPoint> targetHistory,
        IReadOnlyList<IReadOnlyList<HistoricoDetectorPoint>> regimeHistories,
        DateTime currentTimestamp)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(additionalComponents);
        ArgumentNullException.ThrowIfNull(targetHistory);
        ArgumentNullException.ThrowIfNull(regimeHistories);

        if (regimeHistories.Count != additionalComponents.Count + 1)
        {
            throw new ArgumentException(
                $"São esperadas {additionalComponents.Count + 1} séries de regime " +
                $"(componente 1 + {additionalComponents.Count} adicionais); " +
                $"recebidas {regimeHistories.Count}.",
                nameof(regimeHistories));
        }

        for (var i = 0; i < regimeHistories.Count; i++)
        {
            if (regimeHistories[i] is null)
            {
                throw new ArgumentException(
                    $"Série de regime do componente {i + 1} é nula.",
                    nameof(regimeHistories));
            }
        }

        var configurationError =
            ValidateConfiguration(configuration);

        if (configurationError is not null)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                configurationError);
        }

        var componentCount =
            regimeHistories.Count;

        var specs =
            new ComponentSpec[componentCount];

        var componentError =
            BuildComponentSpecs(
                configuration,
                additionalComponents,
                specs);

        if (componentError is not null)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                componentError);
        }

        var targetOrdered =
            targetHistory
                .Where(IsValidPoint)
                .OrderBy(x => x.DataHora)
                .ToArray();

        if (targetOrdered.Length == 0)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "O histórico da tag analisada não possui pontos válidos.");
        }

        var regimeOrdered =
            new HistoricoDetectorPoint[componentCount][];

        for (var k = 0; k < componentCount; k++)
        {
            regimeOrdered[k] =
                regimeHistories[k]
                    .Where(IsValidPoint)
                    .OrderBy(x => x.DataHora)
                    .ToArray();

            if (regimeOrdered[k].Length == 0)
            {
                return OperationalRegimeHistoryResolution.CreateNotApplied(
                    "O histórico da tag de regime não possui pontos válidos." +
                    ComponentSuffix(specs, k));
            }
        }

        var expectedCurrentRegimeTimestamp =
            currentTimestamp.AddMinutes(
                -configuration.LagMinutos);

        var currentRaw =
            new double[componentCount];

        var currentState =
            new OperationalRegimeStateMapping[componentCount];

        for (var k = 0; k < componentCount; k++)
        {
            var currentPoint =
                FindCausalRegimePoint(
                    regimeOrdered[k],
                    expectedCurrentRegimeTimestamp,
                    configuration.ToleranciaAlinhamentoMinutos);

            if (currentPoint is null)
            {
                return OperationalRegimeHistoryResolution.CreateNotApplied(
                    "Não foi possível determinar o regime operacional atual " +
                    "dentro da tolerância temporal configurada." +
                    ComponentSuffix(specs, k));
            }

            currentRaw[k] =
                currentPoint.Valor;

            currentState[k] =
                specs[k].State(currentPoint.Valor);
        }

        var currentRegimeValue =
            currentRaw[0];

        var keyComponents =
            new OperationalRegimeKeyComponent[componentCount];

        for (var k = 0; k < componentCount; k++)
        {
            keyComponents[k] =
                new OperationalRegimeKeyComponent(
                    specs[k].TagName,
                    currentState[k].MappedValue);
        }

        var currentRegimeKey =
            OperationalRegimeKey.Build(
                keyComponents);

        var selectedHistory =
            new List<HistoricoDetectorPoint>(
                targetOrdered.Length);

        var alignedPoints = 0;

        // Um ponteiro por componente (RC-3): quantidade de pontos daquele
        // componente com DataHora <= instante esperado. Só avança.
        var cursors =
            new int[componentCount];

        var regimePoints =
            new HistoricoDetectorPoint[componentCount];

        foreach (var targetPoint in targetOrdered)
        {
            var expectedRegimeTimestamp =
                targetPoint.DataHora.AddMinutes(
                    -configuration.LagMinutos);

            var allAligned = true;

            for (var k = 0; k < componentCount; k++)
            {
                var series =
                    regimeOrdered[k];

                var cursor =
                    cursors[k];

                while (cursor < series.Length &&
                       series[cursor].DataHora <=
                       expectedRegimeTimestamp)
                {
                    cursor++;
                }

                cursors[k] =
                    cursor;

                var regimePoint =
                    cursor == 0
                        ? null
                        : AcceptWithinTolerance(
                            series[cursor - 1],
                            expectedRegimeTimestamp,
                            configuration.ToleranciaAlinhamentoMinutos);

                if (regimePoint is null)
                {
                    // Componente sem leitura na tolerância: ponto descartado
                    // (não conta como alinhado). Os ponteiros que não
                    // avançaram aqui alcançam o próximo instante na volta
                    // seguinte, porque os instantes esperados só crescem.
                    allAligned = false;
                    break;
                }

                regimePoints[k] =
                    regimePoint;
            }

            if (!allAligned)
            {
                continue;
            }

            alignedPoints++;

            var sameState = true;

            for (var k = 0; k < componentCount; k++)
            {
                if (!specs[k].SameState(
                        regimePoints[k].Valor,
                        currentState[k]))
                {
                    sameState = false;
                    break;
                }
            }

            if (sameState)
            {
                selectedHistory.Add(
                    targetPoint);
            }
        }

        if (selectedHistory.Count <
            configuration.MinimoAmostrasPorRegime)
        {
            return OperationalRegimeHistoryResolution.CreateNotApplied(
                "Histórico insuficiente para o regime operacional atual.",
                currentRegimeValue,
                targetOrdered.Length,
                alignedPoints,
                selectedHistory.Count,
                currentRegimeKey);
        }

        return OperationalRegimeHistoryResolution.CreateApplied(
            currentRegimeValue,
            targetOrdered.Length,
            alignedPoints,
            selectedHistory,
            currentRegimeKey);
    }

    /// <summary>
    /// Validações da configuração comuns às duas formas de Resolve
    /// (mesmas mensagens e mesma ordem do caminho atual).
    /// Retorna o motivo de "não aplicado" ou nulo se a configuração é válida.
    /// </summary>
    private static string? ValidateConfiguration(
        TagRegimeOperacionalIa configuration)
    {
        if (!configuration.Ativo)
        {
            return "A configuração de regime operacional está desativada.";
        }

        if (configuration.LagMinutos < 0)
        {
            return "LagMinutos não pode ser negativo.";
        }

        if (configuration.ToleranciaAlinhamentoMinutos < 0)
        {
            return "ToleranciaAlinhamentoMinutos não pode ser negativa.";
        }

        if (!double.IsFinite(configuration.ToleranciaEstado) ||
            configuration.ToleranciaEstado < 0)
        {
            return "ToleranciaEstado deve ser um número finito maior ou igual a zero.";
        }

        if (configuration.MinimoAmostrasPorRegime <= 0)
        {
            return "MinimoAmostrasPorRegime deve ser maior que zero.";
        }

        return null;
    }

    /// <summary>
    /// Monta a descrição de cada componente (tag, mapa, tolerância) e valida
    /// os componentes 2..N: Ordem ≥ 2 e estritamente crescente, tag não vazia
    /// e sem separadores da chave, tags distintas, ToleranciaEstado finita e
    /// ≥ 0, mapa com sintaxe válida. Retorna o motivo de "não aplicado" ou nulo.
    /// </summary>
    private static string? BuildComponentSpecs(
        TagRegimeOperacionalIa configuration,
        IReadOnlyList<TagRegimeComponenteIa> additionalComponents,
        ComponentSpec[] specs)
    {
        var seenTags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var previousOrder = 1;

        for (var k = 0; k < specs.Length; k++)
        {
            string? tag;
            string? mapText;
            double tolerance;

            if (k == 0)
            {
                tag = configuration.TagRegime;
                mapText = configuration.MapaEstados;
                tolerance = configuration.ToleranciaEstado;
            }
            else
            {
                var component =
                    additionalComponents[k - 1];

                if (component is null)
                {
                    return $"Componente {k + 1} do regime combinado é nulo.";
                }

                if (component.Ordem <= previousOrder)
                {
                    return
                        $"Componente {k + 1} do regime combinado com Ordem {component.Ordem}: " +
                        "a Ordem deve começar em 2 e ser estritamente crescente.";
                }

                previousOrder = component.Ordem;
                tag = component.TagRegime;
                mapText = component.MapaEstados;
                tolerance = component.ToleranciaEstado;

                if (!double.IsFinite(tolerance) || tolerance < 0)
                {
                    return
                        $"Componente {k + 1} do regime combinado ({tag}): " +
                        "ToleranciaEstado deve ser um número finito maior ou igual a zero.";
                }
            }

            var trimmedTag =
                tag?.Trim();

            if (string.IsNullOrEmpty(trimmedTag))
            {
                return $"Componente {k + 1} do regime combinado sem tag de regime.";
            }

            if (trimmedTag.Contains(OperationalRegimeKey.ComponentSeparator) ||
                trimmedTag.Contains(OperationalRegimeKey.ValueSeparator))
            {
                return
                    $"Componente {k + 1} do regime combinado ({trimmedTag}) contém " +
                    $"'{OperationalRegimeKey.ComponentSeparator}' ou " +
                    $"'{OperationalRegimeKey.ValueSeparator}'.";
            }

            if (!seenTags.Add(trimmedTag))
            {
                return $"Tag {trimmedTag} repetida nos componentes do regime combinado.";
            }

            // MapaEstados nulo ou só com espaços = componente sem mapa.
            OperationalRegimeStateMap? map = null;

            if (!string.IsNullOrWhiteSpace(mapText) &&
                !OperationalRegimeStateMap.TryParse(mapText, out map, out var mapError))
            {
                return
                    $"Componente {k + 1} do regime combinado ({trimmedTag}): " +
                    $"MapaEstados inválido. {mapError}";
            }

            specs[k] =
                new ComponentSpec(
                    trimmedTag,
                    map,
                    tolerance);
        }

        return null;
    }

    private static string ComponentSuffix(
        ComponentSpec[] specs,
        int index)
    {
        // Com 1 componente, a mensagem é a do caminho atual.
        return specs.Length == 1
            ? ""
            : $" Componente {index + 1} ({specs[index].TagName}).";
    }

    private sealed class ComponentSpec
    {
        public ComponentSpec(
            string tagName,
            OperationalRegimeStateMap? map,
            double tolerance)
        {
            TagName = tagName;
            Map = map;
            Tolerance = tolerance;
        }

        public string TagName { get; }

        public OperationalRegimeStateMap? Map { get; }

        public double Tolerance { get; }

        /// <summary>
        /// Estado de uma leitura: mapeada, ou sem mudança (OutOfMap) quando
        /// o componente não tem mapa ou a leitura não casou com o mapa.
        /// </summary>
        public OperationalRegimeStateMapping State(
            double value)
        {
            return Map is null
                ? new OperationalRegimeStateMapping(
                    value,
                    value,
                    OperationalRegimeStateMapStatus.OutOfMap)
                : Map.Apply(value, Tolerance);
        }

        /// <summary>
        /// Decisões de §6.2 (RC-6, 13/11/2026):
        /// - os dois lados mapeados (entrada ou <c>*</c>): compara exato;
        /// - algum lado sem mapeamento (componente sem mapa, ou valor fora do
        ///   mapa e sem <c>*</c>): |Δ| ≤ ToleranciaEstado, como o caminho atual.
        /// Sem mapa, os dois lados são OutOfMap com o valor lido, então o
        /// teste é exatamente o do caminho atual.
        /// </summary>
        public bool SameState(
            double historicalValue,
            OperationalRegimeStateMapping current)
        {
            var historical =
                State(historicalValue);

            if (historical.Status != OperationalRegimeStateMapStatus.OutOfMap &&
                current.Status != OperationalRegimeStateMapStatus.OutOfMap)
            {
                return historical.MappedValue ==
                       current.MappedValue;
            }

            return BelongsToSameState(
                historical.MappedValue,
                current.MappedValue,
                Tolerance);
        }
    }

    /// <summary>
    /// Retorna a leitura mais recente da tag de regime que:
    ///
    /// 1. não ocorre depois do instante esperado;
    /// 2. não é mais antiga que a tolerância configurada.
    ///
    /// Dessa forma o algoritmo não utiliza informação futura.
    /// </summary>
    private static HistoricoDetectorPoint? FindCausalRegimePoint(
        IReadOnlyList<HistoricoDetectorPoint> regimeHistory,
        DateTime expectedTimestamp,
        int toleranceMinutes)
    {
        HistoricoDetectorPoint? candidate =
            null;

        for (var index = 0;
             index < regimeHistory.Count;
             index++)
        {
            var point =
                regimeHistory[index];

            if (point.DataHora >
                expectedTimestamp)
            {
                break;
            }

            candidate =
                point;
        }

        if (candidate is null)
        {
            return null;
        }

        return AcceptWithinTolerance(
            candidate,
            expectedTimestamp,
            toleranceMinutes);
    }

    /// <summary>
    /// Testes de idade do candidato causal, comuns à busca linear
    /// (ponto atual) e à busca com ponteiro (laço do histórico).
    /// </summary>
    private static HistoricoDetectorPoint? AcceptWithinTolerance(
        HistoricoDetectorPoint candidate,
        DateTime expectedTimestamp,
        int toleranceMinutes)
    {
        var age =
            expectedTimestamp -
            candidate.DataHora;

        if (age < TimeSpan.Zero)
        {
            return null;
        }

        if (age >
            TimeSpan.FromMinutes(
                toleranceMinutes))
        {
            return null;
        }

        return candidate;
    }

    private static bool BelongsToSameState(
        double historicalValue,
        double currentValue,
        double tolerance)
    {
        if (!double.IsFinite(historicalValue) ||
            !double.IsFinite(currentValue))
        {
            return false;
        }

        return Math.Abs(
                   historicalValue -
                   currentValue)
               <= tolerance;
    }

    private static bool IsValidPoint(
        HistoricoDetectorPoint point)
    {
        return point is not null &&
               point.DataHora != default &&
               double.IsFinite(point.Valor);
    }
}

/// <summary>
/// Resultado da seleção do histórico condicionado pelo regime operacional.
///
/// Applied = false significa fallback:
/// o chamador deve continuar utilizando o histórico/baseline global existente.
/// </summary>
public sealed class OperationalRegimeHistoryResolution
{
    public bool Applied { get; init; }

    public double? CurrentRegimeValue { get; init; }

    public int OriginalTargetSamples { get; init; }

    public int AlignedSamples { get; init; }

    public int SelectedSamples { get; init; }

    public IReadOnlyList<HistoricoDetectorPoint> History { get; init; } =
        Array.Empty<HistoricoDetectorPoint>();

    public string Reason { get; init; } = "";

    /// <summary>
    /// Chave do regime atual (ex.: <c>TAG42=2|TAG43=0|TAG46=380</c>), só na
    /// sobrecarga com componentes (RC-6) e só quando o estado atual foi
    /// determinado. Nula no caminho atual (um componente, assinatura antiga).
    /// </summary>
    public string? CurrentRegimeKey { get; init; }

    public static OperationalRegimeHistoryResolution CreateApplied(
        double currentRegimeValue,
        int originalTargetSamples,
        int alignedSamples,
        IReadOnlyList<HistoricoDetectorPoint> history,
        string? currentRegimeKey = null)
    {
        return new OperationalRegimeHistoryResolution
        {
            Applied = true,

            CurrentRegimeValue =
                currentRegimeValue,

            CurrentRegimeKey =
                currentRegimeKey,

            OriginalTargetSamples =
                originalTargetSamples,

            AlignedSamples =
                alignedSamples,

            SelectedSamples =
                history.Count,

            History =
                history,

            Reason =
                "Baseline condicionado por regime operacional aplicado."
        };
    }

    public static OperationalRegimeHistoryResolution CreateNotApplied(
        string reason,
        double? currentRegimeValue = null,
        int originalTargetSamples = 0,
        int alignedSamples = 0,
        int selectedSamples = 0,
        string? currentRegimeKey = null)
    {
        return new OperationalRegimeHistoryResolution
        {
            Applied = false,

            CurrentRegimeValue =
                currentRegimeValue,

            CurrentRegimeKey =
                currentRegimeKey,

            OriginalTargetSamples =
                originalTargetSamples,

            AlignedSamples =
                alignedSamples,

            SelectedSamples =
                selectedSamples,

            History =
                Array.Empty<HistoricoDetectorPoint>(),

            Reason =
                reason
        };
    }
}