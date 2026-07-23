# AP Flow

AP Flow is a cloud-native, multi-tenant SaaS platform for Accounts Payable automation.

## Technology

- .NET 9
- ASP.NET Core
- React
- TypeScript
- Azure SQL
- Azure Blob Storage
- Microsoft Graph
- Azure AI Document Intelligence

## Solution

```
src/
tests/
docs/
```

## Status

Sprint 1 – MVP build in progress.

### Sprint 1 deliverable

A deployable MVP capable of:

- Microsoft Entra authentication and role-based access.
- Connecting to a Microsoft 365 mailbox using Microsoft Graph.
- Reading invoice emails.
- Extracting PDF attachments.
- Storing PDFs in Azure Blob Storage.
- Extracting invoice data using Azure AI Document Intelligence.
- Persisting invoices in Azure SQL.
- Detecting duplicate invoices.
- Providing a React web interface for reviewing invoices.
- Managing the manual workflow (Inbox → Needs Query → On Query → Approved).
- Recording notes and audit history.
- Running from an automated Azure deployment pipeline with monitoring and logging enabled.

### Work packages

| WP | Work Package | Role | Status |
|----|---|---|---|
| WP-001 | Solution Foundation | Backend Engineer | Done |
| WP-002 | Authentication & RBAC | Backend Engineer | Done |
| WP-003 | Database Foundation | Backend Engineer | Done |
| WP-004 | Microsoft Graph Integration | Backend Engineer | Done |
| WP-005 | Azure Blob Storage | Backend Engineer | Done |
| WP-006 | Email Synchronisation Service | Backend Engineer | Done |
| WP-007 | PDF Attachment Extraction | Backend Engineer | Done |
| WP-008 | Azure AI Document Intelligence Integration | Backend Engineer | Done |
| WP-009 | Invoice Domain Model & Persistence | Backend Engineer | Done |
| WP-010 | Duplicate Invoice Detection | Backend Engineer | Done |
| WP-011 | Invoice Repository & Query Services | Backend Engineer | Done |
| WP-012 | Invoice Processing Pipeline (orchestration only) | Backend Engineer | Done |
| WP-013 | Audit Logging & Activity History | Backend Engineer | Done |
| WP-014 | Dashboard Shell & Navigation | Senior React Engineer | Done |
| WP-015 | Invoice Work Queue | Senior React Engineer | Done |
| WP-016 | Invoice Review Screen | Senior React Engineer | Done |
| WP-017 | Notes & Comments Component | Senior React Engineer | Not started |
| WP-018 | Query / On Query / Approved Workflow UI | Senior React Engineer | Not started |
| WP-019 | Supplier Folder View | Senior React Engineer | Not started |
| WP-020 | API Integration & Error Handling | Senior React Engineer | Not started |
| WP-021 | Azure Infrastructure (App Service, SQL, Storage) | DevOps Engineer | Not started |
| WP-022 | CI/CD Pipeline (GitHub Actions) | DevOps Engineer | Not started |
| WP-023 | Application Configuration & Secrets (Key Vault) | DevOps Engineer | Not started |
| WP-024 | Logging, Monitoring & Application Insights | DevOps Engineer | Not started |
| WP-025 | Sprint 1 QA Review & Regression Testing | Senior QA Engineer | Not started |
| WP-046 | Role Catalogue Remediation (SA-007 E-05) | Backend Engineer | Done |
| WP-047 | Duplicate Matching Criteria Reconciliation | Backend Engineer | Done |
| WP-048 | Persist Duplicate Detection Result; Pure-Compute Detection Service | Backend Engineer | Done |
| WP-049 | Duplicate Check Auto-Invocation in Processing Pipeline | Backend Engineer | Done. Replaces the prior ad-hoc three-commit adaptation (create → advance status → persist duplicate flag) with a true atomic single-save pipeline — see `docs/WP-049-Wire-Duplicate-Detection-Into-Pipeline.md` |
| WP-050 | Tenant-Configurable Workflow Engine | Backend Engineer | Not started |
| WP-051 | Confirm GB Skips Role Mapping (Full/Approver → FINANCE_MANAGER) | Chief Technical Architect / Product Owner | Not started |

Open architecture decisions pending Chief Technical Architect sign-off (see individual docs for detail):

- `docs/WP-012-Invoice-Processing-Pipeline-Decisions.md`
- `docs/WP-013-Audit-Logging-Decisions.md`
- `docs/WP-014-Dashboard-Shell-Decisions.md`
- `docs/WP-015-Invoice-Queue-Decisions.md`
- `docs/WP-016-Invoice-Review-Decisions.md`
- `docs/WP-046-Role-Catalogue-Remediation.md` (two items flagged for confirmation)

Resolved architecture decisions — ruling recorded 2026-07-20; follow-up implementation tracked in `docs/Backlog.md`:

- `docs/WP-004-Graph-Multitenancy-Decision.md`
- `docs/WP-004-Health-Check-Severity-Decision.md`
- `docs/WP-010-Duplicate-Flag-Persistence-Decision.md`