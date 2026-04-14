using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;

/// <summary>
/// [Cách 2] Depot tạo FundingRequest kèm danh sách vật phẩm.
/// DepotId được tự động lấy từ manager đang đăng nhập.
/// TotalAmount được tự tính từ sum(items[].TotalPrice).
/// </summary>
public class CreateFundingRequestHandler : IRequestHandler<CreateFundingRequestCommand, int>
{
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService;
    private readonly IFundingRequestRepository _fundingRequestRepo;
    private readonly IDepotInventoryRepository _depotInventoryRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFundingRequestHandler(
            RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
        IFundingRequestRepository fundingRequestRepo,
        IDepotInventoryRepository depotInventoryRepo,
        IUnitOfWork unitOfWork)
    {
        _managerDepotAccessService = managerDepotAccessService;
        _fundingRequestRepo = fundingRequestRepo;
        _depotInventoryRepo = depotInventoryRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(CreateFundingRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. L?y depotId t? manager token
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.RequestedBy, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Bạn không có kho đang hoạt động. Vui lòng liên hệ admin.");

        // 2. Tính tổng tiền tự động từ danh sách items
        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);

        // 3. T?o FundingRequest domain model
        var fundingRequest = new FundingRequestModel(
            depotId,
            request.RequestedBy,
            totalAmount,
            request.Description,
            null
        );

        // 3. Thêm items
        foreach (var item in request.Items)
        {
            fundingRequest.AddItem(new FundingRequestItemModel
            {
                Row          = item.Row,
                ItemName     = item.ItemName,
                CategoryCode = item.CategoryCode,
                TargetGroups = item.TargetGroup.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                ItemType     = item.ItemType,
                Unit         = item.Unit,
                Quantity     = item.Quantity,
                UnitPrice    = item.UnitPrice,
                TotalPrice   = item.Quantity * item.UnitPrice,
                Notes        = item.Description,
                ImageUrl     = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim(),
                VolumePerUnit = item.VolumePerUnit ?? 0,
                WeightPerUnit = item.WeightPerUnit ?? 0
            });
        }

        // 4. Persist - CreateAsync lưu ngay và trả về ID thực từ DB
        var fundingRequestId = await _fundingRequestRepo.CreateAsync(fundingRequest, cancellationToken);
        await _unitOfWork.SaveAsync();

        return fundingRequestId;
    }
}
