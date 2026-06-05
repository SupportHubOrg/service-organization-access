using System.Security.Claims;
using MediatR;

using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Users.GetUsers;

public static class GetUsersEndpoint
{
    /// <summary>
    /// Maps the endpoint for retrieving a collection of users based on provided filters.
    /// </summary>
    public static void MapGetUsersEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/organizations/users", async (
            [AsParameters] GetUsersRequest parameters,
            ClaimsPrincipal claimsPrincipal,
            IMediator mediator) =>
        {
            //Extract user ID from JWT claims
            var userIdString = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            //Construct the query with the provided filters and pagination parameters
            var query = new GetUsersQuery(
                UserId: userId,
                OrganizationId: parameters.OrganizationId,
                Role: parameters.Role,
                IsSupportHubEmployee: parameters.IsSupportHubEmployee,
                CreatedAfter: parameters.CreatedAfter,
                CreatedBefore: parameters.CreatedBefore,
                UpdatedAfter: parameters.UpdatedAfter,
                UpdatedBefore: parameters.UpdatedBefore,
                InvitationCreatedAfter: parameters.InvitationCreatedAfter,
                InvitationCreatedBefore: parameters.InvitationCreatedBefore,
                SortBy: parameters.SortBy,
                SortDescending: parameters.SortDescending ?? true,
                PageNumber: parameters.PageNumber ?? 1,
                PageSize: parameters.PageSize ?? 20
            );

            //Send the query to retrieve the list of users
            var result = await mediator.Send(query);

            //Return 200 OK with the list of user summaries and pagination metadata
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}

/// <summary>
/// HTTP request model bound from the query string.
/// </summary>
public record GetUsersRequest(
    Guid? OrganizationId,
    Role? Role,
    bool? IsSupportHubEmployee,
    DateTime? CreatedAfter,
    DateTime? CreatedBefore,
    DateTime? UpdatedAfter,
    DateTime? UpdatedBefore,
    DateTime? InvitationCreatedAfter,
    DateTime? InvitationCreatedBefore,
    string? SortBy,
    bool? SortDescending,
    int? PageNumber,
    int? PageSize
);