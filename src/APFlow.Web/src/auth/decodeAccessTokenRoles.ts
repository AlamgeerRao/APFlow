/**
 * Decodes the `roles` claim from a raw access token JWT (WP-081).
 *
 * Deliberately only ever called against the *access token* (audience = the
 * API), never the ID token (audience = the SPA client) - this is the fix
 * for WP-020/WP-076's original bug: Entra only includes app-role claims in
 * a token issued for the resource the role is assigned against. These
 * roles are assigned against the API's service principal (WP-064), so the
 * ID token never carries them at all, no matter what the caller does -
 * confirmed by decoding a real, live ID token during WP-025's QA pass.
 *
 * A JWT's payload segment is not cryptographically verified here - this
 * mirrors WP-020's original `deriveActingUser` approach (trusting whatever
 * MSAL itself already validated when it issued the token) and only ever
 * feeds a cosmetic display label, never an authorization decision (that
 * happens server-side, from the same token, via normal JWT bearer auth).
 */
export function decodeAccessTokenRoles(accessToken: string): string[] {
  const payloadSegment = accessToken.split('.')[1];
  if (!payloadSegment) return [];

  try {
    const base64 = payloadSegment.replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((char) => '%' + char.charCodeAt(0).toString(16).padStart(2, '0'))
        .join(''),
    );
    const payload: unknown = JSON.parse(json);

    if (typeof payload !== 'object' || payload === null || !('roles' in payload)) {
      return [];
    }

    const roles = (payload as { roles: unknown }).roles;
    return Array.isArray(roles) ? roles.filter((role): role is string => typeof role === 'string') : [];
  } catch {
    // Malformed/unparseable token: same "nothing to show" fallback as an
    // absent claim, not a thrown error - a display label is not worth
    // crashing the app over.
    return [];
  }
}
