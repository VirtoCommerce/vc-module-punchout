using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.Punchout.ExperienceApi.Queries;

public class GetPunchoutCheckoutQueryHandler(IPunchoutOrderMessageService orderMessageService)
    : IQueryHandler<GetPunchoutCheckoutQuery, PunchoutCheckoutResult>
{
    public virtual Task<PunchoutCheckoutResult> Handle(GetPunchoutCheckoutQuery request, CancellationToken cancellationToken)
    {
        return orderMessageService.ProcessAsync(CreateRequest(request));
    }

    protected virtual PunchoutOrderMessageRequest CreateRequest(GetPunchoutCheckoutQuery query)
    {
        var request = AbstractTypeFactory<PunchoutOrderMessageRequest>.TryCreateInstance();

        request.SessionId = query.SessionId;
        request.StoreId = query.StoreId;
        request.UserId = query.UserId;
        request.OrganizationId = query.OrganizationId;
        request.CurrencyCode = query.CurrencyCode;
        request.CultureName = query.CultureName;

        return request;
    }
}
