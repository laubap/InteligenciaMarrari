using IAApi.Services.AnomalyDetection.BehaviorProfiles.History;

namespace IAApi.Tests.Services.AnomalyDetection.BehaviorProfiles.History;

public sealed class BehaviorHistoryCollectionServiceTests
{
    [Fact]
    public void Collect_DeveBuscarExatamenteOPeriodoSolicitado()
    {
        var inicio =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var fim =
            new DateTime(
                2026,
                9,
                2,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var fake =
            new FakeHistoricoService
            {
                HistoricoPorPeriodo =
                [
                    new LeituraBruta
                    {
                        DataHora =
                            inicio,

                        Valor =
                            10
                    },

                    new LeituraBruta
                    {
                        DataHora =
                            inicio.AddMinutes(1),

                        Valor =
                            11
                    }
                ]
            };

        var service =
            new BehaviorHistoryCollectionService(
                fake);

        var result =
            service.Collect(
                "PK7",
                "PK7.TAG2",
                inicio,
                fim);

        Assert.Equal(
            1,
            fake.BuscarPorPeriodoChamadas);

        Assert.Equal(
            "PK7",
            fake.UltimoClienteId);

        Assert.Equal(
            "PK7.TAG2",
            fake.UltimaTagName);

        Assert.Equal(
            inicio,
            fake.UltimoInicio);

        Assert.Equal(
            fim,
            fake.UltimoFim);

        Assert.Equal(
            "PK7",
            result.ClienteId);

        Assert.Equal(
            "PK7.TAG2",
            result.TagName);

        Assert.Equal(
            inicio,
            result.PeriodoInicio);

        Assert.Equal(
            fim,
            result.PeriodoFim);

        Assert.Equal(
            2,
            result.QuantidadeLeituras);
    }

    [Fact]
    public void Collect_DeveSanitizarOrdenarEDeduplicarHistorico()
    {
        var inicio =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var fim =
            inicio.AddHours(1);

        var minuto1 =
            inicio.AddMinutes(1);

        var minuto2 =
            inicio.AddMinutes(2);

        var fake =
            new FakeHistoricoService
            {
                HistoricoPorPeriodo =
                [
                    // Fora de ordem.
                    new LeituraBruta
                    {
                        DataHora =
                            minuto2,

                        Valor =
                            20
                    },

                    new LeituraBruta
                    {
                        DataHora =
                            minuto1,

                        Valor =
                            10
                    },

                    // Timestamp duplicado.
                    // A última ocorrência deve prevalecer.
                    new LeituraBruta
                    {
                        DataHora =
                            minuto1,

                        Valor =
                            11
                    },

                    // Timestamp inválido.
                    new LeituraBruta
                    {
                        DataHora =
                            default,

                        Valor =
                            99
                    },

                    // Valor inválido.
                    new LeituraBruta
                    {
                        DataHora =
                            inicio.AddMinutes(3),

                        Valor =
                            float.NaN
                    }
                ]
            };

        var service =
            new BehaviorHistoryCollectionService(
                fake);

        var result =
            service.Collect(
                "PK7",
                "PK7.TAG2",
                inicio,
                fim);

        Assert.Equal(
            2,
            result.QuantidadeLeituras);

        Assert.Equal(
            minuto1,
            result.Historico[0].DataHora);

        Assert.Equal(
            11,
            result.Historico[0].Valor);

        Assert.Equal(
            minuto2,
            result.Historico[1].DataHora);

        Assert.Equal(
            20,
            result.Historico[1].Valor);

        Assert.Equal(
            5,
            result.Qualidade.OriginalPoints);

        Assert.Equal(
            2,
            result.Qualidade.ValidPoints);

        Assert.Equal(
            1,
            result.Qualidade.InvalidValues);

        Assert.Equal(
            1,
            result.Qualidade.InvalidTimestamps);

        Assert.Equal(
            1,
            result.Qualidade.DuplicateTimestamps);

        Assert.True(
            result.Qualidade.OutOfOrderTransitions > 0);
    }

    [Fact]
    public void Collect_DeveRejeitarPeriodoInvalidoSemConsultarHistorico()
    {
        var instante =
            new DateTime(
                2026,
                9,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        var fake =
            new FakeHistoricoService();

        var service =
            new BehaviorHistoryCollectionService(
                fake);

        Assert.Throws<ArgumentException>(() =>
            service.Collect(
                "PK7",
                "PK7.TAG2",
                instante,
                instante));

        Assert.Equal(
            0,
            fake.BuscarPorPeriodoChamadas);
    }

    [Theory]
    [InlineData("", "PK7.TAG2")]
    [InlineData("PK7", "")]
    public void Collect_DeveRejeitarIdentificacaoObrigatoria(
        string clienteId,
        string tagName)
    {
        var inicio =
            new DateTime(
                2026,
                9,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var fake =
            new FakeHistoricoService();

        var service =
            new BehaviorHistoryCollectionService(
                fake);

        Assert.Throws<ArgumentException>(() =>
            service.Collect(
                clienteId,
                tagName,
                inicio,
                inicio.AddHours(1)));

        Assert.Equal(
            0,
            fake.BuscarPorPeriodoChamadas);
    }

    private sealed class FakeHistoricoService :
        IHistoricoService
    {
        public List<LeituraBruta> HistoricoPorPeriodo { get; init; } =
            [];

        public int BuscarPorPeriodoChamadas { get; private set; }

        public string? UltimoClienteId { get; private set; }

        public string? UltimaTagName { get; private set; }

        public DateTime? UltimoInicio { get; private set; }

        public DateTime? UltimoFim { get; private set; }

        public List<LeituraBruta> BuscarHistorico(
            string clienteId,
            string tagName,
            string tipoTag)
        {
            throw new InvalidOperationException(
                "BuscarHistorico não deve ser utilizado pela memória comportamental.");
        }

        public List<LeituraBruta> BuscarHistoricoPorPeriodo(
            string clienteId,
            string tagName,
            DateTime inicio,
            DateTime fim)
        {
            BuscarPorPeriodoChamadas++;

            UltimoClienteId =
                clienteId;

            UltimaTagName =
                tagName;

            UltimoInicio =
                inicio;

            UltimoFim =
                fim;

            return HistoricoPorPeriodo;
        }
    }
}