using System.ComponentModel.DataAnnotations;

using SupportHub.OrganizationAccesss.Domain.Enums;

namespace SupportHub.OrganizationAccesss.Domain.Entities
{
    /// <summary>
    /// Represents an organization that can have multiple users with different roles and access levels.
    /// The organization entity also contains SLA information for AI and human operator management.
    /// </summary>
    public class Organization
    {
        [Key]
        public Guid Id { get; set; } = default!;

        [Required]
        [StringLength(255, ErrorMessage = "Organization name cannot exceed 255 characters.")]
        [MinLength(2, ErrorMessage = "Organization name must be at least 1 characters long.")]
        public string Name { get; set; } = default!;

        [Required]
        [EnumDataType(typeof(SubscriptionTier), ErrorMessage = "Invalid subscription tier.")]
        public SubscriptionTier Tier { get; set; } = default!;

        [Required]
        public bool PaymentProcessed { get; set; } = false;
        
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "SLA first response minutes must be a non-negative value.")]
        public decimal SlaFirstResponseMinutes { get; set; } = default!;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Ai trust threshold minutes must be a non-negative value.")]
        public decimal AiTrustThreshold { get; set; } = default!;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Sla resolution minutes must be a non-negative value.")]
        public decimal SlaResolutionMinutes { get; set; } = default!;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Sla human response minutes must be a non-negative value.")]
        public decimal SlaHumanResponseMinutes { get; set; } = default!;

        //Email Creation properties
        [StringLength(500)]
        public string? CreationToken { get; set; } = default!;

        [Required]
        public InvitationStatus CreationConfirmationStatus { get; set; } = InvitationStatus.PENDING;

        public DateTime? CreationTokenExpiresAt { get; set; } = default!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }
        

        public ICollection<User> Users { get; set; } = [];
    }
}