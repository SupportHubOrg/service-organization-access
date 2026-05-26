using FluentValidation;
using MediatR;
using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Users.GetUser;

/// <summary>
/// Query to retrieve specific user's details.
/// </summary>
/// <param name="UserId">The ID of the user to retrieve.</param>
/// <param name="OperatorId">The ID of the user requesting the user.</param>
public record GetUserQuery(
    Guid UserId,
    Guid OperatorId
) : IRequest<UserDetailsResponse>;

/// <summary>
/// Representation of a user returned to the API client.
/// </summary>
public record UserDetailsResponse(
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

public class GetUserValidator : AbstractValidator<GetUserQuery>
{
    public GetUserValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}