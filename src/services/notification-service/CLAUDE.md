# notification-service — CLAUDE.md

All rules in `/CLAUDE.md` and `/src/services/_template/CLAUDE.md` apply.

## Domain

Notification service is the single delivery gateway for all outbound and in-app notifications across the
CRM platform. It owns templates, preferences, delivery records and in-app notification inboxes.

**Channels:** Email, SMS, Web Push, Mobile Push, In-App (web + mobile)
**Provider:** Azure Communication Services (ACS) — Email + SMS. Web/Mobile Push via ACS Notification Hub.
**Templates:** Staff-managed HTML/plain-text templates with Handlebars variable substitution.
**Delivery tracking:** every send is recorded; delivery status (Queued/Sent/Delivered/Failed/Bounced/Opened/Clicked) updated via ACS webhooks.
**Preferences:** per-tenant, per-user opt-in/opt-out per channel per notification category.

## Key Entities

### NotificationTemplate
- `Id`, `TenantId`, `Name` (unique per tenant), `Category` (enum), `Channel` (enum), `SubjectTemplate` (nullable — email/push only), `BodyHtmlTemplate` (nullable), `BodyPlainTemplate`, `IsActive` (bool), `Version` (int, auto-increment on update), `CreatedAt`, `UpdatedAt`
- Immutable once `IsActive=true` — create new version instead
- `Render(variables: Dictionary<string,string>)` — Handlebars substitution, returns `RenderedNotification`

### NotificationRecord
- `Id`, `TenantId`, `RecipientUserId` (Guid?), `RecipientAddress` (string — email/phone/device token), `Channel` (enum), `Category` (enum), `TemplateId` (Guid?), `Subject` (nullable), `BodyHtml` (nullable), `BodyPlain`, `Status` (enum), `ProviderMessageId` (string? — ACS message id), `FailureReason` (string?), `SentAt` (DateTime?), `DeliveredAt` (DateTime?), `OpenedAt` (DateTime?), `ClickedAt` (DateTime?), `CreatedAt`
- Append-only — never update a record, create new one per retry

### InAppNotification
- `Id`, `TenantId`, `RecipientUserId` (Guid), `Title`, `Body`, `ActionUrl` (string?), `Category` (enum), `IsRead` (bool), `ReadAt` (DateTime?), `CreatedAt`
- Soft-delete on tenant suspension only

### NotificationPreference
- `Id`, `TenantId`, `UserId` (Guid), `Channel` (enum), `Category` (enum), `IsEnabled` (bool), `UpdatedAt`
- Unique index on `{TenantId, UserId, Channel, Category}`
- Default: all channels enabled unless explicitly opted out

## API Endpoints

### Templates (staff only — RequireAuthorization with role Staff/Admin)
- `GET    /notification-templates`                       → paginated list (filter by channel, category)
- `POST   /notification-templates`                       → create template
- `GET    /notification-templates/{id}`                  → get by id
- `PUT    /notification-templates/{id}`                  → update (bumps Version, deactivates old if IsActive)
- `POST   /notification-templates/{id}/activate`         → set IsActive=true

### Preferences (user-facing — any authenticated user)
- `GET    /notification-preferences`                     → get my preferences (current user from JWT)
- `PUT    /notification-preferences`                     → bulk upsert my preferences

### In-App Inbox (user-facing — web + mobile)
- `GET    /notifications`                                → paginated inbox (unread first, then by date)
- `POST   /notifications/{id}/read`                      → mark single as read
- `POST   /notifications/read-all`                       → mark all as read
- `GET    /notifications/unread-count`                   → returns `{ count: int }` (for badge)

### Delivery (internal + webhook)
- `POST   /internal/notifications/send`                  → called by other services to trigger a send
- `POST   /webhooks/acs`                                 → ACS delivery status webhook (unsigned public endpoint — validate HMAC header)

## Service Bus

### Consumes (all on sub `notification-service`)
| Topic | Event type | Action |
|---|---|---|
| `crm.platform` | `tenant.provisioned` | Send welcome email to provisioned admin user |
| `crm.platform` | `tenant.suspended` | Soft-delete in-app notifications for tenant |
| `crm.identity` | `user.provisioned` | Send welcome/onboarding email to new user |
| `crm.css` | `case.created` | Send case confirmation to contact |
| `crm.css` | `case.assigned` | Send assignment notification to assignee |
| `crm.css` | `case.status.changed` | Notify contact on Resolved/Closed transitions |
| `crm.css` | `sla.breached` | Alert assignee + manager (in-app + email) |
| `crm.sfa` | `lead.assigned` | In-app notification to assignee |
| `crm.sfa` | `opportunity.won` | In-app + email to sales rep + manager |
| `crm.marketing` | `journey.enrollment.created` | Trigger first journey email step |

### Publishes (topic `crm.notifications`)
| Event | Trigger |
|---|---|
| `notification.sent` | After successful ACS send |
| `notification.failed` | After ACS error or preference opt-out |
| `notification.delivered` | ACS webhook confirms delivery |

## Business Rules

1. **Always check preferences first** — if user has opted out of a channel+category combination, skip silently and record a `NotificationRecord` with `Status=Skipped`.
2. **Template fallback** — if no active template found for channel+category, fall back to plain-text body passed in the send request. Never fail silently — log a warning.
3. **Idempotency** — all Service Bus consumers check `MessageId` before processing. `NotificationRecord.ProviderMessageId` must be unique per channel (upsert guard).
4. **ACS webhook HMAC** — validate `x-acs-signature` header on every webhook call. Reject with 401 if invalid. Secret comes from Key Vault.
5. **In-app only** — push/email/SMS respects preferences; in-app notifications are ALWAYS created regardless of preferences (they are opt-out at the UI level, not skipped by the service).
6. **Retry policy** — failed sends are retried via Service Bus dead-letter redelivery, not in-process. Never retry in a tight loop.
7. **PII** — recipient email/phone stored in `NotificationRecord.RecipientAddress` — this field must be excluded from Application Insights telemetry. Log `RecipientUserId` only.
8. **Tenant isolation** — all queries automatically filtered via `TenantId` HasQueryFilter. In-app inbox endpoint returns ONLY the calling user's notifications (filter by `RecipientUserId == currentUserId` from JWT).
