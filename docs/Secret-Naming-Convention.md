# AP Flow — Key Vault Secret Naming Convention (Master Reference)

**Status:** Approved — reference document
**Owner:** DevOps Engineer, ruled on by Chief Technical Architect where noted
**Purpose:** The single place that defines every Key Vault secret name AP Flow
uses, why each is shaped the way it is, and how the pattern extends to new
integrations and new environments. This exists specifically so the
`graph-cred-{tenantId}` / `graph-secret-{tenantId}` mix-up (WP-021c → WP-021d)
doesn't happen again — that mistake existed because the convention lived only
in scattered examples, never in one authoritative place. This document is
that place. If a naming question ever arises, this document wins over any
other example, comment, or prior chat/decision-log reference.

---

## 1. The pattern

```
{service}-secret[-{tenantId}]
```

- **`{service}`** — lowercase, hyphenated identifier for the external
  system/capability the secret authenticates to. Not an abbreviation unless
  the full name is unreasonably long (`docintel` for Document Intelligence is
  the one accepted exception, established below).
- **`-secret`** — literal, always present. Not `-cred`, `-credential`,
  `-key`, `-pw`, or any other synonym. This word choice is now fixed
  precisely because a synonym drifted in once already.
- **`-{tenantId}`** — present **only** for secrets that are genuinely
  tenant-specific. Uses the tenant's actual GUID, never a friendly name,
  environment label, or customer name. **Omitted entirely** for secrets that
  are platform-wide — this is an explicit architectural ruling per secret,
  not a default to fall back on when a value happens to be shared today.

## 2. Current secrets (this document is the authority — supersedes any other reference)

| Secret name | Service | Scope | Ruling source |
|---|---|---|---|
| `graph-secret-{mailboxTenantId}` | Microsoft Graph (`Mail.ReadWrite`) | Per-tenant — each customer's invoice mailbox lives in its own tenant | `docs/WP-004-Graph-Multitenancy-Decision.md`; corrected naming per WP-021d |
| `docintel-secret` | Azure AI Document Intelligence | **Platform-wide** — one Document Intelligence resource serves every tenant's invoice processing regardless of whose invoice it is | WP-023 ruling |
| `openai-secret` | Azure OpenAI | **Platform-wide** — same reasoning as Document Intelligence | WP-023 ruling |
| `sage-secret-{tenantId}` | Sage 50 connector (future) | Per-tenant — each customer's own Sage 50 credentials | WP-023 design, not yet provisioned (see §4) |

**Example, this Development environment's actual values:**
- `graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`
- `docintel-secret`
- `openai-secret`

## 3. Why `docintel` and not `documentintelligence`

`{service}` is normally the plain, unabbreviated capability name (`graph`,
`sage`, `openai`). Document Intelligence is the one deliberate exception —
`documentintelligence-secret` is needlessly long with no clarity benefit, and
`docintel` is already how the service is referred to informally in
`03_Solution_Structure.md`'s own folder-grouping guidance (`DocumentIntelligence`
folder, informally "DocIntel"). This is the **only** sanctioned abbreviation
in the pattern — do not introduce further ad hoc shortenings for future
services without recording them here first.

## 4. Extending the pattern to a new integration (e.g. Sage 50)

When a new connector needs a secret:

1. **Decide platform-wide vs. per-tenant first, explicitly.** Ask: "does one
   resource/credential serve every tenant, or does each tenant have its
   own?" Sage 50 is per-tenant (each customer has their own Sage 50
   installation and credentials) — hence `sage-secret-{tenantId}`, following
   Graph's shape, not Document Intelligence/OpenAI's.
2. **Record the ruling in this document** (§2's table) before creating the
   secret — not after, and not only in a script comment or a chat message.
3. **Only then** create the secret in Key Vault, named per the pattern.

`sage-secret-{tenantId}` is not yet provisioned — no Sage 50 connector work
has started. It's recorded here now, ahead of that work, specifically so the
first engineer who builds it has an unambiguous name to use rather than
inventing one under time pressure.

## 5. How the pattern extends across environments (Development / Test / Production)

**Environment isolation happens at the Key Vault level, never inside the
secret name.** Each environment gets its own, separate Key Vault
(`kv-apflow-dev-*`, `kv-apflow-test-*`, `kv-apflow-prod-*`, following
WP-021's existing naming), and the exact same secret names from §2 are reused
identically in every one of them:

| Environment | Key Vault (example) | `docintel-secret` lives at... |
|---|---|---|
| Development | `kv-apflow-dev-ryd3y6` | that vault's `docintel-secret` |
| Test | `kv-apflow-test-<suffix>` | that vault's `docintel-secret` |
| Production | `kv-apflow-prod-<suffix>` | that vault's `docintel-secret` |

**Why not `docintel-secret-dev` / `docintel-secret-prod`?** Embedding the
environment into the secret name would be redundant (the vault itself already
scopes the environment) and actively risky — it opens the door to a
misconfigured app in one environment successfully resolving a same-named
secret meant for another, if it ever pointed at the wrong vault by mistake.
Keeping names identical across vaults, with the vault itself as the only
environment boundary, means a misconfigured `KEY_VAULT_URI` fails loudly
(wrong vault, secret not found or wrong value) rather than silently
succeeding against the wrong environment's data.

This is a **design**, not a provisioning action — no Test or Production Key
Vault exists yet (only `kv-apflow-dev-ryd3y6`, from WP-021). Provisioning
those is a future WP, using WP-021's `main.bicep` once its `environmentName`
parameter is extended beyond its current `'dev'`-only lock (see WP-021's
README, "Important — `main.bicep` is currently locked to Development").

## 6. Retrieval at runtime — already correct, nothing further needed

Every secret in §2 is retrievable the same way: the requesting App Service's
system-assigned managed identity holds **Key Vault Secrets User** (RBAC,
vault-wide — not scoped per-secret) on the vault, and reads
`KEY_VAULT_URI` (a non-secret app setting) plus the secret's name to resolve
the value via the Azure SDK's `SecretClient` (or a Key Vault reference in app
settings, for values also needed as plain configuration). **Adding a new
secret to an existing vault requires no new RBAC grant** — this was
confirmed already working for `graph-secret-{tenantId}` in WP-021, and the
same vault-wide grant covers `docintel-secret`/`openai-secret` automatically.
Only `APFlow.Api` holds this grant (`APFlow.Web` does not — see WP-021's
least-privilege ruling).
