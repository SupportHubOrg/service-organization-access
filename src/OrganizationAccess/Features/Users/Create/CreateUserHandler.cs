using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Users.CreateUser;

public class CreateUserHandler(OrganizationDbContext db, IEventPublisher publisher) : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly OrganizationDbContext _db = db;
    private readonly IEventPublisher _publisher = publisher;

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        //Retrieve the creator from the database, if they do not exist
        User? creator = await _db.Users
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Id == request.CreatorId, cancellationToken);
        if (creator == null)
            throw new AppException(StatusCodes.Status404NotFound, $"User with ID {request.CreatorId} not found.");

        //Enforce authorization rules
        bool isSystemAdmin = creator.Role == Role.SUPPORT_HUB_ADMINISTRATOR || creator.Role == Role.SYSTEM;
        bool isOrganizationManager = creator.Role == Role.ORGANIZATION_MANAGER && creator.OrganizationId == request.OrganizationId;
        if (!isSystemAdmin && !isOrganizationManager)
            throw new AppException(StatusCodes.Status403Forbidden, "You do not have administrative permissions to invite users.");

        //Ensure only system admins can create SupportHub employee accounts and organization managers
        if ((request.IsSupportHubEmployee || request.Role == Role.ORGANIZATION_MANAGER) && !isSystemAdmin)
            throw new AppException(StatusCodes.Status403Forbidden, "Only system administrators can create SupportHub and Organization Manager accounts.");

        //Ensure the email is not already in use
        bool emailInUse = await _db.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailInUse)
            throw new AppException(StatusCodes.Status400BadRequest, "Email is already in use.");

        //Check if the user role is valid based on the creator's role
        bool managerAllowedRoles = request.Role == Role.EMPLOYEE || request.Role == Role.LEVEL_1_OPERATOR || request.Role == Role.LEVEL_2_OPERATOR;
        bool systemAdminDisallowedRoles = request.Role == Role.SYSTEM || request.Role == Role.AI;
        if (isOrganizationManager && !managerAllowedRoles)
            throw new AppException(StatusCodes.Status403Forbidden, "Organization Managers can only assign Employee, Level 1 Operator, or Level 2 Operator roles.");
        if (systemAdminDisallowedRoles)
            throw new AppException(StatusCodes.Status403Forbidden, "System and AI roles cannot be assigned through this endpoint.");

        //Create the new user entity
        var user = new User
        {
            Id = request.KeycloakId ?? Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Email = request.Email,
            Role = request.Role,
            IsSupportHubEmployee = request.IsSupportHubEmployee,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            
            //If the user is being invited (not a SupportHub employee), generate an invitation token and set invitation properties
            InvitationToken = request.IsSupportHubEmployee ? null : Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            InvitationStatus = request.IsSupportHubEmployee ? InvitationStatus.ACCEPTED : InvitationStatus.PENDING,
            InvitationCreatedAt = request.IsSupportHubEmployee ? null : DateTime.UtcNow,
            InvitationExpiresAt = request.IsSupportHubEmployee ? null : DateTime.UtcNow.AddDays(7)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        //Publish UserInvitationCreatedEvent if the user is not a SupportHub employee
        if (!request.IsSupportHubEmployee)
        {
            var invitationEvent = new UserInvitationCreatedEvent(
                OrganizationId: user.OrganizationId!.Value,
                UserId: user.Id,
                UserEmail: user.Email,
                TargetRole: user.Role.ToString(),
                InvitationToken: user.InvitationToken!
            );
            await _publisher.PublishAsync("UserInvitationCreated", invitationEvent, cancellationToken);
        }
        else
        {
            var invitationEvent = new UserInvitationAcceptedEvent(
                OrganizationId: user.OrganizationId!.Value,
                UserId: user.Id,
                UserEmail: user.Email,
                Role: user.Role
            );
            await _publisher.PublishAsync("UserInvitationAccepted", invitationEvent, cancellationToken);
        }

        //Return the assigned ID
        return user.Id;
    }
}