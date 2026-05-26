using FluentValidation;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Organizations.UpdateOrganization;

/// <summary>
/// Allows updating an organization's status and/or assigned operator.
/// </summary>
/// <param name="OrganizationId">ID of the organization to update.</param>
/// <param name="UserId">ID of the user performing the update.</param>
public record UpdateOrganizationCommand(
    Guid UserId,
    Guid OrganizationId,
    string? Name,
    decimal? SlaFirstResponseMinutes,
    decimal? SlaHumanResponseMinutes,
    decimal? SlaResolutionMinutes,
    decimal? AiTrustThreshold
) : IRequest<bool>;

public class UpdateOrganizationValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Tag ID is required.");
            
        RuleFor(x => x.Name)
            .MaximumLength(255).WithMessage("Organization name cannot exceed 255 characters.")
            .MinimumLength(2).WithMessage("Organization name must be at least 2 characters long.")
            .When(x => x.Name != null);

        RuleFor(x => x.SlaFirstResponseMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("SLA first response minutes must be a non-negative value.")
            .When(x => x.SlaFirstResponseMinutes.HasValue);

        RuleFor(x => x.SlaHumanResponseMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("SLA human response minutes must be a non-negative value.")
            .When(x => x.SlaHumanResponseMinutes.HasValue);
        
        RuleFor(x => x.SlaResolutionMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("SLA resolution minutes must be a non-negative value.")
            .When(x => x.SlaResolutionMinutes.HasValue);

        RuleFor(x => x.AiTrustThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("AI trust threshold must be a non-negative value.")
            .When(x => x.AiTrustThreshold.HasValue);
    }
}