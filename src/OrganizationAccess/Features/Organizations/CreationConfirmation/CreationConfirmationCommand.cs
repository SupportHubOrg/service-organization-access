using FluentValidation;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Organizations.CreationConfirmation;

/// <summary>
/// Command to confirm the creation of a new organization after the organization manager accepts the email invitation.
/// </summary>
public record CreationConfirmationCommand(
    Guid OrganizationId,
    Guid ManagerId,
    string Token
) : IRequest<Guid>;

public class CreationConfirmationValidator : AbstractValidator<CreationConfirmationCommand>
{
    public CreationConfirmationValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.ManagerId)
            .NotEmpty().WithMessage("Manager ID is required.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Creation confirmation token is required.");
    }
}