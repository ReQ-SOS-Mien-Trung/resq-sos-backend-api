using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreateSupplyRequestCommandHandler(
    IDepotInventoryRepository depotInventoryRepository,
    ISupplyRequestRepository supplyRequestRepository,
    ISupplyRequestPriorityConfigRepository supplyRequestPriorityConfigRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<CreateSupplyRequestCommandHandler> logger)
    : IRequestHandler<CreateSupplyRequestCommand, CreateSupplyRequestResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly ISupplyRequestRepository _supplyRequestRepository = supplyRequestRepository;
    private readonly ISupplyRequestPriorityConfigRepository _supplyRequestPriorityConfigRepository = supplyRequestPriorityConfigRepository;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
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
            throw new InvalidSupplyRequestException("Không thể tạo yêu cầu cung cấp từ chính kho của bạn.");

        // 3. Xử lý từng kho nguồn trong transaction
        var config = await _supplyRequestPriorityConfigRepository.GetAsync(cancellationToken);
        var timing = config == null
            ? SupplyRequestPriorityPolicy.DefaultTiming
            : new SupplyRequestPriorityTiming(config.UrgentMinutes, config.HighMinutes, config.MediumMinutes);

        var createdRequests = new List<CreatedSupplyRequestDto>();

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var group in request.Requests)
            {
                var items = group.Items
                    .Select(i => (i.ItemModelId, i.Quantity))
                    .ToList();

                var autoRejectAt = SupplyRequestPriorityPolicy.ResolveAutoRejectAt(
                    DateTime.UtcNow,
                    group.PriorityLevel,
                    timing);

                var supplyRequestId = await _supplyRequestRepository.CreateAsync(
                    requestingDepotId.Value,
                    group.SourceDepotId,
                    items,
                    group.PriorityLevel,
                    autoRejectAt,
                    group.Note,
                    request.RequestingUserId,
                    cancellationToken);

                createdRequests.Add(new CreatedSupplyRequestDto
                {
                    SupplyRequestId = supplyRequestId,
                    SourceDepotId = group.SourceDepotId
                });
            }
        });

        // 4. Gửi notification cho manager của kho nguồn
        foreach (var created in createdRequests)
        {
            var sourceManagerUserId = await _supplyRequestRepository.GetActiveManagerUserIdByDepotIdAsync(created.SourceDepotId, cancellationToken);
            if (sourceManagerUserId.HasValue)
            {
                var createdGroup = request.Requests.First(x => x.SourceDepotId == created.SourceDepotId);
                var isUrgent = createdGroup.PriorityLevel == SupplyRequestPriorityLevel.Urgent;
                var notificationTitle = isUrgent ? "Yêu cầu tiếp tế khẩn cấp" : "Yêu cầu cung cấp vật tư mới";
                var notificationBody = isUrgent
                    ? $"Yêu cầu #{created.SupplyRequestId} đã vào mức khẩn cấp. Vui lòng ưu tiên xử lý ngay."
                    : $"Kho của bạn vừa nhận được yêu cầu cung cấp vật tư #{created.SupplyRequestId}. Vui lòng kiểm tra và xử lý.";
                var notificationType = isUrgent ? "supply_request_urgent" : "supply_request";

                await _firebaseService.SendNotificationToUserAsync(
                    sourceManagerUserId.Value,
                    notificationTitle,
                    notificationBody,
                    notificationType,
                    cancellationToken);

                if (isUrgent)
                    await _supplyRequestRepository.MarkUrgentEscalationNotifiedAsync(created.SupplyRequestId, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Kho nguồn {SourceDepotId} không có manager active. Không thể gửi thông báo.", created.SourceDepotId);
            }
        }

        return new CreateSupplyRequestResponse
        {
            CreatedRequests = createdRequests,
            Message = $"Đã tạo {createdRequests.Count} yêu cầu cung cấp vật tư thành công."
        };
    }
}
