using FluentValidation;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Organizations.DeleteOrganization;

/// <summary>
/// Command to permanently delete a tag.
/// </summary>
/// <param name="OrganizationId">ID of the Organization to be deleted.</param>
/// <param name="UserId">ID of the user attempting to perform the deletion.</param>
public record DeleteOrganizationCommand(
    Guid OrganizationId,
    Guid UserId
) : IRequest<bool>;

public class DeleteOrganizationValidator : AbstractValidator<DeleteOrganizationCommand>
{
    public DeleteOrganizationValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");
    }
}