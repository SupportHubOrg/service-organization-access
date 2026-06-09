using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Users.InvitationConfirmation;

/// <summary>
/// Handles the creation of a new organization.
/// </summary>
/// <param name="db"></param>
/// <param name="publisher"></param>
/// <param name="logger"></param>
public class InvitationConfirmationHandler(OrganizationDbContext db, IEventPublisher publisher) : IRequestHandler<InvitationConfirmationCommand, Guid>
{
    private readonly OrganizationDbContext _db = db;
    private readonly IEventPublisher _publisher = publisher;

    public async Task<Guid> Handle(InvitationConfirmationCommand request, CancellationToken cancellationToken)
    {
        //Check if the user exists in the database, if they do not exist return 404 Not Found
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null)
            throw new AppException(StatusCodes.Status404NotFound, $"User {request.UserId} not found.");

        //Check if the user has a pending invitation, if they do not have a pending invitation return 400 Bad Request
        if (user.InvitationStatus != InvitationStatus.PENDING && user.InvitationExpiresAt != null)
            throw new AppException(StatusCodes.Status400BadRequest, $"User {request.UserId} does not have a pending invitation.");

        //Check if the invitation token is valid and not expired, if it is invalid or expired return 400 Bad Request
        if (user.InvitationToken != request.Token || user.InvitationExpiresAt < DateTime.UtcNow)
            throw new AppException(StatusCodes.Status400BadRequest, $"Invalid or expired invitation token for user {request.UserId}.");

        //Check if the Keycloak ID already exists
        if (await _db.Users.AnyAsync(u => u.Id == request.KeycloakId, cancellationToken))
            throw new AppException(StatusCodes.Status400BadRequest, "User account is already linked.");

        //Remove the temporary user record
        _db.Users.Remove(user);

        //Create the finalized user record with the actual Keycloak ID
        var finalizedUser = new User
        {
            Id = request.KeycloakId,
            OrganizationId = user.OrganizationId,
            Email = user.Email,
            Role = user.Role,
            IsSupportHubEmployee = user.IsSupportHubEmployee,
            CreatedAt = user.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            InvitationStatus = InvitationStatus.ACCEPTED,
            InvitationAcceptedAt = DateTime.UtcNow,
            InvitationToken = null,
            InvitationCreatedAt = user.InvitationCreatedAt,
            InvitationExpiresAt = null
        };
        _db.Users.Add(finalizedUser);

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Publish UserInvitationAccepted event to Kafka
        UserInvitationAcceptedEvent userInvitationAcceptedEvent = new(
            OrganizationId: finalizedUser.OrganizationId,
            UserId: finalizedUser.Id,
            UserEmail: finalizedUser.Email,
            Role: finalizedUser.Role
        );
        await _publisher.PublishAsync("UserInvitationAccepted", userInvitationAcceptedEvent, cancellationToken);

        //Return the new true ID of the accepted user
        return finalizedUser.Id;
    }
}