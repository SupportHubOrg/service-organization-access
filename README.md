# Organization Access Service

## General information
The Organization Access service owns organizations and users in SupportHub, including subscription tiers, SLA settings, invitations, and role assignments. It exposes a REST API and integrates with other services through Kafka events.

Core characteristics:
- ASP.NET Core minimal API with MediatR for commands/queries.
- SQL Server persistence with EF Core and automatic migrations on startup.
- JWT Bearer authentication; most endpoints require authorization.
- Kafka for incoming and outgoing domain events.
- Structured logging with Serilog.
- Health checks and Prometheus metrics.
- CORS configured via `Cors:AllowedOrigins`.

Configuration highlights:
- `Authentication:Authority` and `Authentication:Audience` are required.
- `Kafka` settings include `BootstrapServers`, `PublishTopic`, `SubscribeTopics`, `GroupId`, and `DeadLetterQueueTopic`.

## Entities

### `Organization`
*Represents an organization that can have multiple users with different roles and access levels. Contains SLA information for AI and human operator management.*

**Core Fields**

| Field | Type | Restrictions |
| :--- | :--- | :--- |
| `Id` | `Guid` | Primary Key, Required |
| `Name` | `string` | Required, Length: 2–255 characters |
| `Tier` | `SubscriptionTier` | Required, Valid Enum |
| `PaymentProcessed` | `bool` | Required |
| `SlaFirstResponseMinutes` | `decimal` | Required, Non-negative (Min: 0) |
| `AiTrustThreshold` | `decimal` | Required, Non-negative (Min: 0) |
| `SlaResolutionMinutes` | `decimal` | Required, Non-negative (Min: 0) |
| `SlaHumanResponseMinutes` | `decimal` | Required, Non-negative (Min: 0) |
| `CreatedAt` | `DateTime` | Required |
| `UpdatedAt` | `DateTime` | Required |
| `ConfirmedAt` | `DateTime?` | Optional |

**Creation Fields**

| Field | Type | Restrictions |
| :--- | :--- | :--- |
| `CreationToken` | `string?` | Optional, Max Length: 500 |
| `CreationConfirmationStatus` | `InvitationStatus` | Required |
| `CreationTokenExpiresAt` | `DateTime?` | Optional |

**Relationships**
* `Users`: 1-to-Many (`ICollection<User>`)

---

### `User`
*Represents a user that belongs to an organization, including properties related to onboarding and email invitations.*

**Core Fields**

| Field | Type | Restrictions |
| :--- | :--- | :--- |
| `Id` | `Guid` | Primary Key, Required |
| `OrganizationId` | `Guid?` | Optional |
| `Email` | `string` | Required, Valid Email Address, Max Length: 255 |
| `Role` | `Role` | Required, Valid Enum |
| `IsSupportHubEmployee` | `bool` | Required |
| `CreatedAt` | `DateTime` | Required |
| `UpdatedAt` | `DateTime` | Required |

**Invitation Fields**

| Field | Type | Restrictions |
| :--- | :--- | :--- |
| `InvitationToken` | `string?` | Optional, Max Length: 500 |
| `InvitationStatus` | `InvitationStatus` | Required |
| `InvitationCreatedAt` | `DateTime?` | Optional |
| `InvitationExpiresAt` | `DateTime?` | Optional |
| `InvitationAcceptedAt` | `DateTime?` | Optional |

**Relationships**
* `Organization`: Many-to-1 (`Organization`)

## Enums:
- `Role`: `AI`, `EMPLOYEE`, `LEVEL_1_OPERATOR`, `LEVEL_2_OPERATOR`, `ORGANIZATION_MANAGER`, `SUPPORT_HUB_ADMINISTRATOR`, `SYSTEM`.
- `SubscriptionTier`: `FREE`, `STANDARD`, `PRO`, `ENTERPRISE`, `SUSPENDED`.
- `InvitationStatus`: `PENDING`, `ACCEPTED`, `REJECTED`.
- `SlaType`: `FIRST_RESPONSE`, `HUMAN_RESPONSE`, `RESOLUTION`.

## API endpoints
All endpoints requiring authorization read the user ID from the `NameIdentifier` claim.

### Users
- `POST /api/organizations/users`
  - Body: `{ "keycloakId": "...", "email": "...", "role": "...", "organizationId": "...", "isSupportHubEmployee": false }`
  - Creates a user.
  - **Publishes:** `UserInvitationCreated` (if not a SupportHub employee)
- `GET /api/organizations/users`
  - Query: `organizationId`, `role`, `isSupportHubEmployee`, `createdAfter`, `createdBefore`, `updatedAfter`, `updatedBefore`, `invitationCreatedAfter`, `invitationCreatedBefore`, `sortBy`, `sortDescending`, `pageNumber`, `pageSize`
  - Returns a filtered, paged list.
- `GET /api/organizations/users/{userId}`
  - Returns user details.
- `PUT /api/organizations/users/{userId}`
  - Body: `{ "email": "...", "targetRole": "..." }`
  - Updates user email and/or role.
  - **Publishes:** `UserRoleUpdated` (if role changed)
- `DELETE /api/organizations/users/{userId}`
  - Deletes a user.
  - **Publishes:** `UserRemovedFromOrganization`
