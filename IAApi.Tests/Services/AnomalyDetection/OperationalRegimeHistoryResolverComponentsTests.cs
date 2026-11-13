using IAApi.Services.AnomalyDetection;
using Xunit;

/// <summary>
/// RC-6 (PLANO_REGIME_COMBINADO.md §6.3): sobrecarga do resolver com N
/// componentes. Parte da mesma classe dos testes da RC-3 para reaproveitar o
/// oráculo linear (<c>LinearOracle</c>) e o gerador (<c>RandomScenario</c>).
/// </summary>
public partial class OperationalRegimeHistoryResolverTests
{
    private static readonly TagRegimeComponenteIa[] NoAdditionalComponents =
        Array.Empty<TagRegimeComponenteIa>();

    // ------------------------------------------------------------------
    // 1 componente sem mapa = caminho atual (mesmo oráculo da RC-3)
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveComponents_OneComponentWithoutMap_MatchesLinearOracle_OnRandomSeries()
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

            Assert.Null(scenario.Configuration.MapaEstados);

            var expected =
                LinearOracle.Resolve(
                    scenario.Configuration,
                    scenario.Target,
                    scenario.Regime,
                    scenario.CurrentTimestamp);

            var actual =
                resolver.Resolve(
                    scenario.Configuration,
                    NoAdditionalComponents,
                    scenario.Target,
                    new[] { scenario.Regime },
                    scenario.CurrentTimestamp);

            AssertSameResolution(
                expected,
                actual,
                seed);

            AssertKeyForSingleComponent(
                actual,
                scenario.Configuration.TagRegime,
                seed);

            // O caminho atual continua sem chave.
            Assert.Null(
                resolver.Resolve(
                        scenario.Configuration,
                        scenario.Target,
                        scenario.Regime,
                        scenario.CurrentTimestamp)
                    .CurrentRegimeKey);

            if (actual.Applied)
            {
                applied++;
            }
            else
            {
                notApplied++;
            }

            if (expected.OriginalTargetSamples > expected.AlignedSamples)
            {
                casesWithUnalignedPoints++;
            }

