interface DuplicateIndicatorProps {
  reason: string | null;
}

/**
 * Visual + accessible indicator for a row flagged as a potential
 * duplicate (Invoice.IsPotentialDuplicate, per WP-010/WP-012). Renders
 * nothing when the invoice is not flagged.
 */
export function DuplicateIndicator({ reason }: DuplicateIndicatorProps) {
  return (
    <span
      className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-medium text-amber-800"
      title={reason ?? 'Potential duplicate'}
    >
      <svg aria-hidden="true" viewBox="0 0 20 20" fill="currentColor" className="h-3.5 w-3.5">
        <path
          fillRule="evenodd"
          d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.63-1.516 2.63H3.72c-1.347 0-2.189-1.463-1.516-2.63L8.485 2.495ZM10 6a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 10 6Zm0 8a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z"
          clipRule="evenodd"
        />
      </svg>
      <span>Possible duplicate</span>
      <span className="sr-only">{reason ? `: ${reason}` : ''}</span>
    </span>
  );
}
