using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Services.Finance;

/// <summary>
/// Drain toàn bộ quỹ kho (balance > 0) về quỹ hệ thống khi đóng kho.
/// Gọi bên trong transaction của handler để đảm bảo atomic.
/// </summary>
public class DepotFundDrainService(
    IDepotFundRepository depotFundRepo,
    ISystemFundRepository systemFundRepo,
    ILogger<DepotFundDrainService> logger) : IDepotFundDrainService
{
    public async Task<decimal> DrainAllToSystemFundAsync(
        int depotId, int closureId, Guid performedBy, CancellationToken cancellationToken = default)
    {
        var depotFunds = await depotFundRepo.GetAllByDepotIdAsync(depotId, cancellationToken);

        var totalDrained = 0m;
        var now = DateTime.UtcNow;

        foreach (var fund in depotFunds)
        {
            if (fund.Balance <= 0) continue;

            var amount = fund.Balance;

            // Trừ quỹ kho
            fund.Debit(amount);
            await depotFundRepo.UpdateAsync(fund, cancellationToken);

            // Tạo transaction quỹ kho
            await depotFundRepo.CreateTransactionAsync(new DepotFundTransactionModel
            {
                DepotFundId = fund.Id,
                TransactionType = DepotFundTransactionType.ClosureFundReturn,
                Amount = -amount,
                ReferenceType = "DepotClosure",
                ReferenceId = closureId,
                Note = $"Hoàn quỹ kho #{depotId} về quỹ hệ thống khi đóng kho — {amount:N0} VNĐ",
                CreatedBy = performedBy,
                CreatedAt = now
            }, cancellationToken);

            totalDrained += amount;
        }

        if (totalDrained > 0)
        {
            // Cộng vào quỹ hệ thống
            var systemFund = await systemFundRepo.GetOrCreateAsync(cancellationToken);
            systemFund.Credit(totalDrained);
            await systemFundRepo.UpdateAsync(systemFund, cancellationToken);

            // Tạo transaction quỹ hệ thống
            await systemFundRepo.CreateTransactionAsync(new SystemFundTransactionModel
            {
                SystemFundId = systemFund.Id,
                TransactionType = SystemFundTransactionType.DepotClosureFundReturn,
                Amount = totalDrained,
                ReferenceType = "DepotClosure",
                ReferenceId = closureId,
                Note = $"Hoàn quỹ từ kho #{depotId} khi đóng kho — {totalDrained:N0} VNĐ",
                CreatedBy = performedBy,
                CreatedAt = now
            }, cancellationToken);

            logger.LogInformation(
                "DepotFundDrain | DepotId={DepotId} ClosureId={ClosureId} TotalDrained={Amount}",
                depotId, closureId, totalDrained);
        }

        return totalDrained;
    }
}
