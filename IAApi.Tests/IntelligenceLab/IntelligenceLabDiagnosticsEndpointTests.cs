using System.Net;
using IAApi.Controllers;
using IAApi.Infrastructure.Observability;
using IAApi.IntelligenceLab.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace IAApi.Tests.IntelligenceLab;

public sealed class IntelligenceLabDiagnosticsEndpointTests
{
    private const string Secret = "local-intelligence-lab-test-secret-0123456789";

    [Fact]
    public void ValidList_ReturnsNewestFirst_OnlySummaries_AndDoesNotMutateBuffer()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 4, enabled: true);
        var first = Diagnostic("cliente-a", "A.TAG1");
        var second = Diagnostic("cliente-b", "B.TAG2");
        var third = Diagnostic("cliente-a", "A.TAG3");
        buffer.TryAdd(first);
        buffer.TryAdd(second);
        buffer.TryAdd(third);
        var controller = CreateController(buffer: buffer);

        var result = Assert.IsType<OkObjectResult>(controller.List(Secret, limit: 2).Result);
        var items = Assert.IsType<IntelligenceLabDiagnosticSummary[]>(result.Value);
        Assert.Equal(2, items.Length);
        Assert.Equal(third.Context.DiagnosticId, items[0].DiagnosticId);
        Assert.Equal(second.Context.DiagnosticId, items[1].DiagnosticId);
        Assert.Equal("cliente-a", items[0].ClienteId);
        Assert.Equal(DiagnosticCompletenessStatus.Partial, items[0].Completeness);
        Assert.Null(items[0].EnsembleScoreNormalized);
        Assert.Null(items[0].FinalAnomaly);
        Assert.Equal(3, buffer.Count);
        Assert.Equal("no-store", controller.Response.Headers["Cache-Control"].ToString());
    }

    [Fact]
    public void List_FiltersByExactClientAndTag_WithoutReturningOtherClients()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 5, enabled: true);
        var expected = Diagnostic("cliente-a", "TEMP.TAG");
        buffer.TryAdd(Diagnostic("cliente-b", "TEMP.TAG"));
        buffer.TryAdd(Diagnostic("cliente-a", "FLOW.TAG"));
        buffer.TryAdd(expected);
        var controller = CreateController(buffer: buffer);

        var result = Assert.IsType<OkObjectResult>(controller.List(
            Secret, clienteId: "cliente-a", tagName: "TEMP.TAG").Result);
        var item = Assert.Single(Assert.IsType<IntelligenceLabDiagnosticSummary[]>(result.Value));
        Assert.Equal(expected.Context.DiagnosticId, item.DiagnosticId);

        var differentCase = Assert.IsType<OkObjectResult>(controller.List(
            Secret, clienteId: "CLIENTE-A").Result);
        Assert.Empty(Assert.IsType<IntelligenceLabDiagnosticSummary[]>(differentCase.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void List_RejectsInvalidLimits(int limit)
    {
        var controller = CreateController();
        Assert.IsType<BadRequestObjectResult>(controller.List(Secret, limit: limit).Result);
    }

    [Fact]
    public void List_RejectsInvalidFilterValues()
    {
        var controller = CreateController();
        Assert.IsType<BadRequestObjectResult>(controller.List(Secret, clienteId: " ").Result);
        Assert.IsType<BadRequestObjectResult>(controller.List(Secret, tagName: new string('x', 129)).Result);
    }

    [Fact]
    public void List_WithoutRecords_ReturnsEmptyArray()
    {
        var controller = CreateController();
        var result = Assert.IsType<OkObjectResult>(controller.List(Secret).Result);
        Assert.Empty(Assert.IsType<IntelligenceLabDiagnosticSummary[]>(result.Value));
    }

    [Fact]
    public void Detail_ReturnsOriginalDiagnostic_WithoutChangingBuffer()
    {
        var buffer = new BoundedDiagnosticBuffer(2, enabled: true);
        var diagnostic = Diagnostic("cliente-a", "A.TAG1");
        buffer.TryAdd(diagnostic);
        var controller = CreateController(buffer: buffer);

        var result = Assert.IsType<OkObjectResult>(
            controller.GetById(diagnostic.Context.DiagnosticId, Secret).Result);
        Assert.Same(diagnostic, result.Value);
        Assert.Equal(1, buffer.Count);
        Assert.Equal("no-store", controller.Response.Headers["Cache-Control"].ToString());
    }

    [Fact]
    public void Detail_UnknownEmptyAndEvictedIds_Return404()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 1, enabled: true);
        var old = Diagnostic("cliente-a", "A.TAG1");
        buffer.TryAdd(old);
        buffer.TryAdd(Diagnostic("cliente-a", "A.TAG2"));
        var controller = CreateController(buffer: buffer);

        Assert.IsType<NotFoundResult>(controller.GetById(old.Context.DiagnosticId, Secret).Result);
        Assert.IsType<NotFoundResult>(controller.GetById(Guid.NewGuid(), Secret).Result);
        Assert.IsType<NotFoundResult>(controller.GetById(Guid.Empty, Secret).Result);
    }

    [Theory]
    [InlineData(false, "Development")]
    [InlineData(true, "Production")]
    [InlineData(true, "Staging")]
    public void DisabledOrNonDevelopment_DeniesBothEndpoints(bool enabled, string environment)
    {
        var controller = CreateController(enabled: enabled, environment: environment);
        Assert.IsType<NotFoundResult>(controller.List(Secret).Result);
        Assert.IsType<NotFoundResult>(controller.GetById(Guid.NewGuid(), Secret).Result);
    }

    [Fact]
    public void NonLoopbackAndMissingRemoteAddress_DenyBothEndpoints()
    {
        var remote = CreateController(remoteAddress: IPAddress.Parse("192.0.2.40"));
        Assert.IsType<NotFoundResult>(remote.List(Secret).Result);
        Assert.IsType<NotFoundResult>(remote.GetById(Guid.NewGuid(), Secret).Result);
        var missing = CreateController(missingRemoteAddress: true);
        Assert.IsType<NotFoundResult>(missing.List(Secret).Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    public void MissingOrWeakConfiguredKey_DeniesBothEndpoints(string? configuredKey)
    {
        var controller = CreateController(configuredKey: configuredKey);
        Assert.IsType<NotFoundResult>(controller.List(Secret).Result);
        Assert.IsType<NotFoundResult>(controller.GetById(Guid.NewGuid(), Secret).Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("incorrect-local-key-01234567890123456789")]
    public void WrongProvidedKey_Returns401ForBothEndpoints(string? providedKey)
    {
        var controller = CreateController();
        Assert.IsType<UnauthorizedResult>(controller.List(providedKey).Result);
        Assert.IsType<UnauthorizedResult>(controller.GetById(Guid.NewGuid(), providedKey).Result);
    }

    [Fact]
    public void ExcessivelyLongProvidedKey_IsRejected()
    {
        var controller = CreateController();
        Assert.IsType<UnauthorizedResult>(controller.List(new string('x', 257)).Result);
    }

    [Fact]
    public void NoRegisteredBuffer_DeniesBothEndpoints()
    {
        var controller = CreateController(withBuffer: false);
        Assert.IsType<NotFoundResult>(controller.List(Secret).Result);
        Assert.IsType<NotFoundResult>(controller.GetById(Guid.NewGuid(), Secret).Result);
    }

    [Fact]
    public void DisabledBuffer_DeniesBothEndpoints()
    {
        var controller = CreateController(buffer: new BoundedDiagnosticBuffer(2, enabled: false));
        Assert.IsType<NotFoundResult>(controller.List(Secret).Result);
        Assert.IsType<NotFoundResult>(controller.GetById(Guid.NewGuid(), Secret).Result);
    }

    [Fact]
    public void Ipv6Loopback_AllowsBothEndpoints()
    {
        var buffer = new BoundedDiagnosticBuffer(2, enabled: true);
        var d = Diagnostic("cliente-a", "TAG");
        buffer.TryAdd(d);
        var controller = CreateController(buffer: buffer, remoteAddress: IPAddress.IPv6Loopback);
        Assert.IsType<OkObjectResult>(controller.List(Secret).Result);
        Assert.IsType<OkObjectResult>(controller.GetById(d.Context.DiagnosticId, Secret).Result);
    }

    [Fact]
    public void UnauthenticatedRequests_DontRevealDataEvenWhenBufferHasRecords()
    {
        var buffer = new BoundedDiagnosticBuffer(2, enabled: true);
        var d = Diagnostic("sensitive-client", "SENSITIVE.TAG");
        buffer.TryAdd(d);
        var controller = CreateController(buffer: buffer);
        Assert.IsType<UnauthorizedResult>(controller.List(null).Result);
        Assert.IsType<UnauthorizedResult>(controller.GetById(d.Context.DiagnosticId, null).Result);
        Assert.Equal(1, buffer.Count);
        Assert.Equal("no-store", controller.Response.Headers["Cache-Control"].ToString());
    }

    private static IntelligenceLabReadingDiagnostic Diagnostic(string client, string tag) => new(
        DiagnosticContext.CreateNew(),
        new DiagnosticVersionMetadata("api-test", "build-test", "commit-test", "config-test"),
        client, tag, DateTimeOffset.UtcNow, 100,
        new DiagnosticCompleteness(DiagnosticCompletenessStatus.Partial, reason: "Teste local."),
        new DiagnosticTiming(TimeSpan.Zero));

    private static IntelligenceLabDiagnosticsController CreateController(
        bool enabled = true,
        string environment = "Development",
        IPAddress? remoteAddress = null,
        string? configuredKey = Secret,
        bool withBuffer = true,
        BoundedDiagnosticBuffer? buffer = null,
        bool missingRemoteAddress = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IntelligenceLab:Enabled"] = enabled.ToString(),
                ["IntelligenceLab:ReadAccessKey"] = configuredKey
            })
            .Build();

        IEnumerable<BoundedDiagnosticBuffer> buffers = withBuffer
            ? new[] { buffer ?? new BoundedDiagnosticBuffer(10, enabled: true) }
            : Array.Empty<BoundedDiagnosticBuffer>();

        var controller = new IntelligenceLabDiagnosticsController(
            buffers, configuration, new StubEnvironment(environment));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = missingRemoteAddress
            ? null
            : remoteAddress ?? IPAddress.Loopback;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private sealed class StubEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "IAApi.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
