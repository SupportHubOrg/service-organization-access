using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Infrastructure.Data;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;

namespace SupportHub.OrganizationAccesss.Features.Events;

public record ProcessPaymentConfirmedCommand(PaymentConfirmedPayload Payload) : IRequest;

/// <summary>
/// Handles the processing of a payment confirmation event for an organization.
/// When a payment confirmation is received, the organization's subscription tier and payment processed flag are updated accordingly.
/// </summary>
/// <param name="db"></param>
/// <param name="publisher"></param>
/// <param name="logger"></param>
public class ProcessPaymentConfirmedHandler(
    OrganizationDbContext db, 
    IEventPublisher publisher,
    ILogger<ProcessPaymentConfirmedHandler> logger) : IRequestHandler<ProcessPaymentConfirmedCommand>
{
    private readonly OrganizationDbContext _db = db;
    private readonly IEventPublisher _publisher = publisher;
    private readonly ILogger<ProcessPaymentConfirmedHandler> _logger = logger;

    public async Task Handle(ProcessPaymentConfirmedCommand request, CancellationToken cancellationToken)
    {
        //Get the payload from the command
        PaymentConfirmedPayload payload = request.Payload;

        //Fetch the organization
        Organization? organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == payload.OrganizationId, cancellationToken);
        if (organization == null)
        {
            _logger.LogError("Organization with ID {OrganizationId} not found for payment confirmation processing.", payload.OrganizationId);
            return;
        }

        //Update the organization's subscription status and tier based on the payment confirmation
        organization.Tier = payload.Tier;
        organization.PaymentProcessed = true;
        organization.UpdatedAt = DateTime.UtcNow;

        //Save changes to the database
        await _db.SaveChangesAsync(cancellationToken);

        //Log the successful payment confirmation
        _logger.LogInformation("Payment confirmed for Organization ID {OrganizationId}. Amount: {Amount}, Tier: {Tier}", payload.OrganizationId, payload.Amount, payload.Tier);
    }
}