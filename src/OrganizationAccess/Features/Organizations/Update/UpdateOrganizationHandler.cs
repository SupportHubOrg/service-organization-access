using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Organizations.UpdateOrganization;

public class UpdateOrganizationHandler(OrganizationDbContext db, IEventPublisher publisher) : IRequestHandler<UpdateOrganizationCommand, bool>
{
    private readonly OrganizationDbContext _db = db;
    private readonly IEventPublisher _publisher = publisher;

    public async Task<bool> Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        //Retrieve the organization to update and return 404 Not Found if it doesn't exist
        var organization = await _db.Organizations.Where(o => o.Id == request.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (organization == null)
            throw new AppException(StatusCodes.Status404NotFound, $"Organization {request.OrganizationId} could not be found.");

        //Check if the user exists and return 401 Unauthorized if not
        var user = await _db.Users.Where(u => u.Id == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (user == null)
            throw new AppException(StatusCodes.Status401Unauthorized, $"User {request.UserId} could not be found.");

        //Enforce strict authorization rules
        bool isSystemAdmin = user.Role == Role.SUPPORT_HUB_ADMINISTRATOR || user.Role == Role.SYSTEM;
        bool isOrganizationManager = user.Role == Role.ORGANIZATION_MANAGER && user.OrganizationId == organization.Id;
        if (!isSystemAdmin && !isOrganizationManager)
            throw new AppException(StatusCodes.Status403Forbidden, "User does not have permission to update this organization.");

        //Note if the setting have been changed to determine if an even should be published after the update
        bool settingsChanged = organization.SlaFirstResponseMinutes != request.SlaFirstResponseMinutes ||
            organization.SlaHumanResponseMinutes != request.SlaHumanResponseMinutes ||
            organization.SlaResolutionMinutes != request.SlaResolutionMinutes ||
            organization.AiTrustThreshold != request.AiTrustThreshold;

        //Update organization properties if new values were provided
        if (request.Name != null)
            organization.Name = request.Name;
        if (request.SlaFirstResponseMinutes.HasValue)
            organization.SlaFirstResponseMinutes = request.SlaFirstResponseMinutes.Value;
        if (request.SlaHumanResponseMinutes.HasValue)
            organization.SlaHumanResponseMinutes = request.SlaHumanResponseMinutes.Value;
        if (request.SlaResolutionMinutes.HasValue)
            organization.SlaResolutionMinutes = request.SlaResolutionMinutes.Value;
        if (request.AiTrustThreshold.HasValue)
            organization.AiTrustThreshold = request.AiTrustThreshold.Value;

        //Apply updates to the organization entity
        _db.Organizations.Update(organization);
        await _db.SaveChangesAsync(cancellationToken);

        //Publish an event to notify other services of the organization settings were updated
        if (settingsChanged)
        {
            var settingsUpdatedEvent = new OrganizationSettingsUpdatedEvent(
                organization.Id,
                organization.SlaFirstResponseMinutes,
                organization.SlaHumanResponseMinutes,
                organization.SlaResolutionMinutes,
                organization.AiTrustThreshold
            );

            await _publisher.PublishAsync("OrganizationSettingsUpdated", settingsUpdatedEvent, cancellationToken);
        }

        return true;
    }
}