using IAApi.Services.AnomalyDetection;
using Xunit;

public partial class OperationalRegimeHistoryResolverTests
{
    [Fact]
    public void Resolve_SelectsOnlyHistoryFromCurrentOffRegime()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var configuration =
            CreateConfiguration(
                minimumSamples: 1);

        var targetHistory =
            CreateTargetHistory();

        var regimeHistory =
            CreateRegimeHistory();

        /*
         * Leitura atual: 00:07
         * Lag: 1 minuto
         *
         * Estado esperado:
         * 00:06.
         *
         * Como não existe exatamente 00:06,
         * a leitura causal mais recente é 00:05 = 0.
         *
         * Portanto o regime atual é OFF.
         */
        var currentTimestamp =
            Utc(0, 7);

        var result =
            resolver.Resolve(
                configuration,
                targetHistory,
                regimeHistory,
                currentTimestamp);

        Assert.True(result.Applied);

        Assert.NotNull(
            result.CurrentRegimeValue);

        Assert.Equal(
            0,
            result.CurrentRegimeValue!.Value,
            3);

        Assert.Equal(
            6,
            result.OriginalTargetSamples);

        Assert.Equal(
            6,
            result.AlignedSamples);

        Assert.Equal(
            4,
            result.SelectedSamples);

        var values =
            result.History
                .Select(x => x.Valor)
                .ToArray();

