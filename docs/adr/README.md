# Architecture Decision Records

This directory contains Architecture Decision Records (ADRs) for the CRM Platform.

## What is an ADR?

An ADR documents a significant architectural decision: the context, the options considered, the decision made, and the consequences. They are immutable once accepted — if a decision is reversed, a new ADR supersedes it.

## Format

Files are numbered sequentially: `NNNN-short-title.md`

## Index

| # | Decision | Status |
|---|---|---|
| [0001](0001-multi-tenant-database-strategy.md) | Multi-tenant database strategy (shared DB + RLS) | Accepted |
| [0002](0002-service-communication-via-service-bus.md) | Service-to-service communication via Service Bus | Accepted |
| [0003](0003-iac-bicep-over-terraform.md) | IaC: Bicep over Terraform | Accepted |

## Adding a new ADR

Copy the template below, increment the number, and submit via PR.

```markdown
# ADR NNNN — Title

**Status**: Proposed | Accepted | Superseded by NNNN
**Date**: YYYY-MM-DD
**Deciders**: Name(s)

## Context
## Decision
## Consequences
```
