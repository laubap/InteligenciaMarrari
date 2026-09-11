using IAApi.Services.AnomalyDetection.Multivariate;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class MultivariateIndustrialReviewQueueControllerTests
{
    [Fact]
    public async Task RequisicaoValida_DeveRetornarFila()
    {
        var fake =
            new FakeQueueService();

        var controller =
            new MultivariateIndustrialReviewQueueController(
                fake);

        var action =
            await controller
                .BuildAsync(
                    "PK7",
                    "GRUPO-AB",
                    ValidRequest(),
                    CancellationToken.None);

        var ok =
            Assert.IsType<OkObjectResult>(
                action.Result);

        var result =
            Assert.IsType<
                MultivariateIndustrialReviewQueueResult>(
                    ok.Value);

        Assert.Equal(
            2,
            result.TotalEvents);

        Assert.Equal(
            1,
            fake.CallCount);

        Assert.Equal(
            "PK7",
            fake.LastClienteId);

        Assert.Equal(
            "GRUPO-AB",
            fake.LastGroupName);
    }

    [Fact]
    public async Task HoldoutSobreposto_DeveRetornarBadRequestSemChamarServico()
    {
        var fake =
            new FakeQueueService();

        var controller =
            new MultivariateIndustrialReviewQueueController(
                fake);

        var request =
            ValidRequest();

        request.HoldoutInicio =
            request.ReferenciaFim
                .AddMinutes(-1);

        var action =
            await controller
                .BuildAsync(
                    "PK7",
                    "GRUPO-AB",
                    request,
                    CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            action.Result);

        Assert.Equal(
            0,
            fake.CallCount);
    }

    [Fact]
    public async Task ThresholdInvalido_DeveRetornarBadRequestSemChamarServico()
    {
        var fake =
            new FakeQueueService();

        var controller =
            new MultivariateIndustrialReviewQueueController(
                fake);

        var request =
            ValidRequest();

        request.Threshold =
            100;

        var action =
            await controller
                .BuildAsync(
                    "PK7",
                    "GRUPO-AB",
                    request,
                    CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(
            action.Result);

        Assert.Equal(
            0,
            fake.CallCount);
    }

    private static MultivariateIndustrialReviewQueueRequest
        ValidRequest()
    {
        return new MultivariateIndustrialReviewQueueRequest
        {
            ReferenciaInicio =
                new DateTime(
                    2026,
                    9,
                    1),

            ReferenciaFim =
                new DateTime(
                    2026,
                    9,
                    3,
                    23,
                    59,
                    0),

            HoldoutInicio =
                new DateTime(
                    2026,
                    9,
                    4),

            HoldoutFim =
                new DateTime(
                    2026,
                    9,
                    4,
                    17,
                    0,
                    0),

            Threshold =
                75
        };
    }

    private sealed class FakeQueueService
        : IMultivariateIndustrialReviewQueueService
    {
        public int CallCount { get; private set; }

        public string? LastClienteId { get; private set; }

        public string? LastGroupName { get; private set; }

        public Task<MultivariateIndustrialReviewQueueResult>
            BuildAsync(
                string clienteId,
                string groupName,
                DateTime referenciaInicio,
                DateTime referenciaFim,
                DateTime holdoutInicio,
                DateTime holdoutFim,
                double threshold = 75,
                CancellationToken cancellationToken = default)
        {
            CallCount++;

            LastClienteId =
                clienteId;

            LastGroupName =
                groupName;

            return Task.FromResult(
                new MultivariateIndustrialReviewQueueResult
                {
                    ClienteId =
                        clienteId,

                    GroupName =
                        groupName,

                    Threshold =
                        threshold,

                    TotalEvents =
                        2,

                    PendingCount =
                        1,

                    ReviewedCount =
                        1,

                    Events =
                        Array.Empty<
                            MultivariateIndustrialReviewQueueEvent>()
                });
        }
    }
}
