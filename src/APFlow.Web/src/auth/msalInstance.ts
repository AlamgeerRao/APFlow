import { PublicClientApplication, EventType, type AuthenticationResult } from '@azure/msal-browser';
import { getMsalConfig } from '@/auth/msalConfig';

/**
 * Single MSAL instance for the app, created lazily (not at module import
 * time) so importing this module doesn't require environment config to be
 * present — mirrors msalConfig.ts's own lazy-validation design.
 */
let instance: PublicClientApplication | null = null;

export function getMsalInstance(): PublicClientApplication {
  if (!instance) {
    instance = new PublicClientApplication(getMsalConfig());
    // Keep MSAL's "active account" in sync automatically, so callers
    // elsewhere in the app (token acquisition, useAuth) don't have to
    // track/set it themselves after every sign-in or token refresh.
    instance.addEventCallback((event) => {
      if (
        (event.eventType === EventType.LOGIN_SUCCESS || event.eventType === EventType.ACQUIRE_TOKEN_SUCCESS) &&
        event.payload
      ) {
        const result = event.payload as AuthenticationResult;
        if (result.account) {
          instance!.setActiveAccount(result.account);
        }
      }
    });
  }
  return instance;
}

/**
 * Initializes MSAL and processes any pending redirect response. Must be
 * awaited before the app renders (called once from main.tsx).
 */
export async function initializeMsal(): Promise<PublicClientApplication> {
  const app = getMsalInstance();
  await app.initialize();
  await app.handleRedirectPromise();
  return app;
}
