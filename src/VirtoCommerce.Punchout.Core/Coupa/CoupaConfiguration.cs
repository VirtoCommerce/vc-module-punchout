using System;
using System.Collections.Generic;

namespace VirtoCommerce.Punchout.Core.Coupa;

public class CoupaConfiguration
{
    public static readonly TimeSpan DefaultTokenLifeTime = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan DefaultSessionLifeTime = TimeSpan.FromHours(24);

    public string StoreId { get; set; }

    public string SenderDomain { get; set; }

    public string SharedSecret { get; set; }

    /// <summary>
    /// Allowed BrowserFormPost URLs. Either exact URL or a prefixed, e.g. 'http://localhost/*'
    /// If empty the return URL is not checked.
    /// </summary>
    public IList<string> AllowedReturnUrls { get; set; } = [];

    /// <summary>
    /// How long the start page URL stays redeemable, e.g. '00:15:00'. Defaults to 15 minutes.
    /// </summary>
    public TimeSpan? TokenLifeTime { get; set; }

    /// <summary>
    /// How long the buyer may shop once the session is activated, e.g. '1.00:00:00'. Defaults to 24 hours.
    /// </summary>
    public TimeSpan? SessionLifeTime { get; set; }
}
