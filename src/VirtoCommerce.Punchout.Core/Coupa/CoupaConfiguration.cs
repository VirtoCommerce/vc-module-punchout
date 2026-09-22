using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Coupa;

public class CoupaConfiguration
{
    public string StoreId { get; set; }

    public string SenderDomain { get; set; }

    public string SharedSecret { get; set; }

    /// <summary>
    /// Allowed BrowserFormPost URLs. Either exact URL or a prefixed, e.g. 'http://localhost/*'.
    /// If empty the return URL is not checked.
    /// </summary>
    public IList<string> AllowedReturnUrls { get; set; } = [];
}
