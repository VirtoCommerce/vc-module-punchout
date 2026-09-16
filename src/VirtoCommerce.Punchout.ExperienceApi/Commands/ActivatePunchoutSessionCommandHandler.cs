using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.ExperienceApi.Commands;

public class ActivatePunchoutSessionCommandHandler : IRequestHandler<ActivatePunchoutSessionCommand, PunchoutSessonActivationResult>
{
    private readonly IMediator _mediator;
    private readonly IPunchoutSessionService _punchoutSessionService;
    private readonly IStoreService _storeService;


    public ActivatePunchoutSessionCommandHandler(IMediator mediator, IPunchoutSessionService punchoutSessionService, IStoreService storeService)
    {
        _mediator = mediator;
        _punchoutSessionService = punchoutSessionService;
        _storeService = storeService;
    }

    public Task<PunchoutSessonActivationResult> Handle(ActivatePunchoutSessionCommand request, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}
