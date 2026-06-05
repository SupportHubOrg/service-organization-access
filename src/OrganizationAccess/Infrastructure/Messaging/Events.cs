using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Infrastructure.Messaging;

//Outgoing events published to Kafka
public record UserInvitationCreatedEvent(Guid OrganizationId, Guid UserId, string UserEmail, string InvitationToken);
public record OrganizationSettingsUpdatedEvent(Guid OrganizationId, decimal SlaFirstResponseMinutes, decimal SlaHumanResponseMinutes, decimal SlaResolutionMinutes, decimal AiTrustThreshold);
public record OrganizationDeletedEvent(Guid OrganizationId, string OrganizationName);
public record OrganizationCreatedEvent(Guid OrganizationId, string OrganizationName, SubscriptionTier Tier, Guid ManagerId);
public record OrganizationRegisteredEvent(Guid OrganizationId, string OrganizationName, SubscriptionTier Tier, Guid ManagerId, string Token);
public record UserInvitationAcceptedEvent(Guid? OrganizationId, Guid UserId, string UserEmail, Role Role);
public record UserRoleUpdatedEvent(Guid OrganizationId, Guid UserId, Role OldRole, Role NewRole);
public record UserRemovedFromOrganizationEvent(Guid OrganizationId, Guid UserId, string UserEmail);

//Incoming events consumed from the Billing Service
public record PaymentConfirmedPayload(Guid OrganizationId, decimal Amount, SubscriptionTier Tier, DateTime PaymentDate);
public record PaymentFailedPayload(Guid OrganizationId, decimal Amount, DateTime PaymentDate, string FailureReason);
public record SubscriptionCancelledPayload(Guid OrganizationId, DateTime CancellationDate, string CancellationReason);
public record SubscriptionRenewedPayload(Guid OrganizationId, DateTime RenewalDate, SubscriptionTier NewTier);
public record SubscriptionUpdatedPayload(Guid OrganizationId, DateTime UpdateDate, SubscriptionTier OldTier, SubscriptionTier NewTier, string UpdateReason);

//Incoming events consumed from Notification Service
public record NotificationFailedPayload(Guid NotificationId, Guid UserId, string FailureReason);