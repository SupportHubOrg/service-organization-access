using System.Security.Claims;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Organizations.DeleteOrganization;

public static class DeleteOrganizationEndpoint
{
    /// <summary>
    /// Maps the endpoint for deleting an organization.
    /// </summary>
    public static void MapDeleteOrganizationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/organizations/{organizationId}", async (
            Guid organizationId,
            ClaimsPrincipal user,
            IMediator mediator) =>
        {
            //Extract user ID from JWT claims
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            //Create and send the command to delete the organization
            var command = new DeleteOrganizationCommand(organizationId, userId);
            await mediator.Send(command);

            //Return success response
            return Results.NoContent();
        }).RequireAuthorization();
    }
}