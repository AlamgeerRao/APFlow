# AP Flow — Support Agent Architecture Plan

**Status:** Deferred to Sprint 2 planning (2026-08-03) — not in scope for the remainder of Sprint 1. To be weighed alongside the rest of Sprint 2's existing scope (Supplier Management, Query/Statement workflows, Remittances, Sage 50, Administration Portal) when that planning pass happens, not bolted on ad hoc.
**Prepared by:** Chief Technical Architect
**Purpose:** Capture the two-agent support architecture discussed, in enough detail to resume the conversation without re-deriving it from scratch, whenever Sprint 2 planning reaches it.

---

## 1. The core decision: two agents, not one

Everything below rests on one deliberate architectural boundary: **a customer-facing support agent and an internal engineering-support agent must be two separate systems, with no shared trust boundary between them.** Conflating them — letting an end-user's support question have any path, however indirect, to triggering a code change — would be a serious security regression, not a convenience worth the risk.

| | Customer Support Agent | Engineering Support Agent |
|---|---|---|
| **Who talks to it** | GB Skips' AP staff (Reviewers/Approvers), inside the AP Flow portal | Your own engineering/support team, outside the portal |
| **What it can do** | Answer questions, navigate/explain the UI, query read-only data via AP Flow's own API | Investigate bugs, propose and (with review) implement fixes via Claude Code |
| **Underlying engine** | Claude API (Messages API) or Azure OpenAI, with function-calling | Claude Code — Agent SDK or headless (`claude -p`) mode |
| **Write access to anything** | None. Read-only against AP Flow's own already-authenticated API. | Yes — but gated by a human review step before merge/deploy, same as every WP in this project has been reviewed |
| **Where it runs** | Inside/alongside the AP Flow application, callable by any signed-in user | Your own compute (a container, Azure Container App, or VM) — not customer-facing, not reachable from the portal at all |

---

## 2. Customer Support Agent (in-portal, for GB Skips users)

### 2.1 Purpose

Answer "how do I..." and "why is this..." questions for signed-in AP Flow users, and — if useful later — perform read-only lookups on their behalf ("show me invoices still awaiting my review," "why was this flagged as a duplicate"). Not a replacement for the UI; a faster way to get an answer or find something without hunting through menus.

### 2.2 What it must never do

- Never call anything that writes, updates, deletes, or changes a workflow status. If a user asks it to "approve this invoice," it should explain how to do that in the UI, not do it on their behalf via a hidden write path — the existing role-gated UI actions remain the only way state actually changes.
- Never have any path — direct or indirect — to code, infrastructure, deployment, or the repository. It is not a tool for surfacing or fixing bugs; if a user describes something that sounds like a bug, the correct behavior is to say so plainly and hand off to the issue-reporting flow in §3.6 below — not attempt to diagnose or fix it live, in-conversation.
- Never see another tenant's data. It must go through the exact same tenant-scoped, role-checked API endpoints a real UI action would use — no privileged or service-level data access of its own.

### 2.3 Suggested implementation shape

- **Engine:** Claude API (Messages API) with tool-use/function-calling, or Azure OpenAI — either is viable; Claude API is the more natural fit given the rest of this stack and this engagement's own tooling, but Azure OpenAI keeps everything inside one cloud vendor relationship if that matters for procurement reasons.
- **Tools exposed to the model:** a small, explicit, read-only set — e.g. `getMyPendingInvoices`, `getInvoiceStatus(invoiceId)`, `explainDuplicateFlag(invoiceId)`, `getAvailableActions(invoiceId)`. Each tool call is just a thin wrapper around an existing, already-authenticated `GET` endpoint — the agent inherits the calling user's own real Bearer token and role, so it can never see or do more than that user already could through the UI.
- **Where it runs:** as a new, small backend service or a new controller within `APFlow.Api` itself (simplest — reuses the existing auth pipeline directly, no new token-passing scheme to design), calling out to the Claude/Azure OpenAI API from there. Not a new Azure Cognitive Services-style resource the way Document Intelligence is — it's an application-layer feature, hosted like any other part of the API.
- **Frontend:** a chat-style panel or widget within the existing React app, calling a new `APFlow.Api` endpoint (e.g. `POST /api/support/chat`), same auth model as everything else.

### 2.4 Open questions for later

- Does it need to see the current page/invoice the user is looking at (contextual help), or is a standalone chat sufficient for a first version?
- Should conversation history persist (so a user can return to a prior support thread), or is each session stateless?
- Should responses be reviewed/tunable by your team (a system prompt you control) before this goes in front of GB Skips, given it'll represent the product directly to a paying customer?

---

