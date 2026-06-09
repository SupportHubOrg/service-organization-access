using FluentValidation;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Users.InvitationConfirmation;

/// <summary>
/// Command to confirm a user invitation to an organization.
/// </summary>
/// <param name="UserId">ID of the user confirming the invitation.</param>
/// <param name="OrganizationId">ID of the organization the user is being invited to.</param>
/// <param name="Token">Invitation confirmation token sent in the invitation email.</param>
public record InvitationConfirmationCommand(
    Guid UserId,
    Guid KeycloakId,
    string Token
) : IRequest<Guid>;

public class InvitationConfirmationValidator : AbstractValidator<InvitationConfirmationCommand>
{
    public InvitationConfirmationValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Invitation confirmation token is required.");

        RuleFor(x => x.KeycloakId)
            .NotEmpty().WithMessage("Keycloak ID is required. User must be authenticated to confirm invitation.");
    }
}