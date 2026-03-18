using MediatR;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;

/// <summary>
/// [Cách 2] Depot tạo FundingRequest kèm danh sách vật tư.
/// TotalAmount được tự tính từ sum(items[].TotalPrice).
/// </summary>
public class CreateFundingRequestHandler : IRequestHandler<CreateFundingRequestCommand, int>
{
    private readonly IFundingRequestRepository _fundingRequestRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFundingRequestHandler(
        IFundingRequestRepository fundingRequestRepo,
        IUnitOfWork unitOfWork)
    {
        _fundingRequestRepo = fundingRequestRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(CreateFundingRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. Tính tổng tiền tự động từ danh sách items
        var totalAmount = request.Items.Sum(i => i.TotalPrice);

        // 2. Tạo FundingRequest domain model
        var fundingRequest = new FundingRequestModel(
            request.DepotId,
            request.RequestedBy,
            totalAmount,
            request.Description,
            request.AttachmentUrl
        );

        // 3. Thêm items
        foreach (var item in request.Items)
        {
            fundingRequest.AddItem(new FundingRequestItemModel
            {
                Row          = item.Row,
                ItemName     = item.ItemName,
                CategoryCode = item.CategoryCode,
                Unit         = item.Unit,
                Quantity     = item.Quantity,
                UnitPrice    = item.UnitPrice,
                TotalPrice   = item.TotalPrice,
                ItemType     = item.ItemType,
                TargetGroup  = item.TargetGroup,
                ReceivedDate = item.ReceivedDate,
                ExpiredDate  = item.ExpiredDate,
                Notes        = item.Notes
            });
        }

        // 4. Persist — CreateAsync lưu ngay và trả về ID thực từ DB
        var fundingRequestId = await _fundingRequestRepo.CreateAsync(fundingRequest, cancellationToken);
        await _unitOfWork.SaveAsync();

        return fundingRequestId;
    }
}
