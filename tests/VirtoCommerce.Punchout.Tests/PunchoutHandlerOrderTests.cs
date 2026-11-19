using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Services;
using VirtoCommerce.Punchout.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

/// <summary>
/// The handler matches and reserves the requisition items around the change of the customer order.
/// </summary>
[Trait("Category", "Unit")]
public class PunchoutHandlerOrderTests
{
    [Fact]
    public async Task CreateOrder_Success_KeepsTheReservedItems()
    {
        var requisitionService = new FakeRequisitionService();
        var handler = CreateHandler(requisitionService, new FakeCustomerOrderService());

        var context = CreateContext("new");
        await handler.HandleOrderRequestAsync(context);

        Assert.False(context.IsFailed);
        Assert.Equal(["match:new", "reserve"], requisitionService.Calls);
    }

    [Fact]
    public async Task CreateOrder_RequisitionMismatch_IsRejectedWithoutReservation()
    {
        var requisitionService = new FakeRequisitionService { MatchError = "Line 1: unit price 11.00 does not match the requisition price 10.00." };
        var orderService = new FakeCustomerOrderService();
        var handler = CreateHandler(requisitionService, orderService);

        var context = CreateContext("new");
        await handler.HandleOrderRequestAsync(context);

        Assert.Equal(PunchoutOrderStatus.InvalidRequest, context.ErrorCode);
        Assert.Equal(requisitionService.MatchError, context.ErrorMessage);
        Assert.Equal(["match:new"], requisitionService.Calls);
        Assert.Empty(orderService.Calls);
    }

    [Fact]
    public async Task CreateOrder_ItemReservedByConcurrentPurchaseOrder_IsRejected()
    {
        var requisitionService = new FakeRequisitionService { ReserveConflict = true };
        var orderService = new FakeCustomerOrderService();
        var handler = CreateHandler(requisitionService, orderService);

        var context = CreateContext("new");
        await handler.HandleOrderRequestAsync(context);

        Assert.Equal(PunchoutOrderStatus.InvalidRequest, context.ErrorCode);
        Assert.Contains("already ordered by another purchase order", context.ErrorMessage);
        Assert.Empty(orderService.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateOrder_Failure_ReleasesTheReservedItems(bool throws)
    {
        var requisitionService = new FakeRequisitionService();
        var handler = CreateHandler(requisitionService, new FakeCustomerOrderService { Fails = !throws, Throws = throws });

        var context = CreateContext("new");

        if (throws)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleOrderRequestAsync(context));
        }
        else
        {
            await handler.HandleOrderRequestAsync(context);
            Assert.True(context.IsFailed);
        }

        Assert.Equal(["match:new", "reserve", "release:reserved"], requisitionService.Calls);
    }

    [Fact]
    public async Task UpdateOrder_Success_ReleasesTheRemovedItems()
    {
        var requisitionService = new FakeRequisitionService();
        var orderService = new FakeCustomerOrderService();
        var handler = CreateHandler(requisitionService, orderService);

        var context = CreateContext("update", hasPreviousRequest: true);
        await handler.HandleOrderRequestAsync(context);

        Assert.False(context.IsFailed);
        Assert.Equal(["update"], orderService.Calls);
        Assert.Equal(["match:update", "reserve", "release:removed"], requisitionService.Calls);
    }

    [Fact]
    public async Task UpdateOrder_Failure_ReleasesOnlyTheAddedItems()
    {
        var requisitionService = new FakeRequisitionService();
        var handler = CreateHandler(requisitionService, new FakeCustomerOrderService { Fails = true });

        var context = CreateContext("update", hasPreviousRequest: true);
        await handler.HandleOrderRequestAsync(context);

        Assert.True(context.IsFailed);
        Assert.Equal(["match:update", "reserve", "release:reserved"], requisitionService.Calls);
    }

    [Fact]
    public async Task UpdateWithoutPreviousVersion_IsCreatedAsNewOrder()
    {
        var requisitionService = new FakeRequisitionService();
        var orderService = new FakeCustomerOrderService();
        var handler = CreateHandler(requisitionService, orderService);

        await handler.HandleOrderRequestAsync(CreateContext("update"));

        Assert.Equal(["create"], orderService.Calls);
        Assert.Equal(["match:new", "reserve"], requisitionService.Calls);
    }

    [Theory]
    [InlineData(false, new[] { "release:purchase-order" })]
    [InlineData(true, new string[0])]
    public async Task CancelOrder_ReleasesThePurchaseOrderItemsWhenCancelled(bool fails, string[] expectedCalls)
    {
        var requisitionService = new FakeRequisitionService();
        var handler = CreateHandler(requisitionService, new FakeCustomerOrderService { Fails = fails });

        await handler.HandleOrderRequestAsync(CreateContext("delete", hasPreviousRequest: true));

        Assert.Equal(expectedCalls, requisitionService.Calls);
    }

    [Fact]
    public async Task UpdateOrder_ReleaseFailure_DoesNotChangeTheResult()
    {
        var requisitionService = new FakeRequisitionService { ReleaseThrows = true };
        var handler = CreateHandler(requisitionService, new FakeCustomerOrderService());

        var context = CreateContext("update", hasPreviousRequest: true);
        await handler.HandleOrderRequestAsync(context);

        Assert.False(context.IsFailed);
    }

    private static DefaultPunchoutHandler CreateHandler(IPunchoutRequisitionService requisitionService, IPunchoutCustomerOrderService orderService)
    {
        return new DefaultPunchoutHandler(null, null, null, null, requisitionService, orderService, NullLogger<DefaultPunchoutHandler>.Instance);
    }