- `PUT /api/organizations/users/{userId}/invitation-confirmation/{token}`
  - Confirms user invitation via token.
  - **Publishes:** `UserInvitationAccepted`

### Organizations
- `POST /api/organizations`
  - Body: `{ "name": "...", "tier": "...", "managerKeycloakId": "...",  "managerEmail": "...", "slaFirstResponseMinutes": 0, "slaHumanResponseMinutes": 0, "slaResolutionMinutes": 0, "aiTrustThreshold": 0 }`
  - Creates an organization.
  - **Publishes:** `OrganizationRegistered`
- `GET /api/organizations`
  - Query: `subscriptionTier`, `sortBy`, `sortDescending`, `pageNumber`, `pageSize`
  - Returns a filtered, paged list.
- `GET /api/organizations/{organizationId}`
  - Returns organization details.
- `PUT /api/organizations/{organizationId}`
  - Body: `{ "name": "...", "slaFirstResponseMinutes": 0, "slaHumanResponseMinutes": 0, "slaResolutionMinutes": 0, "aiTrustThreshold": 0 }`
  - Updates organization name and SLA settings.
  - **Publishes:** `OrganizationSettingsUpdated` (if settings changed)
- `DELETE /api/organizations/{organizationId}`
  - Deletes an organization.
  - **Publishes:** `OrganizationDeleted`
- `PUT /api/organizations/{organizationId}/creation-confirmation/{token}`
  - Body: `{ "managerId": "..." }`
  - Confirms organization creation via token.
  - **Publishes:** `OrganizationCreated`, `OrganizationSettingsUpdated` (if SLA settings are non-zero)

### Health and metrics
- `GET /health/live`
- `GET /health/ready`
- `GET /metrics`

## Kafka events

### Envelope format
All published events are wrapped in a standard envelope:
{
  "metadata": {
    "eventId": "guid",
    "eventType": "string",
    "version": 1,
    "timestamp": "ISO-8601"
  },
  "payload": { ... }
}

### Outgoing events
Event type strings and payloads:
- `UserInvitationCreated` → `UserInvitationCreatedEvent`: `OrganizationId`, `UserId`, `UserEmail`, `InvitationToken`
- `OrganizationSettingsUpdated` → `OrganizationSettingsUpdatedEvent`: `OrganizationId`, `SlaFirstResponseMinutes`, `SlaHumanResponseMinutes`, `SlaResolutionMinutes`, `AiTrustThreshold`
- `OrganizationDeleted` → `OrganizationDeletedEvent`: `OrganizationId`, `OrganizationName`
- `OrganizationCreated` → `OrganizationCreatedEvent`: `OrganizationId`, `OrganizationName`, `Tier`, `ManagerId`
- `OrganizationRegistered` → `OrganizationRegisteredEvent`: `OrganizationId`, `OrganizationName`, `Tier`, `ManagerId`, `ManagerEmail`, `Token`
- `UserInvitationAccepted` → `UserInvitationAcceptedEvent`: `OrganizationId`, `UserId`, `UserEmail`, `Role`
- `UserRoleUpdated` → `UserRoleUpdatedEvent`: `OrganizationId`, `UserId`, `OldRole`, `NewRole`
- `UserRemovedFromOrganization` → `UserRemovedFromOrganizationEvent`: `OrganizationId`, `UserId`, `UserEmail`

### Incoming events
Event type strings and payloads:
- `PaymentConfirmed` → `PaymentConfirmedPayload`: `OrganizationId`, `Amount`, `Tier`, `PaymentDate`
- `PaymentFailed` → `PaymentFailedPayload`: `OrganizationId`, `Amount`, `PaymentDate`, `FailureReason`
- `SubscriptionCancelled` → `SubscriptionCancelledPayload`: `OrganizationId`, `CancellationDate`, `CancellationReason`
- `SubscriptionRenewed` → `SubscriptionRenewedPayload`: `OrganizationId`, `RenewalDate`, `NewTier`
- `SubscriptionUpdated` → `SubscriptionUpdatedPayload`: `OrganizationId`, `UpdateDate`, `OldTier`, `NewTier`, `UpdateReason`
- `NotificationFailed` → `NotificationFailedPayload`: `NotificationId`, `UserId`, `FailureReason`

Dead-letter queue behavior:
- If processing fails, the raw message is sent to `DeadLetterQueueTopic` with failure metadata in headers.

## How incoming Kafka events are handled
Incoming events are consumed by a hosted Kafka consumer service, deserialized based on `metadata.eventType`, and routed to MediatR command handlers:

- `PaymentConfirmed`
  - Updates organization tier and sets `PaymentProcessed` to `true`.
- `PaymentFailed`
  - Sets tier to `SUSPENDED` and sets `PaymentProcessed` to `false`.
- `SubscriptionCancelled`
  - Sets tier to `FREE` and sets `PaymentProcessed` to `false`.
- `SubscriptionRenewed`
  - Updates tier from payload and sets `PaymentProcessed` to `true`.
- `SubscriptionUpdated`
  - Updates tier from payload.

Dead-letter queue behavior:
- Any exception during processing routes the raw message to the DLQ, including error info in headers, and commits offsets only after successful processing.