        Assert.Equal(
            new[]
            {
                0.05,
                0.04,
                0.03,
                0.02
            },
            values);
    }

    [Fact]
    public void Resolve_SelectsOnlyHistoryFromCurrentOnRegime()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var configuration =
            CreateConfiguration(
                minimumSamples: 1);

        var targetHistory =
            CreateTargetHistory();

        var regimeHistory =
            CreateRegimeHistory();

        /*
         * Leitura atual: 00:04
         * Lag: 1 minuto
         *
         * Estado esperado:
         * 00:03 = 1.
         *
         * Portanto o regime atual é ON.
         */
        var currentTimestamp =
            Utc(0, 4);

        var result =
            resolver.Resolve(
                configuration,
                targetHistory,
                regimeHistory,
                currentTimestamp);

        Assert.True(result.Applied);

        Assert.NotNull(
            result.CurrentRegimeValue);

        Assert.Equal(
            1,
            result.CurrentRegimeValue!.Value,
            3);

        Assert.Equal(
            2,
            result.SelectedSamples);

        var values =
            result.History
                .Select(x => x.Valor)
                .ToArray();

        Assert.Equal(
            new[]
            {
                21.50,
                21.60
            },
            values);
    }

    [Fact]
    public void Resolve_NeverUsesFutureRegimeReading()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var configuration =
            CreateConfiguration(
                minimumSamples: 1);

        var targetHistory =
            new[]
            {
                Point(
                    0,
                    1,
                    0.05)
            };

        /*
         * Para INLET em 00:01 e Lag=1,
         * precisamos explicar o valor usando o estado em 00:00.
         *
         * Temos:
         *
         * 23:59 = OFF
         * 00:01 = ON
         *
         * A leitura de 00:01 é futura em relação ao instante esperado
         * 00:00 e nunca pode ser usada.
         *
         * Logo o resolver deve selecionar o estado OFF de 23:59.
         */
        var regimeHistory =
            new[]
            {
                new HistoricoDetectorPoint
                {
                    DataHora =
                        new DateTime(
                            2026,
                            9,
                            10,
                            23,
                            59,
                            0,
                            DateTimeKind.Utc),

                    Valor =
                        0
                },

                Point(
                    0,
                    1,
                    1)
            };

        var currentTimestamp =
            Utc(0, 1);

        var result =
            resolver.Resolve(
                configuration,
                targetHistory,
                regimeHistory,
                currentTimestamp);

        Assert.True(result.Applied);

        Assert.NotNull(
            result.CurrentRegimeValue);

        Assert.Equal(
            0,
            result.CurrentRegimeValue!.Value,
            3);

        Assert.Single(
            result.History);

        Assert.Equal(
            0.05,
            result.History[0].Valor,
            3);
    }

    [Fact]
    public void Resolve_FallsBackWhenCurrentRegimeHasInsufficientHistory()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        /*
         * Nosso histórico contém quatro amostras OFF.
         * Exigimos cinco.
         *
         * Portanto o baseline condicionado NÃO deve ser aplicado.
         */
        var configuration =
            CreateConfiguration(
                minimumSamples: 5);

        var result =
            resolver.Resolve(
                configuration,
                CreateTargetHistory(),
                CreateRegimeHistory(),
                Utc(0, 7));

        Assert.False(
            result.Applied);

        Assert.Equal(
            4,
            result.SelectedSamples);

        Assert.Empty(
            result.History);

        Assert.Contains(
            "Histórico insuficiente",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_FallsBackWhenConfigurationIsDisabled()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var configuration =
            CreateConfiguration(
                minimumSamples: 1);

        configuration.Ativo =
            false;

        var result =
            resolver.Resolve(
                configuration,
                CreateTargetHistory(),
                CreateRegimeHistory(),
                Utc(0, 7));

        Assert.False(
            result.Applied);

        Assert.Empty(
            result.History);

        Assert.Contains(
            "desativada",
            result.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    private static TagRegimeOperacionalIa CreateConfiguration(
        int minimumSamples)
    {
        return new TagRegimeOperacionalIa
        {
            ClienteId =
                "TEST_CLIENT",

            TagName =
                "TEST.INLET_FLOW",

            TagRegime =
                "TEST.PUMP_RUNNING",

            Ativo =
                true,

            LagMinutos =
                1,

            ToleranciaAlinhamentoMinutos =
                2,

            ToleranciaEstado =
                0.001,

            MinimoAmostrasPorRegime =
                minimumSamples
        };
    }

    private static IReadOnlyList<HistoricoDetectorPoint>
        CreateTargetHistory()
    {
        /*
         * Associação com Lag = 1:
         *
         * Target 00:01 -> Regime 00:00 = OFF
         * Target 00:02 -> Regime 00:01 = OFF
         * Target 00:03 -> Regime 00:02 = ON
         * Target 00:04 -> Regime 00:03 = ON
         * Target 00:05 -> Regime 00:04 = OFF
         * Target 00:06 -> Regime 00:05 = OFF
         */
        return new[]
        {
            Point(0, 1, 0.05),
            Point(0, 2, 0.04),

            Point(0, 3, 21.50),
            Point(0, 4, 21.60),

            Point(0, 5, 0.03),
            Point(0, 6, 0.02)
        };
    }

    private static IReadOnlyList<HistoricoDetectorPoint>
        CreateRegimeHistory()
    {
        return new[]
        {
            Point(0, 0, 0),
            Point(0, 1, 0),

            Point(0, 2, 1),
            Point(0, 3, 1),

            Point(0, 4, 0),
            Point(0, 5, 0)
        };
    }

    private static HistoricoDetectorPoint Point(
        int hour,
        int minute,
        double value)
    {
        return new HistoricoDetectorPoint
        {
            DataHora =
                Utc(
                    hour,
                    minute),

            Valor =
                value
        };
    }

    private static DateTime Utc(
        int hour,
        int minute)
    {
        return new DateTime(
            2026,
            9,
            11,
            hour,
            minute,
            0,
            DateTimeKind.Utc);
    }

    // ------------------------------------------------------------------
    // RC-3: equivalência da busca com dois ponteiros × busca linear
    // ------------------------------------------------------------------

    [Fact]
    public void Resolve_TwoPointerSearch_MatchesLinearOracle_OnRandomSeries()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var applied = 0;
        var notApplied = 0;
        var casesWithUnalignedPoints = 0;
        var casesWithDuplicates = 0;

        for (var seed = 1; seed <= 3000; seed++)
        {
            var scenario =
                RandomScenario.Create(seed);

            var expected =
                LinearOracle.Resolve(
                    scenario.Configuration,
                    scenario.Target,
                    scenario.Regime,
                    scenario.CurrentTimestamp);

            var actual =
                resolver.Resolve(
                    scenario.Configuration,
                    scenario.Target,
                    scenario.Regime,
                    scenario.CurrentTimestamp);

            AssertSameResolution(
                expected,
                actual,
                seed);

            if (actual.Applied)
            {
                applied++;
            }
            else
            {
                notApplied++;
            }

            if (expected.OriginalTargetSamples >
                expected.AlignedSamples)
            {
                casesWithUnalignedPoints++;
            }

            if (scenario.HasDuplicateTimestamps)
            {
                casesWithDuplicates++;
            }
        }

        // O teste só vale se as séries cobrirem os dois desfechos e os
        // casos difíceis (pontos sem alinhamento, timestamps repetidos).
        Assert.True(applied > 300, $"applied = {applied}");
        Assert.True(notApplied > 300, $"notApplied = {notApplied}");
        Assert.True(casesWithUnalignedPoints > 300, $"unaligned = {casesWithUnalignedPoints}");
        Assert.True(casesWithDuplicates > 300, $"duplicates = {casesWithDuplicates}");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 5)]
    public void Resolve_TwoPointerSearch_MatchesLinearOracle_WithRepeatedTimestamps(
        int lagMinutes,
        int toleranceMinutes)
    {
        // Série de regime com timestamps repetidos e valores diferentes
        // no mesmo instante: a regra é "o último na ordem do OrderBy".
        var regime = new[]
        {
            Point(0, 0, 0),
            Point(0, 0, 1),
            Point(0, 2, 1),
            Point(0, 2, 0),
            Point(0, 2, 1),
            Point(0, 5, 0),
            Point(0, 9, 1),
            Point(0, 9, 0)
        };

        var target = Enumerable.Range(0, 15)
            .SelectMany(minute => new[]
            {
                Point(0, minute, minute),
                Point(0, minute, minute + 0.5)
            })
            .Reverse()
            .ToArray();

        var configuration =
            CreateConfiguration(
                minimumSamples: 1);

        configuration.LagMinutos = lagMinutes;
        configuration.ToleranciaAlinhamentoMinutos = toleranceMinutes;

        foreach (var current in Enumerable.Range(0, 16).Select(m => Utc(0, m)))
        {
            AssertSameResolution(
                LinearOracle.Resolve(configuration, target, regime, current),
                new OperationalRegimeHistoryResolver().Resolve(configuration, target, regime, current),
                seed: -1);
        }
    }

    private static void AssertSameResolution(
        OperationalRegimeHistoryResolution expected,
        OperationalRegimeHistoryResolution actual,
        int seed)
    {
        var context = $"seed {seed}";

        Assert.True(expected.Applied == actual.Applied, context);
        Assert.True(expected.Reason == actual.Reason, context);
        Assert.True(Nullable.Equals(expected.CurrentRegimeValue, actual.CurrentRegimeValue), context);
        Assert.True(expected.OriginalTargetSamples == actual.OriginalTargetSamples, context);
        Assert.True(expected.AlignedSamples == actual.AlignedSamples, context);
        Assert.True(expected.SelectedSamples == actual.SelectedSamples, context);
        Assert.True(expected.History.Count == actual.History.Count, context);

        // Mesma lista: mesmos objetos, na mesma ordem.
        for (var i = 0; i < expected.History.Count; i++)
        {
            Assert.True(
                ReferenceEquals(expected.History[i], actual.History[i]),
                $"{context}, posição {i}");
        }
    }

    private sealed class RandomScenario
    {
        public required TagRegimeOperacionalIa Configuration { get; init; }

        public required IReadOnlyList<HistoricoDetectorPoint> Target { get; init; }

        public required IReadOnlyList<HistoricoDetectorPoint> Regime { get; init; }

        public required DateTime CurrentTimestamp { get; init; }

        public required bool HasDuplicateTimestamps { get; init; }

        private static readonly DateTime Origin =
            new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

        public static RandomScenario Create(int seed)
        {
            var random = new Random(seed);

            var configuration = new TagRegimeOperacionalIa
            {
                ClienteId = "TEST_CLIENT",
                TagName = "TEST.TARGET",
                TagRegime = "TEST.REGIME",
                Ativo = true,
                LagMinutos = random.Next(0, 6),
                ToleranciaAlinhamentoMinutos = random.Next(0, 6),
                ToleranciaEstado = random.Next(3) switch
                {
                    0 => 0,
                    1 => 0.001,
                    _ => 0.6
                },
                MinimoAmostrasPorRegime = random.Next(1, 10)
            };

            var regime = CreateSeries(
                random,
                count: random.Next(0, 120),
                startOffsetMinutes: random.Next(-30, 30),
                value: () => random.NextDouble() < 0.9
                    ? random.Next(0, 3)
                    : random.NextDouble() * 3,
                out var regimeDuplicates);

            var target = CreateSeries(
                random,
                count: random.Next(0, 150),
                startOffsetMinutes: random.Next(-10, 20),
                value: () => random.NextDouble() * 100,
                out var targetDuplicates);

            // Entrada fora de ordem: o resolver ordena.
            if (random.NextDouble() < 0.5)
            {
                Shuffle(random, regime);
            }

            if (random.NextDouble() < 0.5)
            {
                Shuffle(random, target);
            }

            var current = Origin.AddMinutes(random.Next(-10, 160))
                .AddSeconds(random.NextDouble() < 0.3 ? random.Next(1, 60) : 0);

            return new RandomScenario
            {
                Configuration = configuration,
                Target = target,
                Regime = regime,
                CurrentTimestamp = current,
                HasDuplicateTimestamps = regimeDuplicates || targetDuplicates
            };
        }

        private static List<HistoricoDetectorPoint> CreateSeries(
            Random random,
            int count,
            int startOffsetMinutes,
            Func<double> value,
            out bool hasDuplicates)
        {
            var points = new List<HistoricoDetectorPoint>(count);
            var time = Origin.AddMinutes(startOffsetMinutes);
            hasDuplicates = false;

            for (var i = 0; i < count; i++)
            {
                var step = random.NextDouble();

                if (step < 0.12 && points.Count > 0)
                {
                    // Timestamp repetido.
                    hasDuplicates = true;
                }
                else if (step < 0.20)
                {
                    // Buraco maior que qualquer tolerância usada (0–5 min).
                    time = time.AddMinutes(random.Next(6, 25));
                }
                else if (step < 0.30)
                {
                    // Passo fora da grade de minutos.
                    time = time.AddSeconds(random.Next(1, 90));
                }
                else
                {
                    time = time.AddMinutes(1);
                }

                var kind = random.Next(3) switch
                {
                    0 => DateTimeKind.Utc,
                    1 => DateTimeKind.Local,
                    _ => DateTimeKind.Unspecified
                };

                points.Add(new HistoricoDetectorPoint
                {
                    DataHora = DateTime.SpecifyKind(time, kind),
                    Valor = value()
                });

                // Pontos inválidos, que o resolver descarta.
                var invalid = random.NextDouble();

                if (invalid < 0.02)
                {
                    points.Add(new HistoricoDetectorPoint { DataHora = time, Valor = double.NaN });
                }
                else if (invalid < 0.03)
                {
                    points.Add(new HistoricoDetectorPoint { DataHora = time, Valor = double.PositiveInfinity });
                }
                else if (invalid < 0.04)
                {
                    points.Add(new HistoricoDetectorPoint { DataHora = default, Valor = 1 });
                }
                else if (invalid < 0.05)
                {
                    points.Add(null!);
                }
            }

            return points;
        }

        private static void Shuffle(
            Random random,
            List<HistoricoDetectorPoint> points)
        {
            for (var i = points.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (points[i], points[j]) = (points[j], points[i]);
            }
        }
    }

    /// <summary>
    /// Cópia literal da implementação linear de
    /// OperationalRegimeHistoryResolver.Resolve anterior à RC-3
    /// (commit b8f7eab). Serve de oráculo: não alterar.
    /// </summary>
    private static class LinearOracle
    {
        public static OperationalRegimeHistoryResolution Resolve(
            TagRegimeOperacionalIa configuration,
            IReadOnlyList<HistoricoDetectorPoint> targetHistory,
            IReadOnlyList<HistoricoDetectorPoint> regimeHistory,
            DateTime currentTimestamp)
        {
            if (!configuration.Ativo)
            {
                return OperationalRegimeHistoryResolution.CreateNotApplied(
                    "A configuração de regime operacional está desativada.");
            }

            if (configuration.LagMinutos < 0)
            {
                return OperationalRegimeHistoryResolution.CreateNotApplied(
                    "LagMinutos não pode ser negativo.");
            }

            if (configuration.ToleranciaAlinhamentoMinutos < 0)
            {
                return OperationalRegimeHistoryResolution.CreateNotApplied(
                    "ToleranciaAlinhamentoMinutos não pode ser negativa.");
            }

            if (!double.IsFinite(configuration.ToleranciaEstado) ||
                configuration.ToleranciaEstado < 0)
            {
                return OperationalRegimeHistoryResolution.CreateNotApplied(
                    "ToleranciaEstado deve ser um número finito maior ou igual a zero.");
            }

            if (configuration.MinimoAmostrasPorRegime <= 0)
            {
                return OperationalRegimeHistoryResolution.CreateNotApplied(
                    "MinimoAmostrasPorRegime deve ser maior que zero.");
            }

            var targetOrdered = targetHistory.Where(IsValidPoint).OrderBy(x => x.DataHora).ToArray();
            var regimeOrdered = regimeHistory.Where(IsValidPoint).OrderBy(x => x.DataHora).ToArray();

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

            var currentRegimePoint = FindCausalRegimePoint(
                regimeOrdered,
                currentTimestamp.AddMinutes(-configuration.LagMinutos),
                configuration.ToleranciaAlinhamentoMinutos);

            if (currentRegimePoint is null)
            {
                return OperationalRegimeHistoryResolution.CreateNotApplied(
                    "Não foi possível determinar o regime operacional atual " +
                    "dentro da tolerância temporal configurada.");
            }

            var currentRegimeValue = currentRegimePoint.Valor;
            var selectedHistory = new List<HistoricoDetectorPoint>(targetOrdered.Length);
            var alignedPoints = 0;

            foreach (var targetPoint in targetOrdered)
            {
                var regimePoint = FindCausalRegimePoint(
                    regimeOrdered,
                    targetPoint.DataHora.AddMinutes(-configuration.LagMinutos),
                    configuration.ToleranciaAlinhamentoMinutos);

                if (regimePoint is null)
                {
                    continue;
                }

                alignedPoints++;

                if (BelongsToSameState(regimePoint.Valor, currentRegimeValue, configuration.ToleranciaEstado))
                {
                    selectedHistory.Add(targetPoint);
                }
            }

            if (selectedHistory.Count < configuration.MinimoAmostrasPorRegime)
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

        private static HistoricoDetectorPoint? FindCausalRegimePoint(
            IReadOnlyList<HistoricoDetectorPoint> regimeHistory,
            DateTime expectedTimestamp,
            int toleranceMinutes)
        {
            HistoricoDetectorPoint? candidate = null;

            for (var index = 0; index < regimeHistory.Count; index++)
            {
                var point = regimeHistory[index];

                if (point.DataHora > expectedTimestamp)
                {
                    break;
                }

                candidate = point;
            }

            if (candidate is null)
            {
                return null;
            }

            var age = expectedTimestamp - candidate.DataHora;

            if (age < TimeSpan.Zero)
            {
                return null;
            }

            if (age > TimeSpan.FromMinutes(toleranceMinutes))
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
            if (!double.IsFinite(historicalValue) || !double.IsFinite(currentValue))
            {
                return false;
            }

            return Math.Abs(historicalValue - currentValue) <= tolerance;
        }

        private static bool IsValidPoint(HistoricoDetectorPoint point)
        {
            return point is not null &&
                   point.DataHora != default &&
                   double.IsFinite(point.Valor);
        }
    }
}