    private static PunchoutOrderRequestHandlerContext CreateContext(string orderType, bool hasPreviousRequest = false)
    {
        return new PunchoutOrderRequestHandlerContext
        {
            Request = new PunchoutOrderRequestData { OrderId = "PO-1", PayloadId = "msg-1", OrderType = orderType },
            OrderRequestId = "request",
            Configuration = new PunchoutConfiguration { Id = "configuration" },
            PreviousRequest = hasPreviousRequest ? new PunchoutOrderRequest { CustomerOrderId = "order" } : null,
            Result = new PunchoutOrderResult(),
        };
    }

    private sealed class FakeRequisitionService : IPunchoutRequisitionService
    {
        private readonly IList<PunchoutOrderLine> _reservedLines = [new PunchoutOrderLine { Id = "reserved" }];

        public string MatchError { get; init; }

        public bool ReserveConflict { get; init; }

        public bool ReleaseThrows { get; init; }

        public List<string> Calls { get; } = [];

        public Task<string> MatchItemsAsync(PunchoutOrderRequestHandlerContext context, bool isNewOrder)
        {
            Calls.Add(isNewOrder ? "match:new" : "match:update");
            return Task.FromResult(MatchError);
        }

        public Task<IList<PunchoutOrderLine>> ReserveItemsAsync(PunchoutOrderRequestHandlerContext context)
        {
            Calls.Add("reserve");
            return Task.FromResult(ReserveConflict ? null : _reservedLines);
        }

        public Task ReleaseItemsAsync(IList<PunchoutOrderLine> lines)
        {
            Assert.Same(_reservedLines, lines);
            return Release("release:reserved");
        }

        public Task ReleaseRemovedItemsAsync(PunchoutOrderRequestHandlerContext context) => Release("release:removed");

        public Task ReleasePurchaseOrderItemsAsync(PunchoutOrderRequestHandlerContext context) => Release("release:purchase-order");

        private Task Release(string call)
        {
            Calls.Add(call);
            return ReleaseThrows ? throw new InvalidOperationException("The database is unavailable.") : Task.CompletedTask;
        }
    }

    private sealed class FakeCustomerOrderService : IPunchoutCustomerOrderService
    {
        public bool Fails { get; init; }

        public bool Throws { get; init; }

        public List<string> Calls { get; } = [];

        public Task CreateOrderAsync(PunchoutOrderRequestHandlerContext context) => Run(context, "create");

        public Task UpdateOrderAsync(PunchoutOrderRequestHandlerContext context) => Run(context, "update");

        public Task CancelOrderAsync(PunchoutOrderRequestHandlerContext context) => Run(context, "cancel");

        private Task Run(PunchoutOrderRequestHandlerContext context, string call)
        {
            Calls.Add(call);

            if (Throws)
            {
                throw new InvalidOperationException("The order cannot be saved.");
            }

            if (Fails)
            {
                context.Fail(PunchoutOrderStatus.InvalidRequest, "The order cannot be changed.");
            }
            else
            {
                context.Result.CustomerOrderId = "order";
            }

            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// The cart is cleared by the handler that implements the optional interface, after the requisition is returned.
/// </summary>
[Trait("Category", "Unit")]
public class PunchoutOrderMessageReturnedTests
{
    [Fact]
    public async Task CompleteOrderMessage_CallsHandlerThatImplementsReturnedInterface()
    {
        var handler = new ReturnedHandler();

        await new TestOrderMessageProcessor().CompletePublicAsync(handler, new PunchoutOrderMessageHandlerContext());

        Assert.True(handler.IsCalled);
    }

    [Fact]
    public async Task CompleteOrderMessage_HandlerWithoutReturnedInterface_IsSkipped()
    {
        await new TestOrderMessageProcessor().CompletePublicAsync(new PlainHandler(), new PunchoutOrderMessageHandlerContext());
    }

    [Fact]
    public async Task CompleteOrderMessage_Failure_IsLoggedOnly()
    {
        var context = new PunchoutOrderMessageHandlerContext { Session = new PunchoutSession { Id = "session" } };

        await new TestOrderMessageProcessor().CompletePublicAsync(new ReturnedHandler { Throws = true }, context);
    }

    private sealed class TestOrderMessageProcessor()
        : PunchoutOrderMessageProcessor(null, null, null, null, null, null, NullLogger<PunchoutOrderMessageProcessor>.Instance)
    {
        public Task CompletePublicAsync(IPunchoutHandler handler, PunchoutOrderMessageHandlerContext context) =>
            CompleteOrderMessageAsync(handler, context);
    }

    private class PlainHandler : IPunchoutHandler
    {
        public Task HandleSetupAsync(PunchoutSetupHandlerContext context) => Task.CompletedTask;

        public Task HandleOrderMessageAsync(PunchoutOrderMessageHandlerContext context) => Task.CompletedTask;

        public Task HandleOrderRequestAsync(PunchoutOrderRequestHandlerContext context) => Task.CompletedTask;
    }

    private sealed class ReturnedHandler : PlainHandler, IPunchoutOrderMessageReturnedHandler
    {
        public bool Throws { get; init; }

        public bool IsCalled { get; private set; }

        public Task HandleOrderMessageReturnedAsync(PunchoutOrderMessageHandlerContext context)
        {
            IsCalled = true;
            return Throws ? throw new InvalidOperationException("The cart cannot be saved.") : Task.CompletedTask;
        }
    }
}
