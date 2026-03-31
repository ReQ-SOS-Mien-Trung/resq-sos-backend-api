using MediatR;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertSupplyRequestPriorityConfig;

public class UpsertSupplyRequestPriorityConfigCommandHandler(
    ISupplyRequestPriorityConfigRepository supplyRequestPriorityConfigRepository)
    : IRequestHandler<UpsertSupplyRequestPriorityConfigCommand, UpsertSupplyRequestPriorityConfigResponse>
{
    private readonly ISupplyRequestPriorityConfigRepository _supplyRequestPriorityConfigRepository = supplyRequestPriorityConfigRepository;

    public async Task<UpsertSupplyRequestPriorityConfigResponse> Handle(
        UpsertSupplyRequestPriorityConfigCommand request,
        CancellationToken cancellationToken)
    {
        var saved = await _supplyRequestPriorityConfigRepository.UpsertAsync(
            request.UrgentMinutes,
            request.HighMinutes,
            request.MediumMinutes,
            request.UserId,
            cancellationToken);

        return new UpsertSupplyRequestPriorityConfigResponse
        {
            UrgentMinutes = saved.UrgentMinutes,
            HighMinutes = saved.HighMinutes,
            MediumMinutes = saved.MediumMinutes,
            UpdatedBy = saved.UpdatedBy,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật cấu hình thời gian ưu tiên tiếp tế thành công."
        };
    }
}
