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
export const STATIC_NAV_SECTIONS: Omit<NavSection, 'children'>[] = [
  { key: 'dashboard', label: 'Dashboard', path: '/dashboard' },
  { key: 'inbox', label: 'Inbox', path: '/inbox' },
  { key: 'invoice-queue', label: 'Invoice Queue', path: '/invoices' },
  { key: 'query-queue', label: 'Query Queue', path: '/queries' },
  { key: 'approved', label: 'Approved', path: '/approved' },
  { key: 'suppliers', label: 'Suppliers', path: '/suppliers' },
  { key: 'administration', label: 'Administration', path: '/administration' },
];

/**
 * Builds one sub-link per non-terminal status in the acting tenant's
 * WorkflowTemplate, ordered by StatusReference.order. This is the only
 * data-driven part of the nav — it is what makes GB Skips' extra states
 * (CHECKED_READY_TO_APPROVE, NEEDS_REVIEW_FEBINA) appear without a code
 * change, per WP-014 task 2.
 */
export function buildInvoiceQueueLinks(template: WorkflowTemplate): NavLink[] {
  return [...template.statuses]
    .filter((status: StatusReference) => !status.isTerminal)
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
