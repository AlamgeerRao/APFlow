# WP-031 — Query Management: Outbound Supplier Email: Report

**Status:** Done. Committed only — not pushed, not deployed, not live-verified
this round, per explicit session instruction. The Azure/Entra `Mail.Send`
permission grant was genuinely **attempted**, not skipped, and is documented
as blocked below — everything else in this WP's task list is complete.

**Role:** Backend Engineer. **Sprint:** Sprint 2. **Dependencies:** None
(`docs/Sprint2-Plan.md` §3 WP-031: "Dependencies: None now"). WP-040
(remittance email) depends on this WP's `IEmailSendService`.

---

## What already existed — confirmed before writing anything

Per this WP's own brief, and re-confirmed directly against the code rather
than taken on trust:

- `EmailService`/`IEmailService` (`src/APFlow.Integrations/Graph/EmailService.cs`,
  `src/APFlow.Application/Interfaces/IEmailService.cs`) had exactly one method,
  `VerifyMailboxConnectionAsync` — mailbox *connection verification* only
  (WP-004). Grepped the whole codebase for any existing "send" method: none
  exists anywhere.
- `GraphOptions` (`src/APFlow.Integrations/Graph/GraphOptions.cs`) already has
  `TenantId`/`ClientId`/`ClientSecret`/`MailboxUserPrincipalName`, with the
  client-secret-or-`DefaultAzureCredential`-fallback pattern implemented in
  `APFlow.Integrations.DependencyInjection.AddGraph`.
- `InvoiceStatusCodes.QueryRaised = "QUERY_RAISED"` (`src/APFlow.Domain/Common/Constants/InvoiceStatusCodes.cs`) —
  confirmed directly, not guessed, against both the constants file and
  `docs/AI/06_Domain_Reference_Data.md`'s status catalogue.
- `UpdateInvoiceStatusRequest.Notes` is already mandatory on every status
  transition (`InvoiceService.UpdateAsync`, WP-084) — `Invoice.NoteRequiredForTransition`
  rejects a transition with no note, and the note is staged onto the same
  tracked `Invoice` and committed by the same `SaveChangesAsync` call as the
  status change itself. Confirmed this means no separate "query reason" field
  is needed, per `docs/Sprint2-Plan.md`'s own explicit note.
- `Supplier.Email` (`string?`, WP-026, just merged into `main` this session)
  is optional — a supplier can genuinely have no email on file.
- `NeedsQuery → QueryRaised` is a seeded, valid transition
  (`WorkflowTransitionSeedData`) and is **not** one of `RoleGatedTransitions`'
  gated pairs — confirmed by reading that class directly, not assumed from
  its being query-related.

---

## What was built

### 1. `GraphOptions` — confirmed to need no new fields

Task 1 asked to "extend `GraphOptions` with the fields a send capability
needs... if any." The send capability needs exactly the same mailbox
(`MailboxUserPrincipalName`) and the same app-only credential
(`TenantId`/`ClientId`/`ClientSecret`-or-Managed-Identity) the read/sync path
already resolves — the only thing genuinely new is a second Graph
**permission** (`Mail.Send`) granted to the same app registration, not a new
field on this options class. No new field was added.

