using FluentValidation;
using MediatR;

using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Users.CreateUser;

/// <summary>
/// Command to create a new support ticket.
/// </summary>
/// <param name="CreatorId">ID of the user creating the new user.</param>
/// <param name="KeycloakId">Keycloak ID of the new user.</param>
/// <param name="Email">Email of the new user.</param>
/// <param name="Role">Role of the new user.</param>
/// <param name="OrganizationId">Organization ID the new user belongs to. Not required if the user is a SupportHub employee.</param>
/// <param name="IsSupportHubEmployee">Indicates if the new user is a SupportHub employee. SupportHub employees do not require an OrganizationId.</param>
public record CreateUserCommand(
    Guid CreatorId,
    Guid? KeycloakId,
    string Email,
    Role Role,
    Guid? OrganizationId,
    bool IsSupportHubEmployee = false
) : IRequest<Guid>;

public class CreateTicketValidator : AbstractValidator<CreateUserCommand>
{
    public CreateTicketValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Role must be a valid enum value.");

        RuleFor(x => x.OrganizationId)
            .NotEmpty().When(x => !x.IsSupportHubEmployee).WithMessage("OrganizationId is required for non-SupportHub employees.");

        RuleFor(x => x)
            .Must(x => x.OrganizationId.HasValue || x.IsSupportHubEmployee)
            .WithMessage("OrganizationId must be provided if the user is not a SupportHub employee.");
    }
}