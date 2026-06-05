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

        //Update the invitation status to accepted
        user.InvitationStatus = InvitationStatus.ACCEPTED;
        user.UpdatedAt = DateTime.UtcNow;
        user.InvitationExpiresAt = null;
        user.InvitationToken = null;

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Publish UserInvitationAccepted event to Kafka
        UserInvitationAcceptedEvent userInvitationAcceptedEvent = new(
            OrganizationId: user.OrganizationId,
            UserId: user.Id,
            UserEmail: user.Email,
            Role: user.Role
        );
        await _publisher.PublishAsync("UserInvitationAccepted", userInvitationAcceptedEvent, cancellationToken);

        //Return the ID of the newly accepted user
        return user.Id;
    }
}