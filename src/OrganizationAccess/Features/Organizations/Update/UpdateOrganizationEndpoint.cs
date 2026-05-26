using System.Security.Claims;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Organizations.UpdateOrganization;

public static class UpdateOrganizationEndpoint
{
    /// <summary>
    /// Maps the endpoint for updating an organization's status and assigned operator.
    /// </summary>
    /// <param name="app"></param>
    public static void MapUpdateOrganizationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/organizations/{organizationId}", async (
            Guid organizationId,
            UpdateOrganizationRequest request,
            ClaimsPrincipal user,
            IMediator mediator ) =>
        {
            //Extract user ID from JWT claims
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            //Create and send the command to update the organization
            var command = new UpdateOrganizationCommand(
                UserId: userId,
                OrganizationId: organizationId,
                Name: request.Name,
                SlaFirstResponseMinutes: request.SlaFirstResponseMinutes,
                SlaHumanResponseMinutes: request.SlaHumanResponseMinutes,
                SlaResolutionMinutes: request.SlaResolutionMinutes,
                AiTrustThreshold: request.AiTrustThreshold
            );
            await mediator.Send(command);

            //Return success response
            return Results.NoContent();
        }).RequireAuthorization();
    }
}

public record UpdateOrganizationRequest(
    string? Name,
    decimal? SlaFirstResponseMinutes,
    decimal? SlaHumanResponseMinutes,
    decimal? SlaResolutionMinutes,
    decimal? AiTrustThreshold
);