## 3. Engineering Support Agent (internal, Claude Code-based)

### 3.1 Purpose

Automate the triage-and-propose-a-fix loop that has, up to now, been done manually in this project: something is reported (an error, a log anomaly, a QA finding), an engineer investigates, proposes a fix, and a human reviews before it merges. This agent would do the investigation and drafting automatically; the human review gate stays exactly as strict as it has been throughout this engagement.

### 3.2 How it would actually invoke Claude Code

Confirmed via current documentation: Claude Code supports exactly this pattern today, in two forms:

- **Headless CLI mode** (`claude -p "<prompt>" --output-format json`) — simplest, good for a discrete, one-shot "investigate this and propose a fix" trigger (e.g. from a script, a scheduled job, or a webhook).
- **Claude Agent SDK** (Python/TypeScript) — a proper library form of the same engine, for building a real orchestrating application around it rather than shelling out to a CLI. Worth using once this graduates past a first prototype.

Both are designed to be invoked by another system, not just a human at a terminal — this is a supported, intended usage pattern, not a workaround.

### 3.3 Non-negotiable guardrails

- **No `--dangerously-skip-permissions` / unrestricted bypass mode.** Use an explicit, scoped permission mode — auto-approve read/investigate actions (file reads, searches, running tests), but require human approval before any write, commit, or push.
- **A human reviews every proposed change before it merges** — exactly the pattern this entire engagement has followed for every one of the 80+ work packages so far. This agent should produce a report and a proposed diff, the same shape as every "post-WP-XXX-report.md" already reviewed in this project — not a system that merges or deploys on its own judgment.
- **Scoped to the repository only** — no access to production infrastructure, no ability to run `az` commands against live Azure resources, no credentials beyond what's needed to read the codebase and open a PR.
- **Deterministic hooks, not just prompt-level instructions, for the hard limits** — e.g. a pre-tool-use hook that vetoes any `git push` or any write outside the repository's own working directory, regardless of what the model decides. Rules that must never be violated should be enforced in code, not just requested in the prompt.

### 3.4 Suggested trigger sources (pick one to start, not all at once)

- A new GitHub issue matching a certain label (e.g. `auto-triage`).
- A failed CI run, feeding the failure output in as the initial prompt.
- A manually-run script for now — the simplest possible starting point, before wiring any automatic trigger at all.
- **A user-submitted issue report from within the portal — see §3.6, the specific flow requested for this project.**

### 3.5 Where it runs

Its own compute — a container or small VM, not inside `APFlow.Api` and not reachable from the customer-facing portal at all. It needs filesystem/git access to a clone of the repository and network access to the Anthropic API (via an API key, not a Max subscription — the documented, supported path for anything wrapped into a larger system rather than used interactively).

---

### 3.6 Concrete flow: user-reported issue → auto-investigate → single human gate → deploy → verify → notify

This is the specific, primary use case for the Engineering Support Agent on this project: **users hit something odd in a newly-built system, report it in two clicks, and — apart from one human checkpoint immediately before anything ships — the rest is automated.**

**Step 1 — Report capture (in-portal, lightweight)**

A "Report an issue" affordance, available from anywhere in the app (not just a dead-end contact form). On submission, capture automatically, without asking the user to describe technical detail they won't have:

- Who: user identity, role, tenant (already known from their session — no need to ask).
- Where: current page/route, and the specific invoice/record ID if applicable.
- What: their own free-text description ("the page went blank when I clicked X").
- **Recent client-side errors, captured automatically.** If Application Insights' browser SDK is added to the frontend (not currently present — worth doing specifically to support this flow), any JS exception in that session shortly before the report can be attached automatically. This is exactly the missing piece in both incidents from the last live test — a user could describe "it crashed," but the actual `RangeError`/stack trace that would have taken an investigator straight to the cause was only ever found by someone else digging through logs after the fact. Attaching it automatically at report time removes that whole manual step.

**Step 2 — Ticket created, not immediately investigated**

The report becomes a durable record (a new `SupportTicket`-style entity, or a GitHub issue created via API — either works; a lightweight internal entity is probably simpler to start, since it avoids needing to expose repo-level access to anything user-facing). Status: `Reported`.

**Design decision worth flagging: batch/dedupe before triggering an investigation.** If three people report the same underlying bug within an hour, this should become one investigation, not three parallel Claude Code sessions. A simple heuristic (same route + similar error signature within a short window) is enough for a first version — don't over-engineer this before it's a real problem.

**Step 3 — Claude Code investigates and drafts a fix (headless, unattended, no push)**

