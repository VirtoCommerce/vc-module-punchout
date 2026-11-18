using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Models;
using VirtoCommerce.Punchout.Data.Services;
using VirtoCommerce.Punchout.ExperienceApi.Services;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutRequisitionTests
{
    private static readonly PunchoutConfiguration Configuration = new() { Id = "configuration", StoreId = "store" };

    [Fact]
    public async Task ValidateRequisitionItems_ItemsFromSeveralSessions_AreMatchedWithTheirSnapshots()
    {
        var processor = CreateProcessor();
        var request = CreateRequest(
            ("1", "SKU-1", 2, 10m, "line-1", "session-1"),
            ("2", "SKU-2", 1, 20m, "line-2", "session-2"));

        var error = await processor.ValidateRequisitionItemsPublicAsync(request, isNewOrder: true);

        Assert.Null(error);
        Assert.Equal("session-1", request.Items[0].RequisitionItem.SessionId);
        Assert.Equal("session-2", request.Items[1].RequisitionItem.SessionId);
    }

    [Fact]
    public async Task ValidateRequisitionItems_ItemWithoutSession_UsesLastReturnedSession()
    {
        var processor = CreateProcessor();
        var request = CreateRequest(("1", "SKU-3", 1, 30m, "line-3", null));

        var error = await processor.ValidateRequisitionItemsPublicAsync(request, isNewOrder: true);

        Assert.Null(error);
        Assert.Equal("session-last", request.Items[0].RequisitionItem.SessionId);
    }

    [Theory]
    [InlineData("SKU-1", 2, 10, "line-1", "session-1", null)]
    [InlineData("OTHER", 2, 10, "line-1", "session-1", "does not match the requisition item")]
    [InlineData("SKU-1", 3, 10, "line-1", "session-1", "does not match the requisition quantity")]
    [InlineData("SKU-1", 2, 11, "line-1", "session-1", "does not match the requisition price")]
    [InlineData("SKU-1", 2, 10, "line-2", "session-1", "is not in the punchout requisition")]
    [InlineData("SKU-1", 2, 10, "line-1", "unknown", "no punchout session 'unknown' returned a requisition")]
    [InlineData("SKU-1", 2, 10, "", "session-1", "SupplierPartAuxiliaryID is missing")]
    public async Task ValidateRequisitionItems_ComparesTheLineWithTheSnapshot(string sku, int quantity, decimal price, string lineItemId, string sessionId, string expectedError)
    {
        var processor = CreateProcessor();
        var request = CreateRequest(("1", sku, quantity, price, lineItemId, sessionId));

        var error = await processor.ValidateRequisitionItemsPublicAsync(request, isNewOrder: true);

        if (expectedError is null)
        {
            Assert.Null(error);
        }
        else
        {
            Assert.Contains(expectedError, error);
        }
    }

    [Fact]
    public async Task ValidateRequisitionItems_UpdateDoesNotCompareQuantity()
    {
        var processor = CreateProcessor();
        var request = CreateRequest(("1", "SKU-1", 5, 10m, "line-1", "session-1"));

        Assert.Null(await processor.ValidateRequisitionItemsPublicAsync(request, isNewOrder: false));
    }

    [Fact]
    public async Task ValidateRequisitionItems_SameItemTwice_IsRejected()
    {
        var processor = CreateProcessor();
        var request = CreateRequest(
            ("1", "SKU-1", 1, 10m, "line-1", "session-1"),
            ("2", "SKU-1", 1, 10m, "line-1", "session-1"));

        Assert.Contains("is used by another line", await processor.ValidateRequisitionItemsPublicAsync(request, isNewOrder: true));
    }

    [Fact]
    public async Task ValidateRequisitionItems_ItemOrderedByAnotherPurchaseOrder_IsRejected()
    {
        var lineService = new InMemoryOrderLineService();
        await lineService.TryAddAsync([CreateLine("session-1", "line-1", "PO-OTHER")]);
        var processor = CreateProcessor(lineService);
        var request = CreateRequest(("1", "SKU-1", 2, 10m, "line-1", "session-1"));

        var error = await processor.ValidateRequisitionItemsPublicAsync(request, isNewOrder: true);

        Assert.Contains("is already ordered by purchase order 'PO-OTHER'", error);
    }

    [Fact]
    public async Task ValidateRequisitionItems_UpdateLineOrderedByThePurchaseOrder_IsNotMatched()
    {
        var lineService = new InMemoryOrderLineService();
        await lineService.TryAddAsync([CreateLine("session-1", "line-1", "PO-1")]);
        var processor = CreateProcessor(lineService);

        // The ordered line is compared with the customer order by the handler, the new line with its snapshot
        var request = CreateRequest(
            ("1", "SKU-1", 7, 10m, "line-1", "session-1"),
            ("2", "SKU-2", 1, 20m, "line-2", "session-2"));

        var error = await processor.ValidateRequisitionItemsPublicAsync(request, isNewOrder: false);

        Assert.Null(error);
        Assert.Null(request.Items[0].RequisitionItem);
        Assert.NotNull(request.Items[1].RequisitionItem);
    }

    [Fact]
    public async Task OrderLines_AreReservedAndReleasedOnFailure()
    {
        var lineService = new InMemoryOrderLineService();
        var processor = CreateProcessor(lineService);
        var request = CreateRequest(("1", "SKU-1", 2, 10m, "line-1", "session-1"));
        await processor.ValidateRequisitionItemsPublicAsync(request, isNewOrder: true);
        var orderRequest = new PunchoutOrderRequest { Id = "request", ConfigurationId = Configuration.Id };

        var addedLines = await processor.AddOrderLinesPublicAsync(request, orderRequest);

        var line = Assert.Single(lineService.Lines);
        Assert.Equal("PO-1", line.PurchaseOrderNumber);
        Assert.Equal("1", line.LineNumber);
        Assert.Equal("request", line.OrderRequestId);

        await processor.UpdateOrderLinesPublicAsync(request, orderRequest, PunchoutOrderResult.Error(PunchoutOrderStatus.OrderError), addedLines);

        Assert.Empty(lineService.Lines);
    }

    [Fact]
    public async Task OrderLines_ConcurrentPurchaseOrder_CannotReserveTheItem()
    {
        var lineService = new InMemoryOrderLineService();
        var processor = CreateProcessor(lineService);
        var request = CreateRequest(("1", "SKU-1", 2, 10m, "line-1", "session-1"));
        await processor.ValidateRequisitionItemsPublicAsync(request, isNewOrder: true);

        // Another purchase order reserved the item after the validation
        await lineService.TryAddAsync([CreateLine("session-1", "line-1", "PO-OTHER")]);

        Assert.Null(await processor.AddOrderLinesPublicAsync(request, new PunchoutOrderRequest { Id = "request", ConfigurationId = Configuration.Id }));
    }

    [Theory]
    [InlineData("update", new[] { "line-1" })]
    [InlineData("delete", new string[0])]
    public async Task OrderLines_ProcessedChange_ReleasesTheRemovedItems(string orderType, string[] expectedLineItemIds)
    {
        var lineService = new InMemoryOrderLineService();
        await lineService.TryAddAsync([CreateLine("session-1", "line-1", "PO-1"), CreateLine("session-2", "line-2", "PO-1")]);
        var processor = CreateProcessor(lineService);
        var request = CreateRequest(("1", "SKU-1", 2, 10m, "line-1", "session-1"));
        request.OrderType = orderType;

        await processor.UpdateOrderLinesPublicAsync(request, new PunchoutOrderRequest { ConfigurationId = Configuration.Id }, PunchoutOrderResult.Success("order", "PO-1"), []);

        Assert.Equal(expectedLineItemIds, lineService.Lines.Select(x => x.LineItemId));
    }

    [Fact]
    public async Task BuildCart_UsesTheSnapshotPricesAndLineNumbers()
    {
        var handler = new TestCartHandler();
        var request = CreateRequest(
            ("10", "SKU-1", 2, 8m, "line-1", "session-1"),
            ("20", "SKU-2", 1, 20m, "line-2", "session-2"));
        request.Items[0].RequisitionItem = PunchoutOrderUpdateTests.CreateRequisitionItem("line-1", "SKU-1", 2, 8m, listPrice: 10m);
        request.Items[1].RequisitionItem = PunchoutOrderUpdateTests.CreateRequisitionItem("line-2", "SKU-2", 1, 20m, sessionId: "session-2");
        request.ShipTo = new PunchoutAddress { Name = "Acme", CountryCode = "US" };

        var context = new PunchoutOrderRequestHandlerContext
        {
            Request = request,
            Configuration = Configuration,
            Store = new Store { Id = "store", DefaultLanguage = "en-US" },
            UserMapping = new PunchoutUserMapping { UserId = "user", UserName = "buyer" },
        };

        var cart = await handler.BuildCartPublicAsync(context);

        Assert.Equal("PO-1", cart.PurchaseOrderNumber);
        Assert.Equal("user", cart.CustomerId);
        Assert.Equal(["line-1", "line-2"], cart.Items.Select(x => x.Id));
        Assert.Equal(["10", "20"], cart.Items.Select(x => x.PurchaseOrderLineNumber));

        var discounted = cart.Items.First();
        Assert.Equal(10m, discounted.ListPrice);
        Assert.Equal(2m, discounted.DiscountAmount);
        Assert.Equal(8m, discounted.PlacedPrice);
        Assert.Equal("product-SKU-1", discounted.ProductId);

        var shipment = Assert.Single(cart.Shipments);
        Assert.Equal(["line-1", "line-2"], shipment.Items.Select(x => x.LineItemId));
        Assert.Equal("USA", shipment.DeliveryAddress.CountryCode);

        var payment = Assert.Single(cart.Payments);
        Assert.Equal(36m, payment.Amount);
    }

    [Fact]
    public void BuildRequisitionItem_KeepsTheCartPrices()
    {
        var handler = new TestCartHandler();
        var cart = new ShoppingCart { Currency = "USD" };
        var lineItem = new LineItem { Id = "line-1", ProductId = "product", Sku = "SKU-1", Quantity = 3, ListPrice = 10m, SalePrice = 9m, DiscountAmount = 1.5m, PlacedPrice = 8.5m, TaxPercentRate = 0.2m };

        var item = handler.BuildRequisitionItemPublic(cart, lineItem);

        Assert.Equal("line-1", item.LineItemId);
        Assert.Equal("USD", item.Currency);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(10m, item.ListPrice);
        Assert.Equal(1.5m, item.DiscountAmount);
        Assert.Equal(8.5m, item.PlacedPrice);
        Assert.Equal(0.2m, item.TaxPercentRate);
    }

    [Fact]
    public void OrderMessageEntity_StoresTheSnapshot()
    {
        var message = new PunchoutOrderMessage
        {
            SessionId = "session-1",
            Cxml = "<cXML/>",
            Items = [PunchoutOrderUpdateTests.CreateRequisitionItem("line-1", "SKU-1", 2, 8m, listPrice: 10m)],
        };

        var entity = new PunchoutOrderMessageEntity().FromModel(message, new PrimaryKeyResolvingMap());
        var model = entity.ToModel(new PunchoutOrderMessage());

        var item = Assert.Single(model.Items);
        Assert.Equal("session-1", item.SessionId);
        Assert.Equal("line-1", item.LineItemId);
        Assert.Equal(10m, item.ListPrice);
        Assert.Equal(8m, item.PlacedPrice);
    }

    private static TestRequisitionProcessor CreateProcessor(InMemoryOrderLineService lineService = null)
    {
        var snapshots = new Dictionary<string, IList<PunchoutRequisitionItem>>
        {
            ["session-1"] = [PunchoutOrderUpdateTests.CreateRequisitionItem("line-1", "SKU-1", 2, 10m, sessionId: "session-1")],
            ["session-2"] = [PunchoutOrderUpdateTests.CreateRequisitionItem("line-2", "SKU-2", 1, 20m, sessionId: "session-2")],
            ["session-last"] = [PunchoutOrderUpdateTests.CreateRequisitionItem("line-3", "SKU-3", 1, 30m, sessionId: "session-last")],
        };

        return new TestRequisitionProcessor(lineService ?? new InMemoryOrderLineService(), snapshots);
    }

    private static PunchoutOrderRequestData CreateRequest(params (string LineNumber, string Sku, decimal Quantity, decimal Price, string LineItemId, string SessionId)[] items)
    {
        return new PunchoutOrderRequestData
        {
            PayloadId = "msg-1",
            OrderId = "PO-1",
            Currency = "USD",
            Items = items
                .Select(x => new PunchoutOrderRequestItem
                {
                    LineNumber = x.LineNumber,
                    SupplierPartId = x.Sku,
                    Quantity = x.Quantity,
                    UnitPrice = x.Price,
                    Currency = "USD",
                    LineItemId = x.LineItemId,
                    SessionId = x.SessionId,
                    SupplierPartAuxiliaryId = PunchoutItemReference.Format(x.LineItemId, x.SessionId),
                })
                .ToList(),
        };
    }

    private static PunchoutOrderLine CreateLine(string sessionId, string lineItemId, string purchaseOrderNumber)
    {
        return new PunchoutOrderLine
        {
            ConfigurationId = Configuration.Id,
            SessionId = sessionId,
            LineItemId = lineItemId,
            PurchaseOrderNumber = purchaseOrderNumber,
        };
    }

    private sealed class TestRequisitionProcessor(IPunchoutOrderLineService lineService, IDictionary<string, IList<PunchoutRequisitionItem>> snapshots)
        : PunchoutOrderRequestProcessor(null, null, null, null, null, null, null, null, null, lineService, null, NullLogger<PunchoutOrderRequestProcessor>.Instance)
    {
        public Task<string> ValidateRequisitionItemsPublicAsync(PunchoutOrderRequestData request, bool isNewOrder) =>
            ValidateRequisitionItemsAsync(request, Configuration, new PunchoutUserMapping { UserId = "user" }, isNewOrder);

        public Task<IList<PunchoutOrderLine>> AddOrderLinesPublicAsync(PunchoutOrderRequestData request, PunchoutOrderRequest orderRequest) =>
            AddOrderLinesAsync(request, orderRequest);

        public Task UpdateOrderLinesPublicAsync(PunchoutOrderRequestData request, PunchoutOrderRequest orderRequest, PunchoutOrderResult result, IList<PunchoutOrderLine> addedLines) =>
            UpdateOrderLinesAsync(request, orderRequest, result, addedLines);

        protected override Task<PunchoutSession> GetSessionAsync(string sessionId, PunchoutConfiguration settings, PunchoutUserMapping userMapping)
        {
            var id = string.IsNullOrEmpty(sessionId) ? "session-last" : sessionId;

            return Task.FromResult(snapshots.ContainsKey(id) ? new PunchoutSession { Id = id } : null);
        }

        protected override Task<IList<PunchoutRequisitionItem>> GetRequisitionItemsAsync(PunchoutSession session) =>
            Task.FromResult(snapshots[session.Id]);
    }

    private sealed class TestCartHandler()
        : DefaultPunchoutHandler(null, null, null, null, null, null, null, new SumTotalsCalculator(), null, null, null, NullLogger<DefaultPunchoutHandler>.Instance)
    {
        public Task<ShoppingCart> BuildCartPublicAsync(PunchoutOrderRequestHandlerContext context) =>
            BuildCartAsync(context);

        public PunchoutRequisitionItem BuildRequisitionItemPublic(ShoppingCart cart, LineItem lineItem) =>
            BuildRequisitionItem(cart, lineItem);
    }

    private sealed class SumTotalsCalculator : IShoppingCartTotalsCalculator
    {
        public void CalculateTotals(ShoppingCart cart)
        {
            cart.Total = cart.Items.Sum(x => x.PlacedPrice * x.Quantity) + cart.Shipments.Sum(x => x.Price);
        }
    }

    private sealed class InMemoryOrderLineService : IPunchoutOrderLineService
    {
        public List<PunchoutOrderLine> Lines { get; } = [];

        public Task<IList<PunchoutOrderLine>> GetByLineItemIdsAsync(string configurationId, IList<string> lineItemIds) =>
            Task.FromResult<IList<PunchoutOrderLine>>(Lines.Where(x => x.ConfigurationId == configurationId && lineItemIds.Contains(x.LineItemId)).ToList());

        public Task<IList<PunchoutOrderLine>> GetByPurchaseOrderAsync(string configurationId, string purchaseOrderNumber) =>
            Task.FromResult<IList<PunchoutOrderLine>>(Lines.Where(x => x.ConfigurationId == configurationId && x.PurchaseOrderNumber == purchaseOrderNumber).ToList());

        public Task<bool> TryAddAsync(IList<PunchoutOrderLine> lines)
        {
            var isOrdered = lines.Any(line => Lines.Any(x =>
                x.ConfigurationId == line.ConfigurationId && x.SessionId == line.SessionId && x.LineItemId == line.LineItemId));

            if (isOrdered)
            {
                return Task.FromResult(false);
            }

            foreach (var line in lines)
            {
                line.Id ??= Guid.NewGuid().ToString();
                Lines.Add(line);
            }

            return Task.FromResult(true);
        }

        public Task DeleteAsync(IList<string> ids)
        {
            Lines.RemoveAll(x => ids.Contains(x.Id));

            return Task.CompletedTask;
        }
    }
}
