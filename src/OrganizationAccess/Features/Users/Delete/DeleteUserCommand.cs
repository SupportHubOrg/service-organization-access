using FluentValidation;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Users.DeleteUser;

/// <summary>
/// Command to permanently delete a user.
/// </summary>
/// <param name="UserId">ID of the user to delete.</param>
/// <param name="OperatorId">ID of the user attempting to perform the deletion.</param>
public record DeleteUserCommand(
    Guid UserId, 
    Guid OperatorId
) : IRequest<bool>;

public class DeleteUserValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}