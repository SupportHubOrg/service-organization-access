using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Infrastructure.Data;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;

namespace SupportHub.OrganizationAccesss.Features.Events;

public record ProcessSubscriptionRenewedCommand(SubscriptionRenewedPayload Payload) : IRequest;

/// <summary>
/// Handles the processing of a subscription renewal event for an organization.
/// When a subscription renewal is received, the organization's subscription tier and payment processed flag are updated accordingly.
/// </summary>
/// <param name="db"></param>
/// <param name="logger"></param>
public class ProcessSubscriptionRenewedHandler(
    OrganizationDbContext db,
    ILogger<ProcessSubscriptionRenewedHandler> logger) : IRequestHandler<ProcessSubscriptionRenewedCommand>
{
    private readonly OrganizationDbContext _db = db;
    private readonly ILogger<ProcessSubscriptionRenewedHandler> _logger = logger;

    public async Task Handle(ProcessSubscriptionRenewedCommand request, CancellationToken cancellationToken)
    {
        //Get the payload from the command
        SubscriptionRenewedPayload payload = request.Payload;

        //Fetch the organization
        Organization? organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == payload.OrganizationId, cancellationToken);
        if (organization == null)
        {
            _logger.LogError("Organization with ID {OrganizationId} not found for subscription renewal processing.", payload.OrganizationId);
            return;
        }

        //Update the organization's subscription status and tier based on the subscription renewal
        organization.Tier = payload.NewTier;
        organization.PaymentProcessed = true;
        organization.UpdatedAt = DateTime.UtcNow;

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Log the successful subscription renewal
        _logger.LogInformation("Subscription renewed for Organization ID {OrganizationId}. New Tier: {NewTier}", payload.OrganizationId, payload.NewTier);
    }
}