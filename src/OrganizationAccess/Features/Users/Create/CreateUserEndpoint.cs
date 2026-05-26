using System.Security.Claims;
using MediatR;

using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Users.CreateUser;

public static class CreateUserEndpoint
{
    /// <summary>
    /// Maps the endpoint for creating a new user.
    /// </summary>
    /// <param name="app"></param>
    public static void MapCreateUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users", async (
            CreateUserRequest request,
            ClaimsPrincipal user,
            IMediator mediator) =>
        {
            //Extract user ID from JWT claims
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid creatorId))
                return Results.Unauthorized();

            //Create and send the command
            var command = new CreateUserCommand(
                CreatorId: creatorId,
                Email: request.Email,
                Role: request.Role,
                OrganizationId: request.OrganizationId,
                IsSupportHubEmployee: request.IsSupportHubEmployee
            );
            var userId = await mediator.Send(command);

            //Return 201 Created with the new resource route
            return Results.Created($"/api/users/{userId}", new CreateUserResult(userId));
        }).RequireAuthorization();
    }
}

public record CreateUserRequest(
    string Email,
    Role Role,
    Guid? OrganizationId,
    bool IsSupportHubEmployee = false
);
public record CreateUserResult(Guid UserId);