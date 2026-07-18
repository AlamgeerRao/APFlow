using System.Runtime.CompilerServices;

// Lets APFlow.Infrastructure (specifically AppDbContext) set the internal-only audit
// field setters on AuditEntity - see AuditEntity.cs. This turns "do not set these
// directly from application code" from a doc-comment convention into a compiler-
// enforced rule: only Infrastructure can write these properties.
[assembly: InternalsVisibleTo("APFlow.Infrastructure")]
