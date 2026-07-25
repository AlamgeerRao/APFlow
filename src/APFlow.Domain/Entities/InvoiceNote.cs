namespace APFlow.Domain.Entities;

/// <summary>
/// A freeform note/remark recorded against an invoice, e.g. by a reviewer. This is
/// plain annotation storage, not the "query workflow" (structured supplier
/// clarification/dispute process) that is explicit WP-009 out-of-scope - there is no
/// status, no addressee, no resolution state here, just a timestamped note with an
/// author (via <see cref="AuditEntity.CreatedBy"/>).
/// TenantEntity-derived (not just linked via <see cref="InvoiceId"/>) deliberately:
/// defense-in-depth per Security Standards §4, so tenant isolation on notes doesn't
/// depend solely on always joining through Invoice correctly.
/// </summary>
public sealed class InvoiceNote : TenantEntity
{
    /// <summary>The invoice this note is attached to.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Navigation property to the parent invoice.</summary>
    public Invoice? Invoice { get; set; }

    /// <summary>The note's text content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The human-readable name of the note's author, e.g. "Priya Shah" (WP-055).
    /// Populated once, at creation, from <c>ICurrentUserService</c>'s name claim on
    /// the validated Entra token - never recomputed later, so it reflects the
    /// author's display name AT THE TIME the note was written even if their Entra
    /// profile name later changes. Deliberately separate from
    /// <see cref="AuditEntity.CreatedBy"/> (which carries the author's stable
    /// object id, not a display-friendly name, and exists on every audited entity
    /// for a different purpose - see that property's own doc comment). Nullable:
    /// a caller's token may not carry a name claim (see
    /// <c>CurrentUserService.DisplayName</c>'s own doc comment for the exact claim
    /// and fallback used).
    /// </summary>
    public string? AuthorDisplayName { get; set; }
}
