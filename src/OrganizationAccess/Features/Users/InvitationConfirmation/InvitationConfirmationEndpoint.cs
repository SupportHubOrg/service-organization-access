using System.Security.Claims;
using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Users.InvitationConfirmation;

public static class InvitationConfirmationEndpoint
{
    /// <summary>
    /// Maps the endpoint for confirming the creation of a new organization after the organization manager accepts the email invitation.
    /// </summary>
    /// <param name="app"></param>
    public static void MapInvitationConfirmationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/organizations/users/{userId}/invitation-confirmation/{token}", async (
            Guid userId,
            string token,
            ClaimsPrincipal claimsPrincipal,
            IMediator mediator) =>
        {
            //Extract user ID from JWT claims (this is the actual Keycloak ID)
            var keycloakIdString = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(keycloakIdString, out Guid keycloakId))
                return Results.Unauthorized();

            //Create and send the command
            var command = new InvitationConfirmationCommand(
                UserId: userId,
                KeycloakId: keycloakId,
                Token: token
            );
            await mediator.Send(command);

            //Return 204 No Content if successful
            return Results.NoContent();
        }).RequireAuthorization();
    }
}