import { useAuth } from '@/auth/useAuth';

interface HeaderProps {
  onToggleNav: () => void;
}

/**
 * Application header: menu toggle (mobile), product identity, and acting
 * tenant/user summary with sign-out.
 */
export function Header({ onToggleNav }: HeaderProps) {
  const { user, signOut } = useAuth();

  return (
    <header className="flex h-14 items-center justify-between border-b border-slate-200 bg-white px-4">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={onToggleNav}
          className="rounded-md p-2 text-slate-600 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 md:hidden"
          aria-label="Toggle navigation menu"
        >
          <svg aria-hidden="true" viewBox="0 0 20 20" fill="currentColor" className="h-5 w-5">
            <path
              fillRule="evenodd"
              d="M3 5.5A.75.75 0 0 1 3.75 4.75h12.5a.75.75 0 0 1 0 1.5H3.75A.75.75 0 0 1 3 5.5Zm0 5a.75.75 0 0 1 .75-.75h12.5a.75.75 0 0 1 0 1.5H3.75A.75.75 0 0 1 3 10.5Zm.75 4.25a.75.75 0 0 0 0 1.5h12.5a.75.75 0 0 0 0-1.5H3.75Z"
              clipRule="evenodd"
            />
          </svg>
        </button>
        <span className="text-base font-semibold text-ink-900">AP Flow</span>
      </div>

      {user && (
        <div className="flex items-center gap-3">
          <div className="hidden text-right sm:block">
            <p className="text-sm font-medium text-ink-900">{user.displayName}</p>
            <p className="text-xs text-slate-400">{user.tenantName}</p>
          </div>
          <button
            type="button"
            onClick={signOut}
            className="rounded-md px-3 py-1.5 text-sm font-medium text-slate-600 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
          >
            Sign out
          </button>
        </div>
      )}
    </header>
  );
}
