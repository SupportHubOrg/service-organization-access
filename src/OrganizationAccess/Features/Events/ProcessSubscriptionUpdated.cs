using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Infrastructure.Data;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;

namespace SupportHub.OrganizationAccesss.Features.Events;

public record ProcessSubscriptionUpdatedCommand(SubscriptionUpdatedPayload Payload) : IRequest;

/// <summary>
/// Handles the processing of a subscription update event for an organization.
/// When a subscription update is received, the organization's subscription tier is updated accordingly.
/// </summary>
/// <param name="db"></param>
/// <param name="logger"></param>
public class ProcessSubscriptionUpdatedHandler(
    OrganizationDbContext db,
    ILogger<ProcessSubscriptionUpdatedHandler> logger) : IRequestHandler<ProcessSubscriptionUpdatedCommand>
{
    private readonly OrganizationDbContext _db = db;
    private readonly ILogger<ProcessSubscriptionUpdatedHandler> _logger = logger;

    public async Task Handle(ProcessSubscriptionUpdatedCommand request, CancellationToken cancellationToken)
    {
        //Get the payload from the command
        SubscriptionUpdatedPayload payload = request.Payload;

        //Fetch the organization
        Organization? organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == payload.OrganizationId, cancellationToken);
        if (organization == null)
        {
            _logger.LogError("Organization with ID {OrganizationId} not found for subscription update processing.", payload.OrganizationId);
            return;
        }

        //Update the organization's subscription tier based on the subscription update
        organization.Tier = payload.NewTier;
        organization.UpdatedAt = DateTime.UtcNow;

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Log the successful subscription update
        _logger.LogInformation("Subscription updated for Organization ID {OrganizationId}. Old Tier: {OldTier}, New Tier: {NewTier}", payload.OrganizationId, payload.OldTier, payload.NewTier);
    }
}