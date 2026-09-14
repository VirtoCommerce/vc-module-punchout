using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Models;

public class PunchoutSetupContext
{
    public string PayloadId { get; set; }

    public string BuyerCookie { get; set; }

    public string ReturnUrl { get; set; }

    public string Operation { get; set; }

    //public PunchoutCredential Credential { get; set; }

    public IDictionary<string, string> Extrinsics { get; set; }
}
