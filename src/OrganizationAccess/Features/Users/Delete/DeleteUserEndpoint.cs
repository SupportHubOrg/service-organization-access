using System.Security.Claims;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Users.DeleteUser;

public static class DeleteUserEndpoint
{
    /// <summary>
    /// Maps the endpoint for deleting a user.
    /// </summary>
    public static void MapDeleteUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/users/{userId}", async (
            Guid userId,
            ClaimsPrincipal user,
            IMediator mediator) =>
        {
            //Extract user ID from JWT claims
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid operatorId))
                return Results.Unauthorized();

            //Create and send the command to delete the user
            var command = new DeleteUserCommand(userId, operatorId);
            await mediator.Send(command);

            //Return success response
            return Results.NoContent();
        }).RequireAuthorization();
    }
}