using System;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Security;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Repositories;

namespace VirtoCommerce.Punchout.Data.Services;

public class PunchoutSessionManagementService : IPunchoutSessionManagementService
{
    private readonly Func<IPunchoutRepository> _repositoryFactory;
    private readonly IPunchoutSessionService _punchoutSessionService;

    public PunchoutSessionManagementService(
        Func<IPunchoutRepository> repositoryFactory,
        IPunchoutSessionService punchoutSessionService)
    {
        _repositoryFactory = repositoryFactory;
        _punchoutSessionService = punchoutSessionService;
    }

    public async Task<ReedeemPunchoutSessionResult> RedeemSessionAsync(ReedeemPunchoutSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = AbstractTypeFactory<ReedeemPunchoutSessionResult>.TryCreateInstance();

        var sessionTokenHash = SessionTokenHasher.Hash(request.SessionToken);
        if (sessionTokenHash is null)
        {
            return result;
        }

        var now = DateTime.UtcNow;

        string sessionId;
        using (var repository = _repositoryFactory())
        {
            sessionId = await repository.RedeemSessionTokenAsync(sessionTokenHash, now);
        }

        if (sessionId is null)
        {
            return result;
        }

        // The token was redeemed bypassing the CRUD service, so the cached session is stale
        ClearCache(sessionId);

        var session = await _punchoutSessionService.GetByIdAsync(sessionId);

        // The session must not outlive its expiration date
        if (session?.ExpirationDate is null || session.ExpirationDate <= now)
        {
            return result;
        }

        result.Session = session;

        return result;
    }

    protected virtual void ClearCache(string sessionId)
    {
        GenericSearchCachingRegion<PunchoutSession>.ExpireRegion();
        GenericCachingRegion<PunchoutSession>.ExpireTokenForKey(sessionId);
    }
}
