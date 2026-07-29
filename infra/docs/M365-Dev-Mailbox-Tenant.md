# M365 Dev Mailbox Tenant — Business Basic (WP-021 STOP Item 2)

**Status: COMPLETE.** This tenant is provisioned and all required values are captured. This document is kept as a record of the process actually used and as a reference for anyone standing up an equivalent tenant later (e.g. for a future environment).

**What this is for:** a real Microsoft 365 tenant with a genuine Exchange Online mailbox, so the Graph/email ingestion pipeline (`Mail.ReadWrite`) has something real to poll. This is a **separate tenant from the CIAM sign-in tenant** created for STOP 1 — they must never be conflated; the CIAM tenant has no mailbox capability at all.

**Route taken:** the free Microsoft 365 Developer Program sandbox was attempted first and found to be genuinely inaccessible — not a fixable error, but a real 2024 policy change restricting the free E5 sandbox to active Visual Studio Enterprise/Professional subscribers or specific Microsoft Partner Program members only. **Microsoft 365 Business Basic, purchased directly on a monthly (cancel-anytime) commitment, was used instead.** This route has no eligibility gate — it's a standard commercial signup — and is the recommended default for any future tenant of this kind; don't attempt the Developer Program route again unless the organization holds a qualifying Visual Studio subscription.

---

## Record of what was provisioned

| Item | Value |
|---|---|
| Tenant domain | `acoounts01.onmicrosoft.com` (intentional naming — the preferred name was unavailable) |
| Tenant ID | `1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` |
| Mailbox to poll (UPN) | `invoices@acoounts01.onmicrosoft.com` |
| Graph App (Client) ID | `40d63c64-ff18-4028-ba92-01ca93c1c432` |
| Graph Client Secret | Rotated after initial capture; stored in Key Vault only — not recorded here or anywhere else in plain text. Key Vault secret name: `gbskipdev`. Expires ~1 month from creation (created 2026-07-29) — acceptable for dev, but rotate before it lapses; there is no automated rotation reminder configured. |
| `Mail.ReadWrite` permission | Application permission added, admin consent granted |
| Billing | Business Basic, monthly commitment, monthly payment (cancel-anytime) |

---

## Process followed (for repeating this later, e.g. a second environment)

### 1. Purchase Business Basic directly

Go to [microsoft.com/en-us/microsoft-365/business/microsoft-365-plans-and-pricing](https://www.microsoft.com/en-us/microsoft-365/business/microsoft-365-plans-and-pricing) (the **business** plans page, not the consumer/personal one) and purchase **Business Basic** on a **monthly commitment, monthly payment** basis — no eligibility check, standard checkout. This creates the tenant and its default `<name>.onmicrosoft.com` domain.

### 2. Create the mailbox to be polled

In the Microsoft 365 admin center ([admin.cloud.microsoft](https://admin.cloud.microsoft)):
- **Users → Active users → Add a user**
- Name it clearly, e.g. `invoices@<tenant>.onmicrosoft.com`
- Assign it a Business Basic license during creation (without this it won't get a real mailbox)
- Confirm afterward: **Users → Active users → click the user → Mail** tab shows real mailbox details

### 3. Record the tenant ID

Via [entra.microsoft.com](https://entra.microsoft.com) (same tenant) → **Overview** → copy the **Tenant ID**.

### 4. Register the Graph application

This is the third, dedicated app registration referenced in the Chief Technical Architect's ruling — separate from the SPA and API registrations, which live in the CIAM tenant instead.

In [entra.microsoft.com](https://entra.microsoft.com) or [portal.azure.com](https://portal.azure.com), while in this M365 tenant:

1. **App registrations** → **+ New registration**
2. Name: e.g. `apflow-graph-dev`
3. Supported account types: **Accounts in this organizational directory only**
4. Redirect URI: leave blank (application-only, no user context)
5. **Register**

### 5. Add the `Mail.ReadWrite` application permission

1. **API permissions** → **+ Add a permission** → **Microsoft Graph** → **Application permissions**
2. Search and select **Mail.ReadWrite** → **Add permissions**
3. **Grant admin consent for [tenant]** → confirm

### 6. Create a client secret — and capture it correctly the first time

1. **Certificates & secrets** → **+ New client secret**
2. Description + expiry → **Add**
3. **Immediately** copy the string under the **Value** column — not the **Secret ID** column (a plain GUID next to it that looks similar but is not a credential; this was the one mix-up encountered during this tenant's setup, caught and corrected by rotating the secret).
4. Paste the Value **directly into Key Vault or a password manager** — never into chat, email, or any other transient channel. If a secret value is ever typed somewhere outside its final secure storage (including an AI chat, even for a dev tenant), treat it as exposed and rotate it immediately rather than use it as-is — this happened once during this setup and was correctly remediated.

### 7. Feed into WP-021's script

```bash
./infra/scripts/create-entra-app-registrations.sh \
  --tenant-id <ciam-tenant-id-from-STOP-1> \
  --web-app-url <webAppServiceUrl> \
  --mail-tenant-id 1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf \
  --graph-client-id 40d63c64-ff18-4028-ba92-01ca93c1c432
```

`--tenant-id` (CIAM, STOP 1) and `--mail-tenant-id` (this tenant) are deliberately different values — that's the whole point of keeping the two tenants separate. `--graph-client-id` tells the script this tenant's Graph app already exists (created via steps 4–6 above) — the script will not re-create it, touch its permissions, or reset its secret.

---

## Reminder — what this tenant is and isn't for

This tenant provides the mailbox only. Sign-in for the SPA and API still happens through the CIAM tenant from STOP 1 — the SPA/API app registrations must never be added here, and no end users sign into this tenant directly.
