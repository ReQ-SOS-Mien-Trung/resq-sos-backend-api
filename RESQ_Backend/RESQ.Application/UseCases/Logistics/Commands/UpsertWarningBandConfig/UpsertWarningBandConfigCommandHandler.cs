using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Thresholds;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;

public class UpsertWarningBandConfigCommandHandler(
    IStockWarningBandConfigRepository repo,
    IStockWarningEvaluatorService evaluatorService)
    : IRequestHandler<UpsertWarningBandConfigCommand, WarningBandConfigDto>
{
    private readonly IStockWarningBandConfigRepository _repo = repo;
    private readonly IStockWarningEvaluatorService _evaluatorService = evaluatorService;

    public async Task<WarningBandConfigDto> Handle(UpsertWarningBandConfigCommand request, CancellationToken cancellationToken)
    {
        // Validate bands bằng WarningBandSet (throws InvalidWarningBandSetException nếu sai)
        // DomainExceptionBehaviour sẽ map exception này thành 422.
        var domainBands = request.Bands
            .Select(b => new WarningBand(b.Name, b.From, b.To))
            .ToList();

        _ = new WarningBandSet(domainBands); // validate only — throws on invalid

        var saved = await _repo.UpsertAsync(request.Bands, request.UserId, cancellationToken);

        // Invalidate cache để lần đánh giá tiếp theo dùng config mới
        await _evaluatorService.InvalidateBandCacheAsync();

        return saved;
    }
}
