# WP-004 — Graph Integration Verification Checklist

**Status:** Must be completed before this reaches any environment expected to
actually verify a real mailbox connection.
**Owner:** Whoever performs the Graph App Registration (likely the customer's
Microsoft 365 admin, or the Chief Technical Architect coordinating with them).

This checklist exists because WP-004 was implemented without a real Microsoft 365
tenant, App Registration, or mailbox available. Complete each item against the
real environment before relying on `VerifyMailboxConnectionAsync` /
`/health/ready`'s Graph check.

## App Registration

- [ ] An App Registration exists in the target tenant (GB Skips' M365 tenant, per
      the current single-tenant MVP scope - see
      `docs/WP-004-Graph-Multitenancy-Decision.md`).
- [ ] It has been granted the `Mail.ReadWrite` **application** permission (not
      delegated) against Microsoft Graph. **Widened from `Mail.Read` in WP-006**:
      `EmailSyncService.MarkAsProcessedAsync` performs a write operation
      (`PATCH` to update a message's categories) to mark emails as processed.
      `Mail.Read` alone is read-only and is insufficient for this - a Graph App
      Registration granted only `Mail.Read` will authenticate successfully and
      `SyncUnreadEmailsAsync`/`VerifyMailboxConnectionAsync` will work, but every
      call to `MarkAsProcessedAsync` will fail with `403 Forbidden`.
      **If this checklist was already completed against a real tenant before
      WP-006** (i.e. the App Registration only has `Mail.Read` today), the
      permission must be widened to `Mail.ReadWrite` and admin consent
      re-granted before WP-006's mark-as-processed functionality will work.
- [ ] A tenant admin has granted admin consent for that permission.
- [ ] Decide and configure one of:
      - **Client secret**: generate one, store it in Key Vault only (secret name
        `Graph--ClientSecret`, matching the double-dash convention from
        WP-003), never in appsettings.
      - **Managed Identity** (preferred, no secret to manage): grant the Azure
        resource's Managed Identity the same `Mail.ReadWrite` application
        permission directly, and leave `Graph:ClientSecret` blank so
        `EmailService`/`EmailSyncService` fall back to `DefaultAzureCredential`.

## Configuration

- [ ] `Graph:TenantId` set to the target M365 tenant's ID.
- [ ] `Graph:ClientId` set to the App Registration's Application (client) ID.
- [ ] `Graph:MailboxUserPrincipalName` set to the actual mailbox address AP Flow
      should read (a shared mailbox is likely more appropriate than a personal
      one - confirm with the customer).
- [ ] If using client secret auth, `Graph:ClientSecret` set via Key Vault.

## Verification

- [ ] With real configuration in place, call `GET /health/ready` and confirm the
      `graph-mailbox` check reports `Healthy`.
- [ ] Deliberately misconfigure (wrong mailbox, revoked permission) once and
      confirm the check reports `Unhealthy` with a logged warning rather than
      crashing the app - proves the failure path is safe, not just the happy path.
- [ ] **(WP-006)** With real configuration in place, call `MarkAsProcessedAsync`
      against a real unread message and confirm it succeeds (not a `403`) and
      the category actually appears on the message in Outlook. A successful
      `SyncUnreadEmailsAsync`/`graph-mailbox` health check does NOT prove
      `Mail.ReadWrite` is granted - only a real write call does.

## Related

- `docs/WP-002-Entra-Verification-Checklist.md` - same pattern, for the
  customer-facing Entra External ID auth (a *different* tenant/app registration
  than this one - see `GraphOptions`'s remarks on why).
- **WP-006** widened the required permission from `Mail.Read` to
  `Mail.ReadWrite` - see the App Registration section above.
