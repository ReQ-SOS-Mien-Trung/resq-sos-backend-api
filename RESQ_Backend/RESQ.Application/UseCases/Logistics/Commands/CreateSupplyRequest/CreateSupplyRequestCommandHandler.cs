using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreateSupplyRequestCommandHandler(
    IDepotInventoryRepository depotInventoryRepository,
    ISupplyRequestRepository supplyRequestRepository,
    IFirebaseService firebaseService,
    ILogger<CreateSupplyRequestCommandHandler> logger)
    : IRequestHandler<CreateSupplyRequestCommand, CreateSupplyRequestResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly ISupplyRequestRepository _supplyRequestRepository = supplyRequestRepository;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<CreateSupplyRequestCommandHandler> _logger = logger;

    public async Task<CreateSupplyRequestResponse> Handle(CreateSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. Lấy kho của manager đang đăng nhập
        var requestingDepotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.RequestingUserId, cancellationToken);
        if (requestingDepotId == null)
            throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động.");

        // 2. Validate không có group nào trỏ về chính kho của manager
        var selfRequest = request.Requests.FirstOrDefault(r => r.SourceDepotId == requestingDepotId.Value);
        if (selfRequest != null)
            throw new BadRequestException("Không thể tạo yêu cầu cung cấp từ chính kho của bạn.");

        // 3. Xử lý từng kho nguồn
        var createdRequests = new List<CreatedSupplyRequestDto>();

        foreach (var group in request.Requests)
        {
            var items = group.Items
                .Select(i => (i.ReliefItemId, i.Quantity))
                .ToList();

            var supplyRequestId = await _supplyRequestRepository.CreateAsync(
                requestingDepotId.Value,
                group.SourceDepotId,
                items,
                group.Note,
                request.RequestingUserId,
                cancellationToken);

            createdRequests.Add(new CreatedSupplyRequestDto
            {
                SupplyRequestId = supplyRequestId,
                SourceDepotId   = group.SourceDepotId
            });

            // 4. Gửi Firebase notification cho manager của kho nguồn
            var sourceManagerUserId = await _supplyRequestRepository.GetActiveManagerUserIdByDepotIdAsync(group.SourceDepotId, cancellationToken);
            if (sourceManagerUserId.HasValue)
            {
                await _firebaseService.SendNotificationToUserAsync(
                    sourceManagerUserId.Value,
                    "Yêu cầu cung cấp vật tư mới",
                    $"Kho của bạn vừa nhận được yêu cầu cung cấp vật tư #{supplyRequestId}. Vui lòng kiểm tra và xử lý.",
                    cancellationToken);
            }
            else
            {
                _logger.LogWarning("Kho nguồn {SourceDepotId} không có manager active. Không thể gửi thông báo.", group.SourceDepotId);
            }
        }

        return new CreateSupplyRequestResponse
        {
            CreatedRequests = createdRequests,
            Message = $"Đã tạo {createdRequests.Count} yêu cầu cung cấp vật tư thành công."
        };
    }
}
