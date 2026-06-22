using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Organizations.CreateOrganization;

/// <summary>
/// Handles the creation of a new organization.
/// </summary>
/// <param name="db"></param>
/// <param name="publisher"></param>
/// <param name="logger"></param>
public class CreateOrganizationHandler(OrganizationDbContext db, IEventPublisher publisher) : IRequestHandler<CreateOrganizationCommand, Guid>
{
    private readonly OrganizationDbContext _db = db;
    private readonly IEventPublisher _publisher = publisher;

    public async Task<Guid> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        //Verify that the incoming user identity record is not already registered
        bool emailExists = await _db.Users
            .AnyAsync(u => u.Email == request.ManagerEmail, cancellationToken);
        if (emailExists)
            throw new AppException(StatusCodes.Status400BadRequest, $"User profile with email address {request.ManagerEmail} already exists.");

        //Check if organization name already exists
        bool organizationNameExists = await _db.Organizations
            .AnyAsync(o => o.Name == request.Name, cancellationToken);
        if (organizationNameExists)
            throw new AppException(StatusCodes.Status400BadRequest, $"Organization with the name {request.Name} already exists.");

        //Create the new organization entity
        Organization organization = new()
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Tier = request.Tier,
            PaymentProcessed = false,
            SlaFirstResponseMinutes = request.SlaFirstResponseMinutes ?? 0.0m,
            SlaHumanResponseMinutes = request.SlaHumanResponseMinutes ?? 0.0m,
            SlaResolutionMinutes = request.SlaResolutionMinutes ?? 0.0m,
            AiTrustThreshold = request.AiTrustThreshold ?? 0.0m,
            CreationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            CreationTokenExpiresAt = DateTime.UtcNow.AddDays(7),
            CreationConfirmationStatus = InvitationStatus.PENDING,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Organizations.Add(organization);

        //Create the organization manager user entity
        User manager = new()
        {
            Id = request.ManagerKeycloakId ?? Guid.NewGuid(),
            Email = request.ManagerEmail,
            Role = Role.ORGANIZATION_MANAGER,
            OrganizationId = organization.Id,
            InvitationStatus = InvitationStatus.ACCEPTED,
            InvitationAcceptedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(manager);

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Publish OrganizationRegisteredEvent to Kafka
        OrganizationRegisteredEvent organizationRegisteredEvent = new(organization.Id, organization.Name, organization.Tier, manager.Id, manager.Email, organization.CreationToken);
        await _publisher.PublishAsync("OrganizationRegistered", organizationRegisteredEvent, cancellationToken);

        //Return the new organization ID
        return organization.Id;
    }
}