# Backlog

Items raised by architectural decisions but not implemented at the time the
decision was made. Each entry links back to the decision doc that created it.
Remove an entry once its work package/task lands and note the commit/WP that
closed it.

| Item | Trigger | Priority | Raised by |
|---|---|---|---|
| **Per-Tenant Graph Configuration** — redesign `GraphOptions` from a single app-wide config to per-tenant data (`TenantGraphConfiguration` table in Azure SQL), multi-tenant Entra app registration with per-customer admin consent (client secret or certificate per tenant, certificate preferred), Key Vault secrets under a `graph-secret-{tenantId}` naming convention, and a per-tenant (or removed-from-shared-endpoint) health check. Owned by the Chief Technical Architect (design) / a Backend Engineer (implementation), as its own dedicated WP. | Before any second customer's mailbox is connected | **Blocking (future)** | `docs/WP-004-Graph-Multitenancy-Decision.md` |
| **Component tags on health check responses** — add a `"component"` field (`"graph"` / `"blob"` / `"database"`) to each entry in the `/health/ready` response body, so monitoring can filter/alert per component without a new endpoint. Small addition to `GraphMailboxHealthCheck`/`BlobStorageHealthCheck`. | Next touch of WP-004 or WP-005 | Low, small | `docs/WP-004-Health-Check-Severity-Decision.md` |
| **`IInvoiceRepository.GetBySupplierAsync`** — targeted, indexed supplier-scoped read, to replace the full in-memory `GetAllAsync()` scans currently used by `DuplicateDetectionService.CheckAsync` and `InvoiceProcessingService.ResolveSupplierAsync`. | When invoice volume makes `GetAllAsync()` costly | Low, deferred | `docs/WP-010-Duplicate-Flag-Persistence-Decision.md` (also noted independently in `docs/WP-012-Invoice-Processing-Pipeline-Decisions.md` item 6) |

## Closed

| Item | Closed by | Date |
|---|---|---|
| ~~`Invoice.IsPotentialDuplicate` / `Invoice.DuplicateCheckReason`~~ — persisted fields on `Invoice`, written by `InvoiceProcessingService.PersistDuplicateCheckResultAsync` via a new `IInvoiceRepository` dependency; `DuplicateDetectionService` unchanged (still pure compute). | This session's implementation - see `docs/WP-010-Duplicate-Flag-Persistence-Decision.md`'s "## Implementation" section | 2026-07-20 |
