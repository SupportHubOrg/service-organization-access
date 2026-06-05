using System.Security.Claims;
using MediatR;

using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Organizations.CreateOrganization;

public static class CreateOrganizationEndpoint
{
    /// <summary>
    /// Maps the endpoint for creating a new Organization.
    /// </summary>
    /// <param name="app"></param>
    public static void MapCreateOrganizationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/organizations", async (
            CreateOrganizationRequest request,
            ClaimsPrincipal user,
            IMediator mediator) =>
        {
            //Create and send the command
            var command = new CreateOrganizationCommand(
                request.Name,
                request.Tier,
                request.ManagerKeycloakId,
                request.ManagerEmail,
                request.SlaFirstResponseMinutes,
                request.SlaHumanResponseMinutes,
                request.SlaResolutionMinutes,
                request.AiTrustThreshold
            );
            var organizationId = await mediator.Send(command);

            //Return 201 Created with the new resource route
            return Results.Created($"/api/organizations/{organizationId}", new CreateOrganizationResult(organizationId));
        });
    }
}

public record CreateOrganizationRequest(
    string Name,
    SubscriptionTier Tier,
    Guid ManagerKeycloakId,
    string ManagerEmail,
    decimal? SlaFirstResponseMinutes,
    decimal? SlaHumanResponseMinutes,
    decimal? SlaResolutionMinutes,
    decimal? AiTrustThreshold
);

public record CreateOrganizationResult(Guid OrganizationId);