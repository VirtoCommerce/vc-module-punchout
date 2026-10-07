using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class CreatePunchoutRequisitionCommandHandler(IPunchoutOrderMessageProcessor orderMessageService)
    : IRequestHandler<CreatePunchoutRequisitionCommand, PunchoutCheckoutResult>
{
    public Task<PunchoutCheckoutResult> Handle(CreatePunchoutRequisitionCommand request, CancellationToken cancellationToken)
    {
        return orderMessageService.ProcessAsync(CreateRequest(request));
    }

    protected virtual PunchoutOrderMessageRequest CreateRequest(CreatePunchoutRequisitionCommand query)
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
