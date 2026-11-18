using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Data.Services;
using VirtoCommerce.Punchout.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutOrderUpdateTests
{
    [Theory]
    [InlineData(null, "1", true)]
    [InlineData("2", null, true)]
    [InlineData("2", "1", true)]
    [InlineData("3", "1", true)]
    [InlineData("1", null, false)]
    [InlineData("2", "2", false)]
    [InlineData("1", "2", false)]
    [InlineData("v2", "1", false)]
    [InlineData("2", "v1", true)]
    public void ValidateOrderVersion_ComparesWithCurrentVersion(string version, string currentVersion, bool isValid)
    {
        var processor = new TestOrderRequestProcessor();
        var request = new PunchoutOrderRequestData { OrderVersion = version };
        var currentRequest = new PunchoutOrderRequest { OrderVersion = currentVersion };

        var error = processor.ValidateOrderVersionPublic(request, currentRequest);

        Assert.Equal(isValid, error is null);
    }

    [Fact]
    public void ReconcileOrder_MatchingLines_ReturnsNull()
    {
        var handler = new TestPunchoutHandler();
        var request = CreateRequest(("1", "SKU-1", 5m, 10m));

        Assert.Null(handler.ReconcileOrderPublic(request, CreateOrder()));
    }

    [Theory]
    [InlineData("3", "SKU-3", 1, 10, "not in the order and not in a punchout requisition")]
    [InlineData("1", "OTHER", 1, 10, "does not match the order item")]
    [InlineData("1", "SKU-1", 1, 11, "does not match the order price")]
    [InlineData("1", "SKU-1", 1.5, 10, "must be a whole number")]
    [InlineData("", "SKU-1", 1, 10, "lineNumber is missing")]
    public void ReconcileOrder_Mismatch_ReturnsError(string lineNumber, string sku, decimal quantity, decimal price, string expectedError)
    {
        var handler = new TestPunchoutHandler();
        var request = CreateRequest((lineNumber, sku, quantity, price));

        var error = handler.ReconcileOrderPublic(request, CreateOrder());

        Assert.Contains(expectedError, error);
    }

    [Theory]
    [InlineData(2, null)]
    [InlineData(3, "does not match the requisition quantity")]
    public void ReconcileOrder_AddedLine_ComparesRequisitionQuantity(decimal quantity, string expectedError)
    {
        var handler = new TestPunchoutHandler();
        var request = CreateRequest(("1", "SKU-1", 5m, 10m), ("3", "SKU-3", quantity, 7m));
        request.Items[1].RequisitionItem = CreateRequisitionItem("cart-line-3", "SKU-3", 2, 7m);

        var error = handler.ReconcileOrderPublic(request, CreateOrder());

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
    public void UpdateOrderItems_AddsRequisitionItemWithItsPrice()
    {
        var handler = new TestPunchoutHandler();
        var order = CreateOrder();
        var request = CreateRequest(("1", "SKU-1", 5m, 10m), ("2", "SKU-2", 1m, 20m), ("3", "SKU-3", 2m, 7m));
        request.Items[2].RequisitionItem = CreateRequisitionItem("cart-line-3", "SKU-3", 2, 7m, listPrice: 9m);

        handler.UpdateOrderItemsPublic(request, order);

        var lineItem = Assert.Single(order.Items, x => x.PurchaseOrderLineNumber == "3");
        Assert.Equal("SKU-3", lineItem.Sku);
        Assert.Equal("product-SKU-3", lineItem.ProductId);
        Assert.Equal(2, lineItem.Quantity);
        Assert.Equal(9m, lineItem.Price);
        Assert.Equal(2m, lineItem.DiscountAmount);
        Assert.Equal(7m, lineItem.PlacedPrice);

        var shipmentItem = Assert.Single(order.Shipments.Single().Items, x => x.LineItem == lineItem);
        Assert.Equal(2, shipmentItem.Quantity);
    }

    [Fact]
    public void ReconcileOrder_DuplicateLineNumber_ReturnsError()
    {
        var handler = new TestPunchoutHandler();
        var request = CreateRequest(("1", "SKU-1", 1m, 10m), ("1", "SKU-1", 2m, 10m));

        Assert.Contains("used by another line", handler.ReconcileOrderPublic(request, CreateOrder()));
    }

    [Fact]
    public void UpdateOrderItems_ChangesQuantityAndRemovesMissingLines()
    {
        var handler = new TestPunchoutHandler();
        var order = CreateOrder();
        var request = CreateRequest(("1", "SKU-1", 7m, 10m));

        handler.UpdateOrderItemsPublic(request, order);

        // Line 2 is removed, the gift without a line number is kept
        Assert.Equal(["line-1", "gift"], order.Items.Select(x => x.Id));
        Assert.Equal(7, order.Items.First().Quantity);

        var shipmentItem = Assert.Single(order.Shipments.Single().Items);
        Assert.Equal("line-1", shipmentItem.LineItemId);
        Assert.Equal(7, shipmentItem.Quantity);
    }

    [Fact]
    public void ValidateOrderUpdate_ChecksStatusCancellationAndPayments()
    {
        var handler = new TestPunchoutHandler();
        var context = new PunchoutOrderRequestHandlerContext
        {
            Request = new PunchoutOrderRequestData { OrderId = "PO-1" },
            Store = new() { Id = "store" },
            Configuration = new PunchoutConfiguration(),
        };

        var order = CreateOrder();
        Assert.Null(handler.ValidateOrderUpdatePublic(context, order));

        order.Status = "Completed";
        Assert.Contains("cannot be changed in status", handler.ValidateOrderUpdatePublic(context, order));

        context.Configuration.UpdatableOrderStatuses = ["Completed"];
        Assert.Null(handler.ValidateOrderUpdatePublic(context, order));

        order.InPayments.Single().PaymentStatus = PaymentStatus.Paid;
        Assert.Contains("payment is already processed", handler.ValidateOrderUpdatePublic(context, order));

        order.IsCancelled = true;
        Assert.Contains("is cancelled", handler.ValidateOrderUpdatePublic(context, order));
    }

    [Fact]
    public void CancelOrder_CancelsOrderAndRequestsPaymentCancellation()
    {
        var handler = new TestPunchoutHandler();
        var order = CreateOrder();
        order.InPayments.Add(new PaymentIn { IsCancelled = true, CancelledState = CancelledState.Completed });

        handler.CancelOrderPublic(new PunchoutOrderRequestData { PayloadId = "msg-003" }, order);

        Assert.True(order.IsCancelled);
        Assert.Equal("Cancelled", order.Status);
        Assert.NotNull(order.CancelledDate);
        Assert.Equal("msg-003", order.CancelReason);

        // Nothing is removed
        Assert.Equal(3, order.Items.Count);
        Assert.Equal([CancelledState.Requested, CancelledState.Completed], order.InPayments.Select(x => x.CancelledState));
    }

    [Fact]
    public void ValidateOrderCancellation_ChecksStatus()
    {
        var handler = new TestPunchoutHandler();
        var context = new PunchoutOrderRequestHandlerContext
        {
            Request = new PunchoutOrderRequestData { OrderId = "PO-1" },
            Configuration = new PunchoutConfiguration(),
        };

        var order = CreateOrder();
        // A paid order can be cancelled, the payment gets a cancellation request
        order.InPayments.Single().PaymentStatus = PaymentStatus.Paid;
        Assert.Null(handler.ValidateOrderCancellationPublic(context, order));

        order.Status = "Shipped";
        Assert.Contains("cannot be cancelled in status", handler.ValidateOrderCancellationPublic(context, order));
    }

    [Theory]
    [InlineData(null, "update", "new order")]
    [InlineData("order", "update", "update")]
    [InlineData(null, "delete", "no order")]
    [InlineData("order", "delete", "update")]
    public async Task ValidateUpdate_WithoutProcessedVersion_UpdateIsCreatedAsNewOrder(string customerOrderId, string orderType, string expected)
    {
        var currentRequest = customerOrderId is null ? null : new PunchoutOrderRequest { Id = "current", CustomerOrderId = customerOrderId };
        var processor = new TestUpdateValidationProcessor(currentRequest);
        var request = new PunchoutOrderRequestData { OrderId = "PO-1", PayloadId = "msg-2", OrderType = orderType, OrderVersion = "2" };

        var result = await processor.ValidateUpdatePublicAsync(request);

        switch (expected)
        {
            case "new order":
                Assert.True(processor.NewOrderValidated);
                Assert.True(result.IsValid);
                Assert.Null(result.PreviousRequest);
                break;
            case "update":
                Assert.False(processor.NewOrderValidated);
                Assert.True(result.IsValid);
                Assert.Same(currentRequest, result.PreviousRequest);
                break;
            default:
                Assert.False(processor.NewOrderValidated);
                Assert.True(result.IsValid);
                Assert.Null(result.PreviousRequest);
                break;
        }
    }

    [Theory]
    [InlineData("update", true, "update")]
    [InlineData("update", false, "create")]
    [InlineData("delete", true, "cancel")]
    [InlineData("new", false, "create")]
    [InlineData(null, false, "create")]
    public async Task HandleOrderRequest_RoutesByTypeAndPreviousRequest(string orderType, bool hasPreviousRequest, string expected)
    {
        var handler = new TestRoutingHandler();
        var context = new PunchoutOrderRequestHandlerContext
        {
            Request = new PunchoutOrderRequestData { OrderType = orderType },
            PreviousRequest = hasPreviousRequest ? new PunchoutOrderRequest() : null,
        };

        await handler.HandleOrderRequestAsync(context);

        Assert.Equal(expected, handler.Route);
    }

    [Fact]
    public async Task CancelOrder_WithoutCustomerOrder_SucceedsWithoutChanges()
    {
        var handler = new TestPunchoutHandler();
        var context = new PunchoutOrderRequestHandlerContext
        {
            Request = new PunchoutOrderRequestData { OrderId = "PO-1", OrderType = "delete" },
            Result = new PunchoutOrderResult(),
        };

        await handler.HandleOrderRequestAsync(context);

        Assert.False(context.IsFailed);
        Assert.Null(context.Result.CustomerOrderId);
        Assert.Equal("PO-1", context.Result.PurchaseOrderNumber);
    }

    private static PunchoutOrderRequestData CreateRequest(params (string LineNumber, string Sku, decimal Quantity, decimal Price)[] items)
    {
        return new PunchoutOrderRequestData
        {
            OrderId = "PO-1",
            Currency = "USD",
            Items = items
                .Select(x => new PunchoutOrderRequestItem { LineNumber = x.LineNumber, SupplierPartId = x.Sku, Quantity = x.Quantity, UnitPrice = x.Price })
                .ToList(),
        };
    }

    internal static PunchoutRequisitionItem CreateRequisitionItem(string lineItemId, string sku, int quantity, decimal price, decimal? listPrice = null, string sessionId = "session-1")
    {
        return new PunchoutRequisitionItem
        {
            SessionId = sessionId,
            LineItemId = lineItemId,
            ProductId = $"product-{sku}",
            CatalogId = "catalog",
            Sku = sku,
            Name = $"Product {sku}",
            Quantity = quantity,
            Currency = "USD",
            ListPrice = listPrice ?? price,
            DiscountAmount = (listPrice ?? price) - price,
            PlacedPrice = price,
        };
    }

    private static CustomerOrder CreateOrder()
    {
        return new CustomerOrder
        {
            Id = "order",
            StoreId = "store",
            Status = "New",
            Currency = "USD",
            Items = new List<LineItem>
            {
                new() { Id = "line-1", Sku = "SKU-1", Quantity = 5, PlacedPrice = 10m, PurchaseOrderLineNumber = "1" },
                new() { Id = "line-2", Sku = "SKU-2", Quantity = 1, PlacedPrice = 20m, PurchaseOrderLineNumber = "2" },
                new() { Id = "gift", Sku = "GIFT", Quantity = 1, PlacedPrice = 0m, IsGift = true },
            },
            Shipments = new List<Shipment>
            {
                new()
                {
                    Items = new List<ShipmentItem>
                    {
                        new() { Id = "shipment-item-1", LineItemId = "line-1", Quantity = 5 },
                        new() { Id = "shipment-item-2", LineItemId = "line-2", Quantity = 1 },
                    },
                },
            },
            InPayments = new List<PaymentIn>
            {
                new() { PaymentStatus = PaymentStatus.New },
            },
        };
    }

    private sealed class TestOrderRequestProcessor()
        : PunchoutOrderRequestProcessor(null, null, null, null, null, null, null, null, null, null, null, null)
    {
        public string ValidateOrderVersionPublic(PunchoutOrderRequestData request, PunchoutOrderRequest currentRequest) =>
            ValidateOrderVersion(request, currentRequest);
    }

    private sealed class TestUpdateValidationProcessor(PunchoutOrderRequest currentRequest)
        : PunchoutOrderRequestProcessor(null, null, null, null, null, null, null, null, null, null, null, NullLogger<PunchoutOrderRequestProcessor>.Instance)
    {
        public bool NewOrderValidated { get; private set; }

        public Task<PunchoutOrderValidationResult> ValidateUpdatePublicAsync(PunchoutOrderRequestData request) =>
            ValidateUpdateAsync(request, new PunchoutConfiguration { Id = "configuration" }, new PunchoutUserMapping(), null);

        protected override Task<bool> IsOrderInProgressAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration) =>
            Task.FromResult(false);

        protected override Task<string> ValidateDocumentReferenceAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration) =>
            Task.FromResult<string>(null);

        protected override Task<PunchoutOrderRequest> FindCurrentOrderVersionAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration) =>
            Task.FromResult(currentRequest);

        protected override Task<PunchoutOrderValidationResult> ValidateNewOrderAsync(PunchoutOrderRequestData request, PunchoutConfiguration settings,
            PunchoutUserMapping userMapping, VirtoCommerce.StoreModule.Core.Model.Store store)
        {
            NewOrderValidated = true;

            return Task.FromResult(PunchoutOrderValidationResult.Valid(settings, userMapping, store));
        }

        protected override Task<string> ValidateRequisitionItemsAsync(PunchoutOrderRequestData request, PunchoutConfiguration configuration,
            PunchoutUserMapping userMapping, bool isNewOrder) =>
            Task.FromResult<string>(null);
    }

    private sealed class TestRoutingHandler()
        : DefaultPunchoutHandler(null, null, null, null, null, null, null, null, null, null, null, null)
    {
        public string Route { get; private set; }

        protected override Task CreateOrderFromRequestAsync(PunchoutOrderRequestHandlerContext context)
        {
            Route = "create";
            return Task.CompletedTask;
        }

        protected override Task UpdateOrderAsync(PunchoutOrderRequestHandlerContext context)
        {
            Route = "update";
            return Task.CompletedTask;
        }

        protected override Task CancelOrderAsync(PunchoutOrderRequestHandlerContext context)
        {
            Route = "cancel";
            return Task.CompletedTask;
        }
    }

    private sealed class TestPunchoutHandler()
        : DefaultPunchoutHandler(null, null, null, null, null, null, null, null, null, null, null, NullLogger<DefaultPunchoutHandler>.Instance)
    {
        public string ReconcileOrderPublic(PunchoutOrderRequestData request, CustomerOrder order) =>
            ReconcileOrder(request, order);

        public void UpdateOrderItemsPublic(PunchoutOrderRequestData request, CustomerOrder order) =>
            UpdateOrderItems(request, order);

        public string ValidateOrderUpdatePublic(PunchoutOrderRequestHandlerContext context, CustomerOrder order) =>
            ValidateOrderUpdate(context, order);

        public string ValidateOrderCancellationPublic(PunchoutOrderRequestHandlerContext context, CustomerOrder order) =>
            ValidateOrderCancellation(context, order);

        public void CancelOrderPublic(PunchoutOrderRequestData request, CustomerOrder order) =>
            CancelOrder(request, order);
    }
}
