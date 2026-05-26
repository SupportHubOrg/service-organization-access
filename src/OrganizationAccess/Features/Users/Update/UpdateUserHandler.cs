using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Users.UpdateUser;

public class UpdateUserHandler(OrganizationDbContext db, IEventPublisher publisher) : IRequestHandler<UpdateUserCommand, bool>
{
    private readonly OrganizationDbContext _db = db;
    private readonly IEventPublisher _publisher = publisher;

    public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        //Retrieve the user to update from the database, check if they exist, and return 404 Not Found if not
        var user = await _db.Users.Where(u => u.Id == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (user == null)
            throw new AppException(StatusCodes.Status404NotFound, $"User {request.UserId} to update could not be found.");

        //Check if the operator exists and return 401 Unauthorized if not
        var operatorUser = await _db.Users.Where(u => u.Id == request.OperatorId)
            .FirstOrDefaultAsync(cancellationToken);
        if (operatorUser == null)
            throw new AppException(StatusCodes.Status401Unauthorized, $"User {request.OperatorId} could not be found.");

        //Enforce strict authorization rules
        bool isSystemAdmin = operatorUser.Role == Role.SUPPORT_HUB_ADMINISTRATOR || operatorUser.Role == Role.SYSTEM;
        bool isOrganizationManager = operatorUser.Role == Role.ORGANIZATION_MANAGER && operatorUser.OrganizationId == user.OrganizationId && user.Role != Role.ORGANIZATION_MANAGER;
        if (!isSystemAdmin && !isOrganizationManager)
            throw new AppException(StatusCodes.Status403Forbidden, "User does not have permission to update this user.");

        //Check if there are any changes to be made; if not, return true without saving to the database
        bool emailChanged = !string.IsNullOrEmpty(request.Email) && request.Email != user.Email;
        bool roleChanged = request.TargetRole.HasValue && request.TargetRole != user.Role;
        if (!emailChanged && !roleChanged)
            return true;
        
        //Check if the user role update is valid based on the operator's role and the target role
        if (roleChanged)
        {
            bool managerAllowedRoles = request.TargetRole == Role.EMPLOYEE || request.TargetRole == Role.LEVEL_1_OPERATOR || request.TargetRole == Role.LEVEL_2_OPERATOR;
            bool systemAdminDisallowedRoles = request.TargetRole == Role.SYSTEM || request.TargetRole == Role.AI;

            if (isOrganizationManager && !managerAllowedRoles)
                throw new AppException(StatusCodes.Status403Forbidden, "Organization Managers can only assign Employee, Level 1 Operator, or Level 2 Operator roles.");

            if (systemAdminDisallowedRoles)
                throw new AppException(StatusCodes.Status403Forbidden, "System and AI roles cannot be assigned through this endpoint.");
        }

        //Save the user's current role before updating it
        var oldRole = user.Role;

        //Update the user's role and email
        if (roleChanged)
            user.Role = (Role)request.TargetRole!;
        if (emailChanged)
            user.Email = request.Email!;

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Publish a UserRoleUpdatedEvent to Kafka if the user's role was changed
        if (roleChanged)
        {
            UserRoleUpdatedEvent userRoleUpdatedEvent = new(
                OrganizationId: user.OrganizationId ?? Guid.Empty,
                UserId: user.Id,
                OldRole: oldRole,
                NewRole: (Role)request.TargetRole!
            );
            await _publisher.PublishAsync("UserRoleUpdated", userRoleUpdatedEvent, cancellationToken);
        }

        return true;
    }
}