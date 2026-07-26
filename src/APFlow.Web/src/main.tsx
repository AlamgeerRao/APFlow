import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/App';
import { initializeMsal } from '@/auth/msalInstance';
import '@/index.css';

const rootElement = document.getElementById('root');
if (!rootElement) {
  throw new Error('Root element #root not found in index.html');
}

// MSAL must be initialized — and any pending redirect response from a
// sign-in handled — before the app renders (WP-020 task 1).
initializeMsal()
  .then((msalInstance) => {
    createRoot(rootElement).render(
      <StrictMode>
        <App msalInstance={msalInstance} />
      </StrictMode>,
    );
  })
  .catch((error: unknown) => {
    // Fail loudly rather than silently: an app that can't initialize auth
    // has nothing safe to render.
    rootElement.innerHTML =
      '<div style="padding:2rem;font-family:sans-serif;color:#7f1d1d;">Failed to initialize sign-in. Please refresh, or contact support if this persists.</div>';
    console.error('MSAL initialization failed', error);
  });
