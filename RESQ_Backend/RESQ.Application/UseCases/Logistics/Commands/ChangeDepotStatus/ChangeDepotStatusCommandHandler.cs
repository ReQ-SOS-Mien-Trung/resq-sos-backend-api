using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics.Exceptions;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;

public class ChangeDepotStatusCommandHandler(
    IDepotRepository depotRepository,
    IUnitOfWork unitOfWork,
    ILogger<ChangeDepotStatusCommandHandler> logger) 
    : IRequestHandler<ChangeDepotStatusCommand, ChangeDepotStatusResponse>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ChangeDepotStatusCommandHandler> _logger = logger;

    public async Task<ChangeDepotStatusResponse> Handle(ChangeDepotStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling ChangeDepotStatusCommand for Id={Id} to Status={Status}", request.Id, request.Status);

        var depot = await _depotRepository.GetByIdAsync(request.Id, cancellationToken);
        if (depot == null)
        {
            throw new NotFoundException("Không tìm thấy kho cứu trợ");
        }

        // Validate: Cannot switch to Available without a Manager
        if (request.Status == DepotStatus.Available && depot.CurrentManagerId == null)
        {
            throw new InvalidDepotStatusTransitionException(depot.Status, request.Status, "Không thể chuyển kho sang trạng thái hoạt động khi chưa có quản lý");
        }

        // Apply change
        depot.ChangeStatus(request.Status);

        await _depotRepository.UpdateAsync(depot, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Depot status updated successfully: Id={Id}", request.Id);

        return new ChangeDepotStatusResponse
        {
            Id = depot.Id,
            Status = depot.Status.ToString(),
            Message = "Cập nhật trạng thái kho thành công."
        };
    }
}
