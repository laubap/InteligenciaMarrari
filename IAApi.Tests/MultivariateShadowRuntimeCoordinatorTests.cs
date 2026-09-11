using System.Text.Json;
using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IAApi.Tests;

public sealed class MultivariateShadowRuntimeCoordinatorTests
{
    [Fact]
    public async Task LeiturasEntreChamadas_FormamVetorEProduzemResultadoCorrelacionadoAoVetorFinalizado()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "IAApiTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempRoot);

        try
        {
            var provider =
                new StaticGrupoProvider(
                    new GrupoAnaliseDto
                    {
                        Id = 1,
                        Nome = "GRUPO-AB",
                        MinimumAvailableTags = 2,
                        MaxLagMinutes = 2,
                        Tags = ["TAG-A", "TAG-B"]
                    });

            var grupoService =
                new GrupoAnaliseService(provider);

            var store =
                new MultivariateRuntimeStateStore();

            var buffer =
                new MultivariateGroupBufferService(
                    grupoService,
                    store);

            var persistence =
                new MultivariateModelPersistenceService(
                    new FakeWebHostEnvironment(tempRoot));

            await GravarModeloAsync(
                persistence,
                "CLIENTE-1",
                "GRUPO-AB");

            var detector =
                new MultivariateDetector(persistence);

            var coordinator =
                new MultivariateShadowRuntimeCoordinator(
                    buffer,
                    detector,
                    NullLogger<MultivariateShadowRuntimeCoordinator>.Instance);

            var instante =
                new DateTime(
                    2026, 9, 3, 10, 0, 0,
                    DateTimeKind.Unspecified);

            var r1 =
                await coordinator.ProcessarLeituraAsync(
                    "CLIENTE-1",
                    "TAG-A",
                    instante,
                    10);

            var r2 =
                await coordinator.ProcessarLeituraAsync(
                    "CLIENTE-1",
                    "TAG-B",
                    instante,
                    20);

            // O vetor completo ainda permanece aberto até a próxima rodada.
            Assert.Empty(r1);
            Assert.Empty(r2);
            Assert.Equal(1, store.ObterQuantidadeBuffers());

            // TAG-A reaparece no minuto seguinte: fecha o vetor anterior.
            var r3 =
                await coordinator.ProcessarLeituraAsync(
                    "CLIENTE-1",
                    "TAG-A",
                    instante.AddMinutes(1),
                    10);

            var resultado =
                Assert.Single(r3);

            Assert.True(resultado.Aplicavel);
            Assert.False(resultado.EhAnomalia);
            Assert.Equal("CLIENTE-1", resultado.ClienteId);
            Assert.Equal("GRUPO-AB", resultado.GroupName);
            Assert.Equal(
                MultivariateShadowRuntimeCoordinator
                    .CriarMinuteIndex(instante),
                resultado.MinuteIndex);
            Assert.Equal(2, resultado.TagsUtilizadas.Count);
            Assert.Contains("TAG-A", resultado.TagsUtilizadas);
            Assert.Contains("TAG-B", resultado.TagsUtilizadas);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(
                    tempRoot,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task FalhaNoProviderMultivariado_DeveSerFailOpen()
    {
        var grupoService =
            new GrupoAnaliseService(
                new ThrowingGrupoProvider());

        var buffer =
            new MultivariateGroupBufferService(
                grupoService,
                new MultivariateRuntimeStateStore());

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "IAApiTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempRoot);

        try
        {
            var persistence =
                new MultivariateModelPersistenceService(
                    new FakeWebHostEnvironment(tempRoot));

            var detector =
                new MultivariateDetector(persistence);

            var coordinator =
                new MultivariateShadowRuntimeCoordinator(
                    buffer,
                    detector,
                    NullLogger<MultivariateShadowRuntimeCoordinator>.Instance);

            var resultado =
                await coordinator.ProcessarLeituraAsync(
                    "CLIENTE-1",
                    "TAG-A",
                    new DateTime(2026, 9, 3, 10, 0, 0),
                    10);

            Assert.Empty(resultado);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(
                    tempRoot,
                    recursive: true);
            }
        }
    }


    [Fact]
    public async Task ReinicioDaAplicacao_NaoPodeReaproveitarVetorParcialAnterior()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "IAApiTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempRoot);

        try
        {
            var provider =
                new StaticGrupoProvider(
                    new GrupoAnaliseDto
                    {
                        Id = 1,
                        Nome = "GRUPO-AB",
                        MinimumAvailableTags = 2,
                        MaxLagMinutes = 2,
                        Tags = ["TAG-A", "TAG-B"]
                    });

            var persistence =
                new MultivariateModelPersistenceService(
                    new FakeWebHostEnvironment(tempRoot));

            await GravarModeloAsync(
                persistence,
                "CLIENTE-1",
                "GRUPO-AB");

            var detector =
                new MultivariateDetector(
                    persistence);

            var instante =
                new DateTime(
                    2026, 9, 4, 14, 0, 0,
                    DateTimeKind.Unspecified);

            /*
             * Antes do restart:
             * apenas TAG-A chega e fica em um vetor parcial.
             */
            var storeAntesDoRestart =
                new MultivariateRuntimeStateStore();

            var coordinatorAntesDoRestart =
                new MultivariateShadowRuntimeCoordinator(
                    new MultivariateGroupBufferService(
                        new GrupoAnaliseService(provider),
                        storeAntesDoRestart),
                    detector,
                    NullLogger<
                        MultivariateShadowRuntimeCoordinator>.Instance);

            var antes =
                await coordinatorAntesDoRestart
                    .ProcessarLeituraAsync(
                        "CLIENTE-1",
                        "TAG-A",
                        instante,
                        10);

            Assert.Empty(
                antes);

            Assert.Equal(
                1,
                storeAntesDoRestart
                    .ObterQuantidadeBuffers());

            /*
             * Simula restart da aplicação:
             * o singleton de estado temporal nasce novamente vazio.
             */
            var storeDepoisDoRestart =
                new MultivariateRuntimeStateStore();

            var coordinatorDepoisDoRestart =
                new MultivariateShadowRuntimeCoordinator(
                    new MultivariateGroupBufferService(
                        new GrupoAnaliseService(provider),
                        storeDepoisDoRestart),
                    detector,
                    NullLogger<
                        MultivariateShadowRuntimeCoordinator>.Instance);

            /*
             * Depois do restart chega somente TAG-B.
             * Ela NÃO pode completar o vetor antigo que tinha TAG-A.
             */
            var depois =
                await coordinatorDepoisDoRestart
                    .ProcessarLeituraAsync(
                        "CLIENTE-1",
                        "TAG-B",
                        instante,
                        20);

            Assert.Empty(
                depois);

            Assert.Equal(
                1,
                storeDepoisDoRestart
                    .ObterQuantidadeBuffers());

            /*
             * Avançamos além do MaxLag com uma tag fora do grupo,
             * apenas para forçar o fechamento do vetor parcial pós-restart.
             *
             * Como só existe TAG-B no novo store, o vetor deve ser
             * não aplicável por não atingir MinimumAvailableTags=2.
             *
             * Se TAG-A do processo anterior tivesse vazado através do restart,
             * esse vetor seria completo e Aplicavel=true.
             */
            var expirado =
                await coordinatorDepoisDoRestart
                    .ProcessarLeituraAsync(
                        "CLIENTE-1",
                        "TAG-X",
                        instante.AddMinutes(3),
                        999);

            /*
             * Como o buffer pós-restart contém somente TAG-B,
             * ele não atinge MinimumAvailableTags=2.
             *
             * Ao expirar, o buffer é removido sem produzir resultado.
             * Isso é exatamente o que queremos provar: TAG-A do processo
             * anterior não vazou para o novo estado em memória.
             */
            Assert.Empty(
                expirado);

            Assert.Equal(
                0,
                storeDepoisDoRestart
                    .ObterQuantidadeBuffers());

            /*
             * Confirma também que o objeto antigo continua isolado:
             * o novo coordinator nunca reutilizou seu buffer.
             */
            Assert.Equal(
                1,
                storeAntesDoRestart
                    .ObterQuantidadeBuffers());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(
                    tempRoot,
                    recursive: true);
            }
        }
    }


    [Fact]
    public async Task ScopesConcorrentes_NaoPodemMisturarClientes()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "IAApiTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempRoot);

        try
        {
            var provider =
                new StaticGrupoProvider(
                    new GrupoAnaliseDto
                    {
                        Id = 1,
                        Nome = "GRUPO-AB",
                        MinimumAvailableTags = 2,
                        MaxLagMinutes = 2,
                        Tags = ["TAG-A", "TAG-B"]
                    });

            var persistence =
                new MultivariateModelPersistenceService(
                    new FakeWebHostEnvironment(tempRoot));

            await GravarModeloAsync(
                persistence,
                "CLIENTE-1",
                "GRUPO-AB");

            await GravarModeloAsync(
                persistence,
                "CLIENTE-2",
                "GRUPO-AB");

            var detector =
                new MultivariateDetector(
                    persistence);

            /*
             * Simula o runtime real:
             *
             * - cada request/fluxo possui seu próprio serviço scoped;
             * - o estado temporal é singleton e compartilhado.
             */
            var sharedStore =
                new MultivariateRuntimeStateStore();

            var coordinatorScope1 =
                new MultivariateShadowRuntimeCoordinator(
                    new MultivariateGroupBufferService(
                        new GrupoAnaliseService(provider),
                        sharedStore),
                    detector,
                    NullLogger<
                        MultivariateShadowRuntimeCoordinator>.Instance);

            var coordinatorScope2 =
                new MultivariateShadowRuntimeCoordinator(
                    new MultivariateGroupBufferService(
                        new GrupoAnaliseService(provider),
                        sharedStore),
                    detector,
                    NullLogger<
                        MultivariateShadowRuntimeCoordinator>.Instance);

            var instante =
                new DateTime(
                    2026, 9, 4, 14, 30, 0,
                    DateTimeKind.Unspecified);

            /*
             * Primeira rodada concorrente:
             * cada cliente envia apenas uma tag.
             *
             * Se a chave do buffer não segregasse ClienteId,
             * essas duas leituras poderiam formar um vetor incorreto.
             */
            var gate1 =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            var cliente1TagA =
                Task.Run(
                    async () =>
                    {
                        await gate1.Task;

                        return await coordinatorScope1
                            .ProcessarLeituraAsync(
                                "CLIENTE-1",
                                "TAG-A",
                                instante,
                                10);
                    });

            var cliente2TagB =
                Task.Run(
                    async () =>
                    {
                        await gate1.Task;

                        return await coordinatorScope2
                            .ProcessarLeituraAsync(
                                "CLIENTE-2",
                                "TAG-B",
                                instante,
                                20);
                    });

            gate1.SetResult(true);

            var primeiraRodada =
                await Task.WhenAll(
                    cliente1TagA,
                    cliente2TagB);

            Assert.All(
                primeiraRodada,
                resultado =>
                    Assert.Empty(resultado));

            // Um buffer parcial independente para cada cliente.
            Assert.Equal(
                2,
                sharedStore.ObterQuantidadeBuffers());

            /*
             * Segunda rodada concorrente:
             * completa cada cliente com a tag que falta.
             */
            var gate2 =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            var cliente1TagB =
                Task.Run(
                    async () =>
                    {
                        await gate2.Task;

                        return await coordinatorScope2
                            .ProcessarLeituraAsync(
                                "CLIENTE-1",
                                "TAG-B",
                                instante,
                                20);
                    });

            var cliente2TagA =
                Task.Run(
                    async () =>
                    {
                        await gate2.Task;

                        return await coordinatorScope1
                            .ProcessarLeituraAsync(
                                "CLIENTE-2",
                                "TAG-A",
                                instante,
                                10);
                    });

            gate2.SetResult(true);

            var segundaRodada =
                await Task.WhenAll(
                    cliente1TagB,
                    cliente2TagA);

            Assert.All(
                segundaRodada,
                resultado =>
                    Assert.Empty(resultado));

            // Continuam existindo exatamente dois buffers, um por cliente.
            Assert.Equal(
                2,
                sharedStore.ObterQuantidadeBuffers());

            /*
             * A repetição de TAG-A no minuto seguinte fecha a rodada
             * anterior de cada cliente.
             */
            var gate3 =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            var finalizarCliente1 =
                Task.Run(
                    async () =>
                    {
                        await gate3.Task;

                        return await coordinatorScope1
                            .ProcessarLeituraAsync(
                                "CLIENTE-1",
                                "TAG-A",
                                instante.AddMinutes(1),
                                10);
                    });

            var finalizarCliente2 =
                Task.Run(
                    async () =>
                    {
                        await gate3.Task;

                        return await coordinatorScope2
                            .ProcessarLeituraAsync(
                                "CLIENTE-2",
                                "TAG-A",
                                instante.AddMinutes(1),
                                10);
                    });

            gate3.SetResult(true);

            var finalizados =
                await Task.WhenAll(
                    finalizarCliente1,
                    finalizarCliente2);

            var resultadoCliente1 =
                Assert.Single(
                    finalizados[0]);

            var resultadoCliente2 =
                Assert.Single(
                    finalizados[1]);

            Assert.Equal(
                "CLIENTE-1",
                resultadoCliente1.ClienteId);

            Assert.Equal(
                "CLIENTE-2",
                resultadoCliente2.ClienteId);

            Assert.Equal(
                "GRUPO-AB",
                resultadoCliente1.GroupName);

            Assert.Equal(
                "GRUPO-AB",
                resultadoCliente2.GroupName);

            Assert.True(
                resultadoCliente1.Aplicavel);

            Assert.True(
                resultadoCliente2.Aplicavel);

            Assert.False(
                resultadoCliente1.EhAnomalia);

            Assert.False(
                resultadoCliente2.EhAnomalia);

            Assert.Equal(
                2,
                resultadoCliente1.TagsUtilizadas.Count);

            Assert.Equal(
                2,
                resultadoCliente2.TagsUtilizadas.Count);

            Assert.Contains(
                "TAG-A",
                resultadoCliente1.TagsUtilizadas);

            Assert.Contains(
                "TAG-B",
                resultadoCliente1.TagsUtilizadas);

            Assert.Contains(
                "TAG-A",
                resultadoCliente2.TagsUtilizadas);

            Assert.Contains(
                "TAG-B",
                resultadoCliente2.TagsUtilizadas);

            /*
             * Após finalizar as rodadas anteriores, cada cliente abriu
             * sua nova rodada com TAG-A do minuto seguinte.
             */
            Assert.Equal(
                2,
                sharedStore.ObterQuantidadeBuffers());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(
                    tempRoot,
                    recursive: true);
            }
        }
    }


    [Fact]
    public async Task MesmaTagConcorrenteNoMesmoMinuto_NaoPodeContarComoDuasTags()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "IAApiTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempRoot);

        try
        {
            var provider =
                new StaticGrupoProvider(
                    new GrupoAnaliseDto
                    {
                        Id = 1,
                        Nome = "GRUPO-AB",
                        MinimumAvailableTags = 2,
                        MaxLagMinutes = 2,
                        Tags = ["TAG-A", "TAG-B"]
                    });

            var persistence =
                new MultivariateModelPersistenceService(
                    new FakeWebHostEnvironment(tempRoot));

            await GravarModeloAsync(
                persistence,
                "CLIENTE-1",
                "GRUPO-AB");

            var detector =
                new MultivariateDetector(
                    persistence);

            var sharedStore =
                new MultivariateRuntimeStateStore();

            /*
             * Dois scopes diferentes compartilhando o mesmo estado singleton,
             * simulando monitoramento + chamada manual/Swagger.
             */
            var coordinatorMonitor =
                new MultivariateShadowRuntimeCoordinator(
                    new MultivariateGroupBufferService(
                        new GrupoAnaliseService(provider),
                        sharedStore),
                    detector,
                    NullLogger<
                        MultivariateShadowRuntimeCoordinator>.Instance);

            var coordinatorSwagger =
                new MultivariateShadowRuntimeCoordinator(
                    new MultivariateGroupBufferService(
                        new GrupoAnaliseService(provider),
                        sharedStore),
                    detector,
                    NullLogger<
                        MultivariateShadowRuntimeCoordinator>.Instance);

            var instante =
                new DateTime(
                    2026, 9, 4, 15, 0, 0,
                    DateTimeKind.Unspecified);

            var gate =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            var leituraMonitor =
                Task.Run(
                    async () =>
                    {
                        await gate.Task;

                        return await coordinatorMonitor
                            .ProcessarLeituraAsync(
                                "CLIENTE-1",
                                "TAG-A",
                                instante,
                                10);
                    });

            var leituraSwagger =
                Task.Run(
                    async () =>
                    {
                        await gate.Task;

                        return await coordinatorSwagger
                            .ProcessarLeituraAsync(
                                "CLIENTE-1",
                                "TAG-A",
                                instante,
                                999);
                    });

            gate.SetResult(true);

            var duplicadas =
                await Task.WhenAll(
                    leituraMonitor,
                    leituraSwagger);

            /*
             * Nenhuma das duas chamadas pode produzir vetor/anomalia.
             * A segunda leitura da mesma tag no mesmo minuto apenas
             * atualiza/substitui o valor existente.
             */
            Assert.All(
                duplicadas,
                resultado =>
                    Assert.Empty(resultado));

            Assert.Equal(
                1,
                sharedStore.ObterQuantidadeBuffers());

            /*
             * Avança além do MaxLag sem fornecer TAG-B.
             *
             * Se as duas ocorrências de TAG-A fossem contadas como duas tags
             * distintas, o buffer teria MinimumAvailableTags=2 e seria
             * finalizado como vetor aplicável. O correto é expirar sem
             * produzir qualquer resultado.
             */
            var expirado =
                await coordinatorMonitor
                    .ProcessarLeituraAsync(
                        "CLIENTE-1",
                        "TAG-X",
                        instante.AddMinutes(3),
                        123);

            Assert.Empty(
                expirado);

            Assert.Equal(
                0,
                sharedStore.ObterQuantidadeBuffers());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(
                    tempRoot,
                    recursive: true);
            }
        }
    }


    [Fact]
    public async Task MesmaTagEmDoisGrupos_NaoPodeMisturarVetoresEntreGrupos()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "IAApiTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempRoot);

        try
        {
            var provider =
                new StaticGrupoProvider(
                    new GrupoAnaliseDto
                    {
                        Id = 1,
                        Nome = "GRUPO-AB",
                        MinimumAvailableTags = 2,
                        MaxLagMinutes = 2,
                        Tags = ["TAG-A", "TAG-B"]
                    },
                    new GrupoAnaliseDto
                    {
                        Id = 2,
                        Nome = "GRUPO-AC",
                        MinimumAvailableTags = 2,
                        MaxLagMinutes = 2,
                        Tags = ["TAG-A", "TAG-C"]
                    });

            var persistence =
                new MultivariateModelPersistenceService(
                    new FakeWebHostEnvironment(tempRoot));

            await GravarModeloAsync(
                persistence,
                "CLIENTE-1",
                "GRUPO-AB",
                segundaTag: "TAG-B",
                segundaMedia: 20);

            await GravarModeloAsync(
                persistence,
                "CLIENTE-1",
                "GRUPO-AC",
                segundaTag: "TAG-C",
                segundaMedia: 30);

            var detector =
                new MultivariateDetector(
                    persistence);

            var store =
                new MultivariateRuntimeStateStore();

            var coordinator =
                new MultivariateShadowRuntimeCoordinator(
                    new MultivariateGroupBufferService(
                        new GrupoAnaliseService(provider),
                        store),
                    detector,
                    NullLogger<
                        MultivariateShadowRuntimeCoordinator>.Instance);

            var instante =
                new DateTime(
                    2026, 9, 4, 15, 30, 0,
                    DateTimeKind.Unspecified);

            /*
             * TAG-A pertence aos dois grupos.
             * Deve abrir um buffer independente para cada grupo.
             */
            var r1 =
                await coordinator.ProcessarLeituraAsync(
                    "CLIENTE-1",
                    "TAG-A",
                    instante,
                    10);

            Assert.Empty(
                r1);

            Assert.Equal(
                2,
                store.ObterQuantidadeBuffers());

            /*
             * TAG-B só pode completar GRUPO-AB.
             * TAG-C só pode completar GRUPO-AC.
             */
            var r2 =
                await coordinator.ProcessarLeituraAsync(
                    "CLIENTE-1",
                    "TAG-B",
                    instante,
                    20);

            var r3 =
                await coordinator.ProcessarLeituraAsync(
                    "CLIENTE-1",
                    "TAG-C",
                    instante,
                    30);

            Assert.Empty(
                r2);

            Assert.Empty(
                r3);

            Assert.Equal(
                2,
                store.ObterQuantidadeBuffers());

            /*
             * TAG-A no minuto seguinte fecha os dois vetores anteriores.
             * Cada resultado deve conter somente as tags do seu grupo.
             */
            var finalizados =
                await coordinator.ProcessarLeituraAsync(
                    "CLIENTE-1",
                    "TAG-A",
                    instante.AddMinutes(1),
                    10);

            Assert.Equal(
                2,
                finalizados.Count);

            var grupoAb =
                Assert.Single(
                    finalizados.Where(
                        x => x.GroupName == "GRUPO-AB"));

            var grupoAc =
                Assert.Single(
                    finalizados.Where(
                        x => x.GroupName == "GRUPO-AC"));

            Assert.True(
                grupoAb.Aplicavel);

            Assert.True(
                grupoAc.Aplicavel);

            Assert.False(
                grupoAb.EhAnomalia);

            Assert.False(
                grupoAc.EhAnomalia);

            Assert.Equal(
                ["TAG-A", "TAG-B"],
                grupoAb.TagsUtilizadas
                    .OrderBy(x => x)
                    .ToArray());

            Assert.Equal(
                ["TAG-A", "TAG-C"],
                grupoAc.TagsUtilizadas
                    .OrderBy(x => x)
                    .ToArray());

            Assert.DoesNotContain(
                "TAG-C",
                grupoAb.TagsUtilizadas);

            Assert.DoesNotContain(
                "TAG-B",
                grupoAc.TagsUtilizadas);

            /*
             * Após fechar a rodada anterior, TAG-A abre novamente
             * um buffer independente para cada grupo.
             */
            Assert.Equal(
                2,
                store.ObterQuantidadeBuffers());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(
                    tempRoot,
                    recursive: true);
            }
        }
    }

    [Fact]
    public void CriarMinuteIndex_MesmoMinutoProduzMesmoIndice()
    {
        var a =
            new DateTime(
                2026, 9, 3, 10, 15, 1);

        var b =
            new DateTime(
                2026, 9, 3, 10, 15, 59);

        Assert.Equal(
            MultivariateShadowRuntimeCoordinator
                .CriarMinuteIndex(a),
            MultivariateShadowRuntimeCoordinator
                .CriarMinuteIndex(b));
    }

    private static async Task GravarModeloAsync(
        MultivariateModelPersistenceService persistence,
        string clienteId,
        string groupName,
        string segundaTag = "TAG-B",
        double segundaMedia = 20)
    {
        var caminho =
            persistence.ObterCaminhoModelo(
                clienteId,
                groupName);

        Directory.CreateDirectory(
            Path.GetDirectoryName(caminho)!);

        var model =
            new MultivariateGroupModel
            {
                Version = 1,
                ClienteId = clienteId,
                GroupName = groupName,
                TrainedAtUtc = DateTime.UtcNow,
                Algorithm = "Mahalanobis",
                SampleCount = 100,
                MinimumAvailableTags = 2,
                MaxLagMinutes = 2,
                Tags = ["TAG-A", segundaTag],
                Means = [10, segundaMedia],
                StandardDeviations = [1, 1],
                CovarianceMatrix =
                [
                    [1, 0],
                    [0, 1]
                ],
                CorrelationMatrix =
                [
                    [1, 0],
                    [0, 1]
                ]
            };

        await File.WriteAllTextAsync(
            caminho,
            JsonSerializer.Serialize(model));

        persistence.InvalidarCache(
            clienteId,
            groupName);
    }

    private sealed class StaticGrupoProvider
        : IGrupoAnaliseProvider
    {
        private readonly List<GrupoAnaliseDto> _grupos;

        public StaticGrupoProvider(
            params GrupoAnaliseDto[] grupos)
        {
            _grupos = grupos.ToList();
        }

        public Task<List<GrupoAnaliseDto>>
            CarregarGruposAsync(
                string clienteId)
        {
            return Task.FromResult(
                _grupos.ToList());
        }
    }

    private sealed class ThrowingGrupoProvider
        : IGrupoAnaliseProvider
    {
        public Task<List<GrupoAnaliseDto>>
            CarregarGruposAsync(
                string clienteId)
        {
            throw new InvalidOperationException(
                "Falha simulada do provider.");
        }
    }

    private sealed class FakeWebHostEnvironment
        : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public FakeWebHostEnvironment(
            string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string ApplicationName { get; set; } =
            "IAApi.Tests";

        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();

        public string? WebRootPath { get; set; }

        public string EnvironmentName { get; set; } =
            "Development";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
