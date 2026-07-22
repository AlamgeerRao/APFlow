import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '@/auth/useAuth';

interface ProtectedRouteProps {
  children: ReactNode;
}

/**
 * Guards a route so it only renders for an authenticated acting user,
 * redirecting to /login otherwise and preserving the originally requested
 * location so sign-in can return the user there.
 */
export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <>{children}</>;
}
