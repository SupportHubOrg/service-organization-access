using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Users.GetUsers;

public class GetUsersHandler(OrganizationDbContext db) : IRequestHandler<GetUsersQuery, PagedResult<UserSummaryResponse>>
{
    private readonly OrganizationDbContext _db = db;

    public async Task<PagedResult<UserSummaryResponse>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        //Retrieve the requesting user and check if they exist, otherwise return 401 Unauthorized
        var requestingUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (requestingUser == null)
            throw new AppException(StatusCodes.Status401Unauthorized, "User could not be found.");

        //Initialize the base query
        var query = _db.Users.AsNoTracking().AsQueryable();

        //Enforce Data Visibility Rules
        bool isSystemAdmin = requestingUser.Role == Role.SUPPORT_HUB_ADMINISTRATOR || requestingUser.Role == Role.SYSTEM;
        if (!isSystemAdmin) //Lock all non-system admins to their own organization
        {
            query = query.Where(u => u.OrganizationId == requestingUser.OrganizationId);
        }
        else //System admins have access to additional filters
        {
            if (request.OrganizationId.HasValue)
                query = query.Where(u => u.OrganizationId == request.OrganizationId.Value);

            if(request.InvitationCreatedAfter.HasValue)
                query = query.Where(u => u.InvitationCreatedAt >= request.InvitationCreatedAfter.Value);

            if(request.InvitationCreatedBefore.HasValue)
                query = query.Where(u => u.InvitationCreatedAt <= request.InvitationCreatedBefore.Value);

            if(request.IsSupportHubEmployee.HasValue)
                query = query.Where(u => u.IsSupportHubEmployee == request.IsSupportHubEmployee.Value);
        }

        //Apply User-Requested Filters
        if (request.Role.HasValue)
            query = query.Where(u => u.Role == request.Role.Value);

        if (request.CreatedAfter.HasValue)
            query = query.Where(u => u.CreatedAt >= request.CreatedAfter.Value);

        if (request.CreatedBefore.HasValue)
            query = query.Where(u => u.CreatedAt <= request.CreatedBefore.Value);

        if (request.UpdatedAfter.HasValue)
            query = query.Where(u => u.UpdatedAt >= request.UpdatedAfter.Value);
        
        if (request.UpdatedBefore.HasValue)
            query = query.Where(u => u.UpdatedAt <= request.UpdatedBefore.Value);

        //Apply Sorting
        query = query.ApplySort(request.SortBy, request.SortDescending);

        //Apply Pagination
        int totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserSummaryResponse(
                u.Id,
                u.OrganizationId,
                u.Email,
                u.Role,
                u.IsSupportHubEmployee,
                (isSystemAdmin || requestingUser.Role == Role.ORGANIZATION_MANAGER) ? u.InvitationStatus : null,
                (isSystemAdmin || requestingUser.Role == Role.ORGANIZATION_MANAGER) ? u.InvitationCreatedAt : null,
                (isSystemAdmin || requestingUser.Role == Role.ORGANIZATION_MANAGER) ? u.InvitationExpiresAt : null,
                (isSystemAdmin || requestingUser.Role == Role.ORGANIZATION_MANAGER) ? u.InvitationAcceptedAt : null,
                u.CreatedAt,
                u.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        //Return the paginated list of user summaries along with pagination metadata
        return new PagedResult<UserSummaryResponse>(
            users,
            totalCount,
            request.PageNumber,
            request.PageSize
        );
    }
}