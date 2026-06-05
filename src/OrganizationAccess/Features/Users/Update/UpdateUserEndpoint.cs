using System.Security.Claims;
using MediatR;

using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Users.UpdateUser;

public static class UpdateUserEndpoint
{
    /// <summary>
    /// Maps the endpoint for updating a user's status and assigned operator.
    /// </summary>
    /// <param name="app"></param>
    public static void MapUpdateUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/organizations/users/{userId}", async (
            Guid userId,
            UpdateUserRequest request,
            ClaimsPrincipal user,
            IMediator mediator ) =>
        {
            //Extract user ID from JWT claims
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid operatorId))
                return Results.Unauthorized();

            //Create and send the command to update the user
            var command = new UpdateUserCommand(
                OperatorId: operatorId,
                UserId: userId,
                Email: request.Email,
                TargetRole: request.TargetRole
            );
            await mediator.Send(command);

            //Return success response
            return Results.NoContent();
        }).RequireAuthorization();
    }
}

public record UpdateUserRequest(
    string? Email,
    Role? TargetRole
);