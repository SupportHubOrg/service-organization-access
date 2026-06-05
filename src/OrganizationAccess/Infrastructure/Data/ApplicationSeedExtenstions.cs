using Microsoft.EntityFrameworkCore;

using SupportHub.OrganizationAccesss.Domain.Entities;
using SupportHub.OrganizationAccesss.Domain.Enums;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;

namespace SupportHub.OrganizationAccesss.Infrastructure.Data;

public static class SystemIdentityConstants
{
    //Static, deterministic GUID constants for global cross-service actors
    public static readonly Guid SystemUserId = new("a40a3698-e127-4a12-82c1-17ea5f55bfe9");
    public static readonly Guid AiUserId = new("cbf940c7-0580-4a93-8393-e57dfbbaa30f");
    
    public const string SystemEmail = "system-orchestrator@supporthub.internal";
    public const string AiEmail = "ai-triage-worker@supporthub.internal";
}

public static class ApplicationSeedExtensions
{
    /// <summary>
    /// Extension method to seed global platform operators and immediately notify downstream consumers.
    /// </summary>
    public static async Task SeedGlobalIdentitiesAsync(this IHost app)
    {
        //Establish an isolated scope to safely resolve scoped dependencies inside the startup pipeline
        using var scope = app.Services.CreateScope();
        
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var now = DateTime.UtcNow;
            bool dataChanged = false;

            //Check if the global system user exists, if not create a new one
            bool systemUserExists = await db.Users.AnyAsync(u => u.Id == SystemIdentityConstants.SystemUserId);
            if (!systemUserExists)
            {
                var systemUser = new User
                {
                    Id = SystemIdentityConstants.SystemUserId,
                    OrganizationId = null,
                    Email = SystemIdentityConstants.SystemEmail,
                    Role = Role.SYSTEM,
                    IsSupportHubEmployee = true,
                    InvitationStatus = InvitationStatus.ACCEPTED,
                    InvitationAcceptedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                db.Users.Add(systemUser);
                dataChanged = true;
            }

            //Check if the global AI user exists, if not create a new one
            bool aiUserExists = await db.Users.AnyAsync(u => u.Id == SystemIdentityConstants.AiUserId);
            if (!aiUserExists)
            {
                var aiUser = new User
                {
                    Id = SystemIdentityConstants.AiUserId,
                    OrganizationId = null,
                    Email = SystemIdentityConstants.AiEmail,
                    Role = Role.AI,
                    IsSupportHubEmployee = true,
                    InvitationStatus = InvitationStatus.ACCEPTED,
                    InvitationAcceptedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                db.Users.Add(aiUser);
                dataChanged = true;
            }

            //Commit transactions if entities were created
            if (dataChanged)
            {
                await db.SaveChangesAsync();

                // 3. Broadcast Kafka synchronization events immediately following database commit confirmation
                if (!systemUserExists)
                {
                    var systemEvent = new UserInvitationAcceptedEvent(
                        OrganizationId: null, 
                        UserId: SystemIdentityConstants.SystemUserId, 
                        UserEmail: SystemIdentityConstants.SystemEmail, 
                        Role: Role.SYSTEM
                    );
                    await publisher.PublishAsync("UserInvitationAccepted", systemEvent);
                }

                if (!aiUserExists)
                {
                    var aiEvent = new UserInvitationAcceptedEvent(
                        OrganizationId: null, 
                        UserId: SystemIdentityConstants.AiUserId, 
                        UserEmail: SystemIdentityConstants.AiEmail, 
                        Role: Role.AI
                    );
                    await publisher.PublishAsync("UserInvitationAccepted", aiEvent);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "A critical failure occurred while executing the global identity seeding startup pipeline.");
            throw;
        }
    }
}