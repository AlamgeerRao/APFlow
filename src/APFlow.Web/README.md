# APFlow.Web

Frontend SPA for AP Flow (React + TypeScript + Vite + Tailwind CSS + React Router). Consumes `APFlow.Api` only, over HTTP — see `03_Solution_Structure.md` §2. Contains no business logic.

## Getting started

```bash
npm install
npm run dev       # start dev server (http://localhost:5173)
npm run build     # type-check + production build
npm run lint      # ESLint
npm run test      # Vitest unit tests
npm run preview   # preview the production build
```

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
- **WP-015 (Invoice Work Queue):** implemented. See `docs/WP-015-Invoice-Queue-Decisions.md` for open decisions pending Architect/WP-011 sign-off.
- **WP-016 (Invoice Review Screen):** implemented. PDF rendering approach (native browser viewer) was explicitly ruled on by the Chief Technical Architect before implementation. See `docs/WP-016-Invoice-Review-Decisions.md` for remaining open decisions pending backend sign-off.
