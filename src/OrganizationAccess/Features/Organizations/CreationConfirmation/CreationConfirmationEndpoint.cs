using MediatR;

namespace SupportHub.OrganizationAccesss.Features.Organizations.CreationConfirmation;

public static class CreationConfirmationEndpoint
{
    /// <summary>
    /// Maps the endpoint for confirming the creation of a new organization after the organization manager accepts the email invitation.
    /// </summary>
    /// <param name="app"></param>
    public static void MapCreationConfirmationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/organizations/{organizationId}/creation-confirmation/{token}", async (
            Guid organizationId,
            string token,
            CreationConfirmationRequest request,
            IMediator mediator) =>
        {
            //Create and send the command
            var command = new CreationConfirmationCommand(
                OrganizationId: organizationId,
                ManagerId: request.ManagerId,
                Token: token
            );
            await mediator.Send(command);

            //Return 204 No Content if successful
            return Results.NoContent();
        });
    }
}

public record CreationConfirmationRequest(
    Guid ManagerId
);

public record CreationConfirmationResult(Guid OrganizationId);