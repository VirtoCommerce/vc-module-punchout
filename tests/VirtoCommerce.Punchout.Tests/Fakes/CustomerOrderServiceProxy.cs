using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;

namespace VirtoCommerce.Punchout.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ICustomerOrderService"/> supporting GetAsync and SaveChangesAsync.
/// </summary>
public class CustomerOrderServiceProxy : DispatchProxy
{
    public IList<CustomerOrder> Orders { get; } = [];

    public int SaveCount { get; private set; }

    public static ICustomerOrderService Create(params CustomerOrder[] orders)
    {
        var service = DispatchProxy.Create<ICustomerOrderService, CustomerOrderServiceProxy>();

        foreach (var order in orders)
        {
            ((CustomerOrderServiceProxy)(object)service).Orders.Add(order);
        }

        return service;
    }

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        switch (targetMethod?.Name)
        {
            case nameof(ICustomerOrderService.GetAsync):
                {
                    var ids = (IList<string>)args[0];
                    IList<CustomerOrder> found = Orders.Where(x => ids.Contains(x.Id)).ToList();
                    return Task.FromResult(found);
                }
            case nameof(ICustomerOrderService.SaveChangesAsync):
                SaveCount++;
                return Task.CompletedTask;
            default:
                throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
