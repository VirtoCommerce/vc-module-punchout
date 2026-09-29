using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Security;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Commands;
using VirtoCommerce.XCart.Core.Queries;
using ModuleConstants = VirtoCommerce.Punchout.Core.ModuleConstants;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class ActivatePunchoutSessionCommandHandler(
    IPunchoutSessionSearchService punchoutSessionSearchService,
    IStoreService storeService,
    IMediator mediator)
    : IRequestHandler<ActivatePunchoutSessionCommand, PunchoutSessionActivationResult>
{
    private const string PunchoutCartChannelId = "punchout";

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

        result.PunchoutCartId = punchoutCart.Cart.Id;
        result.PunchoutCartName = punchoutCart.Cart.Name;
        result.ExpiresIn = session.ExpirationDate is null
            ? 0
            : (int)Math.Max(0, (session.ExpirationDate.Value - DateTime.UtcNow).TotalSeconds);

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

        // The session must belong to this store and this user, be active and not expired.
        // The token is not spent here, it is redeemed once by the punchout grant type in connect/token.
        var criteria = AbstractTypeFactory<PunchoutSessionSearchCriteria>.TryCreateInstance();
        criteria.StoreId = request.StoreId;
        criteria.UserId = request.UserId;
        criteria.SessionToken = SessionTokenHasher.Hash(request.SessionToken);
        criteria.Statuses = [ModuleConstants.SessionStatus.Active];
        criteria.Expired = false;
        criteria.Take = 1;

        var searchResult = await punchoutSessionSearchService.SearchAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

    protected virtual async Task<CartAggregate> CreatePunchoutCartIfNotExistAsync(ActivatePunchoutSessionCommand request, Store store, PunchoutSession session)
    {
        var punchoutCartName = $"{PunchoutCartChannelId}.{session.Id}";

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
        createCartCommand.ChannelId = PunchoutCartChannelId;
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
