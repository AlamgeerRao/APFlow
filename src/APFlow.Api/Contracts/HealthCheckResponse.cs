namespace APFlow.Api.Contracts;

/// <summary>
/// Response shape for <c>/health/live</c> and <c>/health/ready</c> (WP-043's
/// System Status page). Neither endpoint had a <c>ResponseWriter</c>
/// configured before this WP (see <c>ApiServiceCollectionExtensions.UseApiHealthChecks</c>'s
/// own doc comment) — ASP.NET Core's default health-check middleware writes
/// only the aggregate status as plain text, with no per-check detail. This
/// record is built directly from a <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport"/>
/// (see <c>ApiServiceCollectionExtensions.BuildHealthCheckResponse</c>) and
/// serialized with an explicit camelCase policy — this endpoint sits outside
/// the controller pipeline, so it does not inherit <c>AddControllers</c>'s
/// own default naming policy the way every other endpoint in this API does.
/// </summary>
public sealed record HealthCheckResponse(string Status, IReadOnlyList<HealthCheckEntryResponse> Checks);

/// <summary>One dependency check's result within a <see cref="HealthCheckResponse"/>.</summary>
public sealed record HealthCheckEntryResponse(string Name, string Status, string? Description);
