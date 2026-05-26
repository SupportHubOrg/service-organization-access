using FluentValidation;
using MediatR;

using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Organizations.GetOrganizations;

/// <summary>
/// Query to retrieve a filtered, sorted, and paginated list of Organizations.
/// </summary>
public record GetOrganizationsQuery(
    Guid UserId,
    SubscriptionTier? SubscriptionTier,
    string? SortBy,
    bool SortDescending = true,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PagedResult<OrganizationSummaryResponse>>;

/// <summary>
/// Representation returned in a list of Organizations.
/// </summary>
public record OrganizationSummaryResponse(
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

public class GetOrganizationsValidator : PagedQueryValidatorBase<GetOrganizationsQuery, Organization>
{
    public GetOrganizationsValidator() : base(x => x.PageNumber, x => x.PageSize, x => x.SortBy)
    {
        RuleFor(x => x.SubscriptionTier)
            .IsInEnum()
            .When(x => x.SubscriptionTier.HasValue)
            .WithMessage("SubscriptionTier must be a valid enum value.");
    }
}