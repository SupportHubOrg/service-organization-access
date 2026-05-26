using FluentValidation;
using MediatR;

using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Organizations.CreateOrganization;

/// <summary>
/// Command to create a new organization.
/// </summary>
/// <param name="Name">The name of the organization.</param>
/// <param name="Tier">The subscription tier of the organization.</param>
/// <param name="ManagerEmail">The email of the organization manager, who will be invited to join the organization with the role of Organization Manager.</param>
/// <param name="SlaFirstResponseMinutes">The SLA first AI response time in minutes for the organization.</param>
/// <param name="SlaHumanResponseMinutes">The SLA human response time in minutes for the organization.</param>
/// <param name="SlaResolutionMinutes">The SLA resolution time in minutes for the organization.</param>
/// <param name="AiTrustThreshold">The AI trust threshold for the organization, which determines when to escalate to a human operator.</param>
public record CreateOrganizationCommand(
    string Name,
    SubscriptionTier Tier,
    string ManagerEmail,
    decimal? SlaFirstResponseMinutes,
    decimal? SlaHumanResponseMinutes,
    decimal? SlaResolutionMinutes,
    decimal? AiTrustThreshold
) : IRequest<Guid>;

public class CreateOrganizationValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .Length(2, 255).WithMessage("Organization name must be between 2 and 255 characters.");
        
        RuleFor(x => x.Tier)
            .IsInEnum().WithMessage("A valid subscription tier must be specified.");

        RuleFor(x => x.ManagerEmail)
            .NotEmpty().WithMessage("Manager email is required.")
            .EmailAddress().WithMessage("Manager email must be a valid email address.")
            .MaximumLength(255).WithMessage("Manager email cannot exceed 255 characters.");

        RuleFor(x => x.SlaFirstResponseMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SlaFirstResponseMinutes.HasValue)
            .WithMessage("SLA first response minutes must be a non-negative value.");

        RuleFor(x => x.SlaHumanResponseMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SlaHumanResponseMinutes.HasValue)
            .WithMessage("SLA human response minutes must be a non-negative value.");

        RuleFor(x => x.SlaResolutionMinutes)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SlaResolutionMinutes.HasValue)
            .WithMessage("SLA resolution minutes must be a non-negative value.");

        RuleFor(x => x.AiTrustThreshold)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AiTrustThreshold.HasValue)
            .WithMessage("AI trust threshold must be a non-negative value.");
    }
}