The only change to this file is to `ClientId`'s doc comment, updated to name
both permissions (`Mail.ReadWrite` for the WP-004/006/007 read/sync path,
`Mail.Send` for this WP's send path) so a future reader doesn't assume
`Mail.Read` is still the only permission this app registration needs, and to
make explicit that a new Graph capability against the same mailbox means a
new permission grant to this one app, not a second credential.

### 2. `IEmailSendService`/`SendEmailRequest` (`src/APFlow.Application`)

New, deliberately generic interface (`src/APFlow.Application/Interfaces/IEmailSendService.cs`):

```csharp
public interface IEmailSendService
{
    Task<Result> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default);
}
```

`SendEmailRequest` (`src/APFlow.Application/DTOs/SendEmailRequest.cs`) is a
plain `record(string ToAddress, string Subject, string Body, string? ToDisplayName = null)` —
a recipient/subject/body triple with no query-specific concept anywhere in
its shape. This is Task 5's requirement directly: WP-040 (remittance email)
can construct its own `SendEmailRequest` and call `IEmailSendService.SendAsync`
completely unchanged, no new interface method, no new DTO field.

Uses `Result` (not `bool`), matching `IEmailSyncService.MarkAsProcessedAsync`'s
convention for a genuine operation with side effects — `IEmailService.VerifyMailboxConnectionAsync`'s
`bool`-return, diagnostic-only convention was deliberately not copied here,
since sending an email is a real action a caller may need a specific failure
reason for (even though, per Task 4's own decision, `InvoiceService.UpdateAsync`
itself only logs the failure and moves on).

### 3. `GraphEmailSendService` (`src/APFlow.Integrations/Graph/GraphEmailSendService.cs`)

Graph-backed implementation, structured identically to `EmailService`
(WP-004): validates the mailbox is configured and a recipient was given,
delegates the actual Graph call to a thin internal seam, catches and logs
any Graph SDK exception rather than letting it escape, and distinguishes a
caller-initiated `OperationCanceledException` (rethrown) from a genuine send
failure (returned as `Result.Failure`).

### 4. `IGraphMailSender`/`GraphMailSender` (`src/APFlow.Integrations/Graph/GraphMailSender.cs`)

New internal seam, following `IGraphInboxReader` (WP-004) and
`IGraphMessageOperations` (WP-006)'s exact established pattern: the Graph
SDK's Kiota-generated fluent builder can't be reliably faked without the real
package, so this one-method interface is hand-written and kept mechanically
thin (a single pass-through call, `Users[upn].SendMail.PostAsync(...)`), with
all testable logic (mailbox-not-configured, recipient-missing,
success/failure/cancellation handling) living in `GraphEmailSendService`
instead, where it's covered by real fake-based unit tests. Not independently
unit-tested itself, for the same reason `GraphInboxReader`/`GraphMessageOperations`
aren't — doing so would need either a real tenant/mailbox or faking Graph SDK
client types this project can't verify offline.

### 5. DI registration (`src/APFlow.Integrations/DependencyInjection.cs`)

`IGraphMailSender`/`GraphMailSender` and `IEmailSendService`/`GraphEmailSendService`
registered alongside the existing Graph services inside the same `AddGraph`
method, reusing the same `GraphServiceClient`/`GraphOptions` singleton
registrations already there — no new configuration section, no second Graph
client. `GraphEmailSendService`'s constructor is `internal` (same reasoning
as `EmailService`'s), so it's registered via an explicit factory delegate,
matching that class's own registration shape exactly.

### 6. Wired into `InvoiceService.UpdateAsync` (Task 4)

`InvoiceService` gained a new `IEmailSendService` constructor dependency.
After the status change and its mandatory note have already been staged and
committed via the existing single `SaveChangesAsync` call, a new private
`SendQueryEmailAsync` is called — but **only** when this specific call
actually transitioned the invoice's status *into* `QUERY_RAISED`
(`statusIsChanging && request.Status == InvoiceStatusCodes.QueryRaised`), not
on every status change and not on a no-op re-save of an invoice already at
`QUERY_RAISED`.

`SendQueryEmailAsync`:

- If `invoice.Supplier?.Email` is null/blank, logs a warning and returns —
  the already-committed transition is never blocked or unwound (see the
  "Missing supplier email" decision below).
- Otherwise builds a subject (`"Query regarding invoice {SupplierInvoiceNumber ?? Id}"`)
  and a plain-text body (a short fixed intro line, then the mandatory
  transition note verbatim, then a short fixed closing line) and calls
  `IEmailSendService.SendAsync` with the supplier's name as the display name.
- If the send itself fails (`Result.IsFailure`), logs a warning with the
  error code/message and returns — same "log loudly, don't fail an
  already-committed operation" reasoning `UpdateAsync` already applies to its
  own audit-log-staging failures a few lines above.

This method never throws and never returns a `Result` of its own —
`UpdateAsync`'s overall `Result.Success(ToDto(invoice))` is unaffected by
anything that happens inside it, by design.

---

## Decisions made without an existing catalogue to follow

### Missing supplier email at `QUERY_RAISED` — skip and log, don't block

`Supplier.Email` is optional (WP-026) — a supplier with no email on file is a
normal, expected data state, not an error condition. Checked
`docs/AI/06_Domain_Reference_Data.md` first, per its own "never invent,
escalate instead" rule: it has no convention covering this case (it only
documents the role and invoice-status catalogues). Two options were
considered:

