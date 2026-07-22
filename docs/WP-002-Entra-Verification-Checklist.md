# WP-002 — Entra External ID Verification Checklist

**Status:** Must be completed before this reaches any shared/staging environment with real users.
**Owner:** Chief Technical Architect / whoever performs the Entra App Registration.

This checklist exists because WP-002 was implemented without a real Entra External ID
tenant or App Registration available. The values below are best-effort assumptions
based on standard Entra ID v2.0 token conventions, not verified against an actual
issued token. Each item must be checked off against the real App Registration before
go-live.

## Role claim values

**Updated by WP-046** (Remediate Role Catalogue) to reference the corrected role
catalogue - see docs/06_Domain_Reference_Data.md §1 (SA-007 E-05) and
docs/WP-046-Role-Catalogue-Remediation.md. The values below replace the original
WP-002 best-effort guesses (`Administrator`/`AP Manager`/`AP Clerk`/`Finance`/`ReadOnly`),
which are no longer used anywhere in this codebase.

- [ ] Confirm the **Value** field configured for each App Role in the Entra App
      Registration matches exactly, character-for-character, the constants in
      `src/APFlow.Domain/Common/Constants/Roles.cs`:
      - Platform Administrator → currently `"PLATFORM_ADMIN"`
      - AP Reviewer → currently `"AP_REVIEWER"`
      - Finance Manager / Decision-Maker → currently `"FINANCE_MANAGER"`
      - Credit Controller → currently `"CREDIT_CONTROLLER"`
      - Accounts Administrator → currently `"ACCOUNTS_ADMIN"`
      - Read-Only → currently `"READ_ONLY"`
      These now use SA-007 E-05's own "Role Code" column (SNAKE_CASE, no spaces),
      which already matches Entra's own convention that App Role "Value" fields
      avoid spaces - the specific risk this checklist originally flagged against
      the old `"AP Manager"`/`"AP Clerk"` values (a space-containing Value silently
      causing every `[Authorize(Policy = ...)]` check to 403) should no longer
      apply, but still verify character-for-character against the real
      Registration - this checklist's core purpose (nothing here has been tested
      against a real Entra tenant) is otherwise unchanged by WP-046.
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
