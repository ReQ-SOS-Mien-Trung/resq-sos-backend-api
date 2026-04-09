using MediatR;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetSystemFund;

/// <summary>
/// Handler: Lấy thông tin quỹ hệ thống.
/// </summary>
public class GetSystemFundHandler(ISystemFundRepository systemFundRepo)
    : IRequestHandler<GetSystemFundQuery, SystemFundDto>
{
    private readonly ISystemFundRepository _systemFundRepo = systemFundRepo;

    public async Task<SystemFundDto> Handle(GetSystemFundQuery request, CancellationToken cancellationToken)
    {
        var fund = await _systemFundRepo.GetOrCreateAsync(cancellationToken);

        return new SystemFundDto
        {
            Id = fund.Id,
            Name = fund.Name,
            Balance = fund.Balance,
            LastUpdatedAt = fund.LastUpdatedAt == DateTime.MinValue ? null : fund.LastUpdatedAt.ToVietnamTime()
        };
    }
}
