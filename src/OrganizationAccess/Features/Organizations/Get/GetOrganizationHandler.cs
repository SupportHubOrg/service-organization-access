using MediatR;
using Microsoft.EntityFrameworkCore;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Organizations.GetOrganization;

public class GetOrganizationHandler(OrganizationDbContext db) : IRequestHandler<GetOrganizationQuery, OrganizationDetailsResponse>
{
    private readonly OrganizationDbContext _db = db;

    public async Task<OrganizationDetailsResponse> Handle(GetOrganizationQuery request, CancellationToken cancellationToken)
    {
        //Retrieve the Organization with AsNoTracking for read-optimization
        Organization? organization = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken);
        //Check if the Organization exists and return 404 Not Found if it doesn't
        if (organization == null)
            throw new AppException(StatusCodes.Status404NotFound, $"Organization {request.OrganizationId} could not be found.");

        //Retrieve the user requesting the Organization, return 401 Unauthorized if the user doesn't exist
        User? user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null)
            throw new AppException(StatusCodes.Status401Unauthorized, "User could not be found.");

        //Enforce Data Visibility Rules
        bool isSystemAdmin = user.Role == Role.SUPPORT_HUB_ADMINISTRATOR || user.Role == Role.SYSTEM;
        bool isSameOrganization = user.OrganizationId == organization.Id;
        bool isEmployee = user.Role == Role.EMPLOYEE && isSameOrganization;
        if (!isSystemAdmin && !isSameOrganization)
            throw new AppException(StatusCodes.Status403Forbidden, "You do not have permission to view this Organization.");

        //If the user is an employee return limited information about the Organization
        OrganizationDetailsResponse response;
        if (isEmployee)
        {
            response = new OrganizationDetailsResponse(
                organization.Id,
                organization.Name,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
        }
        else if(!isSystemAdmin)
        {
            response = new OrganizationDetailsResponse(
                organization.Id,
                organization.Name,
                organization.Tier,
                organization.PaymentProcessed,
                organization.SlaFirstResponseMinutes,
                organization.SlaHumanResponseMinutes,
                organization.SlaResolutionMinutes,
                organization.AiTrustThreshold,
                organization.CreationConfirmationStatus,
                null,
                organization.ConfirmedAt,
                organization.CreatedAt,
                organization.UpdatedAt
            );
        }
        else
        {
            response = new OrganizationDetailsResponse(
                organization.Id,
                organization.Name,
                organization.Tier,
                organization.PaymentProcessed,
                organization.SlaFirstResponseMinutes,
                organization.SlaHumanResponseMinutes,
                organization.SlaResolutionMinutes,
                organization.AiTrustThreshold,
                organization.CreationConfirmationStatus,
                organization.CreationTokenExpiresAt,
                organization.ConfirmedAt,
                organization.CreatedAt,
                organization.UpdatedAt
            );
        }

        //Return the Organization details
        return response;
    }
}