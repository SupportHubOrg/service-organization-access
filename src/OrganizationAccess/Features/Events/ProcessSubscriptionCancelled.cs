using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Infrastructure.Data;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;

namespace SupportHub.OrganizationAccesss.Features.Events;

public record ProcessSubscriptionCancelledCommand(SubscriptionCancelledPayload Payload) : IRequest;

/// <summary>
/// Handles the processing of a subscription cancellation event for an organization.
/// When a subscription cancellation is received, the organization's subscription tier is downgraded to Free.
/// </summary>
/// <param name="db"></param>
/// <param name="logger"></param>
public class ProcessSubscriptionCancelledHandler(
    OrganizationDbContext db,
    ILogger<ProcessSubscriptionCancelledHandler> logger) : IRequestHandler<ProcessSubscriptionCancelledCommand>
{
    private readonly OrganizationDbContext _db = db;
    private readonly ILogger<ProcessSubscriptionCancelledHandler> _logger = logger;

    public async Task Handle(ProcessSubscriptionCancelledCommand request, CancellationToken cancellationToken)
    {
        //Get the payload from the command
        SubscriptionCancelledPayload payload = request.Payload;

        //Fetch the organization
        Organization? organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == payload.OrganizationId, cancellationToken);
        if (organization == null)
        {
            _logger.LogError("Organization with ID {OrganizationId} not found for subscription cancellation processing.", payload.OrganizationId);
            return;
        }

        //Downgrade the organization's subscription tier to Free and set payment processed to false to reflect the cancellation
        organization.Tier = SubscriptionTier.FREE;
        organization.PaymentProcessed = false;
        organization.UpdatedAt = DateTime.UtcNow;

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Log the payment cancellation
        _logger.LogInformation("Subscription cancelled for Organization ID {OrganizationId}. Cancellation Reason: {CancellationReason}", payload.OrganizationId, payload.CancellationReason);
    }
}