using System.Runtime.CompilerServices;

// Lets APFlow.Infrastructure.Tests construct BlobStorageService directly (its
// constructor is internal because it takes the internal IBlobContainerOperations -
// see BlobContainerOperations.cs) and reference IBlobContainerOperations for
// hand-written test doubles. Same pattern as APFlow.Integrations' AssemblyInfo.cs
// (WP-004).
[assembly: InternalsVisibleTo("APFlow.Infrastructure.Tests")]
