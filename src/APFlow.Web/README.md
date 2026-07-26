# APFlow.Web

Frontend SPA for AP Flow (React + TypeScript + Vite + Tailwind CSS + React Router). Consumes `APFlow.Api` only, over HTTP — see `03_Solution_Structure.md` §2. Contains no business logic.

## Getting started

```bash
cp .env.example .env.local   # fill in real per-environment values — see .env.example
npm install
npm run dev       # start dev server (http://localhost:5173)
npm run build     # type-check + production build
npm run lint      # ESLint
npm run test      # Vitest unit tests
npm run preview   # preview the production build
```

Requires a real (or dev-tier) Entra External ID app registration and a reachable `APFlow.Api` instance to actually sign in and load data — see `docs/WP-020-Manual-Verification-Checklist.md`.

## Structure

```
src/
├── api/            # typed API clients (interfaces first, HTTP implementations swapped in as backend WPs ship)
├── auth/            # auth/tenant context, protected route guard
├── components/
│   └── layout/       # Header, LeftNav, AppShell, shared layout primitives
├── pages/           # one component per top-level route
├── routes/          # route table
└── types/           # shared TypeScript types
```

## Status

- **WP-014 (Dashboard Shell & Navigation):** implemented. See `docs/WP-014-Dashboard-Shell-Decisions.md` for open decisions pending Architect/WP-050 sign-off.
- **WP-015 (Invoice Work Queue):** implemented, now against the real API (WP-020). See `docs/WP-015-Invoice-Queue-Decisions.md`.
- **WP-016 (Invoice Review Screen):** implemented, now against the real API (WP-020). PDF rendering approach (native browser viewer) was explicitly ruled on by the Chief Technical Architect before implementation. See `docs/WP-016-Invoice-Review-Decisions.md`.
- **WP-017 (Notes & Comments Component):** implemented, now against the real API (WP-020). See `docs/WP-017-Invoice-Notes-Decisions.md`.
- **WP-018 (Workflow Actions UI):** implemented, now against the real API (WP-020). See `docs/WP-018-Invoice-Workflow-Actions-Decisions.md` and `docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md` §5 — full reconciliation of the fixture (still used for local dev fallback) against WP-053's 57-row graph remains blocked on not having that table.
- **WP-019 (Supplier & Folder Views):** implemented against a fixture client — **still fixture-only**, not swapped by WP-020, since no backend endpoint exists for it yet. See `docs/WP-019-Supplier-Folder-Views-Decisions.md` and `docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md` §4.
- **WP-020 (Real Authentication & API Integration):** implemented — real MSAL/Entra sign-in, a central `fetch`-based API client, and four of five fixture-client swaps. **Live verification against a real Entra tenant/backend could not be performed in this environment** — see `docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md` §0 and `docs/WP-020-Manual-Verification-Checklist.md`. QA caught (2026-07-27) and this delivery now fixes three DTO field-name mismatches across `invoiceClient.ts`/`invoiceDetailMapping.ts`/`invoiceNoteClient.ts` — see decision doc §7.
