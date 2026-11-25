using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Punchout.Data.Services;
using VirtoCommerce.Punchout.ExperienceApi.Services;
using VirtoCommerce.Punchout.Tests.Fakes;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutOrganizationOrderTests
{
    private const string OrganizationId = "org-1";
    private const string Email = "buyer@acme.com";

    private static readonly PunchoutConfiguration Configuration = new() { Id = "configuration", StoreId = "store" };

    private static readonly ApplicationUser EndUser = new() { Id = "user-2", UserName = "buyer2", MemberId = "contact-2" };

    // End user of the order

    [Fact]
    public async Task ResolveCustomer_UserMapping_MappedUser()
    {
        var resolver = new FakeResolver(PunchoutOrganizationUserResult.NotFound());
        var processor = new TestOrderProcessor(resolver);
        var mapping = new PunchoutUserMapping { ExternalId = "sender", UserId = "user-1", UserName = "buyer1", MemberId = "contact-1" };

        var result = await processor.ResolveCustomerPublicAsync(CreateRequest(), mapping);

        Assert.True(result.IsValid);
        Assert.Equal("user-1", result.Customer.UserId);
        Assert.Equal("buyer1", result.Customer.UserName);
        Assert.Equal("contact-1", result.Customer.MemberId);
        Assert.Null(result.Customer.OrganizationId);
        Assert.Null(resolver.Email);
    }

    [Fact]
    public async Task ResolveCustomer_EndUserFound_EndUserOfOrganization()
    {
        var resolver = new FakeResolver(PunchoutOrganizationUserResult.Found(EndUser));
        var processor = new TestOrderProcessor(resolver);

        var result = await processor.ResolveCustomerPublicAsync(CreateRequest(), CreateOrganizationMapping());

        Assert.True(result.IsValid);
        Assert.Equal(EndUser.Id, result.Customer.UserId);
        Assert.Equal(EndUser.UserName, result.Customer.UserName);
        Assert.Equal(EndUser.MemberId, result.Customer.MemberId);
        Assert.Equal(OrganizationId, result.Customer.OrganizationId);

        Assert.Equal(OrganizationId, resolver.OrganizationId);
        Assert.Equal(Email, resolver.Email);
        Assert.Equal(Configuration.StoreId, resolver.StoreId);
    }

    [Fact]
    public async Task ResolveCustomer_EmailInExtrinsic_Used()
    {
        var resolver = new FakeResolver(PunchoutOrganizationUserResult.Found(EndUser));
        var processor = new TestOrderProcessor(resolver);
        var request = CreateRequest(email: null);
        request.Extrinsics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CxmlConstants.ExtrinsicName.UserEmail] = " " + Email + " ",
        };

        var result = await processor.ResolveCustomerPublicAsync(request, CreateOrganizationMapping());

        Assert.True(result.IsValid);
        Assert.Equal(Email, resolver.Email);
    }

    [Fact]
    public async Task ResolveCustomer_NoEmail_RejectedAndStored()
    {
        var processor = new TestOrderProcessor(new FakeResolver(PunchoutOrganizationUserResult.Found(EndUser)));

        var result = await processor.ResolveCustomerPublicAsync(CreateRequest(email: null), CreateOrganizationMapping());

        AssertRejected(result);
    }

    [Fact]
    public async Task ResolveCustomer_EndUserNotFound_Rejected()
    {
        // Also covers an end user of another organization: the resolver looks only in the mapped one
        var processor = new TestOrderProcessor(new FakeResolver(PunchoutOrganizationUserResult.NotFound()));

        var result = await processor.ResolveCustomerPublicAsync(CreateRequest(), CreateOrganizationMapping());

        AssertRejected(result);
        Assert.Contains(Email, result.Error.ErrorMessage);
    }

    [Fact]
    public async Task ResolveCustomer_EndUserAmbiguous_Rejected()
    {
        var processor = new TestOrderProcessor(new FakeResolver(PunchoutOrganizationUserResult.Ambiguous(2)));

        var result = await processor.ResolveCustomerPublicAsync(CreateRequest(), CreateOrganizationMapping());

        AssertRejected(result);
    }

    [Fact]
    public void CreateOrderRequest_StoresTheEndUser()
    {
        var processor = new TestOrderProcessor(null);
        var validationResult = PunchoutOrderValidationResult.Valid(Configuration, CreateOrganizationMapping(), new Store { Id = "store" });
        validationResult.Customer = new PunchoutOrderCustomer { UserId = EndUser.Id, OrganizationId = OrganizationId };

        var orderRequest = processor.CreateOrderRequestPublic(CreateRequest(), validationResult);

        Assert.Equal(EndUser.Id, orderRequest.UserId);
        Assert.Equal("sender", orderRequest.SenderIdentity);
    }

    // Requisition items

    [Fact]
    public void ReturnedItemsCriteria_OrganizationMapping_SessionsOfOrganization()
    {
        var service = new TestRequisitionService();
        var context = CreateContext(CreateOrganizationMapping());

        var criteria = service.CreateReturnedItemsCriteriaPublic(context, ["line-1"]);

        Assert.Equal(OrganizationId, criteria.OrganizationId);
        Assert.Null(criteria.UserId);
        Assert.Equal(Configuration.Id, criteria.ConfigurationId);
    }

    [Fact]
    public void ReturnedItemsCriteria_UserMapping_SessionsOfUser()
    {
        var service = new TestRequisitionService();
        var context = CreateContext(new PunchoutUserMapping { UserId = "user-1" });

        var criteria = service.CreateReturnedItemsCriteriaPublic(context, ["line-1"]);

        Assert.Equal("user-1", criteria.UserId);
        Assert.Null(criteria.OrganizationId);
    }

    // New order

    [Fact]
    public async Task BuildCart_OrganizationMapping_EndUserAndMappedOrganization()
    {
        // The contact is in two organizations, the order takes the mapped one
        var contact = new Contact { Id = EndUser.MemberId, FullName = "Jane Buyer", Organizations = ["org-0", OrganizationId] };
        var organization = new Organization { Id = OrganizationId, Name = "Acme" };
        var service = new TestCustomerOrderService(MemberServiceProxy.Create(contact, organization));

        var context = CreateContext(CreateOrganizationMapping());
        context.Customer = new PunchoutOrderCustomer { UserId = EndUser.Id, UserName = EndUser.UserName, MemberId = EndUser.MemberId, OrganizationId = OrganizationId };

        var cart = await service.BuildCartPublicAsync(context);

        Assert.Equal(EndUser.Id, cart.CustomerId);
        Assert.Equal("Jane Buyer", cart.CustomerName);
        Assert.Equal(OrganizationId, cart.OrganizationId);
        Assert.Equal("Acme", cart.OrganizationName);
    }

    [Fact]
    public async Task BuildCart_UserMapping_FirstOrganizationOfContact()
    {
        var contact = new Contact { Id = "contact-1", FullName = "John Buyer", Organizations = ["org-0", OrganizationId] };
        var organization = new Organization { Id = "org-0", Name = "First" };
        var service = new TestCustomerOrderService(MemberServiceProxy.Create(contact, organization));

        var context = CreateContext(new PunchoutUserMapping { UserId = "user-1", MemberId = "contact-1" });
        context.Customer = new PunchoutOrderCustomer { UserId = "user-1", UserName = "buyer1", MemberId = "contact-1" };

        var cart = await service.BuildCartPublicAsync(context);

        Assert.Equal("user-1", cart.CustomerId);
        Assert.Equal("org-0", cart.OrganizationId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateOrder_CustomerNotResolved_ConfigurationError(bool emptyCustomer)
    {
        // An override of ValidateAsync or CreateHandlerContext that does not call the base leaves the customer empty
        var service = new TestCustomerOrderService(MemberServiceProxy.Create());

        var context = CreateContext(CreateOrganizationMapping());
        context.Customer = emptyCustomer ? new PunchoutOrderCustomer() : null;

        await service.CreateOrderAsync(context);

        Assert.True(context.IsFailed);
        Assert.Equal(PunchoutOrderStatus.ConfigurationError, context.ErrorCode);
        Assert.Equal("The customer of the order is not resolved.", context.ErrorMessage);
    }

    // Update and delete

    [Fact]
    public void ValidateOrderOrganization_OrderOfAnotherOrganization_Rejected()
    {
        var service = new TestCustomerOrderService(null);
        var context = CreateContext(CreateOrganizationMapping());

        var error = service.ValidateOrderOrganizationPublic(context, CreateOrder("org-2"));

        Assert.Contains("another organization", error);
    }

    [Fact]
    public void ValidateOrderOrganization_OrderOfMappedOrganization_Allowed()
    {
        var service = new TestCustomerOrderService(null);
        var context = CreateContext(CreateOrganizationMapping());

        Assert.Null(service.ValidateOrderOrganizationPublic(context, CreateOrder(OrganizationId)));
    }

    [Fact]
    public void ValidateOrderOrganization_UserMapping_NotChecked()
    {
        var service = new TestCustomerOrderService(null);
        var context = CreateContext(new PunchoutUserMapping { UserId = "user-1" });

        Assert.Null(service.ValidateOrderOrganizationPublic(context, CreateOrder("org-2")));
    }

    [Fact]
    public void ValidateOrderUpdate_OrderOfAnotherOrganization_Rejected()
    {
        var service = new TestCustomerOrderService(null);
        var context = CreateContext(CreateOrganizationMapping());

        var error = service.ValidateOrderUpdatePublic(context, CreateOrder("org-2"));

        Assert.Contains("another organization", error);
    }

    [Fact]
    public async Task CancelOrder_OrderOfAnotherOrganization_Rejected()
    {
        var order = CreateOrder("org-2");
        var service = new TestCustomerOrderService(null, CustomerOrderServiceProxy.Create(order));
        var context = CreateContext(CreateOrganizationMapping());
        context.PreviousRequest = new PunchoutOrderRequest { CustomerOrderId = order.Id };

        await service.CancelOrderAsync(context);

        Assert.True(context.IsFailed);
        Assert.Equal(PunchoutOrderStatus.InvalidRequest, context.ErrorCode);
        Assert.False(order.IsCancelled);
    }

    private static void AssertRejected(PunchoutOrderValidationResult result)
    {
        Assert.False(result.IsValid);
        Assert.Equal(PunchoutOrderStatus.InvalidRequest, result.Error.Status);
        // The configuration is known, so the request is stored as Rejected
        Assert.Same(Configuration, result.Configuration);
        Assert.Null(result.Customer);
    }

    private static PunchoutOrderRequestData CreateRequest(string email = Email) => new()
    {
        PayloadId = "payload-1",
        Sender = "sender",
        OrderId = "PO-1",
        OrderType = CxmlConstants.OrderRequestType.New,
        Currency = "USD",
        User = email is null ? null : new PunchoutUserContext { Name = "Buyer", Email = email },
    };

    private static PunchoutUserMapping CreateOrganizationMapping() => new()
    {
        Id = "mapping-1",
        IsActive = true,
        ExternalId = "sender",
        OrganizationId = OrganizationId,
    };

    private static PunchoutOrderRequestHandlerContext CreateContext(PunchoutUserMapping mapping) => new()
    {
        Request = CreateRequest(),
        OrderRequestId = "request",
        Configuration = Configuration,
        Store = new Store { Id = "store", DefaultLanguage = "en-US" },
        UserMapping = mapping,
        Result = new PunchoutOrderResult(),
    };

    private static CustomerOrder CreateOrder(string organizationId) => new()
    {
        Id = "order-1",
        StoreId = "store",
        Status = "New",
        CustomerId = "user-1",
        OrganizationId = organizationId,
    };

    private sealed class TestOrderProcessor(IPunchoutOrganizationUserResolver resolver)
        : PunchoutOrderRequestProcessor(null, null, null, null, null, null, null, resolver, NullLogger<PunchoutOrderRequestProcessor>.Instance)
    {
        public Task<PunchoutOrderValidationResult> ResolveCustomerPublicAsync(PunchoutOrderRequestData request, PunchoutUserMapping mapping) =>
            ResolveCustomerAsync(request, Configuration, mapping, new Store { Id = "store" });

        public PunchoutOrderRequest CreateOrderRequestPublic(PunchoutOrderRequestData request, PunchoutOrderValidationResult validationResult) =>
            CreateOrderRequest(request, validationResult, null);
    }

    private sealed class TestRequisitionService()
        : PunchoutRequisitionService(null, null, null, NullLogger<PunchoutRequisitionService>.Instance)
    {
        public PunchoutReturnedItemsCriteria CreateReturnedItemsCriteriaPublic(PunchoutOrderRequestHandlerContext context, IList<string> lineItemIds) =>
            CreateReturnedItemsCriteria(context, lineItemIds);
    }

    private sealed class TestCustomerOrderService(
        VirtoCommerce.CustomerModule.Core.Services.IMemberService memberService,
        VirtoCommerce.OrdersModule.Core.Services.ICustomerOrderService customerOrderService = null)
        : PunchoutCustomerOrderService(customerOrderService, null, null, new NoTotalsCalculator(), null, null, memberService, NullLogger<PunchoutCustomerOrderService>.Instance)
    {
        public Task<ShoppingCart> BuildCartPublicAsync(PunchoutOrderRequestHandlerContext context) =>
            BuildCartAsync(context);

        public string ValidateOrderOrganizationPublic(PunchoutOrderRequestHandlerContext context, CustomerOrder order) =>
            ValidateOrderOrganization(context, order);

        public string ValidateOrderUpdatePublic(PunchoutOrderRequestHandlerContext context, CustomerOrder order) =>
            ValidateOrderUpdate(context, order);
    }

    private sealed class NoTotalsCalculator : IShoppingCartTotalsCalculator
    {
        public void CalculateTotals(ShoppingCart cart)
        {
            cart.Total = cart.Items.Sum(x => x.PlacedPrice * x.Quantity);
        }
    }

    private sealed class FakeResolver(PunchoutOrganizationUserResult result) : IPunchoutOrganizationUserResolver
    {
        public string OrganizationId { get; private set; }

        public string Email { get; private set; }

        public string StoreId { get; private set; }

        public Task<PunchoutOrganizationUserResult> ResolveAsync(PunchoutOrganizationUserRequest request)
        {
            OrganizationId = request.OrganizationId;
            Email = request.Email;
            StoreId = request.StoreId;

            return Task.FromResult(result);
        }

        public Task<bool> IsOrganizationUserAsync(ApplicationUser user, string organizationId) =>
            throw new NotSupportedException();
    }
}
