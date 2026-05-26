using System.ComponentModel.DataAnnotations;
using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Domain.Entities
{
    /// <summary>
    /// Represents a user that belongs to an organization.
    /// The User entity also contains properties related to user invitations.
    /// </summary>
    public class User
    {
        [Key]
        public Guid Id { get; set; } = default!;

        public Guid? OrganizationId { get; set; } = default!;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = default!;
        
        [Required]
        [EnumDataType(typeof(Role))]
        public Role Role { get; set; } = default!;

        [Required]
        public bool IsSupportHubEmployee { get; set; } = false;
    
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        //Email invitation properties
        [StringLength(500)]
        public string? InvitationToken { get; set; }

        [Required]
        public InvitationStatus InvitationStatus { get; set; } = InvitationStatus.PENDING;

        public DateTime? InvitationCreatedAt { get; set; }
        public DateTime? InvitationExpiresAt { get; set; }
        public DateTime? InvitationAcceptedAt { get; set; }


        public Organization Organization { get; set; } = default!;
    }
}