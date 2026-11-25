using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Cxml;
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

    /// <summary>
    /// The email of the end user: Contact role="endUser" or, when the contact has no email, the UserEmail extrinsic.
    /// </summary>
    protected virtual string GetUserEmail(PunchoutUserContext user, IDictionary<string, string> extrinsics)
    {
        var email = user?.Email;

        if (string.IsNullOrWhiteSpace(email) &&
            extrinsics?.TryGetValue(CxmlConstants.ExtrinsicName.UserEmail, out var extrinsicEmail) == true)
        {
            email = extrinsicEmail;
        }

        return email?.Trim();
    }

    /// <summary>
    /// Returns the identity the user mapping is looked up by.
    /// </summary>
    protected virtual string GetMappingIdentity(BasePunchoutRequest request) => request.Sender;

    /// <summary>
    /// Finds the active mapping for the identity. A user mapping takes precedence over an organization mapping.
    /// </summary>
    protected virtual async Task<PunchoutUserMapping> FindUserMappingAsync(string identity)
    {
        if (string.IsNullOrEmpty(identity))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<PunchoutUserMappingSearchCriteria>.TryCreateInstance();
        criteria.ExternalIds = [identity];
        criteria.IsActive = true;

        var searchResult = await userMappingSearchService.SearchNoCloneAsync(criteria);

        return searchResult.Results
            .OrderBy(x => x.IsOrganizationMapping)
            .FirstOrDefault();
    }
}
