using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.Data.Services;

/// <summary>
/// Authentication of the cXML requests shared by the punchout processors.
/// </summary>
public abstract class BasePunchoutProcessor(
    IOptions<PunchoutOptions> options,
    IPunchoutUserMappingSearchService userMappingSearchService)
{
    protected PunchoutOptions Options => options.Value;

    protected virtual PunchoutConfiguration FindConfiguration(BasePunchoutRequest request)
    {
        if (request.SharedSecret.IsNullOrEmpty())
        {
            return null;
        }

        var secret = Encoding.UTF8.GetBytes(request.SharedSecret);
        PunchoutConfiguration result = null;

        // Compare with every configuration, so the response time does not depend on the matched position
        foreach (var configuration in Options.Configurations.Where(x => !x.SharedSecret.IsNullOrEmpty()))
        {
            if (CryptographicOperations.FixedTimeEquals(secret, Encoding.UTF8.GetBytes(configuration.SharedSecret)))
            {
                result ??= configuration;
            }
        }

        return result;
    }

    protected virtual bool AreCredentialsValid(BasePunchoutRequest request, PunchoutConfiguration settings)
    {
        // An empty configured domain is not checked
        return settings.SenderDomain.IsNullOrEmpty() ||
               settings.SenderDomain.EqualsIgnoreCase(request.SenderDomain);
    }

    protected virtual async Task<PunchoutUserMapping> FindUserMappingAsync(string senderIdentity)
    {
        if (string.IsNullOrEmpty(senderIdentity))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<PunchoutUserMappingSearchCriteria>.TryCreateInstance();
        criteria.ExternalIds = [senderIdentity];
        criteria.IsActive = true;
        criteria.Take = 1;

        var searchResult = await userMappingSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }
}
