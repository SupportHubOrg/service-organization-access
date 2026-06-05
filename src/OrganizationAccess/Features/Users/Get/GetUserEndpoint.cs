using System.Security.Claims;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Users.GetUser;

public static class GetUserEndpoint
{
    /// <summary>
    /// Maps the endpoint for retrieving a specific user by ID.
    /// </summary>
    public static void MapGetUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/organizations/users/{userId}", async (
            Guid userId,
            ClaimsPrincipal claimsPrincipal,
            IMediator mediator) =>
        {
            //Extract the user ID from JWT claims
            var userIdString = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid operatorId))
                return Results.Unauthorized();

            //Create and send the query
            var query = new GetUserQuery(userId, operatorId);
            var result = await mediator.Send(query);

            //Return 200 OK with the user details DTO
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}