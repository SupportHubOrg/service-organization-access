using FluentValidation;
using MediatR;

using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Users.UpdateUser;

/// <summary>
/// Allows updating a user's status and/or assigned operator.
/// </summary>
/// <param name="UserId">ID of the user to update.</param>
/// <param name="OperatorId">ID of the user performing the update.</param>
/// <param name="Email">Email of the user to update.</param>
/// <param name="TargetRole">The new role to assign to the user.</param>
public record UpdateUserCommand(
    Guid OperatorId,
    Guid UserId,
    string? Email,
    Role? TargetRole
) : IRequest<bool>;

public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email)).WithMessage("Email must be a valid email address.")
            .MaximumLength(255).WithMessage("Email must be at most 255 characters long.")
            .MinimumLength(5).WithMessage("Email must be at least 5 characters long.");

        RuleFor(x => x.TargetRole)
            .IsInEnum().When(x => x.TargetRole.HasValue)
            .WithMessage("Role must be a valid enum value.");
    }
}