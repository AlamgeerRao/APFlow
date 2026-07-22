import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-slate-50 px-4 text-center">
      <h1 className="text-xl font-semibold text-ink-900">Page not found</h1>
      <p className="text-sm text-slate-600">The page you're looking for doesn't exist.</p>
      <Link
        to="/dashboard"
        className="mt-2 rounded-md bg-ink-900 px-4 py-2 text-sm font-medium text-white hover:bg-ink-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
      >
        Go to Dashboard
      </Link>
    </div>
  );
}