            if (scenario.HasDuplicateTimestamps)
            {
                casesWithDuplicates++;
            }
        }

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
    public void ResolveComponents_OneComponentWithoutMap_MatchesLinearOracle_WithRepeatedTimestamps(
        int lagMinutes,
        int toleranceMinutes)
    {
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

        var resolver =
            new OperationalRegimeHistoryResolver();

        foreach (var current in Enumerable.Range(0, 16).Select(m => Utc(0, m)))
        {
            var actual =
                resolver.Resolve(configuration, NoAdditionalComponents, target, new[] { regime }, current);

            AssertSameResolution(
                LinearOracle.Resolve(configuration, target, regime, current),
                actual,
                seed: -1);

            AssertKeyForSingleComponent(actual, configuration.TagRegime, seed: -1);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveComponents_BlankMap_IsTreatedAsNoMap(
        string? map)
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        for (var seed = 1; seed <= 300; seed++)
        {
            var scenario =
                RandomScenario.Create(seed);

            scenario.Configuration.MapaEstados = map;

            AssertSameResolution(
                LinearOracle.Resolve(
                    scenario.Configuration,
                    scenario.Target,
                    scenario.Regime,
                    scenario.CurrentTimestamp),
                resolver.Resolve(
                    scenario.Configuration,
                    NoAdditionalComponents,
                    scenario.Target,
                    new[] { scenario.Regime },
                    scenario.CurrentTimestamp),
                seed);
        }
    }

    // ------------------------------------------------------------------
    // N componentes: ponteiro por componente × busca linear (oráculo)
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveComponents_MatchesLinearTupleOracle_OnRandomSeries()
    {
        var resolver =
            new OperationalRegimeHistoryResolver();

        var applied = 0;
        var notApplied = 0;
        var withMap = 0;
        var withUnaligned = 0;
        var threeComponents = 0;
        var casesWithOutOfMapTolerance = 0;

        for (var seed = 1; seed <= 3000; seed++)
        {
            var scenario =
                TupleScenario.Create(seed);

            var expected =
                TupleOracle.Resolve(scenario);

            var actual =
                resolver.Resolve(
                    scenario.Configuration,
                    scenario.Additional,
                    scenario.Target,
                    scenario.Regimes,
                    scenario.CurrentTimestamp);

            var context = $"seed {seed}";

            Assert.True(expected.Applied == actual.Applied, context);
            Assert.True(Nullable.Equals(expected.CurrentValue, actual.CurrentRegimeValue), context);
            Assert.True(expected.Key == actual.CurrentRegimeKey, $"{context}: {expected.Key} × {actual.CurrentRegimeKey}");
            Assert.True(expected.Original == actual.OriginalTargetSamples, context);
            Assert.True(expected.Aligned == actual.AlignedSamples, context);
            Assert.True(expected.Selected.Count == actual.SelectedSamples, context);

            if (actual.Applied)
            {
                Assert.True(expected.Selected.Count == actual.History.Count, context);

                for (var i = 0; i < expected.Selected.Count; i++)
                {
                    Assert.True(
                        ReferenceEquals(expected.Selected[i], actual.History[i]),
                        $"{context}, posição {i}");
                }

                applied++;
            }
            else
            {
                Assert.Empty(actual.History);
                notApplied++;
            }

            if (scenario.HasMap)
            {
                withMap++;
            }

            if (expected.Original > expected.Aligned)
            {
                withUnaligned++;
            }

            if (scenario.Regimes.Count == 3)
            {
                threeComponents++;
            }

            if (expected.OutOfMapToleranceHits > 0)
            {
                casesWithOutOfMapTolerance++;
            }
        }

        // Casos em que a decisão "fora do mapa ⇒ ToleranciaEstado" mudou o
        // resultado de pelo menos uma comparação em relação ao "exato".
        Assert.True(casesWithOutOfMapTolerance > 50, $"outOfMapTolerance = {casesWithOutOfMapTolerance}");
        Assert.True(applied > 200, $"applied = {applied}");
        Assert.True(notApplied > 200, $"notApplied = {notApplied}");
        Assert.True(withMap > 200, $"withMap = {withMap}");
        Assert.True(withUnaligned > 200, $"unaligned = {withUnaligned}");
        Assert.True(threeComponents > 200, $"threeComponents = {threeComponents}");
    }

    // ------------------------------------------------------------------
    // Unitários de §3.2
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveComponents_TwoComponents_SelectsOnlyMatchingTuple()
    {
        // min  0-4: (1,0)   5-9: (1,1)   10-14: (0,1)   15-19: (1,1)
        var c1 = Series(0, 20, m => m is >= 10 and < 15 ? 0 : 1);
        var c2 = Series(0, 20, m => m < 5 ? 0 : 1);
        var target = Series(0, 20, m => 100 + m);

        var configuration = TupleConfiguration("B01", minimumSamples: 1);
        var additional = new[] { Component(2, "B02") };

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, additional, target, new[] { c1, c2 }, Utc(0, 19));

        Assert.True(result.Applied);
        Assert.Equal("B01=1|B02=1", result.CurrentRegimeKey);
        Assert.Equal(1.0, result.CurrentRegimeValue);
        Assert.Equal(20, result.OriginalTargetSamples);
        Assert.Equal(20, result.AlignedSamples);
        Assert.Equal(
            Enumerable.Range(5, 5).Concat(Enumerable.Range(15, 5)).Select(m => 100.0 + m),
            result.History.Select(p => p.Valor));
    }

    [Fact]
    public void ResolveComponents_PumpLogic_PureTupleWithMaps_ReproducesExpectedClasses()
    {
        // Lógica das bombas (§6.2, configuração alvo da TAG29):
        // TAG42 (própria) mapa 2:2;*:0, TAG43 (outra) mapa 2:2;*:0,
        // TAG46 (set-point) mapa 380:380;*:450.
        //
        // min   TAG42  TAG43  TAG46   classe (mapeada)
        // 0-9     2      0     380    (2,0,380)
        // 10-19   2      2     380    (2,2,380)
        // 20-29   0      2     380    (0,2,380)
        // 30-39   1      3     450    (0,0,450)   <- 1 e 3 caem no curinga
        // 40-44   2      3     450    (2,0,450)
        // 45-59   2      0     400    (2,0,450)   <- 400 cai no curinga
        var tag42 = Series(0, 60, m => m switch { < 20 => 2, < 30 => 0, < 40 => 1, _ => 2 });
        var tag43 = Series(0, 60, m => m switch { < 10 => 0, < 30 => 2, < 45 => 3, _ => 0 });
        var tag46 = Series(0, 60, m => m switch { < 30 => 380, < 45 => 450, _ => 400 });
        var tag29 = Series(0, 60, m => 1000 + m);

        var configuration = TupleConfiguration("TAG42", minimumSamples: 5);
        configuration.MapaEstados = "2:2;*:0";

        var additional = new[]
        {
            Component(2, "TAG43", "2:2;*:0"),
            Component(3, "TAG46", "380:380;*:450")
        };

        var regimes = new[] { tag42, tag43, tag46 };
        var resolver = new OperationalRegimeHistoryResolver();

        // Agora em 59: (2,0,450) -> minutos 40-59.
        var at59 = resolver.Resolve(configuration, additional, tag29, regimes, Utc(0, 59));
        Assert.True(at59.Applied);
        Assert.Equal("TAG42=2|TAG43=0|TAG46=450", at59.CurrentRegimeKey);
        Assert.Equal(2.0, at59.CurrentRegimeValue);
        Assert.Equal(Minutes(40, 59), at59.History.Select(p => (int)(p.Valor - 1000)));

        // Agora em 25: (0,2,380). Tupla pura: (0,0,·) dos minutos 30-39 é outra classe.
        var at25 = resolver.Resolve(configuration, additional, tag29, regimes, Utc(0, 25));
        Assert.True(at25.Applied);
        Assert.Equal("TAG42=0|TAG43=2|TAG46=380", at25.CurrentRegimeKey);
        Assert.Equal(Minutes(20, 29), at25.History.Select(p => (int)(p.Valor - 1000)));

        // Agora em 35: (0,0,450). CurrentRegimeValue é a leitura (1), a chave leva o mapeado (0).
        var at35 = resolver.Resolve(configuration, additional, tag29, regimes, Utc(0, 35));
        Assert.True(at35.Applied);
        Assert.Equal("TAG42=0|TAG43=0|TAG46=450", at35.CurrentRegimeKey);
        Assert.Equal(1.0, at35.CurrentRegimeValue);
        Assert.Equal(Minutes(30, 39), at35.History.Select(p => (int)(p.Valor - 1000)));

        // Agora em 5: (2,0,380) -> minutos 0-9; não junta com (2,0,450).
        var at5 = resolver.Resolve(configuration, additional, tag29, regimes, Utc(0, 9));
        Assert.True(at5.Applied);
        Assert.Equal("TAG42=2|TAG43=0|TAG46=380", at5.CurrentRegimeKey);
        Assert.Equal(Minutes(0, 9), at5.History.Select(p => (int)(p.Valor - 1000)));
    }

    [Fact]
    public void ResolveComponents_MapOnSingleComponent_GroupsStates()
    {
        // Sem mapa, 1 e 0 são estados diferentes; com 2:2;*:0, são o mesmo.
        var regime = Series(0, 12, m => m switch { < 4 => 2, < 8 => 1, _ => 0 });
        var target = Series(0, 12, m => m);

        var configuration = TupleConfiguration("TAG42", minimumSamples: 1);
        configuration.MapaEstados = "2:2;*:0";

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, NoAdditionalComponents, target, new[] { regime }, Utc(0, 11));

        Assert.True(result.Applied);
        Assert.Equal("TAG42=0", result.CurrentRegimeKey);
        Assert.Equal(Minutes(4, 11), result.History.Select(p => (int)p.Valor));

        configuration.MapaEstados = null;

        var withoutMap =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, NoAdditionalComponents, target, new[] { regime }, Utc(0, 11));

        Assert.Equal(Minutes(8, 11), withoutMap.History.Select(p => (int)p.Valor));
    }

    [Fact]
    public void ResolveComponents_ValueOutOfMapWithoutWildcard_PassesUnchangedAndComparesWithTolerance()
    {
        // Decisão de 13/11/2026 (RC-6): fora do mapa e sem curinga, o valor
        // passa sem mudança e é comparado com |Δ| ≤ ToleranciaEstado, como num
        // componente sem mapa.
        // min 0-2: 2 (mapeado)   3-5: 5   6-8: 5.0005   9-11: 5.5   12-14: 0
        var regime = Series(0, 15, m => m switch { < 3 => 2, < 6 => 5, < 9 => 5.0005, < 12 => 5.5, _ => 0 });
        var target = Series(0, 15, m => m);

        var configuration = TupleConfiguration("TAG42", minimumSamples: 1);
        configuration.MapaEstados = "2:2";
        configuration.ToleranciaEstado = 0.001;

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, NoAdditionalComponents, target, new[] { regime }, Utc(0, 4));

        Assert.True(result.Applied);
        Assert.Equal("TAG42=5", result.CurrentRegimeKey);
        Assert.Equal(Minutes(3, 8), result.History.Select(p => (int)p.Valor));

        // Mesmo resultado que o componente sem mapa nesse trecho.
        configuration.MapaEstados = null;

        var withoutMap =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, NoAdditionalComponents, target, new[] { regime }, Utc(0, 4));

        Assert.Equal(Minutes(3, 8), withoutMap.History.Select(p => (int)p.Valor));
    }

    [Fact]
    public void ResolveComponents_MappedAgainstOutOfMap_ComparesWithTolerance()
    {
        // Mapa 2:7 (sem curinga). Um lado mapeado (2 -> 7) e o outro fora do
        // mapa (7 ou 7.0005, que passam sem mudança): compara com tolerância.
        // min 0-2: 2 (-> 7)   3-5: 7.0005   6-8: 7   9-11: 7.5
        var regime = Series(0, 12, m => m switch { < 3 => 2, < 6 => 7.0005, < 9 => 7, _ => 7.5 });
        var target = Series(0, 12, m => m);

        var configuration = TupleConfiguration("TAG42", minimumSamples: 1);
        configuration.MapaEstados = "2:7";
        configuration.ToleranciaEstado = 0.001;

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, NoAdditionalComponents, target, new[] { regime }, Utc(0, 1));

        Assert.True(result.Applied);
        Assert.Equal("TAG42=7", result.CurrentRegimeKey);
        Assert.Equal(Minutes(0, 8), result.History.Select(p => (int)p.Valor));
    }

    [Fact]
    public void ResolveComponents_BothSidesMapped_ComparesExactlyEvenWithinTolerance()
    {
        // 0 -> 10 e 1 -> 10.5: |10 − 10.5| = 0.5 ≤ ToleranciaEstado (0.6), mas os
        // dois lados foram mapeados, então a comparação é exata (§6.2).
        var regime = Series(0, 10, m => m < 5 ? 0 : 1);
        var target = Series(0, 10, m => m);

        var configuration = TupleConfiguration("TAG42", minimumSamples: 1);
        configuration.MapaEstados = "0:10;1:10.5;*:20";
        configuration.ToleranciaEstado = 0.6;

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, NoAdditionalComponents, target, new[] { regime }, Utc(0, 9));

        Assert.True(result.Applied);
        Assert.Equal("TAG42=10.5", result.CurrentRegimeKey);
        Assert.Equal(Minutes(5, 9), result.History.Select(p => (int)p.Valor));
    }

    [Fact]
    public void ResolveComponents_ComponentWithoutReadingWithinTolerance_DiscardsPoint()
    {
        // Componente 2 sem leitura de 10 a 14 (buraco maior que a tolerância de 1 min).
        var c1 = Series(0, 20, _ => 1);
        var c2 = Series(0, 20, _ => 1).Where(p => p.DataHora.Minute is < 10 or >= 15).ToArray();
        var target = Series(0, 20, m => m);

        var configuration = TupleConfiguration("B01", minimumSamples: 1);
        configuration.ToleranciaAlinhamentoMinutos = 1;

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, new[] { Component(2, "B02") }, target, new[] { c1, c2 }, Utc(0, 19));

        // Minuto 10 ainda alinha com a leitura de 9 (idade 1 ≤ 1); 11-14 são descartados.
        Assert.True(result.Applied);
        Assert.Equal(20, result.OriginalTargetSamples);
        Assert.Equal(16, result.AlignedSamples);
        Assert.Equal(16, result.SelectedSamples);
        Assert.DoesNotContain(result.History, p => p.Valor is >= 11 and <= 14);
    }

    [Fact]
    public void ResolveComponents_BelowMinimumSamples_NotAppliedWithCountsAndKey()
    {
        var c1 = Series(0, 20, m => m < 17 ? 0 : 1);
        var c2 = Series(0, 20, _ => 2);
        var target = Series(0, 20, m => m);

        var configuration = TupleConfiguration("B01", minimumSamples: 4);

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, new[] { Component(2, "B02") }, target, new[] { c1, c2 }, Utc(0, 19));

        Assert.False(result.Applied);
        Assert.Equal("Histórico insuficiente para o regime operacional atual.", result.Reason);
        Assert.Equal("B01=1|B02=2", result.CurrentRegimeKey);
        Assert.Equal(1.0, result.CurrentRegimeValue);
        Assert.Equal(20, result.OriginalTargetSamples);
        Assert.Equal(20, result.AlignedSamples);
        Assert.Equal(3, result.SelectedSamples);
        Assert.Empty(result.History);
    }

    [Fact]
    public void ResolveComponents_CurrentStateMissingForOneComponent_NotApplied()
    {
        var c1 = Series(0, 20, _ => 1);
        var c2 = Series(0, 15, _ => 1); // última leitura em 14; agora = 19, tolerância 2
        var target = Series(0, 20, m => m);

        var configuration = TupleConfiguration("B01", minimumSamples: 1);

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, new[] { Component(2, "B02") }, target, new[] { c1, c2 }, Utc(0, 19));

        Assert.False(result.Applied);
        Assert.StartsWith("Não foi possível determinar o regime operacional atual", result.Reason);
        Assert.Contains("Componente 2 (B02)", result.Reason);
        Assert.Null(result.CurrentRegimeKey);
        Assert.Null(result.CurrentRegimeValue);
    }

    [Fact]
    public void ResolveComponents_EmptyComponentSeries_NotAppliedNamingComponent()
    {
        var target = Series(0, 20, m => m);

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                TupleConfiguration("B01", minimumSamples: 1),
                new[] { Component(2, "B02"), Component(3, "SP") },
                target,
                new[] { Series(0, 20, _ => 1), Series(0, 20, _ => 1), new[] { new HistoricoDetectorPoint { DataHora = Utc(0, 1), Valor = double.NaN } } },
                Utc(0, 19));

        Assert.False(result.Applied);
        Assert.StartsWith("O histórico da tag de regime não possui pontos válidos.", result.Reason);
        Assert.Contains("Componente 3 (SP)", result.Reason);
    }

    public static IEnumerable<object[]> InvalidComponents()
    {
        yield return new object[] { new[] { Component(1, "B02") }, "Ordem" };
        yield return new object[] { new[] { Component(0, "B02") }, "Ordem" };
        yield return new object[] { new[] { Component(3, "B02"), Component(2, "SP") }, "Ordem" };
        yield return new object[] { new[] { Component(2, "B02"), Component(2, "SP") }, "Ordem" };
        yield return new object[] { new[] { Component(2, "") }, "sem tag" };
        yield return new object[] { new[] { Component(2, "  ") }, "sem tag" };
        yield return new object[] { new[] { Component(2, "B|02") }, "contém" };
        yield return new object[] { new[] { Component(2, "B=02") }, "contém" };
        yield return new object[] { new[] { Component(2, "b01") }, "repetida" };
        yield return new object[] { new[] { Component(2, "B02"), Component(3, "B02 ") }, "repetida" };
        yield return new object[] { new[] { Component(2, "B02", "2:2;*") }, "MapaEstados inválido" };
        yield return new object[] { new[] { Component(2, "B02", "2,5:1") }, "MapaEstados inválido" };
        yield return new object[] { new[] { Component(2, "B02", tolerance: -1) }, "ToleranciaEstado" };
        yield return new object[] { new[] { Component(2, "B02", tolerance: double.NaN) }, "ToleranciaEstado" };
        yield return new object[] { new TagRegimeComponenteIa[] { null! }, "nulo" };
    }

    [Theory]
    [MemberData(nameof(InvalidComponents))]
    public void ResolveComponents_InvalidComponents_NotApplied(
        TagRegimeComponenteIa[] additional,
        string reasonFragment)
    {
        var regimes =
            Enumerable.Range(0, additional.Length + 1)
                .Select(_ => (IReadOnlyList<HistoricoDetectorPoint>)Series(0, 20, _ => 1))
                .ToArray();

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                TupleConfiguration("B01", minimumSamples: 1),
                additional,
                Series(0, 20, m => m),
                regimes,
                Utc(0, 19));

        Assert.False(result.Applied);
        Assert.Contains(reasonFragment, result.Reason);
        Assert.Null(result.CurrentRegimeKey);
        Assert.Equal(0, result.OriginalTargetSamples);
    }

    [Fact]
    public void ResolveComponents_InvalidMapOnComponentOne_NotApplied()
    {
        var configuration = TupleConfiguration("B01", minimumSamples: 1);
        configuration.MapaEstados = "*:*";

        var result =
            new OperationalRegimeHistoryResolver().Resolve(
                configuration, NoAdditionalComponents, Series(0, 5, m => m), new[] { Series(0, 5, _ => 1) }, Utc(0, 4));

        Assert.False(result.Applied);
        Assert.Contains("Componente 1", result.Reason);
        Assert.Contains("MapaEstados inválido", result.Reason);
    }

    [Fact]
    public void ResolveComponents_ConfigurationValidation_SameReasonsAsCurrentPath()
    {
        var resolver = new OperationalRegimeHistoryResolver();
        var target = Series(0, 5, m => m);
        var regime = Series(0, 5, _ => 1);

        var cases = new Action<TagRegimeOperacionalIa>[]
        {
            c => c.Ativo = false,
            c => c.LagMinutos = -1,
            c => c.ToleranciaAlinhamentoMinutos = -1,
            c => c.ToleranciaEstado = double.NaN,
            c => c.ToleranciaEstado = -0.1,
            c => c.MinimoAmostrasPorRegime = 0
        };

        foreach (var change in cases)
        {
            var configuration = TupleConfiguration("B01", minimumSamples: 1);
            change(configuration);

            var current = resolver.Resolve(configuration, target, regime, Utc(0, 4));
            var overload = resolver.Resolve(
                configuration, new[] { Component(2, "B02") }, target, new[] { regime, regime }, Utc(0, 4));

            Assert.False(current.Applied);
            Assert.False(overload.Applied);
            Assert.Equal(current.Reason, overload.Reason);
        }
    }

    [Fact]
    public void ResolveComponents_SeriesCountMismatchOrNull_Throws()
    {
        var resolver = new OperationalRegimeHistoryResolver();
        var configuration = TupleConfiguration("B01", minimumSamples: 1);
        var series = Series(0, 5, _ => 1);

        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(configuration, new[] { Component(2, "B02") }, series, new[] { series }, Utc(0, 4)));

        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(configuration, NoAdditionalComponents, series, new[] { series, series }, Utc(0, 4)));

        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(configuration, NoAdditionalComponents, series, new IReadOnlyList<HistoricoDetectorPoint>[] { null! }, Utc(0, 4)));

        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(configuration, null!, series, new[] { series }, Utc(0, 4)));
    }

    // ------------------------------------------------------------------
    // Auxiliares
    // ------------------------------------------------------------------

    private static void AssertKeyForSingleComponent(
        OperationalRegimeHistoryResolution actual,
        string tag,
        int seed)
    {
        // Chave presente sempre que o estado atual foi determinado.
        var expectedKey =
            actual.CurrentRegimeValue is double value
                ? $"{tag}={OperationalRegimeKey.FormatValue(value)}"
                : null;

        Assert.True(expectedKey == actual.CurrentRegimeKey, $"seed {seed}: {expectedKey} × {actual.CurrentRegimeKey}");
    }

    private static TagRegimeOperacionalIa TupleConfiguration(
        string tagRegime,
        int minimumSamples)
    {
        return new TagRegimeOperacionalIa
        {
            ClienteId = "TEST_CLIENT",
            TagName = "TEST.TARGET",
            TagRegime = tagRegime,
            Ativo = true,
            LagMinutos = 0,
            ToleranciaAlinhamentoMinutos = 2,
            ToleranciaEstado = 0.001,
            MinimoAmostrasPorRegime = minimumSamples
        };
    }

    private static TagRegimeComponenteIa Component(
        int order,
        string tag,
        string? map = null,
        double tolerance = 0.001)
    {
        return new TagRegimeComponenteIa
        {
            Ordem = order,
            TagRegime = tag,
            MapaEstados = map,
            ToleranciaEstado = tolerance
        };
    }

    private static HistoricoDetectorPoint[] Series(
        int startMinute,
        int count,
        Func<int, double> value)
    {
        return Enumerable.Range(startMinute, count)
            .Select(m => Point(0, m, value(m)))
            .ToArray();
    }

    private static IEnumerable<int> Minutes(
        int first,
        int last) =>
        Enumerable.Range(first, last - first + 1);

    /// <summary>
    /// Cenário aleatório com 2 ou 3 componentes, mapas variados (com e sem
    /// curinga, valores fora do mapa) e séries com buracos, repetições e
    /// pontos inválidos.
    /// </summary>
    private sealed class TupleScenario
    {
        public required TagRegimeOperacionalIa Configuration { get; init; }

        public required TagRegimeComponenteIa[] Additional { get; init; }

        public required IReadOnlyList<HistoricoDetectorPoint> Target { get; init; }

        public required IReadOnlyList<IReadOnlyList<HistoricoDetectorPoint>> Regimes { get; init; }

        public required DateTime CurrentTimestamp { get; init; }

        public bool HasMap =>
            Configuration.MapaEstados is not null ||
            Additional.Any(c => c.MapaEstados is not null);

        private static readonly DateTime Origin =
            new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

        private static readonly string?[] Maps =
        {
            null,
            null,
            "2:2;*:0",
            "1:1;2:2",
            "380:380;*:450",
            "0:10;1:10;*:20",
            "2:2"
        };

        public static TupleScenario Create(int seed)
        {
            var random = new Random(seed);
            var componentCount = random.Next(2, 4);

            var configuration = new TagRegimeOperacionalIa
            {
                ClienteId = "TEST_CLIENT",
                TagName = "TEST.TARGET",
                TagRegime = "C1",
                Ativo = true,
                LagMinutos = random.Next(0, 4),
                ToleranciaAlinhamentoMinutos = random.Next(0, 4),
                ToleranciaEstado = random.Next(3) switch { 0 => 0, 1 => 0.001, _ => 0.6 },
                MinimoAmostrasPorRegime = random.Next(1, 8),
                MapaEstados = Maps[random.Next(Maps.Length)]
            };

            var additional =
                Enumerable.Range(2, componentCount - 1)
                    .Select(order => new TagRegimeComponenteIa
                    {
                        Ordem = order * 10 - random.Next(0, 5), // crescente, não contígua
                        TagRegime = $"C{order}",
                        MapaEstados = Maps[random.Next(Maps.Length)],
                        ToleranciaEstado = random.Next(3) switch { 0 => 0, 1 => 0.001, _ => 0.6 }
                    })
                    .ToArray();

            var regimes =
                Enumerable.Range(0, componentCount)
                    .Select(_ => (IReadOnlyList<HistoricoDetectorPoint>)CreateSeries(
                        random,
                        count: random.Next(10) == 0 ? random.Next(0, 6) : random.Next(50, 120),
                        startOffsetMinutes: random.Next(-20, 6),
                        value: StickyState(random)))
                    .ToArray();

            var target = CreateSeries(
                random,
                count: random.Next(10) == 0 ? random.Next(0, 6) : random.Next(30, 110),
                startOffsetMinutes: random.Next(-10, 15),
                value: () => random.NextDouble() * 100);

            return new TupleScenario
            {
                Configuration = configuration,
                Additional = additional,
                Target = target,
                Regimes = regimes,
                CurrentTimestamp = Origin.AddMinutes(random.Next(-5, 90))
                    .AddSeconds(random.NextDouble() < 0.3 ? random.Next(1, 60) : 0)
            };
        }

        /// <summary>
        /// Estado que tende a se manter (troca com 15% de chance por leitura),
        /// como o de uma bomba; às vezes um valor contínuo (fora de mapa).
        /// </summary>
        private static Func<double> StickyState(Random random)
        {
            double NewState() => random.Next(6) switch
            {
                0 => 0,
                1 => 1,
                2 => 2,
                3 => 380,
                4 => 450,
                _ => random.NextDouble() * 3
            };

            var state = NewState();

            return () =>
            {
                if (random.NextDouble() < 0.15)
                {
                    state = NewState();
                }

                // Ruído pequeno em 30% das leituras (ex.: 1.9998 do CLP):
                // exercita tolerância do mapa e valores fora do mapa próximos.
                return random.NextDouble() < 0.3
                    ? state + (random.NextDouble() - 0.5) * 0.0008
                    : state;
            };
        }

        private static List<HistoricoDetectorPoint> CreateSeries(
            Random random,
            int count,
            int startOffsetMinutes,
            Func<double> value)
        {
            var points = new List<HistoricoDetectorPoint>(count);
            var time = Origin.AddMinutes(startOffsetMinutes);

            for (var i = 0; i < count; i++)
            {
                var step = random.NextDouble();

                if (step < 0.10 && points.Count > 0)
                {
                    // Timestamp repetido.
                }
                else if (step < 0.15)
                {
                    time = time.AddMinutes(random.Next(4, 15));
                }
                else if (step < 0.28)
                {
                    time = time.AddSeconds(random.Next(1, 90));
                }
                else
                {
                    time = time.AddMinutes(1);
                }

                points.Add(new HistoricoDetectorPoint { DataHora = time, Valor = value() });

                var invalid = random.NextDouble();

                if (invalid < 0.02)
                {
                    points.Add(new HistoricoDetectorPoint { DataHora = time, Valor = double.NaN });
                }
                else if (invalid < 0.03)
                {
                    points.Add(null!);
                }
            }

            // Entrada fora de ordem em metade dos casos.
            if (random.NextDouble() < 0.5)
            {
                for (var i = points.Count - 1; i > 0; i--)
                {
                    var j = random.Next(i + 1);
                    (points[i], points[j]) = (points[j], points[i]);
                }
            }

            return points;
        }
    }

    /// <summary>
    /// Oráculo do regime combinado, escrito à parte da implementação:
    /// para cada ponto e cada componente, busca linear do último ponto com
    /// DataHora ≤ esperado (mesma regra da RC-3) e teste de idade; tupla
    /// comparada componente a componente (sem mapa: |Δ| ≤ ToleranciaEstado;
    /// com mapa: exato se os dois lados foram mapeados, |Δ| ≤ ToleranciaEstado
    /// se algum lado ficou fora do mapa).
    /// </summary>
    private static class TupleOracle
    {
        public sealed record Result(
            bool Applied,
            double? CurrentValue,
            string? Key,
            int Original,
            int Aligned,
            List<HistoricoDetectorPoint> Selected,
            int OutOfMapToleranceHits = 0);

        public static Result Resolve(TupleScenario scenario)
        {
            var configuration = scenario.Configuration;
            var n = scenario.Regimes.Count;

            var tags = new List<string> { configuration.TagRegime };
            var maps = new List<OperationalRegimeStateMap?> { ParseMap(configuration.MapaEstados) };
            var tolerances = new List<double> { configuration.ToleranciaEstado };

            foreach (var component in scenario.Additional)
            {
                tags.Add(component.TagRegime);
                maps.Add(ParseMap(component.MapaEstados));
                tolerances.Add(component.ToleranciaEstado);
            }

            var target = scenario.Target
                .Where(Valid)
                .OrderBy(p => p.DataHora)
                .ToList();

            var regimes = scenario.Regimes
                .Select(s => s.Where(Valid).OrderBy(p => p.DataHora).ToList())
                .ToList();

            var none = new Result(false, null, null, 0, 0, new List<HistoricoDetectorPoint>());

            if (target.Count == 0 || regimes.Any(r => r.Count == 0))
            {
                return none;
            }

            var currentExpected = scenario.CurrentTimestamp.AddMinutes(-configuration.LagMinutos);
            var currentRaw = new double[n];

            for (var k = 0; k < n; k++)
            {
                var point = Find(regimes[k], currentExpected, configuration.ToleranciaAlinhamentoMinutos);

                if (point is null)
                {
                    return none;
                }

                currentRaw[k] = point.Valor;
            }

            double StateOf(int k, double v) =>
                maps[k] is null ? v : maps[k]!.Apply(v, tolerances[k]).MappedValue;

            var key = string.Join(
                "|",
                Enumerable.Range(0, n).Select(k => $"{tags[k]}={OperationalRegimeKey.FormatValue(StateOf(k, currentRaw[k]))}"));

            var aligned = 0;
            var selected = new List<HistoricoDetectorPoint>();
            var outOfMapToleranceHits = 0;

            foreach (var targetPoint in target)
            {
                var expected = targetPoint.DataHora.AddMinutes(-configuration.LagMinutos);
                var points = new HistoricoDetectorPoint?[n];

                for (var k = 0; k < n; k++)
                {
                    points[k] = Find(regimes[k], expected, configuration.ToleranciaAlinhamentoMinutos);
                }

                if (points.Any(p => p is null))
                {
                    continue;
                }

                aligned++;

                var same = true;

                for (var k = 0; k < n; k++)
                {
                    same &= Same(k, points[k]!.Valor, currentRaw[k]);
                }

                if (same)
                {
                    selected.Add(targetPoint);
                }
            }

            return new Result(
                selected.Count >= configuration.MinimoAmostrasPorRegime,
                currentRaw[0],
                key,
                target.Count,
                aligned,
                selected,
                outOfMapToleranceHits);

            // Decisões de §6.2 / RC-6: sem mapa ⇒ |Δ| ≤ tol; com mapa, os dois
            // lados mapeados ⇒ exato; algum lado fora do mapa ⇒ |Δ| ≤ tol.
            bool Same(int k, double historical, double current)
            {
                if (maps[k] is null)
                {
                    return Math.Abs(historical - current) <= tolerances[k];
                }

                var h = maps[k]!.Apply(historical, tolerances[k]);
                var c = maps[k]!.Apply(current, tolerances[k]);

                if (h.Status != OperationalRegimeStateMapStatus.OutOfMap &&
                    c.Status != OperationalRegimeStateMapStatus.OutOfMap)
                {
                    return h.MappedValue == c.MappedValue;
                }

                var within = Math.Abs(h.MappedValue - c.MappedValue) <= tolerances[k];

                if (within && h.MappedValue != c.MappedValue)
                {
                    outOfMapToleranceHits++;
                }

                return within;
            }
        }

        private static OperationalRegimeStateMap? ParseMap(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null : OperationalRegimeStateMap.Parse(text);

        private static HistoricoDetectorPoint? Find(
            List<HistoricoDetectorPoint> series,
            DateTime expected,
            int toleranceMinutes)
        {
            HistoricoDetectorPoint? candidate = null;

            foreach (var point in series)
            {
                if (point.DataHora > expected)
                {
                    break;
                }

                candidate = point;
            }

            if (candidate is null)
            {
                return null;
            }

            var age = expected - candidate.DataHora;

            return age < TimeSpan.Zero || age > TimeSpan.FromMinutes(toleranceMinutes)
                ? null
                : candidate;
        }

        private static bool Valid(HistoricoDetectorPoint point) =>
            point is not null &&
            point.DataHora != default &&
            double.IsFinite(point.Valor);
    }
}
