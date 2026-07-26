import type { WorkflowTemplate } from '@/types/workflowTemplate';

/**
 * Platform-default WorkflowTemplate: the baseline state list from
 * 06_Domain_Reference_Data.md §2 (SA-002 §5), unchanged. Applies to every
 * tenant unless they have their own WorkflowTemplate (e.g. GB Skips).
 *
 * Re-synced 2026-07-25 against 06_Domain_Reference_Data.md's own revision
 * history: `EXTRACTED` added (real, shipped status per WP-012/WP-049,
 * previously missing from this fixture), `DUPLICATE_SUSPECTED` removed
 * (retired per WP-047/053/057 — duplicate handling is the existing
 * `isPotentialDuplicate`/`duplicateCheckReason` flag on Invoice, not a
 * status). 13 statuses, matching the live database per
 * status-postwb-057.md §2.6.
 */
export const platformDefaultWorkflowTemplate: WorkflowTemplate = {
  tenantId: 'platform-default',
  templateName: 'Platform Default',
  statuses: [
    { code: 'RECEIVED', name: 'Received', isTerminal: false, order: 1 },
    { code: 'PROCESSING', name: 'Processing', isTerminal: false, order: 2 },
    { code: 'EXTRACTED', name: 'Extracted', isTerminal: false, order: 3 },
    { code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, order: 4 },
    { code: 'NEEDS_QUERY', name: 'Needs Query', isTerminal: false, order: 5 },
    { code: 'QUERY_RAISED', name: 'Query Raised', isTerminal: false, order: 6 },
    { code: 'AWAITING_SUPPLIER_RESPONSE', name: 'Awaiting Supplier Response', isTerminal: false, order: 7 },
    { code: 'APPROVED', name: 'Approved', isTerminal: false, order: 8 },
    { code: 'REJECTED', name: 'Rejected', isTerminal: false, order: 9 },
    { code: 'CANCELLED', name: 'Cancelled', isTerminal: false, order: 10 },
    { code: 'READY_FOR_PAYMENT', name: 'Ready for Payment', isTerminal: false, order: 11 },
    { code: 'PAID', name: 'Paid', isTerminal: false, order: 12 },
    { code: 'ARCHIVED', name: 'Archived', isTerminal: true, order: 13 },
  ],
};
