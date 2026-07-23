interface DuplicateWarningBannerProps {
  reason: string | null;
}

/** Prominent duplicate warning banner for the Review Screen (WP-016 task 4). */
export function DuplicateWarningBanner({ reason }: DuplicateWarningBannerProps) {
  return (
    <div role="alert" className="mb-4 flex items-start gap-3 rounded-md border border-amber-300 bg-amber-50 p-4">
      <svg aria-hidden="true" viewBox="0 0 20 20" fill="currentColor" className="mt-0.5 h-5 w-5 flex-shrink-0 text-amber-600">
        <path
          fillRule="evenodd"
          d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.63-1.516 2.63H3.72c-1.347 0-2.189-1.463-1.516-2.63L8.485 2.495ZM10 6a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 10 6Zm0 8a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z"
          clipRule="evenodd"
        />
      </svg>
      <div>
        <p className="text-sm font-semibold text-amber-800">Possible duplicate invoice</p>
        <p className="mt-0.5 text-sm text-amber-700">
          {reason ?? 'This invoice matched an existing invoice during duplicate detection.'}
        </p>
      </div>
    </div>
  );
}
