using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.XCart.Core;
using VirtoCommerce.XCart.Core.Commands;
using ModuleConstants = VirtoCommerce.Punchout.Core.ModuleConstants;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class ActivatePunchoutSessionCommandHandler(
    IPunchoutSessionService punchoutSessionService,
    IPunchoutSessionSearchService punchoutSessionSearchService,
    IStoreService storeService,
    IMediator mediator)
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
            // Every reason for not finding the session gives the same answer: the caller only holds a start
            // page URL, so telling it whether the token, the store or the user was wrong would help it guess.
            result.Error = ModuleConstants.ActivationErrors.SessionNotFound;
            return result;
        }

        // session found, create punchout cart
        var punchoutCart = await CreatePunchoutCart(request, store);

        session.CartId = punchoutCart.Id;
        session.Status = ModuleConstants.SessionStatus.Active;

        await punchoutSessionService.SaveChangesAsync([session]);

        result.PunchoutCartId = punchoutCart.Id;
        result.PunchoutCartName = punchoutCart.Cart.Name;

        return result;
    }

    /// <summary>
    /// Finds the session the start page URL points at: it must belong to this store and this user, must not
    /// have been activated yet and must not have expired.
    /// </summary>
    protected virtual async Task<PunchoutSession> FindSessionAsync(ActivatePunchoutSessionCommand request)
    {
        // An empty part of the key is not a filter that matches everything here: without all three the
        // request does not identify a session at all.
        if (string.IsNullOrEmpty(request.StoreId) ||
            string.IsNullOrEmpty(request.SessionToken) ||
            string.IsNullOrEmpty(request.UserId))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<PunchoutSessionSearchCriteria>.TryCreateInstance();
        criteria.StoreId = request.StoreId;
        criteria.UserId = request.UserId;
        criteria.SessionToken = request.SessionToken;
        criteria.Statuses = [ModuleConstants.SessionStatus.Created];
        criteria.NotExpired = true;
        criteria.Take = 1;

        // Cloned results: the session found here is modified and saved.
        var searchResult = await punchoutSessionSearchService.SearchAsync(criteria);

        return searchResult.Results.FirstOrDefault();
    }

    protected virtual async Task<CartAggregate> CreatePunchoutCart(ActivatePunchoutSessionCommand request, Store store)
    {
        var createCartCommand = AbstractTypeFactory<CreateCartCommand>.TryCreateInstance();
        createCartCommand.StoreId = request.StoreId;
        createCartCommand.UserId = request.UserId;
        createCartCommand.OrganizationId = request.OrganizationId;
        createCartCommand.CurrencyCode = request.CurrencyCode ?? store.DefaultCurrency;
        createCartCommand.CultureName = request.CultureName ?? store.DefaultLanguage;
        createCartCommand.CartName = PunchoutCartName;

        var punchoutCart = await mediator.Send(createCartCommand);
        return punchoutCart;
    }
}
