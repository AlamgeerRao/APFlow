namespace APFlow.Infrastructure.Configuration;

/// <summary>
/// Binds the "AzureKeyVault" configuration section. Used at startup to wire the
/// Key Vault configuration provider so secrets are never stored in appsettings
/// or source control, per the Security Standards.
/// </summary>
public sealed class KeyVaultOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "AzureKeyVault";

    /// <summary>The Key Vault URI, e.g. https://apflow-{env}.vault.azure.net/.</summary>
    public string VaultUri { get; init; } = string.Empty;

    /// <summary>Whether Key Vault should be loaded as a configuration source at startup.</summary>
    public bool Enabled { get; init; }
}
