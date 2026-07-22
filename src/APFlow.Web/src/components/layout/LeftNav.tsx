import { useState } from 'react';
import { NavLink as RouterNavLink } from 'react-router-dom';
import { useWorkflowTemplate } from '@/api/useWorkflowTemplate';
import { buildNavSections, type NavSection } from '@/components/layout/navConfig';

const linkBaseClasses =
  'block rounded-md px-3 py-2 text-sm font-medium transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600';

function navLinkClasses(isActive: boolean): string {
  return [
    linkBaseClasses,
    isActive ? 'bg-ink-800 text-white' : 'text-slate-600 hover:bg-slate-100 hover:text-ink-900',
  ].join(' ');
}

function InvoiceQueueSection({ section }: { section: NavSection }) {
  const [isExpanded, setIsExpanded] = useState(true);
  const hasChildren = (section.children?.length ?? 0) > 0;

  return (
    <li>
      <div className="flex items-center">
        <RouterNavLink
          to={section.path}
          end
          className={({ isActive }) => `${navLinkClasses(isActive)} flex-1`}
        >
          {section.label}
        </RouterNavLink>
        {hasChildren && (
          <button
            type="button"
            onClick={() => setIsExpanded((previous) => !previous)}
            aria-expanded={isExpanded}
            aria-controls="invoice-queue-status-links"
            className="ml-1 rounded-md p-2 text-slate-400 hover:bg-slate-100 hover:text-ink-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
          >
            <span className="sr-only">
              {isExpanded ? 'Collapse Invoice Queue statuses' : 'Expand Invoice Queue statuses'}
            </span>
            <svg
              aria-hidden="true"
              viewBox="0 0 20 20"
              fill="currentColor"
              className={`h-4 w-4 transition-transform ${isExpanded ? 'rotate-90' : ''}`}
            >
              <path d="M7.05 4.55a.75.75 0 0 1 1.06 0l4.72 4.72a1.06 1.06 0 0 1 0 1.5l-4.72 4.72a.75.75 0 1 1-1.06-1.06L11.24 10 7.05 5.81a.75.75 0 0 1 0-1.06Z" />
            </svg>
          </button>
        )}
      </div>
      {hasChildren && isExpanded && (
        <ul id="invoice-queue-status-links" className="mt-1 space-y-0.5 border-l border-slate-200 pl-3">
          {section.children!.map((link) => (
            <li key={link.key}>
              <RouterNavLink
                to={link.path}
                className={({ isActive }) => `${navLinkClasses(isActive)} text-[13px]`}
              >
                {link.label}
              </RouterNavLink>
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}

/**
 * Left navigation. Renders the static top-level sections from
 * STATIC_NAV_SECTIONS, plus Invoice Queue's per-status sub-links built from
 * the acting tenant's WorkflowTemplate (WP-050) — never a hardcoded array
 * of statuses, per WP-014 task 2.
 */
export function LeftNav() {
  const { template, isLoading, error } = useWorkflowTemplate();
  const sections = buildNavSections(template);

  return (
    <nav aria-label="Primary" className="flex h-full flex-col overflow-y-auto px-3 py-4">
      <ul className="space-y-1">
        {sections.map((section) =>
          section.key === 'invoice-queue' ? (
            <InvoiceQueueSection key={section.key} section={section} />
          ) : (
            <li key={section.key}>
              <RouterNavLink
                to={section.path}
                className={({ isActive }) => navLinkClasses(isActive)}
              >
                {section.label}
              </RouterNavLink>
            </li>
          ),
        )}
      </ul>

      {isLoading && (
        <p className="mt-4 px-3 text-xs text-slate-400" role="status">
          Loading workflow statuses…
        </p>
      )}
      {error && (
        <p className="mt-4 px-3 text-xs text-red-600" role="alert">
          {error}
        </p>
      )}
    </nav>
  );
}