1. **Reject the transition outright** (`400`-style error) if the supplier has
   no email — rejected: this would make `QUERY_RAISED` uniquely unreachable
   for a real, valid supplier record for a reason unrelated to the workflow
   itself, and there's no requirement anywhere calling for that.
2. **Skip the send, log a warning, let the transition succeed** — chosen.
   The internal query-status workflow (Sprint 1) still means something even
   without an outbound email (a reviewer can still track "we have a query
   open" internally), and a missing contact detail on one supplier shouldn't
   block that.

Recorded as an open question in `docs/Backlog.md`: should this also surface
*in-app* somehow (an `IngestionIssue`-style flag, a banner on the invoice)
rather than only a server log line no user ever sees? Not invented here —
genuinely unclear and not asked for by this WP's spec.

### Email body — plain text, minimal fixed template

No requirement specifies formatting beyond "the note is the query content."
Built a short three-part plain-text body (fixed intro line naming the
invoice, the note verbatim, fixed closing line inviting a reply) rather than
inventing HTML formatting, a letterhead, or any templating engine —
`IEmailSendService`'s own doc comment explains why plain text only (no
requirement from either this WP or WP-040's plan-level scope calls for HTML
or attachments yet).

### `Result`, not `bool`, for `IEmailSendService.SendAsync`

