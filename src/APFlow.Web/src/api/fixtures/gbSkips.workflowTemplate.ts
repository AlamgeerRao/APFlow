import type { WorkflowTemplate } from '@/types/workflowTemplate';

/**
 * GB Skips tenant-specific WorkflowTemplate: the platform-default state
 * list with two additional states inserted between AWAITING_REVIEW and
 * APPROVED, per 06_Domain_Reference_Data.md §2 and WP-050 task 3.
 *
 * These two states are tenant-scoped data, not part of the shared/global
 * catalogue — modelled here purely as this tenant's WorkflowTemplate rows,
 * per the Domain Reference Data AI Agent Rules.
 */
export const gbSkipsWorkflowTemplate: WorkflowTemplate = {
  tenantId: 'gb-skips',
  templateName: 'GB Skips',
  statuses: [
    { code: 'RECEIVED', name: 'Received', isTerminal: false, order: 1 },
    { code: 'PROCESSING', name: 'Processing', isTerminal: false, order: 2 },
    { code: 'DUPLICATE_SUSPECTED', name: 'Duplicate Suspected', isTerminal: false, order: 3 },
    { code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, order: 4 },
    { code: 'CHECKED_READY_TO_APPROVE', name: 'Checked & Ready to Approve', isTerminal: false, order: 5 },
    { code: 'NEEDS_REVIEW_FEBINA', name: 'Needs Review by Febina', isTerminal: false, order: 6 },
    { code: 'NEEDS_QUERY', name: 'Needs Query', isTerminal: false, order: 7 },
    { code: 'QUERY_RAISED', name: 'Query Raised', isTerminal: false, order: 8 },
    { code: 'AWAITING_SUPPLIER_RESPONSE', name: 'Awaiting Supplier Response', isTerminal: false, order: 9 },
    { code: 'APPROVED', name: 'Approved', isTerminal: false, order: 10 },
    { code: 'REJECTED', name: 'Rejected', isTerminal: false, order: 11 },
    { code: 'CANCELLED', name: 'Cancelled', isTerminal: false, order: 12 },
    { code: 'READY_FOR_PAYMENT', name: 'Ready for Payment', isTerminal: false, order: 13 },
    { code: 'PAID', name: 'Paid', isTerminal: false, order: 14 },
    { code: 'ARCHIVED', name: 'Archived', isTerminal: true, order: 15 },
  ],
};
