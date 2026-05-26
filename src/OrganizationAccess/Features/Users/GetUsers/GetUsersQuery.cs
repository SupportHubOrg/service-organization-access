using FluentValidation;
using MediatR;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Users.GetUsers;

/// <summary>
/// Query to retrieve a filtered, sorted, and paginated list of users.
/// </summary>
public record GetUsersQuery(
    Guid UserId,
    Guid? OrganizationId,
    Role? Role,
    bool? IsSupportHubEmployee,
    DateTime? CreatedAfter,
    DateTime? CreatedBefore,
    DateTime? UpdatedAfter,
    DateTime? UpdatedBefore,
    DateTime? InvitationCreatedAfter,
    DateTime? InvitationCreatedBefore,
    string? SortBy,
    bool SortDescending = true,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PagedResult<UserSummaryResponse>>;

/// <summary>
/// Representation returned in a list of Tickets.
/// </summary>
public record UserSummaryResponse(
    Guid Id,
    Guid? OrganizationId,
    string Email,
    Role Role,
    bool IsSupportHubEmployee,
    InvitationStatus? InvitationStatus,
    DateTime? InvitationCreatedAt,
    DateTime? InvitationExpiresAt,
    DateTime? InvitationAcceptedAt,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

public class GetUsersValidator : PagedQueryValidatorBase<GetUsersQuery, User>
{
    public GetUsersValidator() : base(x => x.PageNumber, x => x.PageSize, x => x.SortBy)
    {
        RuleFor(x => x)
            .Must(x => !x.CreatedAfter.HasValue || !x.CreatedBefore.HasValue || x.CreatedAfter <= x.CreatedBefore)
            .WithMessage("CreatedAfter date cannot be later than CreatedBefore date.");

        RuleFor(x => x)
            .Must(x => !x.UpdatedAfter.HasValue || !x.UpdatedBefore.HasValue || x.UpdatedAfter <= x.UpdatedBefore)
            .WithMessage("UpdatedAfter date cannot be later than UpdatedBefore date.");

        RuleFor(x => x)
            .Must(x => !x.InvitationCreatedAfter.HasValue || !x.InvitationCreatedBefore.HasValue || x.InvitationCreatedAfter <= x.InvitationCreatedBefore)
            .WithMessage("InvitationCreatedAfter date cannot be later than InvitationCreatedBefore date.");

        RuleFor(x => x.Role)
            .IsInEnum().When(x => x.Role.HasValue)
            .WithMessage("Role must be a valid enum value.");

        
    }
}