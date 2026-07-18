using System.Runtime.CompilerServices;

// Lets APFlow.Integrations.Tests construct EmailService directly (its constructor is
// internal because it takes the internal IGraphInboxReader - see GraphInboxReader.cs)
// and reference IGraphInboxReader/GraphInboxReader for hand-written test doubles.
[assembly: InternalsVisibleTo("APFlow.Integrations.Tests")]
