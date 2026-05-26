using System.Security.Claims;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Organizations.GetOrganization;

public static class GetOrganizationEndpoint
{
    /// <summary>
    /// Maps the endpoint for retrieving a specific Organization by ID.
    /// </summary>
    public static void MapGetOrganizationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/organizations/{organizationId}", async (
            Guid organizationId,
            ClaimsPrincipal claimsPrincipal,
            IMediator mediator) =>
        {
            //Extract the user ID from JWT claims
            var userIdString = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            //Create and send the query
            var query = new GetOrganizationQuery(organizationId, userId);
            var result = await mediator.Send(query);

            //Return 200 OK with the Organization details DTO
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}