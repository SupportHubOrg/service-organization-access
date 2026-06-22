using MediatR;
using Microsoft.EntityFrameworkCore;
using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Domain.Exceptions;
using SupportHub.OrganizationAccesss.Infrastructure.Data;

namespace SupportHub.OrganizationAccesss.Features.Users.GetUser;

public class GetUserHandler(OrganizationDbContext db) : IRequestHandler<GetUserQuery, UserDetailsResponse>
{
    private readonly OrganizationDbContext _db = db;

    public async Task<UserDetailsResponse> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        //Retrieve the user using AsNoTracking, check if they exist, if not return 404 Not Found
        User? user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null)
            throw new AppException(StatusCodes.Status404NotFound, $"User {request.UserId} could not be found.");

        //Retrieve the requesting user, if they don't exist, return 401 Unauthorized
        User? operatorUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.OperatorId, cancellationToken);
        if (operatorUser == null)
            throw new AppException(StatusCodes.Status401Unauthorized, $"Requesting user {request.OperatorId} could not be found.");

        //Enforce Data Visibility Rules
        bool isSystemAdmin = operatorUser.Role == Role.SUPPORT_HUB_ADMINISTRATOR || operatorUser.Role == Role.SYSTEM;
        bool isSameOrganization = operatorUser.OrganizationId == user.OrganizationId;
        if (!isSystemAdmin && !isSameOrganization)
                throw new AppException(StatusCodes.Status403Forbidden, $"User does not have permission to view user {request.UserId}.");

        //Map the User entity to the UserDetailsResponse based on the role of the requesting user.
        if(isSystemAdmin)
        {
            return new UserDetailsResponse(
                user.Id,
                user.OrganizationId,
                user.Email,
                user.Role,
                user.IsSupportHubEmployee,
                user.InvitationStatus,
                user.InvitationCreatedAt,
                user.InvitationExpiresAt,
                user.InvitationAcceptedAt,
                user.CreatedAt,
                user.UpdatedAt
            );
        }
        else
        {
            return new UserDetailsResponse(
                user.Id,
                user.OrganizationId,
                user.Email,
                user.Role,
                user.IsSupportHubEmployee,
                operatorUser.Role == Role.ORGANIZATION_MANAGER ? user.InvitationStatus : null,
                operatorUser.Role == Role.ORGANIZATION_MANAGER ? user.InvitationCreatedAt : null,
                operatorUser.Role == Role.ORGANIZATION_MANAGER ? user.InvitationExpiresAt : null,
                operatorUser.Role == Role.ORGANIZATION_MANAGER ? user.InvitationAcceptedAt : null,
                user.CreatedAt,
                user.UpdatedAt
            );
        }
    }
}