using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// Selects the requisition items returned to the buyer by the sessions of a configuration.
/// </summary>
public class PunchoutReturnedItemsCriteria
{
    public string ConfigurationId { get; set; }

    /// <summary>
    /// The sessions of the user. Used when <see cref="OrganizationId"/> is empty.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// The sessions of an organization mapping, started by any user of the organization.
    /// </summary>
    public string OrganizationId { get; set; }

    /// <summary>
    /// The cart line item ids.
    /// </summary>
    public IList<string> LineItemIds { get; set; }
}
