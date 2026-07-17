namespace APFlow.Api.Configuration;

/// <summary>Binds the "Application" configuration section used for general app metadata.</summary>
public sealed class ApplicationOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "Application";

    /// <summary>The display name of this application, used in logs and diagnostics.</summary>
    public string Name { get; init; } = "APFlow.Api";

    /// <summary>The deployment environment name (e.g. Development, Production).</summary>
    public string Environment { get; init; } = string.Empty;
}
