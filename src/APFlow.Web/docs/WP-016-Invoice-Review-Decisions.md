# WP-016 — Invoice Review Screen — Decisions

**Status:** OPEN — implemented with reasoned defaults; needs explicit sign-off.
**Role:** Senior React Engineer
**Depends on:** WP-008 (Document Intelligence extraction), WP-009 (Invoice Domain Model), WP-011 (Query Services), WP-013 (Audit Logging) — all implemented on the backend, but none of their report/API contracts were available when WP-016 was built.

---

## 1. PDF rendering approach — Architect-approved

Per `02_Project_Standards.md` §2, a ruling was requested and given before implementation began: **Option A, the native browser PDF viewer**, approved over adding a PDF.js-based library. No new dependency was introduced. Implemented via `<object type="application/pdf">`, which:

- Uses the browser's built-in PDF renderer — zero new npm packages, zero bundle size impact.
- Includes fallback content as `<object>`'s children (rendered automatically by supporting browsers when the resource can't be displayed inline), per the HTML spec.
- **Additionally** shows a persistent "Open in new tab" / "Download" link pair alongside the viewer at all times (not only as fallback), per the Architect's implementation note that `<object>` fallback rendering isn't reliably triggered in all locked-down corporate browsers.

Previous/Next navigation swaps the `<object data>` URL directly and resets to page 1 on every navigation — confirmed by the Architect as expected behaviour, not a defect, since each invoice is a distinct document with no state to preserve across it.

## 2. Data source for now

Same pattern as WP-014/WP-015: since none of WP-008/WP-009/WP-013's real contracts were available, this delivery implements:

- `src/types/invoiceDetail.ts` — `InvoiceDetail` (extends WP-015's `InvoiceListItem`), `ExtractedField`, `AuditEntry` (proposed, additive only).
- `src/api/invoiceDetailClient.ts` — an `InvoiceDetailClient` interface, with a `FixtureInvoiceDetailClient` backed by `src/api/fixtures/invoiceDetails.fixture.ts` (4 fully-detailed fixture invoices spanning both tenants, high/medium/low confidence, and duplicate/non-duplicate cases).
- The only confirmed fields are those already established by the WP-012 report (`isPotentialDuplicate`, `duplicateCheckReason`, `sourceDocumentBlobName`); `extractedFields`, `overallConfidenceScore`, `auditEntries`, and `pdfUrl` are reasoned proposals.
- All consumers depend only on the `InvoiceDetailClient` interface — swapping in a real HTTP client is a one-line change.

**Proposed HTTP contract for the relevant backend WPs** (for reference, not binding):

```
GET /api/invoices/{invoiceId}

200 OK
{
  ...same fields as WP-015's InvoiceListItem...,
  "pdfUrl": "string",
  "sourceDocumentBlobName": "string",
  "receivedAt": "2026-07-01T08:12:00Z",
  "overallConfidenceScore": 0.96,
  "extractedFields": [
    { "fieldKey": "string", "label": "string", "value": "string", "confidenceScore": 0.98 }
  ],
  "auditEntries": [
    { "id": "string", "timestamp": "2026-07-01T08:12:00Z", "actor": "string", "action": "string", "description": "string" }
  ]
}
```

**Needs confirmation:** exact field names/casing; whether `pdfUrl` is a direct SAS URL from Blob Storage (WP-005) or a proxied API endpoint (likely preferable for access control/audit logging of who viewed a source document — but that's a backend decision, not made here); the real shape of Document Intelligence's per-field confidence output (WP-008); the real shape of an audit log entry (WP-013).

## 3. Confidence score thresholds

`src/utils/confidence.ts` classifies scores as high (≥0.85), medium (0.6–0.84), or low (<0.6), driving the badge colour (green/amber/red) on both the overall score and each extracted field. These thresholds are a **UI default, not a confirmed business rule** — no source document specifies what Document Intelligence confidence should be treated as "needs manual verification." Flagged for confirmation; trivial to change in one place if a different cutoff is specified.

## 4. Previous/Next navigation scope

**Decision taken:** traversal always uses the tenant's full non-terminal invoice list in the same default order as the unfiltered Invoice Queue (`invoiceDate` descending — WP-015's default), regardless of which search/filter/sort the user had active on the queue page they arrived from.

**Reasoning:** preserving the exact originating queue context (live search text, status filter, and sort) across navigation to a nested route would require either passing that state through router location state or re-deriving it from the URL, adding real complexity WP-016's task list didn't ask for ("Add Previous and Next invoice navigation" — no mention of preserving filter context). This is the simplest option that satisfies the literal requirement, per `02_Project_Standards.md` §1 (Simplicity First).

**Needs confirmation:** whether Previous/Next should instead respect the originating queue's active filters/sort (e.g. if a user filtered to "Awaiting Review" and searched "Northwind," Next should stay within that filtered set). If so, this is a natural follow-up enhancement, not a defect in what's delivered — flagging it now rather than silently assuming it's out of scope.

## 5. "Complete invoice details" vs. "extracted invoice fields" (tasks 1 and 2)

**Decision taken:** these are two distinct panels. "Complete invoice details" (task 1) = the canonical, persisted Invoice fields (the same ones from WP-015's list, plus Received date) — i.e. what the system has stored. "Extracted invoice fields" (task 2) = the raw Document Intelligence extraction output, each with its own confidence score — i.e. what OCR/AI read off the document, which is a different (and typically larger/messier) dataset than the canonical persisted fields.

**Reasoning:** this distinction matches how Document Intelligence-style extraction pipelines normally work (raw extraction feeds a review step; the canonical entity is the reviewed/accepted version), and gives task 5's "processing confidence score" somewhere meaningful to live (per-field, on the extraction data — not on the canonical entity, which doesn't have a confidence concept).

**Needs confirmation:** whether WP-009's actual `Invoice` entity has additional canonical fields (PO number, VAT number, due date, etc.) that should appear in the header summary rather than only in the extracted-fields panel.

## 6. Read-only scope maintained

No approve/reject/query/notes-editing affordance exists anywhere on this screen. The one interactive addition beyond WP-015 is making Invoice Queue rows navigate here — navigation itself performs no mutation, so WP-015's read-only scope is unaffected.

---

## AI Agent Rules acknowledgement

Per `02_Project_Standards.md` §7 and `01_Project_Context.md` §9, none of the above is presented as final. Items in §2, §3, §4, and §5 are implemented with reasoned defaults and flagged here for Chief Technical Architect / backend sign-off, consistent with WP-014/WP-015's precedent. §1 (PDF rendering) was explicitly ruled on before implementation and is not open.
