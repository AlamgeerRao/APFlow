/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Entra External ID application (client) ID for this SPA's app registration. */
  readonly VITE_ENTRA_CLIENT_ID: string;
  /** Entra authority URL, e.g. https://{tenant}.ciamlogin.com/{tenant-id} for External ID, or the standard v2.0 authority for a work/school tenant. */
  readonly VITE_ENTRA_AUTHORITY: string;
  /** Redirect URI registered on the Entra app registration for this environment. Defaults to window.location.origin if unset. */
  readonly VITE_ENTRA_REDIRECT_URI?: string;
  /** The API's exposed scope to request an access token for, e.g. api://{api-client-id}/access_as_user. */
  readonly VITE_API_SCOPE: string;
  /** Base URL of APFlow.Api for this environment, e.g. https://localhost:5001 or https://apflow-api-dev.azurewebsites.net. */
  readonly VITE_API_BASE_URL: string;
  /** Full commit SHA this bundle was built from (WP-043's System Status page). Unset in local dev - not every build runs through CI. */
  readonly VITE_BUILD_SHA?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
