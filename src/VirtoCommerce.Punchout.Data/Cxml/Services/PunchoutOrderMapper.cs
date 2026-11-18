using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Cxml.Services;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Data.Cxml.Extensions;

namespace VirtoCommerce.Punchout.Data.Cxml.Services;

public class PunchoutOrderMapper(ICxmlResponseFactory responseFactory) : IPunchoutOrderMapper
{
    public virtual PunchoutOrderRequestData MapRequest(CxmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var orderContext = AbstractTypeFactory<PunchoutOrderRequestData>.TryCreateInstance();

        var header = document.Header;
        var orderRequest = document.Request?.OrderRequest;
        var orderHeader = orderRequest?.OrderRequestHeader;

        orderContext.PayloadId = document.PayloadId;

        orderContext.From = header?.From?.Credential?.Identity;
        orderContext.FromDomain = header?.From?.Credential?.Domain;

        orderContext.To = header?.To?.Credential?.Identity;
        orderContext.ToDomain = header?.To?.Credential?.Domain;

        orderContext.Sender = header?.Sender?.Credential?.Identity;
        orderContext.SenderDomain = header?.Sender?.Credential?.Domain;
        orderContext.SharedSecret = header?.Sender?.Credential?.SharedSecret;

        orderContext.OrderId = orderHeader?.OrderId;
        orderContext.OrderDate = orderHeader?.OrderDate.ToDateTimeOffset();
        orderContext.OrderType = orderHeader?.Type;

        orderContext.Currency = orderHeader?.Total?.Money?.Currency;
        orderContext.Total = orderHeader?.Total?.Money.ToDecimal();

        orderContext.ShippingAmount = orderHeader?.Shipping?.Money.ToDecimal();
        orderContext.ShippingDescription = orderHeader?.Shipping?.Description?.Value?.Trim();

        orderContext.PaymentTermDays = orderHeader?.PaymentTerm?.PayInNumberOfDays;

        orderContext.ShipTo = MapAddress(orderHeader?.ShipTo?.Address);
        orderContext.BillTo = MapAddress(orderHeader?.BillTo?.Address);
        orderContext.User = MapUser(orderHeader?.Contacts);

        orderContext.Items = orderRequest?.Items?
            .Where(x => x is not null)
            .Select(MapItem)
            .ToList() ?? [];

        return orderContext;
    }

    public virtual CxmlDocument MapResponse(PunchoutOrderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var (code, text) = MapStatus(result.Status);

        return responseFactory.CreateResponse(code, text, result.ErrorMessage);
    }

    protected virtual (string Code, string Text) MapStatus(string status)
    {
        return status switch
        {
            PunchoutOrderStatus.Success => (CxmlConstants.Status.OkCode, CxmlConstants.Status.OkText),
            PunchoutOrderStatus.InvalidCredentials => (CxmlConstants.Status.UnauthorizedCode, CxmlConstants.Status.UnauthorizedText),
            PunchoutOrderStatus.UserNotFound => (CxmlConstants.Status.UnauthorizedCode, CxmlConstants.Status.UnauthorizedText),
            PunchoutOrderStatus.InvalidRequest => (CxmlConstants.Status.BadRequestCode, CxmlConstants.Status.BadRequestText),
            PunchoutOrderStatus.CurrencyNotSupported => (CxmlConstants.Status.BadRequestCode, CxmlConstants.Status.BadRequestText),
            PunchoutOrderStatus.UnsupportedOrderType => (CxmlConstants.Status.NotImplementedCode, CxmlConstants.Status.NotImplementedText),
            PunchoutOrderStatus.StoreNotConfigured => (CxmlConstants.Status.InternalServerErrorCode, CxmlConstants.Status.InternalServerErrorText),
            // 5xx tells the buyer to retry later
            PunchoutOrderStatus.OrderInProgress => (CxmlConstants.Status.InternalServerErrorCode, CxmlConstants.Status.InternalServerErrorText),
            _ => (CxmlConstants.Status.InternalServerErrorCode, CxmlConstants.Status.InternalServerErrorText),
        };
    }

    protected virtual PunchoutUserContext MapUser(IList<CxmlContact> contacts)
    {
        return contacts.ToUserContext();
    }

