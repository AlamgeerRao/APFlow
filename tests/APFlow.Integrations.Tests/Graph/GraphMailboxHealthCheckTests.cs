using APFlow.Application.Interfaces;
using APFlow.Integrations.Graph;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace APFlow.Integrations.Tests.Graph;

public class GraphMailboxHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_MailboxConnected_ReturnsHealthy()
    {
        var healthCheck = new GraphMailboxHealthCheck(new FakeEmailService(isConnected: true));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_MailboxNotConnected_ReturnsDegraded_NotUnhealthy()
    {
        // Degraded, not Unhealthy, is deliberate - see GraphMailboxHealthCheck's
        // type-level doc comment. Degraded does not fail ASP.NET Core's default
        // health-check-to-HTTP-status mapping (both map to 200), so a Graph outage
        // does not take general API readiness offline the way a database outage does.
        var healthCheck = new GraphMailboxHealthCheck(new FakeEmailService(isConnected: false));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    private sealed class FakeEmailService : IEmailService
    {
        private readonly bool _isConnected;

        public FakeEmailService(bool isConnected)
        {
            _isConnected = isConnected;
        }

        public Task<bool> VerifyMailboxConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_isConnected);
    }
}
