using MediatR;
using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Organizations.GetOrganizations;

public class GetOrganizationsHandler(OrganizationDbContext db) : IRequestHandler<GetOrganizationsQuery, PagedResult<OrganizationSummaryResponse>>
{
    private readonly OrganizationDbContext _db = db;

    public async Task<PagedResult<OrganizationSummaryResponse>> Handle(GetOrganizationsQuery request, CancellationToken cancellationToken)
    {
        //Retrieve the requesting user and check if they exist, otherwise return 401 Unauthorized
        var requestingUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (requestingUser == null)
            throw new AppException(StatusCodes.Status401Unauthorized, "User could not be found.");

        //Initialize the base query
        var query = _db.Organizations.AsNoTracking().AsQueryable();

        //Enforce Data Visibility Rules
        bool isSystemAdmin = requestingUser.Role == Role.SUPPORT_HUB_ADMINISTRATOR || requestingUser.Role == Role.SYSTEM;
        if (!isSystemAdmin)
            throw new AppException(StatusCodes.Status403Forbidden, "Only system administrators can access this resource.");

        //Apply User-Requested Filters
        if (request.SubscriptionTier.HasValue)
            query = query.Where(o => o.Tier == request.SubscriptionTier.Value);

        //Apply Sorting
        query = query.ApplySort(request.SortBy, request.SortDescending);

        //Apply Pagination
        int totalCount = await query.CountAsync(cancellationToken);

        var Organizations = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new OrganizationSummaryResponse(
                t.Id,
                t.Name,
                t.Tier,
                t.PaymentProcessed,
                t.SlaFirstResponseMinutes,
                t.AiTrustThreshold,
                t.SlaResolutionMinutes,
                t.SlaHumanResponseMinutes,
                t.CreationConfirmationStatus,
                t.CreationTokenExpiresAt,
                t.ConfirmedAt,
                t.CreatedAt,
                t.UpdatedAt
            ))
            .ToListAsync(cancellationToken);

        //Return the paginated list of organization summaries along with pagination metadata
        return new PagedResult<OrganizationSummaryResponse>(
            Organizations,
            totalCount,
            request.PageNumber,
            request.PageSize
        );
    }
}