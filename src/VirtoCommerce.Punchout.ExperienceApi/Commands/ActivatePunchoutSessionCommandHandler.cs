using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Coupa;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Commands;
using VirtoCommerce.XCart.Core.Queries;
using ModuleConstants = VirtoCommerce.Punchout.Core.ModuleConstants;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class ActivatePunchoutSessionCommandHandler(
    IPunchoutSessionService punchoutSessionService,
    IPunchoutSessionSearchService punchoutSessionSearchService,
    IStoreService storeService,
    IMediator mediator,
    IOptions<CoupaConfiguration> configuration)
    : IRequestHandler<ActivatePunchoutSessionCommand, PunchoutSessionActivationResult>
{
    private const string PunchoutCartName = "punchout";

    public async Task<PunchoutSessionActivationResult> Handle(ActivatePunchoutSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = AbstractTypeFactory<PunchoutSessionActivationResult>.TryCreateInstance();

        var store = await storeService.GetNoCloneAsync(request.StoreId);
        if (store == null)
        {
            result.Error = ModuleConstants.ActivationErrors.StoreNotFound;
            return result;
        }

        var session = await FindSessionAsync(request);

        if (session is null)
        {
            result.Error = ModuleConstants.ActivationErrors.SessionNotFound;
            return result;
        }

        var punchoutCart = await CreatePunchoutCartIfNotExistAsync(request, store, session);

        // The token is single-use, moving the session out of Created spends it, second activation with the same token no longer finds anything
        session.Status = ModuleConstants.SessionStatus.Active;
        session.ExpirationDate = DateTime.UtcNow.Add(configuration.Value.SessionLifeTime ?? CoupaConfiguration.DefaultSessionLifeTime);

        await punchoutSessionService.SaveChangesAsync([session]);

        result.PunchoutCartId = punchoutCart.Cart.Id;
        result.PunchoutCartName = punchoutCart.Cart.Name;

        return result;
    }

    protected virtual async Task<PunchoutSession> FindSessionAsync(ActivatePunchoutSessionCommand request)
    {
        if (string.IsNullOrEmpty(request.StoreId) ||
            string.IsNullOrEmpty(request.SessionToken) ||
            string.IsNullOrEmpty(request.UserId))
        {
            return null;
        }

        // The session must belong to this store and this user, must not have been activated yet and not be expired
        var criteria = AbstractTypeFactory<PunchoutSessionSearchCriteria>.TryCreateInstance();
        criteria.StoreId = request.StoreId;
        criteria.UserId = request.UserId;
        criteria.SessionToken = request.SessionToken;
        criteria.Statuses = [ModuleConstants.SessionStatus.Created];
        criteria.Expired = false;
        criteria.Take = 1;

        var searchResult = await punchoutSessionSearchService.SearchAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

    protected virtual async Task<CartAggregate> CreatePunchoutCartIfNotExistAsync(ActivatePunchoutSessionCommand request, Store store, PunchoutSession session)
    {
        var punchoutCartName = $"{PunchoutCartName}.{session.Id}";

        var getCartQuery = GetGetCartQuery(request, store, punchoutCartName);
        var punchoutCart = await mediator.Send(getCartQuery);
        if (punchoutCart == null)
        {
            var createCartCommand = GetCreateCartCommand(request, store, punchoutCartName);
            punchoutCart = await mediator.Send(createCartCommand);
        }

        return punchoutCart;
    }

    protected virtual CreateCartCommand GetCreateCartCommand(ActivatePunchoutSessionCommand request, Store store, string punchoutCartName)
    {
        var createCartCommand = AbstractTypeFactory<CreateCartCommand>.TryCreateInstance();
        createCartCommand.StoreId = request.StoreId;
        createCartCommand.UserId = request.UserId;
        createCartCommand.OrganizationId = request.OrganizationId;
        createCartCommand.CurrencyCode = request.CurrencyCode ?? store.DefaultCurrency;
        createCartCommand.CultureName = request.CultureName ?? store.DefaultLanguage;
        createCartCommand.CartName = punchoutCartName;
        return createCartCommand;
    }

    protected virtual GetCartQuery GetGetCartQuery(ActivatePunchoutSessionCommand request, Store store, string punchoutCartName)
    {
        var getCartQuery = AbstractTypeFactory<GetCartQuery>.TryCreateInstance();
        getCartQuery.StoreId = request.StoreId;
        getCartQuery.UserId = request.UserId;
        getCartQuery.OrganizationId = request.OrganizationId;
        getCartQuery.CurrencyCode = request.CurrencyCode ?? store.DefaultCurrency;
        getCartQuery.CultureName = request.CultureName ?? store.DefaultLanguage;
        getCartQuery.CartName = punchoutCartName;
        return getCartQuery;
    }
}
