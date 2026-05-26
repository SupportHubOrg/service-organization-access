using System.Security.Claims;
using MediatR;
using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Features.Organizations.GetOrganizations;

public static class GetOrganizationsEndpoint
{
    /// <summary>
    /// Maps the endpoint for retrieving a collection of Organizations based on provided filters.
    /// </summary>
    public static void MapGetOrganizationsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/organizations", async (
            [AsParameters] GetOrganizationsRequest parameters,
            ClaimsPrincipal claimsPrincipal,
            IMediator mediator) =>
        {
            //Extract user ID from JWT claims
            var userIdString = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            //Construct the query with the provided filters and pagination parameters
            var query = new GetOrganizationsQuery(
                UserId: userId,
                SubscriptionTier: parameters.SubscriptionTier,
                SortBy: parameters.SortBy,
                SortDescending: parameters.SortDescending ?? true,
                PageNumber: parameters.PageNumber ?? 1,
                PageSize: parameters.PageSize ?? 20
            );

            //Send the query to retrieve the list of Tags
            var result = await mediator.Send(query);

            //Return 200 OK with the list of Tag summaries and pagination metadata
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}

/// <summary>
/// HTTP request model bound from the query string.
/// </summary>
public record GetOrganizationsRequest(
    SubscriptionTier? SubscriptionTier,
    string? SortBy,
    bool? SortDescending,
    int? PageNumber,
    int? PageSize
);