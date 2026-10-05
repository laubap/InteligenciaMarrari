using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.History;

public sealed class BehaviorHistoryCollectionUtcTests
{
    [Fact]
    public void Collect_TimestampUnspecifiedDoHistorian_DevePreservarExatamente()
    {
        var sourceTimestamp =
            new DateTime(
                2026,
                9,
                30,
                10,
                15,
                0,
                DateTimeKind.Unspecified);

        var historian =
            new CapturingHistoricoService(
                new[]
                {
                    new LeituraBruta
                    {
                        DataHora =
                            sourceTimestamp,

                        Valor =
                            40
                    }
                });

        var service =
            new BehaviorHistoryCollectionService(
                historian);

        var result =
            service.Collect(
                "PK7",
                "PK7.TAG2",
                new DateTime(
                    2026,
                    9,
                    30,
                    0,
                    0,
                    0,
                    DateTimeKind.Unspecified),
                new DateTime(
                    2026,
                    10,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Unspecified));

        var reading =
            Assert.Single(
                result.Historico);

        Assert.Equal(
            sourceTimestamp,
            reading.DataHora);

        Assert.Equal(
            DateTimeKind.Unspecified,
            reading.DataHora.Kind);
    }

    [Fact]
    public void Collect_Periodo_DeveEnviarAoHistorianSemConversao()
    {
        var historian =
            new CapturingHistoricoService(
                Array.Empty<LeituraBruta>());

        var service =
            new BehaviorHistoryCollectionService(
                historian);

        var inicio =
            new DateTime(
                2026,
                9,
                30,
                0,
                0,
                0,
                DateTimeKind.Unspecified);

        var fim =
            new DateTime(
                2026,
                10,
                1,
                0,
                0,
                0,
                DateTimeKind.Unspecified);

        var result =
            service.Collect(
                "PK7",
                "PK7.TAG2",
                inicio,
                fim);

        Assert.Equal(
            inicio,
            historian.CapturedInicio);

        Assert.Equal(
            fim,
            historian.CapturedFim);

        Assert.Equal(
            inicio,
            result.PeriodoInicio);

        Assert.Equal(
            fim,
            result.PeriodoFim);

        Assert.Equal(
            DateTimeKind.Unspecified,
            historian.CapturedInicio.Kind);

        Assert.Equal(
            DateTimeKind.Unspecified,
            result.PeriodoInicio.Kind);
    }

    [Fact]
    public void Collect_TimestampUtc_DevePermanecerInalterado()
    {
        var timestamp =
            new DateTime(
                2026,
                9,
                30,
                12,
                0,
                0,
                DateTimeKind.Utc);

        var historian =
            new CapturingHistoricoService(
                new[]
                {
                    new LeituraBruta
                    {
                        DataHora =
                            timestamp,

                        Valor =
                            41
                    }
                });

        var service =
            new BehaviorHistoryCollectionService(
                historian);

        var result =
            service.Collect(
                "PK7",
                "PK7.TAG2",
                timestamp.AddHours(
                    -1),
                timestamp.AddHours(
                    1));

        var reading =
            Assert.Single(
                result.Historico);

        Assert.Equal(
            timestamp,
            reading.DataHora);

        Assert.Equal(
            DateTimeKind.Utc,
            reading.DataHora.Kind);
    }

    private sealed class CapturingHistoricoService :
        IHistoricoService
    {
        private readonly IReadOnlyList<LeituraBruta> _history;

        public CapturingHistoricoService(
            IEnumerable<LeituraBruta> history)
        {
            _history =
                history.ToArray();
        }

        public DateTime CapturedInicio { get; private set; }

        public DateTime CapturedFim { get; private set; }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            return _history
                .Select(Clone)
                .ToList();
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            CapturedInicio =
                inicio;

            CapturedFim =
                fim;

            return _history
                .Select(Clone)
                .ToList();
        }

        private static LeituraBruta Clone(
            LeituraBruta source)
        {
            return new LeituraBruta
            {
                DataHora =
                    source.DataHora,

                Valor =
                    source.Valor
            };
        }
    }
}
