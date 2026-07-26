import { BrowserRouter } from 'react-router-dom';
import { MsalProvider } from '@azure/msal-react';
import type { PublicClientApplication } from '@azure/msal-browser';
import { AuthProvider } from '@/auth/AuthContext';
import { AppRoutes } from '@/routes/AppRoutes';

interface AppProps {
  msalInstance: PublicClientApplication;
}

/**
 * MSAL must be initialized (`initializeMsal()`, see main.tsx) before this
 * renders — `MsalProvider` assumes an already-initialized instance.
 */
export function App({ msalInstance }: AppProps) {
  return (
    <MsalProvider instance={msalInstance}>
      <AuthProvider>
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
      </AuthProvider>
    </MsalProvider>
  );
}
