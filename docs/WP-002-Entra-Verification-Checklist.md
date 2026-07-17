# WP-002 — Entra External ID Verification Checklist

**Status:** Must be completed before this reaches any shared/staging environment with real users.
**Owner:** Chief Technical Architect / whoever performs the Entra App Registration.

This checklist exists because WP-002 was implemented without a real Entra External ID
tenant or App Registration available. The values below are best-effort assumptions
based on standard Entra ID v2.0 token conventions, not verified against an actual
issued token. Each item must be checked off against the real App Registration before
go-live.

## Role claim values

- [ ] Confirm the **Value** field configured for each App Role in the Entra App
      Registration matches exactly, character-for-character, the constants in
      `src/APFlow.Domain/Common/Constants/Roles.cs`:
      - `Administrator` → currently `"Administrator"`
      - `AP Manager` → currently `"AP Manager"` (contains a space - Entra App Role
        values conventionally avoid spaces; the real Value may be `"ApManager"` or
        similar. **If it differs, `[Authorize(Policy = RequireApManager)]` will fail
        silently with a 403 for every AP Manager - there is no error at startup.**)
      - `AP Clerk` → currently `"AP Clerk"` (same risk as above)
      - `Finance` → currently `"Finance"`
      - `ReadOnly` → currently `"ReadOnly"`
- [ ] If any value differs, update `Roles.cs` to match the App Registration exactly -
      do not change the App Registration to match the code.

## Token claim shape

- [ ] Confirm the object-id claim is actually named `oid` in issued tokens (used for
      `ICurrentUserService.UserId`).
- [ ] Confirm the tenant-id claim is actually named `tid` (used for
      `ICurrentUserService.TenantId`).
- [ ] Confirm the roles claim is actually named `roles` and is an array (used for
      `ICurrentUserService.Roles` and role-based authorization).
- [ ] Confirm the username/email claim is actually `preferred_username` (falls back to
      the standard `ClaimTypes.Email` claim if absent).
- [ ] If any of the above differ, update `CurrentUserService.cs` and the
      `RoleClaimType`/`NameClaimType` settings in `AuthenticationExtensions.cs`
      together - they must stay consistent with each other.

## Configuration

- [ ] `EntraId:Authority`, `EntraId:TenantId`, `EntraId:Audience` set correctly per
      environment (via Key Vault / App Service configuration, never committed).
- [ ] Confirm a real token issued by the tenant validates successfully against a
      running instance before enabling this for real users.

## Related, explicitly deferred (not part of this checklist)

- Tenant isolation enforcement (beyond exposing `TenantId` on `ICurrentUserService`)
  is deferred to a future feature work package - not yet enforced anywhere.
