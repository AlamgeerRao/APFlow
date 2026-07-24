/**
 * Client-side shape for a single invoice note (WP-017 — Invoice Notes &
 * Collaboration).
 *
 * PROVISIONAL CONTRACT: the backend already has the domain model and
 * application logic for notes (`APFlow.Domain.Entities.InvoiceNote`,
 * `IInvoiceService.AddNoteAsync`, `IInvoiceRepository.GetByIdWithNotesAsync`),
 * but — unlike WP-015/WP-016's dependencies — no `InvoicesController` route
 * or response DTO exists yet for notes at all (no `GET`/`POST` under
 * `api/invoices/{id}/notes`). This shape is a reasoned proposal, documented
 * in docs/WP-017-Invoice-Notes-Decisions.md and flagged there for Chief
 * Technical Architect / Backend Engineer confirmation, following the same
 * precedent WP-014/WP-015/WP-016 used for their own unavailable contracts.
 *
 * Field names deliberately mirror what the backend already has:
 * `content` matches `InvoiceNote.Content`; `authorName` stands in for
 * `AuditEntity.CreatedBy` resolved to a display name (the entity only
 * stores a raw identifier — resolving it to a friendly name is assumed to
 * be a backend/API concern, not something this client should do); `id` and
 * `createdAtUtc` match `BaseEntity`/`AuditEntity`'s own field names.
 */
export interface InvoiceNote {
  id: string;
  /** The note's text content. May contain line breaks (multiline notes are supported). */
  content: string;
  /** Display name of the note's author. Stands in for a resolved `AuditEntity.CreatedBy`. */
  authorName: string;
  /** ISO 8601 timestamp the note was created. Notes are never edited or deleted, so there is no "modified" concept. */
  createdAtUtc: string;
}

/**
 * Matches `FieldLimits.InvoiceNoteContent` (APFlow.Application.Common),
 * duplicated here for the same reason that file duplicates it from the EF
 * Core column configuration: Web cannot reference Application (Solution
 * Structure §2), so this must be kept in sync by hand if the backend limit
 * ever changes.
 */
export const INVOICE_NOTE_CONTENT_MAX_LENGTH = 4000;
