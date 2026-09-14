using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.Punchout.Data.Models;

/// <summary>
/// Links a punchout integration to an organization. An organization can have several integrations,
/// and an integration can serve several organizations.
/// </summary>
public class PunchoutIntegrationOrganizationEntity : Entity
{
    [Required]
    [StringLength(128)]
    public string OrganizationId { get; set; }

    [Required]
    [StringLength(128)]
    public string IntegrationId { get; set; }

    [ForeignKey(nameof(IntegrationId))]
    public virtual PunchoutIntegrationEntity Integration { get; set; }
}
