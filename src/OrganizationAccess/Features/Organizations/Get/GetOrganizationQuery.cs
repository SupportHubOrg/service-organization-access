using FluentValidation;
using MediatR;

using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Organizations.GetOrganization;

/// <summary>
/// Query to retrieve specific organization's details.
/// </summary>
/// <param name="OrganizationId">The ID of the Organization to retrieve.</param>
/// <param name="UserId">The ID of the user requesting the Organization, used for authorization.</param>
public record GetOrganizationQuery(
    Guid OrganizationId,
    Guid UserId
) : IRequest<OrganizationDetailsResponse>;

/// <summary>
/// The read-model representation of a Organization returned to the API client.
/// </summary>
public record OrganizationDetailsResponse(
    Guid Id,
    string Name,
    SubscriptionTier? Tier,
    bool? PaymentProcessed,
    decimal? SlaFirstResponseMinutes,
    decimal? AiTrustThreshold,
    decimal? SlaResolutionMinutes,
    decimal? SlaHumanResponseMinutes,
    InvitationStatus? CreationConfirmationStatus,
    DateTime? CreationTokenExpiresAt,
    DateTime? ConfirmedAt,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

public class GetOrganizationValidator : AbstractValidator<GetOrganizationQuery>
{
    public GetOrganizationValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");
    }
}