using System.Linq;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Data.Validators;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutOrderRequestValidatorTests
{
    private readonly PunchoutOrderRequestValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("new")]
    [InlineData("update")]
    [InlineData("delete")]
    public void Validate_ValidRequest_IsValid(string orderType)
    {
        var request = CreateRequest(orderType);

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_ItemWithoutCurrency_IsValid()
    {
        var request = CreateRequest();
        request.Items[0].Currency = null;

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("new")]
    [InlineData("update")]
    public void Validate_NoItems_IsInvalid(string orderType)
    {
        var request = CreateRequest(orderType);
        request.Items.Clear();

        Assert.Equal("The order has no items.", GetError(request));
    }

    [Fact]
    public void Validate_DeleteWithoutItems_IsValid()
    {
        var request = CreateRequest("delete");
        request.Items.Clear();

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Validate_EmptyRequiredFields_IsInvalid()
    {
        Assert.Equal("The cXML document has no payloadID.", GetError(CreateRequest(payloadId: "")));
        Assert.Equal("The order has no orderID.", GetError(CreateRequest(orderId: "")));
        Assert.Equal("The order has no Total currency.", GetError(CreateRequest(currency: "")));
    }

    [Theory]
    [InlineData("", 1, 10, "USD", "SupplierPartID is missing")]
    [InlineData("SKU-1", 0, 10, "USD", "quantity must be positive")]
    [InlineData("SKU-1", 1, -1, "USD", "unit price is missing or invalid")]
    [InlineData("SKU-1", 1, 10, "EUR", "does not match the order currency")]
    public void Validate_InvalidItem_IsInvalid(string sku, decimal quantity, decimal price, string currency, string expectedError)
    {
        var request = CreateRequest();
        var item = request.Items[0];
        item.SupplierPartId = sku;
        item.Quantity = quantity;
        item.UnitPrice = price;
        item.Currency = currency;

        Assert.Contains(expectedError, GetError(request));
    }

    [Theory]
    [InlineData(null, "lineNumber is missing")]
    [InlineData("", "lineNumber is missing")]
    public void Validate_ItemWithoutLineNumber_IsInvalid(string lineNumber, string expectedError)
    {
        var request = CreateRequest("delete");
        request.Items[0].LineNumber = lineNumber;

        Assert.Contains(expectedError, GetError(request));
    }

    [Fact]
    public void Validate_LongLineNumber_IsInvalid()
    {
        var request = CreateRequest();
        request.Items[0].LineNumber = new string('1', 129);

        Assert.Contains("lineNumber is longer than 128 characters", GetError(request));
    }

    [Fact]
    public void Validate_DuplicateLineNumbers_IsInvalid()
    {
        var request = CreateRequest();
        request.Items.Add(new PunchoutOrderRequestItem { LineNumber = "1", SupplierPartId = "SKU-2", Quantity = 1, UnitPrice = 5m, Currency = "USD" });

        Assert.Equal("The order has several lines with the same lineNumber.", GetError(request));
    }

    [Fact]
    public void Validate_ItemWithoutPrice_IsInvalid()
    {
        var request = CreateRequest();
        request.Items[0].UnitPrice = null;

        Assert.Contains("unit price is missing or invalid", GetError(request));
    }

    private string GetError(PunchoutOrderRequestData request)
    {
        return _validator.Validate(request).Errors.FirstOrDefault()?.ErrorMessage;
    }

    private static PunchoutOrderRequestData CreateRequest(string orderType = null, string payloadId = "msg-001", string orderId = "PO-1", string currency = "USD")
    {
        return new PunchoutOrderRequestData
        {
            PayloadId = payloadId,
            OrderId = orderId,
            OrderType = orderType,
            Currency = currency,
            Items =
            [
                new PunchoutOrderRequestItem { LineNumber = "1", SupplierPartId = "SKU-1", Quantity = 1, UnitPrice = 10m, Currency = "USD" },
            ],
        };
    }
}
