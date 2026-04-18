using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Finance.Commands.SetDepotAdvanceLimit;

public class SetDepotAdvanceLimitHandler : IRequestHandler<SetDepotAdvanceLimitCommand, Unit>
{
    private readonly IDepotRepository _depotRepo;
    private readonly IUnitOfWork _unitOfWork;

    public SetDepotAdvanceLimitHandler(IDepotRepository depotRepo, IUnitOfWork unitOfWork)
    {
        _depotRepo = depotRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(SetDepotAdvanceLimitCommand request, CancellationToken cancellationToken)
    {
        if (request.AdvanceLimit < 0)
        {
            throw new BadRequestException("Hạn mức ứng trước không được là số âm.");
        }

        var depot = await _depotRepo.GetByIdAsync(request.DepotId, cancellationToken);
        if (depot == null)
        {
            throw new NotFoundException($"Không tìm thấy kho có id {request.DepotId}.");
        }

        depot.SetAdvanceLimit(request.AdvanceLimit);
        await _depotRepo.UpdateAsync(depot, cancellationToken);
        await _unitOfWork.SaveAsync();

        return Unit.Value;
    }
}
