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
        User? temporaryUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (temporaryUser == null)
            throw new AppException(StatusCodes.Status404NotFound, $"User {request.UserId} not found.");

        //Check if the user has a pending invitation, if they do not have a pending invitation return 400 Bad Request
        if (temporaryUser.InvitationStatus != InvitationStatus.PENDING && temporaryUser.InvitationExpiresAt != null)
            throw new AppException(StatusCodes.Status400BadRequest, $"User {request.UserId} does not have a pending invitation.");

        //Check if the invitation token is valid and not expired, if it is invalid or expired return 400 Bad Request
        if (temporaryUser.InvitationToken != request.Token || temporaryUser.InvitationExpiresAt < DateTime.UtcNow)
            throw new AppException(StatusCodes.Status400BadRequest, $"Invalid or expired invitation token for user {request.UserId}.");

        //Recreate the user using the new proper Id
        User confirmedUser = new()
        {
            Id = request.KeycloakId,
            OrganizationId = temporaryUser.OrganizationId,
            Email = temporaryUser.Email,
            Role = temporaryUser.Role,
            IsSupportHubEmployee = temporaryUser.IsSupportHubEmployee,
            InvitationStatus = InvitationStatus.ACCEPTED,
            InvitationAcceptedAt = DateTime.UtcNow,
            CreatedAt = temporaryUser.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        //Delete the old duplicate user
        _db.Users.Remove(temporaryUser);

        //Add the new user to the database
        _db.Users.Add(confirmedUser);

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Publish UserInvitationAccepted event to Kafka
        UserInvitationAcceptedEvent userInvitationAcceptedEvent = new(
            OrganizationId: confirmedUser.OrganizationId,
            UserId: confirmedUser.Id,
            UserEmail: confirmedUser.Email,
            Role: confirmedUser.Role
        );
        await _publisher.PublishAsync("UserInvitationAccepted", userInvitationAcceptedEvent, cancellationToken);

        //Return the ID of the newly accepted user
        return confirmedUser.Id;
    }
}