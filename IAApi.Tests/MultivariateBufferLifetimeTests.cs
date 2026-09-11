using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IAApi.Tests;

public sealed class MultivariateBufferLifetimeTests
{
    [Fact]
    public async Task BufferMultivariado_DevePreservarEstadoEntreScopesHttp()
    {
        var services =
            new ServiceCollection();

        services.AddSingleton<
            IGrupoAnaliseProvider,
            FakeGrupoAnaliseProvider>();

        services.AddScoped<
            GrupoAnaliseService>();

        /*
         * Reproduz exatamente o lifetime atual da aplicação.
         */

        services.AddSingleton<
    MultivariateRuntimeStateStore>();

        services.AddScoped<
            MultivariateGroupBufferService>();

        await using var provider =
            services.BuildServiceProvider();

        /*
         * Request HTTP #1
         */
        await using (var scope1 =
            provider.CreateAsyncScope())
        {
            var buffer =
                scope1.ServiceProvider
                    .GetRequiredService<
                        MultivariateGroupBufferService>();

            await buffer.AdicionarLeituraAsync(
                clienteId: "CLIENTE-A",
                tagName: "TAG-A",
                minuteIndex: 100,
                valor: 10);

            Assert.Equal(
                1,
                buffer.ObterQuantidadeBuffers());
        }

        /*
         * Request HTTP #2.
         *
         * Para montar um vetor multivariado real,
         * o estado iniciado no request anterior precisa
         * continuar existindo.
         */
        await using (var scope2 =
            provider.CreateAsyncScope())
        {
            var buffer =
                scope2.ServiceProvider
                    .GetRequiredService<
                        MultivariateGroupBufferService>();

            Assert.Equal(
                1,
                buffer.ObterQuantidadeBuffers());
        }
    }

    private sealed class FakeGrupoAnaliseProvider :
        IGrupoAnaliseProvider
    {
        public Task<List<GrupoAnaliseDto>>
            CarregarGruposAsync(
                string clienteId)
        {
            return Task.FromResult(
                new List<GrupoAnaliseDto>
                {
                    new()
                    {
                        Id = 1,
                        Nome = "GRUPO-A",
                        Algoritmo = "Correlation",
                        Sensibilidade = 0.5,
                        PesoEnsemble = 1.0,
                        MinimumAvailableTags = 2,
                        MaxLagMinutes = 1,
                        Tags =
                        [
                            "TAG-A",
                            "TAG-B"
                        ]
                    }
                });
        }
    }
}