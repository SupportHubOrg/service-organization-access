using MediatR;
using Microsoft.EntityFrameworkCore;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Data;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;

namespace SupportHub.OrganizationAccesss.Features.Users.DeleteUser;

public class DeleteUserHandler(OrganizationDbContext db, IEventPublisher publisher) : IRequestHandler<DeleteUserCommand, bool>
{
    private readonly OrganizationDbContext _db = db;
    private readonly IEventPublisher _publisher = publisher;

    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        //Retrieve the user to be deleted, if they don't exist return 404 Not Found
        User? user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null)
            throw new AppException(StatusCodes.Status404NotFound, $"User {request.UserId} could not be found.");

        //Retrieve the operating user to verify permissions and check if they exist, otherwise return 401 Unauthorized
        User? operatorUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.OperatorId, cancellationToken);
        if (operatorUser == null)
            throw new AppException(StatusCodes.Status401Unauthorized, $"User {request.OperatorId} could not be found.");

        //Enforce strict authorization rules for deletion
        bool isSystemAdmin = operatorUser.Role == Role.SUPPORT_HUB_ADMINISTRATOR || operatorUser.Role == Role.SYSTEM;
        bool isOrganizationManager = operatorUser.Role == Role.ORGANIZATION_MANAGER && operatorUser.OrganizationId == user.OrganizationId && user.Role != Role.ORGANIZATION_MANAGER;
        if (!isSystemAdmin && !isOrganizationManager)
            throw new AppException(StatusCodes.Status403Forbidden, $"User {request.OperatorId} does not have permission to delete user {request.UserId}.");

        //Remove the user from the database
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);

        //Publish an event to notify other services of the deletion
        if (user.OrganizationId.HasValue)
        {
            UserRemovedFromOrganizationEvent userRemovedFromOrganizationEvent = new(user.OrganizationId.Value, user.Id, user.Email);
            await _publisher.PublishAsync("UserRemovedFromOrganization", userRemovedFromOrganizationEvent, cancellationToken);
        }

        return true;
    }
}