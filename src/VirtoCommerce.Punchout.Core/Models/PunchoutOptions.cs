using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutOptions
{
    public IList<PunchoutConfiguration> Configurations { get; set; } = [];
}
