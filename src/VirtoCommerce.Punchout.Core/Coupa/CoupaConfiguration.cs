using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Coupa;

/// <summary>
/// The single, global Coupa punchout configuration, bound from the
/// <c>Punchout:CoupaConfiguration</c> section of the platform configuration.
/// </summary>
public class CoupaConfiguration
{
    /// <summary>
    /// The store the punchout session is opened in. Its storefront URL becomes the start page host.
    /// </summary>
    public string StoreId { get; set; }

    /// <summary>
    /// The credential domain Coupa sends in Header/Sender/Credential/@domain, e.g. NetworkId.
    /// Left empty, the domain is not checked.
    /// </summary>
    public string SenderDomain { get; set; }

    /// <summary>
    /// The shared secret agreed with Coupa. Compared with Header/Sender/Credential/SharedSecret.
    /// </summary>
    public string SharedSecret { get; set; }

    /// <summary>
    /// The URLs a setup request is allowed to name in BrowserFormPost/URL. Each entry is an exact URL or a
    /// prefix ending with '*', e.g. <c>http://localhost:5000/*</c>. Left empty, the return URL is not checked.
    /// </summary>
    public IList<string> AllowedReturnUrls { get; set; } = [];
}