`IEmailService.VerifyMailboxConnectionAsync` (WP-004) returns `bool` because
it's explicitly a diagnostic/health-check method. `SendAsync` is a genuine
operation with a real side effect, so it follows `IEmailSyncService.MarkAsProcessedAsync`'s
`Result`-returning convention instead — giving a caller (including a future
WP-040 caller with different failure-handling needs than WP-031's own "log
and continue") a real error code/message to inspect, not just a flat
true/false.

---

## Azure/Entra: `Mail.Send` permission — attempted, genuinely blocked

**This section documents a real attempt and its actual, verified result —
not a claim of success and not a silent skip.**

### What was found

The Graph app registration this project uses (`apflow-graph-dev`, client id
`40d63c64-ff18-4028-ba92-01ca93c1c432`) lives in a **separate M365 tenant**
(`1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`, `acoounts01.onmicrosoft.com`) —
confirmed via `infra/docs/M365-Dev-Mailbox-Tenant.md`, which explicitly warns
this tenant "must never be conflated" with the CIAM sign-in tenant. This
session's `az` CLI is authenticated against the CIAM/Azure-subscription
tenant (`641fc267-7902-48d0-8e1c-1d3d0166c8ac`) instead — confirmed via
`az account show`. These are genuinely two different tenants, not an
assumption.

### What was tried

1. **`az login --tenant 1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf --use-device-code --allow-no-subscriptions`**
   (bounded to 25 seconds). Result: printed a real device code and sign-in
   URL and blocked waiting for interactive completion — `To sign in, use a
   web browser to open the page https://login.microsoft.com/device and enter
   the code D4H5TVPZ3 to authenticate.` This is a genuine interactive human
   step this session cannot complete headlessly (no browser, no ability to
   receive/enter a code on the user's behalf).

2. **Fallback: the app's own stored client-secret credential**
   (`kv-apflow-dev-ryd3y6`, secret `graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`) —
   used to acquire a real client-credentials Graph token, then called
   `GET https://graph.microsoft.com/v1.0/applications?$filter=appId eq '40d63c64-ff18-4028-ba92-01ca93c1c432'`
   directly. Result: `403 Authorization_RequestDenied — "Insufficient
   privileges to complete the operation."` This confirms, with a real API
   response (not assumed), that the app's own credential only carries the
   `Mail.ReadWrite` permission it was already granted — not
   `Application.ReadWrite.All` or any other right to read or modify its own
   (or any) app registration. An app cannot use its own client-credentials
   token to grant itself a new permission; that action requires a directory
   administrator's interactive consent.

### Conclusion

Headless completion of this grant is not possible in this environment with
the credentials available to this session. This is a genuine environment
capability gap, not a shortcut taken to avoid the work.

### Exact commands to complete it (once a human completes the one-time interactive login)

```bash
az login --tenant 1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf

GRAPH_SP_ID=$(az ad sp show --id 00000003-0000-0000-c000-000000000000 --query id -o tsv)
MAIL_SEND_ROLE_ID=$(az ad sp show --id 00000003-0000-0000-c000-000000000000 \
  --query "appRoles[?value=='Mail.Send'].id" -o tsv)

az ad app permission add --id 40d63c64-ff18-4028-ba92-01ca93c1c432 \
  --api 00000003-0000-0000-c000-000000000000 \
  --api-permissions $MAIL_SEND_ROLE_ID=Role

az ad app permission grant --id 40d63c64-ff18-4028-ba92-01ca93c1c432 \
  --api 00000003-0000-0000-c000-000000000000

az ad app permission admin-consent --id 40d63c64-ff18-4028-ba92-01ca93c1c432

# Confirm:
az ad app permission list --id 40d63c64-ff18-4028-ba92-01ca93c1c432
```

Recorded in `docs/Backlog.md` as a blocking-for-live-use (not
blocking-for-code-delivery) item. Until this runs, `GraphEmailSendService.SendAsync`
is fully built, unit-tested, and wired in, but will fail live against the
real mailbox with a Graph `403` the moment it's actually exercised.

---

## Test results

Baseline before this WP (post WP-026/WP-092 merge, confirmed by running the
full suite before making any change): **421** backend tests passing
(11 Domain + 207 Application + 45 Integrations + 86 Infrastructure + 72 Api).

After this WP: **430** backend tests passing (11 Domain + **211** Application
(+4) + **50** Integrations (+5) + 86 Infrastructure (unchanged — one file
gained a fake class to satisfy `InvoiceService`'s new constructor parameter,
no new test methods) + 72 Api (unchanged)).

New tests:

- **`InvoiceServiceTests`** (+4): transitioning to `QUERY_RAISED` with a
  supplier email sends the email with the correct recipient/subject/body
  (asserts the note text appears in the body); transitioning with no
  supplier email skips the send but the transition still succeeds;
  transitioning when the send itself fails still leaves the transition
  successful (the send was attempted, it just failed); transitioning to any
  other status sends no email at all.
- **`GraphEmailSendServiceTests`** (new file, +5): mailbox not configured
  returns a failure without calling the sender; missing recipient returns a
  failure without calling the sender; a successful send returns success and
  passes the exact request through to the sender; a sender exception is
  caught and returned as a failed `Result`, never thrown; caller-initiated
  cancellation propagates as `OperationCanceledException`, not a failed
  `Result`. All against a hand-written fake `IGraphMailSender` — no real
  Graph call anywhere in this suite.

`dotnet build APFlow.sln`: clean, **0 warnings**, 0 errors.

Six pre-existing `new InvoiceService(...)` call sites across
`InvoiceServiceTests.cs` (2), `InvoiceProcessingServiceTests.cs` (1),
`InvoiceProcessingDuplicateDetectionIntegrationTests.cs` (3), and
`AuditLogRepositoryTests.cs` (1) needed updating for the new constructor
parameter — a `FakeEmailSendService` was added to `APFlow.Application.Tests`
(shared, tracks every sent request for assertions) and duplicated as a
minimal private nested always-succeeds fake in each of the two
`APFlow.Infrastructure.Tests` files (that test project has no reference to
`APFlow.Application.Tests`, matching the existing pattern every other fake in
that project already follows — see e.g. `FakeCurrentUserService` duplicated
per-file there rather than shared).

---

## Explicitly NOT done this round

- **Not pushed.** Commits are local only, per this session's ground rules —
  the user pushes manually.
- **Not deployed.** No CI/CD run has been triggered by this work.
- **Not live-verified.** No real test email has been sent, and none was
  attempted — per this WP's own explicit instruction, there is no deployed
  instance reachable from this worktree carrying this round's code, and
  sending a real email was explicitly out of scope for this round regardless.
  This is a genuine, not-yet-closed follow-up item once the code is deployed
  **and** the `Mail.Send` grant above is completed.
- **`Mail.Send` Application permission not yet granted.** Attempted and
  confirmed blocked (see above) — needs a human to complete one interactive
  `az login` to the mailbox's own M365 tenant, then the four `az ad app
  permission`/`admin-consent` commands recorded above and in
  `docs/Backlog.md`.
- **Frontend is untouched.** This WP's spec scopes it as Backend Engineer
  work only — WP-032 (Senior React Engineer, depends on this WP) is where the
  UI surfacing "a real email was sent" gets built.
- **No IngestionIssue-style in-app surfacing for a missing supplier email.**
  Recorded as an open question in `docs/Backlog.md`, not built — genuinely
  unclear whether it's needed, not asked for by this WP's spec.
