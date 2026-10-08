using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.XCart.Core;

namespace VirtoCommerce.Punchout.ExperienceApi.Services;

public class PunchoutOrderMessageBuilder(ICxmlResponseFactory cxmlFactory) : IPunchoutOrderMessageBuilder
{
    protected const string UserAgent = "Virto Commerce Punchout";
    protected const string DefaultLanguage = "en-US";
    protected const string DefaultUnitOfMeasure = "EA";

    public virtual CxmlDocument Build(PunchoutSession session, CartAggregate cartAggregate)
    {
        var document = cxmlFactory.CreateDocument();

        document.Header = BuildHeader(session);
        document.Message = new CxmlMessage
        {
            PunchOutOrderMessage = BuildPunchoutOrderMessage(session, cartAggregate),
        };

        return document;
    }

    protected virtual CxmlHeader BuildHeader(PunchoutSession session)
    {
        return new CxmlHeader
        {
            From = new CxmlFrom
            {
                Credential = CreateCredential(session.SupplierDomain, session.SupplierIdentity),
            },
            To = new CxmlTo
            {
                Credential = CreateCredential(session.BuyerDomain, session.BuyerIdentity),
            },
            Sender = new CxmlSender
            {
                Credential = CreateCredential(session.SupplierDomain, session.SupplierIdentity),
                UserAgent = UserAgent,
            },
        };
    }

    protected virtual CxmlCredential CreateCredential(string domain, string identity)
    {
        return new CxmlCredential
        {
            Domain = domain,
            Identity = identity,
        };
    }

    protected virtual CxmlPunchoutOrderMessage BuildPunchoutOrderMessage(PunchoutSession session, CartAggregate cartAggregate)
    {
        var cart = cartAggregate.Cart;
        var lineItems = cart.Items?.Where(x => x.SelectedForCheckout)?.ToArray() ?? [];

        return new CxmlPunchoutOrderMessage
        {
            BuyerCookie = session.BuyerCookie,
            PunchOutOrderMessageHeader = new CxmlPunchoutOrderMessageHeader
            {
                OperationAllowed = CxmlConstants.OperationAllowed.Create,
                Total = CreateAmount(lineItems.Sum(x => x.ExtendedPrice), cart.Currency),
            },
            Items = [.. lineItems.Select(x => BuildItemIn(cartAggregate, x))],
        };
    }

    protected virtual CxmlItemIn BuildItemIn(CartAggregate cartAggregate, LineItem lineItem)
    {
        return new CxmlItemIn
        {
            Quantity = lineItem.Quantity,
            ItemId = new CxmlItemId
            {
                SupplierPartId = lineItem.Sku,
                SupplierPartAuxiliaryId = lineItem.Id,
            },
            ItemDetail = BuildItemDetail(cartAggregate, lineItem),
        };
    }

    protected virtual CxmlItemDetail BuildItemDetail(CartAggregate cartAggregate, LineItem lineItem)
    {
        return new CxmlItemDetail
        {
            UnitPrice = CreateAmount(lineItem.PlacedPrice, lineItem.Currency ?? cartAggregate.Cart.Currency),
            Description = GetDescription(cartAggregate, lineItem),
            UnitOfMeasure = GetUnitOfMeasure(cartAggregate, lineItem),
            Classifications = GetClassifications(cartAggregate, lineItem),
        };
    }

    protected virtual CxmlDescription GetDescription(CartAggregate cartAggregate, LineItem lineItem)
    {
        return new CxmlDescription
        {
            Language = lineItem.LanguageCode ?? cartAggregate.Cart.LanguageCode ?? DefaultLanguage,
            Value = lineItem.Name,
        };
    }

    protected virtual string GetUnitOfMeasure(CartAggregate cartAggregate, LineItem lineItem)
    {
        var measureUnit = cartAggregate.CartProducts.TryGetValue(cartAggregate.GetCartProductKey(lineItem), out var cartProduct)
            ? cartProduct.Product?.MeasureUnit
            : null;

        return string.IsNullOrEmpty(measureUnit) ? DefaultUnitOfMeasure : measureUnit;
    }

    /// <summary>
    /// Placeholder: the classification depends on the project, override to provide real codes.
    /// </summary>
    protected virtual List<CxmlClassification> GetClassifications(CartAggregate cartAggregate, LineItem lineItem)
    {
        return [];
    }

    protected virtual CxmlAmount CreateAmount(decimal amount, string currency)
    {
        return new CxmlAmount
        {
            Money = new CxmlMoney
            {
                Currency = currency,
                Value = amount.ToString("0.00", CultureInfo.InvariantCulture),
            },
        };
    }
}
