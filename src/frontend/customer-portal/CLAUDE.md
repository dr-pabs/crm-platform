# Customer Portal — CLAUDE.md

All rules in `/CLAUDE.md` apply. This is a React/TypeScript single-page application.

## Stack
- React 18, TypeScript (strict mode)
- Vite
- TanStack Query
- MSAL React — Entra External ID (B2B federation + B2C email/social)

## Auth
- Uses Entra External ID — separate authority URL from staff portal.
- B2B: federated SSO to client's Entra tenant.
- B2C: email/password + social (Google, Microsoft).
- Auth config in `/src/lib/authConfig.ts` — authority comes from environment variable.

## Key Difference from Staff Portal
- Customers can only see their own cases (enforced by API + JWT claims).
- Company super-users can see all cases for their company (role claim: `portal.superuser`).
- No access to SFA, Marketing, or Analytics data — portal is CS&S only.

## Do Not
- Share components directly with the staff portal — they are separate deployments.
- Expose any internal CRM data (opportunity values, lead scores, agent notes) to this portal.
