using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Organizations.CreationConfirmation;

/// <summary>
/// Handles the creation of a new organization.
/// </summary>
/// <param name="db"></param>
/// <param name="publisher"></param>
/// <param name="logger"></param>
public class CreationConfirmationHandler(OrganizationDbContext db, IEventPublisher publisher) : IRequestHandler<CreationConfirmationCommand, Guid>
{
    private readonly OrganizationDbContext _db = db;
    private readonly IEventPublisher _publisher = publisher;

    public async Task<Guid> Handle(CreationConfirmationCommand request, CancellationToken cancellationToken)
    {
        //Retrieve the organization by ID and verify that it exists
        Organization? organization = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken);
        if (organization == null)
            throw new AppException(StatusCodes.Status404NotFound, $"Organization {request.OrganizationId} not found.");
        
        //Check if the organization is already confirmed
        if (organization.CreationConfirmationStatus == InvitationStatus.ACCEPTED)
            throw new AppException(StatusCodes.Status400BadRequest, $"Organization {request.OrganizationId} has already been confirmed.");
        
        //Check if the user is registered as the organization manager
        User? manager = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.ManagerId && u.OrganizationId == request.OrganizationId, cancellationToken);
        if (manager == null)
            throw new AppException(StatusCodes.Status404NotFound, $"Manager with ID {request.ManagerId} not found for organization {request.OrganizationId}.");
        
        //Check if the organization has a pending invitation, if it does not have return 400 Bad Request
        if (organization.CreationConfirmationStatus != InvitationStatus.PENDING && organization.CreationTokenExpiresAt != null)
            throw new AppException(StatusCodes.Status400BadRequest, $"Organization {request.OrganizationId} does not have a pending invitation.");

        //Check if the creation confirmation token is valid and not expired
        if (organization.CreationToken != request.Token || organization.CreationTokenExpiresAt < DateTime.UtcNow)
            throw new AppException(StatusCodes.Status400BadRequest, $"Invalid or expired creation confirmation token for organization {request.OrganizationId}.");

        //Update the organization creation confirmation status to accepted
        organization.CreationConfirmationStatus = InvitationStatus.ACCEPTED;
        organization.UpdatedAt = DateTime.UtcNow;
        organization.CreationToken = null;
        organization.CreationTokenExpiresAt = null;

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Publish OrganizationCreatedEvent to Kafka
        OrganizationCreatedEvent organizationCreatedEvent = new(organization.Id, organization.Name, organization.Tier, manager.Id);
        await _publisher.PublishAsync("OrganizationCreated", organizationCreatedEvent, cancellationToken);

        //If any sla rules are not set to zero publish OrganizationSettingsUpdatedEvent to Kafka to trigger the creation of the default organization settings in the Organization Settings Service
        if (organization.SlaFirstResponseMinutes > 0 || organization.SlaHumanResponseMinutes > 0 || organization.SlaResolutionMinutes > 0 || organization.AiTrustThreshold > 0)
        {
            var organizationSettingsUpdatedEvent = new OrganizationSettingsUpdatedEvent(
                organization.Id,
                organization.SlaFirstResponseMinutes,
                organization.SlaHumanResponseMinutes,
                organization.SlaResolutionMinutes,
                organization.AiTrustThreshold
            );
            await _publisher.PublishAsync("OrganizationSettingsUpdated", organizationSettingsUpdatedEvent, cancellationToken);
        }

        //Return the ID of the newly created organization
        return organization.Id;
    }
}