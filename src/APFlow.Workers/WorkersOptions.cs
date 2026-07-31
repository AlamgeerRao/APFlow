namespace APFlow.Workers;

/// <summary>Binds the "Workers" configuration section.</summary>
public sealed class WorkersOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "Workers";

    /// <summary>
    /// How often <see cref="EmailIngestionWorker"/> polls the mailbox, in seconds.
    /// Defaults to 60 - demo-appropriate for this project's current stage; revisit
    /// once real invoice volume/Graph throttling considerations exist for a
    /// production-scale interval.
    /// </summary>
    public int EmailPollingIntervalSeconds { get; init; } = 60;
}
