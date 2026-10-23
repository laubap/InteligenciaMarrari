using System.Net;
using IAApi.Controllers;
using IAApi.Infrastructure.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public sealed class IntelligenceLabHealthEndpointTests
{
    private const string Secret = "local-intelligence-lab-test-secret-0123456789";

    [Fact]
    public void DisabledFeature_Returns404()
    {
        var controller = CreateController(enabled: false);
        Assert.IsType<NotFoundResult>(controller.GetHealth(Secret).Result);
    }

    [Fact]
    public void ProductionEnvironment_Returns404()
    {
        var controller = CreateController(environment: Environments.Production);
        Assert.IsType<NotFoundResult>(controller.GetHealth(Secret).Result);
    }

    [Fact]
    public void RemoteRequest_Returns404()
    {
        var controller = CreateController(remoteAddress: IPAddress.Parse("192.0.2.40"));
        Assert.IsType<NotFoundResult>(controller.GetHealth(Secret).Result);
    }

    [Fact]
    public void MissingClientAddress_Returns404()
    {
        var controller = CreateController(missingRemoteAddress: true);
        Assert.IsType<NotFoundResult>(controller.GetHealth(Secret).Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void MissingOrWeakConfiguredKey_Returns404(string? configuredKey)
    {
        var controller = CreateController(configuredKey: configuredKey);
        Assert.IsType<NotFoundResult>(controller.GetHealth(Secret).Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-secret-that-does-not-match-the-expected-secret")]
    public void MissingOrWrongProvidedKey_Returns401(string? providedKey)
    {
        var controller = CreateController();
        Assert.IsType<UnauthorizedResult>(controller.GetHealth(providedKey).Result);
    }

    [Fact]
    public void NoBufferRegistered_Returns404()
    {
        var controller = CreateController(withBuffer: false);
        Assert.IsType<NotFoundResult>(controller.GetHealth(Secret).Result);
    }

    [Fact]
    public void ValidLocalRequest_ReturnsOnlyHealthCounters_WithoutChangingBuffer()
    {
        var buffer = new BoundedDiagnosticBuffer(capacity: 12, enabled: true);
        var controller = CreateController(buffer: buffer);

        var response = controller.GetHealth(Secret);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var snapshot = Assert.IsType<IntelligenceLabHealthSnapshot>(ok.Value);

        Assert.True(snapshot.Enabled);
        Assert.Equal(12, snapshot.BufferCapacity);
        Assert.Equal(0, snapshot.BufferedCount);
        Assert.Equal(0, snapshot.StoredTotal);
        Assert.Equal(0, buffer.Count);
        Assert.Equal("no-store", controller.Response.Headers["Cache-Control"].ToString());
    }

    [Fact]
    public void LoopbackIpv6_IsAllowed()
    {
        var controller = CreateController(remoteAddress: IPAddress.IPv6Loopback);
        Assert.IsType<OkObjectResult>(controller.GetHealth(Secret).Result);
    }

    private static IntelligenceLabHealthController CreateController(
        bool enabled = true,
        string environment = "Development",
        IPAddress? remoteAddress = default,
        string? configuredKey = Secret,
        bool withBuffer = true,
        BoundedDiagnosticBuffer? buffer = null,
        bool missingRemoteAddress = false)
    {
        var settings = new Dictionary<string, string?>
        {
            ["IntelligenceLab:Enabled"] = enabled.ToString(),
            ["IntelligenceLab:ReadAccessKey"] = configuredKey
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        IEnumerable<BoundedDiagnosticBuffer> buffers = withBuffer
            ? new[] { buffer ?? new BoundedDiagnosticBuffer(10, enabled: true) }
            : Array.Empty<BoundedDiagnosticBuffer>();

        var controller = new IntelligenceLabHealthController(
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
