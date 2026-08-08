using APFlow.Api.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace APFlow.Api.Tests.Extensions;

public class HealthCheckResponseTests
{
    [Fact]
    public void BuildHealthCheckResponse_AllHealthy_ReturnsHealthyStatusAndEveryCheck()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database"] = new(HealthStatus.Healthy, description: null, duration: TimeSpan.Zero, exception: null, data: null),
                ["graph-mailbox"] = new(HealthStatus.Healthy, description: null, duration: TimeSpan.Zero, exception: null, data: null),
                ["blob-storage"] = new(HealthStatus.Healthy, description: null, duration: TimeSpan.Zero, exception: null, data: null),
            },
            HealthStatus.Healthy,
            TimeSpan.Zero);

        var response = ApiServiceCollectionExtensions.BuildHealthCheckResponse(report);

        Assert.Equal("Healthy", response.Status);
        Assert.Equal(3, response.Checks.Count);
        Assert.Contains(response.Checks, c => c.Name == "database" && c.Status == "Healthy");
        Assert.Contains(response.Checks, c => c.Name == "graph-mailbox" && c.Status == "Healthy");
        Assert.Contains(response.Checks, c => c.Name == "blob-storage" && c.Status == "Healthy");
    }

    [Fact]
    public void BuildHealthCheckResponse_OneDegraded_SurfacesWhichCheckAndItsDescription()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database"] = new(HealthStatus.Healthy, description: null, duration: TimeSpan.Zero, exception: null, data: null),
                ["graph-mailbox"] = new(HealthStatus.Degraded, description: "Mailbox connection could not be verified.", duration: TimeSpan.Zero, exception: null, data: null),
            },
            HealthStatus.Degraded,
            TimeSpan.Zero);

        var response = ApiServiceCollectionExtensions.BuildHealthCheckResponse(report);

        Assert.Equal("Degraded", response.Status);
        var degraded = Assert.Single(response.Checks, c => c.Status == "Degraded");
        Assert.Equal("graph-mailbox", degraded.Name);
        Assert.Equal("Mailbox connection could not be verified.", degraded.Description);
    }

    [Fact]
    public void BuildHealthCheckResponse_NoChecksRun_ReturnsEmptyChecksList()
    {
        // Mirrors /health/live's real shape - Predicate excludes every registered
        // check, so the report carries zero entries.
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), HealthStatus.Healthy, TimeSpan.Zero);

        var response = ApiServiceCollectionExtensions.BuildHealthCheckResponse(report);

        Assert.Equal("Healthy", response.Status);
        Assert.Empty(response.Checks);
    }
}
