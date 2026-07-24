using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Records audit trail entries (WP-013). Deliberately a pure "stage and describe"
/// service, not a "log and save" one - see <see cref="LogAsync"/>'s doc comment for
/// the reasoning, which mirrors the Chief Technical Architect's WP-010 ruling that
/// <c>DuplicateDetectionService</c> should stay a pure compute service and leave
/// persistence to its calling orchestrator.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Stages a new <see cref="Domain.Entities.AuditLog"/> entry (via
    /// <see cref="IAuditLogRepository.AddAsync"/>) but deliberately does NOT call
    /// <see cref="IAuditLogRepository.SaveChangesAsync"/> itself. The caller
    /// describing the change being audited (e.g. <c>InvoiceService.UpdateAsync</c>
    /// for an invoice status change) is expected to commit this staged entry
    /// together with that change, in the very next <c>SaveChangesAsync</c> call it
    /// was already making - both share the same <c>AppDbContext</c> instance within
    /// a request/pipeline-run scope, so one call persists both atomically. This
    /// means an audit entry can never be recorded for a change that didn't actually
    /// commit, or vice versa. A direct consequence: the <see cref="AuditLogDto"/>-shaped
    /// fields that would only be accurate after that save
    /// (<c>PerformedByUserId</c>, <c>PerformedAtUtc</c>) are not yet meaningful the
    /// moment this method returns - so this method returns only the new entry's id,
    /// not a full <see cref="AuditLogDto"/>, to avoid handing back fields that look
    /// populated but are not yet accurate. Fetch the entry via
    /// <see cref="IAuditQueryService"/> after the caller's save completes if the
    /// full read shape is needed. See docs/WP-013-Audit-Logging-Decisions.md.
    /// </summary>
    Task<Result<Guid>> LogAsync(RecordAuditLogRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages AND immediately commits an audit entry - for standalone audit-only
    /// writes where there is no other change happening in the same request to
    /// commit it together with (WP-052 Part D: <c>InvoicesController.Download</c>'s
    /// <c>DocumentViewed</c> entry on an otherwise read-only GET endpoint is the
    /// concrete case this exists for). <see cref="LogAsync"/> remains the right
    /// choice whenever there IS another change in flight (e.g.
    /// <c>InvoiceService.UpdateAsync</c>'s status-change entry) - see its own doc
    /// comment for why staging-without-saving matters there. This method exists
    /// precisely because that atomic-commit reasoning does not apply when nothing
    /// else is being saved.
    /// </summary>
    Task<Result<Guid>> LogAndSaveAsync(RecordAuditLogRequest request, CancellationToken cancellationToken = default);
}
