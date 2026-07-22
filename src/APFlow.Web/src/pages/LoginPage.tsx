import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '@/auth/useAuth';
import type { ActingUser } from '@/auth/authContextDefinition';

/**
 * Sign-in stub. Real authentication (Microsoft Entra External ID) is owned
 * by WP-002 and is out of scope for WP-014 ("No business functionality").
 *
 * This offers a choice between the two tenant fixtures purely so the
 * data-driven nav can be verified locally against both the platform-default
 * tenant and GB Skips' extended tenant, per WP-014's acceptance criteria.
 * It is replaced wholesale once WP-002's real sign-in flow lands.
 */
const DEMO_USERS: ActingUser[] = [
  { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Alex Reviewer' },
  { tenantId: 'gb-skips', tenantName: 'GB Skips', displayName: 'Patrick (GB Skips)' },
];

export function LoginPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const redirectTo = (location.state as { from?: Location })?.from?.pathname ?? '/dashboard';

  function handleSignIn(user: ActingUser) {
    signIn(user);
    navigate(redirectTo, { replace: true });
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-lg font-semibold text-ink-900">Sign in to AP Flow</h1>
        <p className="mt-1 text-sm text-slate-600">
          Select a tenant to continue. (Temporary stand-in for Entra sign-in — see WP-002.)
        </p>
        <div className="mt-6 space-y-2">
          {DEMO_USERS.map((user) => (
            <button
              key={user.tenantId}
              type="button"
              onClick={() => handleSignIn(user)}
              className="w-full rounded-md border border-slate-200 px-4 py-2 text-left text-sm font-medium text-ink-900 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
            >
              {user.tenantName}
              <span className="block text-xs font-normal text-slate-400">{user.displayName}</span>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
