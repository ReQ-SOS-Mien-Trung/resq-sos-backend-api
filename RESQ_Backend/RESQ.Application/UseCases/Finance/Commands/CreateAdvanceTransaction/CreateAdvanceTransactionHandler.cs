using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Finance.Common;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateAdvanceTransaction;

public class CreateAdvanceTransactionHandler : IRequestHandler<CreateAdvanceTransactionCommand, Unit>
{
    private readonly IDepotFundRepository _depotFundRepo;
    private readonly IDepotRepository _depotRepo;
    private readonly IDepotInventoryRepository _depotInventoryRepo;
    private readonly IUserRepository _userRepo;
    private readonly IFirebaseService _firebaseService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAdvanceTransactionHandler(
        IDepotFundRepository depotFundRepo,
        IDepotRepository depotRepo,
        IDepotInventoryRepository depotInventoryRepo,
        IUserRepository userRepo,
        IFirebaseService firebaseService,
        IUnitOfWork unitOfWork)
    {
        _depotFundRepo = depotFundRepo;
        _depotRepo = depotRepo;
        _depotInventoryRepo = depotInventoryRepo;
        _userRepo = userRepo;
        _firebaseService = firebaseService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(CreateAdvanceTransactionCommand request, CancellationToken cancellationToken)
    {
        if (request.Transactions.Count == 0)
        {
            throw new BadRequestException("Cần ít nhất 1 giao dịch ứng trước.");
        }

        if (request.Transactions.Count > 200)
        {
            throw new BadRequestException("Mỗi lần chỉ được tối đa 200 giao dịch ứng trước.");
        }

        var managerDepotIds = await _depotInventoryRepo.GetActiveDepotIdsByManagerAsync(request.RequestedBy, cancellationToken);
        if (managerDepotIds.Count == 0)
        {
            throw new NotFoundException("Tài khoản hiện tại chưa được phân công quản lý kho đang hoạt động.");
        }

        var depotFund = await _depotFundRepo.GetByIdAsync(request.DepotFundId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy quỹ kho có id {request.DepotFundId}.");

        var depot = await _depotRepo.GetByIdAsync(depotFund.DepotId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy kho có id {depotFund.DepotId}.");

        if (!managerDepotIds.Contains(depotFund.DepotId))
        {
            throw new ForbiddenException("Quỹ kho này không thuộc kho bạn đang được phân công.");
        }

        var normalizedItems = request.Transactions.Select(x =>
        {
            if (x.Amount <= 0)
            {
                throw new BadRequestException("Số tiền ứng trước phải lớn hơn 0.");
            }

            return new CreateAdvanceTransactionItem(
                x.Amount,
                ContributorIdentityNormalizer.NormalizeName(x.ContributorName),
                ContributorIdentityNormalizer.NormalizePhoneNumber(x.PhoneNumber));
        }).ToList();

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var item in normalizedItems)
            {
                depot.RecordAdvance(item.Amount);
                depotFund.Advance(item.Amount);

                await _depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
                {
                    DepotFundId = depotFund.Id,
                    TransactionType = DepotFundTransactionType.PersonalAdvance,
                    Amount = item.Amount,
                    ReferenceType = TransactionReferenceType.InternalAdvance.ToString(),
                    ReferenceId = depot.Id,
                    Note = $"Người ứng {item.ContributorName} ({item.PhoneNumber}) đã ứng {item.Amount:N0} VND.",
                    CreatedBy = request.RequestedBy,
                    CreatedAt = DateTime.UtcNow,
                    ContributorName = item.ContributorName,
                    ContributorPhoneNumber = item.PhoneNumber
                }, cancellationToken);
            }

            await _depotRepo.UpdateAsync(depot, cancellationToken);
            await _depotFundRepo.UpdateAsync(depotFund, cancellationToken);
            await _unitOfWork.SaveAsync();
        });
        // Notify admins about the total advance
        var totalAmount = request.Transactions.Sum(x => x.Amount);
        var activeAdminIds = await _userRepo.GetActiveAdminUserIdsAsync(cancellationToken);
        
        var notifyTasks = activeAdminIds.Select(adminId =>
            _firebaseService.SendNotificationToUserAsync(
                adminId,
                "Tạm ứng quỹ kho",
                $"Quản lý kho {depot.Name} vừa ứng trước tổng cộng {totalAmount:N0} VND.",
                "depot_advance",
                cancellationToken));
                
        await Task.WhenAll(notifyTasks);
        return Unit.Value;
    }
}
