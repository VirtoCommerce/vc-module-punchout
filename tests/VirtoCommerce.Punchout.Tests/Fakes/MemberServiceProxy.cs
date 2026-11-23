using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;

namespace VirtoCommerce.Punchout.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IMemberService"/> supporting GetByIdAsync and GetByIdsAsync.
/// </summary>
public class MemberServiceProxy : DispatchProxy
{
    public IList<Member> Members { get; } = [];

    public static IMemberService Create(params Member[] members)
    {
        var service = DispatchProxy.Create<IMemberService, MemberServiceProxy>();

        foreach (var member in members)
        {
            ((MemberServiceProxy)(object)service).Members.Add(member);
        }

        return service;
    }

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        switch (targetMethod?.Name)
        {
            case nameof(IMemberService.GetByIdAsync):
                {
                    var id = (string)args[0];
                    return Task.FromResult(Members.FirstOrDefault(x => x.Id == id));
                }
            case nameof(IMemberService.GetByIdsAsync):
                {
                    var ids = (IEnumerable<string>)args[0];
                    var found = Members.Where(x => ids.Contains(x.Id)).ToArray();
                    return Task.FromResult(found);
                }
            default:
                throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