Triggered (manually at first, or on a schedule/webhook later) with a structured prompt containing everything from Step 1. Claude Code:
- Investigates using read-only tools first (exactly the discipline already established throughout this project — reproduce and understand before writing anything).
- Drafts a fix, with tests, on a branch — never on `main` directly.
- **Also proposes its own live-verification plan** — the specific check that will prove the fix works once deployed (a specific test, a specific API call, a specific page to reload) — mirroring exactly the "Live verification" section every WP report in this project has already been including.
- **Explicitly allowed outcome: "this isn't a code bug."** Not everything reported will be fixable by writing code — some reports will turn out to be a business-rule question, a training/UX-confusion issue, or a duplicate of something already known. Claude Code should be able to conclude and report that plainly, routing to a human for a product/business decision rather than forcing an unwanted code change to "resolve" something that was never a defect. This mirrors the many decision-doc escalations this project has already relied on (workflow states, RBAC scope, duplicate-matching rules) — the agent should escalate ambiguity, not silently guess.

**Step 4 — The single human gate, immediately before push**

The reviewer sees one consolidated package, not a raw diff:
- Original report: who, when, what page, their description, the auto-captured error (if any).
- Claude Code's root-cause diagnosis, in plain language.
- The actual proposed change (diff) and its new/updated tests.
- The specific live-verification plan for after deploy.

Three possible reviewer actions, not just approve/reject:
- **Approve → push.** Goes through the *existing* CI/CD pipeline unchanged — this gate decides whether a commit happens at all, it doesn't bypass the pipeline that already builds/tests/deploys it.
- **Request changes.** Feedback goes back to Claude Code for another pass, same package re-presented.
- **Reject — not a code fix.** Routes to a human-owned backlog/decision item instead (exactly like every architecture escalation this project has already produced), and the reporting user still needs an honest status update (see Step 6) — "acknowledged, being looked at differently" rather than silence.

**Step 5 — Post-deploy verification**

Once pushed and deployed through the normal pipeline, the proposed verification plan from Step 3 is actually run — ideally by Claude Code itself in a short follow-up pass (confirm the specific test/check now passes against the live environment), closing the loop with real evidence before anyone tells the user it's fixed. This is the same discipline this entire project has insisted on throughout — a green build is not the same claim as a live-confirmed fix.

**Step 6 — Notify the reporting user**

Simplest version: an in-app status indicator tied to their own report (`Reported → Investigating → Fix deployed, verifying → Resolved` / `Reported → Reviewed, not a code issue`). No outbound email needed for a first version — avoids needing to build/wire a send-capability that doesn't exist yet. Email notification (reusing whatever Graph send-mail capability Sprint 2's remittance-email feature eventually builds) is a reasonable later upgrade, not a Day 1 requirement.

---

## 4. Suggested phasing (not a commitment, just a sensible order)

1. **Prototype the Engineering Support Agent first, manually triggered, against a manually-created test ticket** — no in-portal reporting UI yet, no auto-triggering. Prove the investigate → draft → human-gate → deploy → verify loop works at all before building the intake UI around it.
2. **Add the in-portal "Report an issue" capture (§3.6, Steps 1–2)**, including client-side error capture via Application Insights' browser SDK — this alone is a useful, standalone improvement even before any automated investigation exists behind it.
3. **Wire the two together**, starting with manual triggering of the investigation step (a human decides which tickets get sent to Claude Code), before considering any fully automatic trigger.
4. **Customer Support Agent** (§2) — independent of the above, can be built in parallel or after, since it shares no components with the engineering-support flow.

---

## 5. Questions to resolve when Sprint 2 planning reaches this

- Confirm the phasing above, or reprioritize against the rest of Sprint 2's scope.
- `SupportTicket` as a new internal entity, or a GitHub issue created via API — preference either way?
- Who is "the reviewer" at Step 4 day-to-day — you specifically, or does this need its own role separate from the Chief Technical Architect function this conversation has been playing?
- Application Insights browser SDK isn't in the frontend yet — worth scoping as its own small, standalone task even before the rest of this, since it's independently useful (this is exactly the piece that would have shortened both incidents from the last live test)?
- For the Customer Support Agent (§2): Claude API or Azure OpenAI — any procurement/vendor preference either way?

---

## 6. Note on why this is deferred (2026-08-03)

Sprint 2's existing work packages (Supplier Management, Query/Statement workflows, Credit Limit Monitoring, Remittances, Sage 50 Integration, Administration Portal — see the original Sprint 2 plan) need a full revisit anyway before that sprint starts, given how much has changed since they were first scoped (the workflow engine, RBAC, and multi-tenant architecture all evolved substantially during Sprint 1's extended run). This support-agent work should be prioritized and sequenced alongside that revisit, not treated as a separate, parallel track.
