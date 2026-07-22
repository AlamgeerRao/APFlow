# AP Flow — Domain Reference Data

**Status:** Approved — Permanent Reference
**Audience:** All AI development agents and human engineers
**Purpose:** This document holds the canonical business reference data (roles, statuses) approved in the Solution Architecture documents (SA-007), so agents never have to infer, guess, or invent this data when implementing a work package. If a work package appears to require a role, status, or other reference value not listed here, escalate to the Chief Technical Architect — do not invent one.

---

## 1. Role Catalogue (SA-007 E-05)

The platform-wide, fixed role catalogue is:

| Role Code | Role Name |
|---|---|
| `PLATFORM_ADMIN` | Platform Administrator |
| `AP_REVIEWER` | AP Reviewer |
| `FINANCE_MANAGER` | Finance Manager / Decision-Maker |
| `CREDIT_CONTROLLER` | Credit Controller |
| `ACCOUNTS_ADMIN` | Accounts Administrator |
| `READ_ONLY` | Read-Only |

This is the complete, approved catalogue. Do not add, rename, or remove roles without explicit Architect sign-off.

### GB Skips tenant-specific role mapping (interim, pending WP-051 confirmation)

GB Skips' Requirements Addendum describes a two-tier approval model: **Full/Approver** (Patrick, Febina) and **Standard/Reviewer** (everyone else). The working assumption, to be confirmed during WP-051, is that this maps onto the *existing* role catalogue without introducing new roles:

- **Full/Approver** → `FINANCE_MANAGER` (Finance Manager / Decision-Maker)
- **Standard/Reviewer** → `AP_REVIEWER`

Prefer this mapping over introducing new role catalogue entries, per the Simplicity First principle (`02_Project_Standards.md` §1). Only introduce a new role if WP-051 finds a concrete reason the existing catalogue cannot represent the distinction.

---

## 2. Invoice Status Catalogue (SA-007 E-14, domain = Invoice)

The baseline, platform-default invoice state list (SA-002 §5), applicable to every tenant unless a tenant has its own `WorkflowTemplate`:

| Status Code | Status Name | Terminal? |
|---|---|---|
| `RECEIVED` | Received | No |
| `PROCESSING` | Processing | No |
| `DUPLICATE_SUSPECTED` | Duplicate Suspected | No |
| `AWAITING_REVIEW` | Awaiting Review | No |
| `NEEDS_QUERY` | Needs Query | No |
| `QUERY_RAISED` | Query Raised | No |
| `AWAITING_SUPPLIER_RESPONSE` | Awaiting Supplier Response | No |
| `APPROVED` | Approved | No |
| `REJECTED` | Rejected | No |
| `CANCELLED` | Cancelled | No |
| `READY_FOR_PAYMENT` | Ready for Payment | No |
| `PAID` | Paid | No |
| `ARCHIVED` | Archived | Yes |

### GB Skips tenant-specific additions (via WP-050's tenant-configurable `WorkflowTemplate`)

GB Skips' tenant-specific `WorkflowTemplate` adds two further states, sitting between `AWAITING_REVIEW` and `APPROVED`:

| Status Code | Status Name | Terminal? |
|---|---|---|
| `CHECKED_READY_TO_APPROVE` | Checked & Ready to Approve | No |
| `NEEDS_REVIEW_FEBINA` | Needs Review by Febina | No |

These two states are **tenant-scoped data**, not part of the global default catalogue above. Do not hardcode them into a shared enum available to every tenant — they must be introduced via GB Skips' specific `WorkflowTemplate`/`StatusReference` rows, per WP-050.

---

## 3. AI Agent Rules

- Never invent a role, status, or other reference-data value not listed here.
- If a work package's requirements imply a role or status this document doesn't cover, stop and escalate to the Chief Technical Architect rather than choosing one.
- Tenant-specific reference data (e.g. GB Skips' two additional invoice statuses) must be modelled as tenant-scoped configuration, never as a change to the shared/global catalogue.
