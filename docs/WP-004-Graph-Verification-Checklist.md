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
- [ ] It has been granted the `Mail.Read` **application** permission (not
      delegated) against Microsoft Graph.
- [ ] A tenant admin has granted admin consent for that permission.
- [ ] Decide and configure one of:
      - **Client secret**: generate one, store it in Key Vault only (secret name
        `Graph--ClientSecret`, matching the double-dash convention from
        WP-003), never in appsettings.
      - **Managed Identity** (preferred, no secret to manage): grant the Azure
        resource's Managed Identity the same `Mail.Read` application permission
        directly, and leave `Graph:ClientSecret` blank so `EmailService` falls
        back to `DefaultAzureCredential`.

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

## Related

- `docs/WP-002-Entra-Verification-Checklist.md` - same pattern, for the
  customer-facing Entra External ID auth (a *different* tenant/app registration
  than this one - see `GraphOptions`'s remarks on why).
