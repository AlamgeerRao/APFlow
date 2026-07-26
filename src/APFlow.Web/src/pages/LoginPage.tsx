import { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '@/auth/useAuth';

/**
 * Sign-in page (WP-020): triggers real Entra External ID sign-in via
 * MSAL, replacing WP-014's demo-tenant/role picker entirely. Redirects
 * away automatically once authenticated (e.g. after MSAL's redirect flow
 * returns here), to whichever route was originally requested.
 */
export function LoginPage() {
  const { signIn, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const redirectTo = (location.state as { from?: Location })?.from?.pathname ?? '/dashboard';

  useEffect(() => {
    if (isAuthenticated) {
      navigate(redirectTo, { replace: true });
    }
  }, [isAuthenticated, navigate, redirectTo]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-6 text-center shadow-sm">
        <h1 className="text-lg font-semibold text-ink-900">Sign in to AP Flow</h1>
        <p className="mt-1 text-sm text-slate-600">Use your organisation account to continue.</p>
        <button
          type="button"
          onClick={signIn}
          className="mt-6 w-full rounded-md bg-ink-900 px-4 py-2 text-sm font-medium text-white hover:bg-ink-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
        >
          Sign in with Microsoft
        </button>
      </div>
    </div>
  );
}
