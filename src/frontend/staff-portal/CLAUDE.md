# Staff Portal — CLAUDE.md

All rules in `/CLAUDE.md` apply. This is a React/TypeScript single-page application.

## Stack
- React 18, TypeScript (strict mode — no `any` without justification)
- Vite (build tooling)
- TanStack Query (server state, caching, pagination)
- Zustand (client-only UI state — not server state)
- React Hook Form + Zod (forms and validation)
- MSAL React (`@azure/msal-react`) — Entra ID auth for staff
- Design system: custom component library (see `/src/components/ui/`)

## Component Rules
- Never fetch data directly in components — always via TanStack Query hooks in `/src/hooks/`.
- All forms must use React Hook Form + Zod schema validation. Never manage form state with useState.
- No inline styles — use CSS modules or Tailwind utility classes only.
- All user-facing text must go through the i18n helper (even if only English for now).
- Components must have explicit TypeScript prop interfaces — never use `any` for props.

## Structure
```
src/
  components/
    ui/          → design system primitives (Button, Input, Modal, Table, etc.)
    features/    → feature-specific components (LeadCard, OpportunityBoard, etc.)
  hooks/         → TanStack Query hooks (useContacts, useOpportunities, etc.)
  pages/         → top-level route pages
  stores/        → Zustand stores (UI state only)
  lib/           → utilities, API client, auth config
  types/         → shared TypeScript types
```

## Auth
- Use `useMsal()` hook to get the current user.
- All API calls must include the Bearer token from MSAL `acquireTokenSilent`.
- Never store tokens in localStorage — MSAL handles token storage.

## API Calls
- All API calls go through the centralised Axios instance in `/src/lib/apiClient.ts`.
- The API client automatically attaches the auth token and `X-Tenant-Id` header.
- Never use `fetch` directly — always use the API client.

## Do Not
- Use `useEffect` for data fetching — use TanStack Query.
- Import from other feature directories directly — use the shared hooks/types only.
- Commit any hardcoded API URLs — all endpoints come from environment config.