    protected virtual PunchoutAddress MapAddress(CxmlAddress cxmlAddress)
    {
        if (cxmlAddress is null)
        {
            return null;
        }

        var address = AbstractTypeFactory<PunchoutAddress>.TryCreateInstance();

        var postalAddress = cxmlAddress.PostalAddress;
        var streets = NonEmptyLines(postalAddress?.Streets);

        address.AddressId = cxmlAddress.AddressId;
        address.Name = cxmlAddress.Name?.Value?.Trim();
        address.Email = cxmlAddress.Email?.Trim();

        address.DeliverTo = JoinLines(NonEmptyLines(postalAddress?.DeliverTo));
        address.Line1 = streets.FirstOrDefault();
        address.Line2 = JoinLines(streets.Skip(1));
        address.City = postalAddress?.City?.Trim();
        address.RegionName = postalAddress?.State?.Trim();
        address.PostalCode = postalAddress?.PostalCode?.Trim();
        address.CountryName = postalAddress?.Country?.Value?.Trim();

        // Country/@isoCountryCode is required by the DTD, Address/@isoCountryCode is optional
        var countryCode = postalAddress?.Country?.IsoCountryCode;
        address.CountryCode = string.IsNullOrEmpty(countryCode) ? cxmlAddress.IsoCountryCode : countryCode;

        return address;
    }

    protected virtual PunchoutOrderRequestItem MapItem(CxmlItemOut itemOut)
    {
        var item = AbstractTypeFactory<PunchoutOrderRequestItem>.TryCreateInstance();

        var itemDetail = itemOut.ItemDetail;

        item.LineNumber = itemOut.LineNumber;
        item.Quantity = itemOut.Quantity;

        item.SupplierPartId = itemOut.ItemId?.SupplierPartId?.Trim();
        item.SupplierPartAuxiliaryId = itemOut.ItemId?.SupplierPartAuxiliaryId?.Trim();
        (item.LineItemId, item.SessionId) = PunchoutItemReference.Parse(item.SupplierPartAuxiliaryId);

        item.UnitPrice = itemDetail?.UnitPrice?.Money.ToDecimal();
        item.Currency = itemDetail?.UnitPrice?.Money?.Currency;
        item.Description = itemDetail?.Description?.Value?.Trim();
        item.UnitOfMeasure = itemDetail?.UnitOfMeasure?.Trim();
        item.Classifications = MapClassifications(itemDetail?.Classifications);

        item.Distributions = itemOut.Distributions?
            .Where(x => x is not null)
            .Select(MapDistribution)
            .ToList() ?? [];

        return item;
    }

    protected virtual PunchoutOrderRequestDistribution MapDistribution(CxmlDistribution cxmlDistribution)
    {
        var distribution = AbstractTypeFactory<PunchoutOrderRequestDistribution>.TryCreateInstance();

        distribution.AccountingName = cxmlDistribution.Accounting?.Name;
        distribution.ChargeAmount = cxmlDistribution.Charge?.Money.ToDecimal();
        distribution.Currency = cxmlDistribution.Charge?.Money?.Currency;

        distribution.Segments = cxmlDistribution.Accounting?.Segments?
            .Where(x => x is not null)
            .Select(MapSegment)
            .ToList() ?? [];

        return distribution;
    }

    protected virtual PunchoutAccountingSegment MapSegment(CxmlSegment cxmlSegment)
    {
        var segment = AbstractTypeFactory<PunchoutAccountingSegment>.TryCreateInstance();

        segment.Type = cxmlSegment.Type;
        segment.Id = cxmlSegment.Id;
        segment.Description = cxmlSegment.Description;

        return segment;
    }

    private static Dictionary<string, string> MapClassifications(IList<CxmlClassification> classifications)
    {
        if (classifications.IsNullOrEmpty())
        {
            return [];
        }

        // Duplicate domains are legal in cXML, the last one wins
        return classifications
            .Where(x => !x.Domain.IsNullOrEmpty())
            .GroupBy(x => x.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().Value?.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> NonEmptyLines(IEnumerable<string> lines)
    {
        return lines?
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList() ?? [];
    }

    private static string JoinLines(IEnumerable<string> lines)
    {
        var result = string.Join(", ", lines);

        return string.IsNullOrEmpty(result) ? null : result;
    }
}
