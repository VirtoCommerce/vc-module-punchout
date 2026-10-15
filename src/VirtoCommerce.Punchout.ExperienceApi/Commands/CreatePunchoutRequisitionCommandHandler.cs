using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class CreatePunchoutRequisitionCommandHandler(IPunchoutOrderMessageProcessor orderMessageProcessor)
    : IRequestHandler<CreatePunchoutRequisitionCommand, PunchoutCheckoutResult>
{
    public Task<PunchoutCheckoutResult> Handle(CreatePunchoutRequisitionCommand request, CancellationToken cancellationToken)
    {
        return orderMessageProcessor.ProcessAsync(CreateRequest(request));
    }

    protected virtual PunchoutOrderMessageRequest CreateRequest(CreatePunchoutRequisitionCommand command)
    {
        var request = AbstractTypeFactory<PunchoutOrderMessageRequest>.TryCreateInstance();

        request.SessionId = command.SessionId;
        request.StoreId = command.StoreId;
        request.UserId = command.UserId;
        request.OrganizationId = command.OrganizationId;
        request.CurrencyCode = command.CurrencyCode;
        request.CultureName = command.CultureName;

        return request;
    }
}
