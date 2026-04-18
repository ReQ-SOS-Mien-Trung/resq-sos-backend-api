using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Thresholds;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;

public class UpsertWarningBandConfigCommandHandler(
    IStockWarningBandConfigRepository repo,
    IStockWarningEvaluatorService evaluatorService)
    : IRequestHandler<UpsertWarningBandConfigCommand, WarningBandConfigResponse>
{
    private readonly IStockWarningBandConfigRepository _repo = repo;
    private readonly IStockWarningEvaluatorService _evaluatorService = evaluatorService;

    public async Task<WarningBandConfigResponse> Handle(UpsertWarningBandConfigCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        // Convert percent → ratio; From của mỗi bậc = To của bậc trước
        var criticalTo = r.Critical / 100m;
        var mediumTo   = r.Medium   / 100m;
        var lowTo      = r.Low      / 100m;

        var internalBands = new List<WarningBandDto>
        {
            new() { Name = "CRITICAL", From = 0m,         To = criticalTo },
            new() { Name = "MEDIUM",   From = criticalTo,  To = mediumTo   },
            new() { Name = "LOW",      From = mediumTo,    To = lowTo      },
            new() { Name = "OK",       From = lowTo,       To = null       }
        };

        // Domain validation (throws InvalidWarningBandSetException → 422 qua DomainExceptionBehaviour)
        var domainBands = internalBands.Select(b => new WarningBand(b.Name, b.From, b.To)).ToList();
        _ = new WarningBandSet(domainBands);

        var saved = await _repo.UpsertAsync(internalBands, request.UserId, cancellationToken);

        // Invalidate cache để lần đánh giá tiếp theo dùng config mới
        await _evaluatorService.InvalidateBandCacheAsync();

        return ToResponse(saved);
    }

    /// <summary>Maps internal <see cref="WarningBandConfigDto"/> to the simplified API response.</summary>
    internal static WarningBandConfigResponse ToResponse(WarningBandConfigDto dto)
    {
        var critical = dto.Bands.FirstOrDefault(b => b.Name == "CRITICAL");
        var medium   = dto.Bands.FirstOrDefault(b => b.Name == "MEDIUM");
        var low      = dto.Bands.FirstOrDefault(b => b.Name == "LOW");

        return new WarningBandConfigResponse
        {
            Id        = dto.Id,
            Critical  = (critical?.To ?? 0m) * 100m,
            Medium    = (medium?.To   ?? 0m) * 100m,
            Low       = (low?.To      ?? 0m) * 100m,
            UpdatedBy = dto.UpdatedBy,
            UpdatedAt = dto.UpdatedAt
        };
    }
}
