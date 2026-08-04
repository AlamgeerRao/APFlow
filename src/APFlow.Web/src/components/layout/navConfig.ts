import type { WorkflowTemplate, StatusReference } from '@/types/workflowTemplate';

/** A single renderable nav link. */
export interface NavLink {
  key: string;
  label: string;
  path: string;
}

/** A top-level nav section: a static link, optionally with data-driven children. */
export interface NavSection {
  key: string;
  label: string;
  path: string;
  children?: NavLink[];
}

/**
 * Static top-level sections per WP-014 task 3. These render identically for
 * every tenant; only Invoice Queue's children are workflow-status-driven
 * (see buildInvoiceQueueLinks below and
 * docs/WP-014-Dashboard-Shell-Decisions.md for the scoping decision).
 */
/**
 * WP-074: "Approved" was removed entirely as a standalone top-level link -
 * it was a dead-end placeholder page, and approved invoices are already
 * reachable via the Invoice Queue (either its own "all statuses" view, or
 * a per-status nav sub-link if APPROVED is ever configured as non-terminal
 * for a tenant) - a second, separate nav entry duplicating that isn't
 * needed.
 */
export const STATIC_NAV_SECTIONS: Omit<NavSection, 'children'>[] = [
  { key: 'dashboard', label: 'Dashboard', path: '/dashboard' },
  { key: 'inbox', label: 'Inbox', path: '/inbox' },
  { key: 'invoice-queue', label: 'Invoice Queue', path: '/invoices' },
  { key: 'query-queue', label: 'Query Queue', path: '/queries' },
  { key: 'suppliers', label: 'Suppliers', path: '/suppliers' },
  { key: 'administration', label: 'Administration', path: '/administration' },
];

/**
 * WP-085: since WP-071, every invoice is created directly at
 * AWAITING_REVIEW, so these three statuses are permanently unreachable as
 * nav destinations in normal operation - a user clicking one always finds
 * an empty folder. They remain valid statuses in the data model, template,
 * and transition graph (a manually reopened invoice or a future
 * non-pipeline entry path could still reach them); only the nav's own
 * rendering is filtered here, by status code so this doesn't silently
 * break if the template's ordering ever changes.
 */
const NON_ACTIONABLE_NAV_STATUS_CODES: ReadonlySet<string> = new Set(['RECEIVED', 'PROCESSING', 'EXTRACTED']);

/**
 * Builds one sub-link per non-terminal, actionable status in the acting
 * tenant's WorkflowTemplate, ordered by StatusReference.order. This is the
 * only data-driven part of the nav — it is what makes GB Skips' extra
 * states (CHECKED_READY_TO_APPROVE, NEEDS_REVIEW_FEBINA) appear without a
 * code change, per WP-014 task 2.
 */
export function buildInvoiceQueueLinks(template: WorkflowTemplate): NavLink[] {
  return [...template.statuses]
    .filter((status: StatusReference) => !status.isTerminal && !NON_ACTIONABLE_NAV_STATUS_CODES.has(status.code))
    .sort((a, b) => a.order - b.order)
    .map((status) => ({
      key: status.code,
      label: status.name,
      path: `/invoices/${status.code.toLowerCase().replace(/_/g, '-')}`,
    }));
}

/** Assembles the full nav tree for the acting tenant's WorkflowTemplate. */
export function buildNavSections(template: WorkflowTemplate | null): NavSection[] {
  return STATIC_NAV_SECTIONS.map((section) => {
    if (section.key === 'invoice-queue' && template) {
      return { ...section, children: buildInvoiceQueueLinks(template) };
    }
    return section;
  });
}
