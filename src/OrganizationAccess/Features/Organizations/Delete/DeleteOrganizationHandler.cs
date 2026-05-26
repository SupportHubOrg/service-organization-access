using MediatR;
using Microsoft.EntityFrameworkCore;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Data;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;

namespace SupportHub.OrganizationAccesss.Features.Organizations.DeleteOrganization;

public class DeleteOrganizationHandler(OrganizationDbContext db, IEventPublisher publisher) : IRequestHandler<DeleteOrganizationCommand, bool>
{
    private readonly OrganizationDbContext _db = db;
    private readonly IEventPublisher _publisher = publisher;

    public async Task<bool> Handle(DeleteOrganizationCommand request, CancellationToken cancellationToken)
    {
        //Retrieve the Organization to be deleted and check if it exists, otherwise return 404 Not Found
        Organization? organization = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken);
        if (organization == null)
            throw new AppException(StatusCodes.Status404NotFound, $"Organization {request.OrganizationId} could not be found.");

        //Retrieve the user to verify permissions and check if they exist, otherwise return 401 Unauthorized
        User? user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null)
            throw new AppException(StatusCodes.Status401Unauthorized, $"User {request.UserId} could not be found.");

        //Enforce strict authorization rules for deletion - only System Admins can delete organizations
        bool isSystemAdmin = user.Role == Role.SUPPORT_HUB_ADMINISTRATOR || user.Role == Role.SYSTEM;
        if (!isSystemAdmin)
            throw new AppException(StatusCodes.Status403Forbidden, $"User {request.UserId} attempted to delete Organization {request.OrganizationId} without sufficient permissions. Only SupportHub Administrators can delete Organizations.");

        //Delete all users associated with the organization
        var organizationUsers = await _db.Users
            .Where(u => u.OrganizationId == request.OrganizationId)
            .ToListAsync(cancellationToken);
        _db.Users.RemoveRange(organizationUsers);

        //Remove the Organization from the database
        _db.Organizations.Remove(organization);
        await _db.SaveChangesAsync(cancellationToken);

        //Publish an event to notify other services that an Organization has been deleted
        OrganizationDeletedEvent organizationDeletedEvent = new(organization.Id, organization.Name);
        await _publisher.PublishAsync("OrganizationDeleted", organizationDeletedEvent, cancellationToken);

        return true;
    }
}