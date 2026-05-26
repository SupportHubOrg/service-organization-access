using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Infrastructure.Data;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;

namespace SupportHub.OrganizationAccesss.Features.Events;

public record ProcessPaymentFailedCommand(PaymentFailedPayload Payload) : IRequest;

/// <summary>
/// Handles the processing of a payment failure event for an organization.
/// When a payment failure is received, the organization's subscription tier is set to SUSPENDED and the payment processed flag is set to false.
/// </summary>
/// <param name="db"></param>
/// <param name="logger"></param>
public class ProcessPaymentFailedHandler(
    OrganizationDbContext db,
    ILogger<ProcessPaymentFailedHandler> logger) : IRequestHandler<ProcessPaymentFailedCommand>
{
    private readonly OrganizationDbContext _db = db;
    private readonly ILogger<ProcessPaymentFailedHandler> _logger = logger;

    public async Task Handle(ProcessPaymentFailedCommand request, CancellationToken cancellationToken)
    {
        //Get the payload from the command
        PaymentFailedPayload payload = request.Payload;

        //Fetch the organization
        Organization? organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == payload.OrganizationId, cancellationToken);
        if (organization == null)
        {
            _logger.LogError("Organization with ID {OrganizationId} not found for payment failure processing.", payload.OrganizationId);
            return;
        }

        //Update the organization's subscription status and tier to signify the payment failure
        organization.Tier = SubscriptionTier.SUSPENDED;
        organization.PaymentProcessed = false;
        organization.UpdatedAt = DateTime.UtcNow;

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Log the payment failure and reason warning
        _logger.LogWarning("Payment failed for Organization ID {OrganizationId}. Amount: {Amount}, Failure Reason: {FailureReason}", payload.OrganizationId, payload.Amount, payload.FailureReason);
    }
}