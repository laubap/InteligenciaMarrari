using IAApi.Services.AnomalyDetection.Multivariate;
using Xunit;

namespace IAApi.Tests;

public sealed class
    MultivariateCorrelationDiagnosticServiceTests
{
    [Fact]
    public void CorrelacaoPositivaForte_DeveAparecerNoTopo()
    {
        var historico =
            new FakeHistoricoService();

        var inicio =
            new DateTime(
                2026, 9, 1, 10, 0, 0);

        for (var i = 0;
             i < 60;
             i++)
        {
            historico.Adicionar(
                "C1",
                "TAG-A",
                inicio.AddMinutes(i),
                i);

            historico.Adicionar(
                "C1",
                "TAG-B",
                inicio.AddMinutes(i),
                2 * i + 10);

            historico.Adicionar(
                "C1",
                "TAG-C",
                inicio.AddMinutes(i),
                i % 7);
        }

        var service =
            new MultivariateCorrelationDiagnosticService(
                historico);

        var result =
            service.Analisar(
                Request(
                    inicio,
                    inicio.AddMinutes(59),
                    "TAG-A",
                    "TAG-B",
                    "TAG-C"));

        var primeiro =
            Assert.Single(
                result.Ranking.Take(1));

        Assert.Equal(
            "TAG-A",
            primeiro.TagA);

        Assert.Equal(
            "TAG-B",
            primeiro.TagB);

        Assert.True(
            primeiro.Aplicavel);

        Assert.NotNull(
            primeiro.Correlacao);

        Assert.True(
            primeiro.Correlacao > 0.999);
    }

    [Fact]
    public void CorrelacaoNegativaForte_DeveSerPreservada()
    {
        var historico =
            new FakeHistoricoService();

        var inicio =
            new DateTime(
                2026, 9, 1, 10, 0, 0);

        for (var i = 0;
             i < 40;
             i++)
        {
            historico.Adicionar(
                "C1",
                "TAG-A",
                inicio.AddMinutes(i),
                i);

            historico.Adicionar(
                "C1",
                "TAG-B",
                inicio.AddMinutes(i),
                100 - i);
        }

        var service =
            new MultivariateCorrelationDiagnosticService(
                historico);

        var result =
            service.Analisar(
                Request(
                    inicio,
                    inicio.AddMinutes(39),
                    "TAG-A",
                    "TAG-B"));

        var pair =
            Assert.Single(
                result.Ranking);

        Assert.True(
            pair.Correlacao < -0.999);

        Assert.Equal(
            "muito-forte-negativa",
            pair.Classificacao);
    }

    [Fact]
    public void SerieConstante_NaoDeveGerarCorrelacaoFalsa()
    {
        var historico =
            new FakeHistoricoService();

        var inicio =
            new DateTime(
                2026, 9, 1, 10, 0, 0);

        for (var i = 0;
             i < 40;
             i++)
        {
            historico.Adicionar(
                "C1",
                "TAG-A",
                inicio.AddMinutes(i),
                i);

            historico.Adicionar(
                "C1",
                "TAG-CONSTANTE",
                inicio.AddMinutes(i),
                5);
        }

        var service =
            new MultivariateCorrelationDiagnosticService(
                historico);

        var result =
            service.Analisar(
                Request(
                    inicio,
                    inicio.AddMinutes(39),
                    "TAG-A",
                    "TAG-CONSTANTE"));

        Assert.Empty(
            result.Ranking);

        Assert.Equal(
            0,
            result.ParesAplicaveis);
    }

    private static
        MultivariateCorrelationDiagnosticRequest
        Request(
            DateTime inicio,
            DateTime fim,
            params string[] tags)
    {
        return
            new MultivariateCorrelationDiagnosticRequest
            {
                ClienteId =
                    "C1",

                Inicio =
                    inicio,

                Fim =
                    fim,

                Tags =
                    tags.ToList(),

                MinimoPontos =
                    10,

                Top =
                    20
            };
    }

    private sealed class FakeHistoricoService
        : IHistoricoService
    {
        private readonly
            Dictionary<
                string,
                List<LeituraBruta>>
            _dados =
                new(
                    StringComparer.OrdinalIgnoreCase);

        public void Adicionar(
            string clienteId,
            string tagName,
            DateTime dataHora,
            double valor)
        {
            var key =
                Key(
                    clienteId,
                    tagName);

            if (!_dados.TryGetValue(
                    key,
                    out var lista))
            {
                lista =
                    [];

                _dados[key] =
                    lista;
            }

            lista.Add(
                new LeituraBruta
                {
                    DataHora =
                        dataHora,

                    Valor =
                        (float)valor
                });
        }

        public List<LeituraBruta>
            BuscarHistorico(
                string clienteId,
                string tagName,
                string tipoTag)
        {
            return
                BuscarHistoricoPorPeriodo(
                    clienteId,
                    tagName,
                    DateTime.MinValue,
                    DateTime.MaxValue);
        }

        public List<LeituraBruta>
            BuscarHistoricoPorPeriodo(
                string clienteId,
                string tagName,
                DateTime inicio,
                DateTime fim)
        {
            if (!_dados.TryGetValue(
                    Key(
                        clienteId,
                        tagName),
                    out var lista))
            {
                return [];
            }

            return
                lista
                    .Where(x =>
                        x.DataHora >= inicio &&
                        x.DataHora <= fim)
                    .OrderBy(x =>
                        x.DataHora)
                    .Select(x =>
                        new LeituraBruta
                        {
                            DataHora =
                                x.DataHora,

                            Valor =
                                x.Valor
                        })
                    .ToList();
        }

        private static string Key(
            string clienteId,
            string tagName)
        {
            return
                $"{clienteId.Trim()}::" +
                $"{tagName.Trim()}";
        }
    }
}
