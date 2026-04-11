using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Queries.StreamRescueMissionSuggestion;

public class StreamRescueMissionSuggestionQueryHandler(
    IMissionContextService missionContextService,
    IRescueMissionSuggestionService suggestionService,
    ISosClusterRepository sosClusterRepository,
    IUnitOfWork unitOfWork,
    ILogger<StreamRescueMissionSuggestionQueryHandler> logger
) : IStreamRequestHandler<StreamRescueMissionSuggestionQuery, SseMissionEvent>
{
    private readonly IMissionContextService _missionContextService = missionContextService;
    private readonly IRescueMissionSuggestionService _suggestionService = suggestionService;
    private readonly ISosClusterRepository _sosClusterRepository = sosClusterRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<StreamRescueMissionSuggestionQueryHandler> _logger = logger;

    public async IAsyncEnumerable<SseMissionEvent> Handle(
        StreamRescueMissionSuggestionQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new SseMissionEvent { EventType = "status", Data = "loading_context" };

        MissionContext? context = null;
        string? contextError = null;
        try
        {
            context = await _missionContextService.PrepareContextAsync(request.ClusterId, cancellationToken);
        }
        catch (Exception ex)
        {
            contextError = ex.Message;
        }

        if (contextError is not null)
        {
            yield return new SseMissionEvent { EventType = "error", Data = contextError };
            yield break;
        }

        RescueMissionSuggestionResult? aiResult = null;
        var hasError = false;

        await foreach (var evt in _suggestionService.GenerateSuggestionStreamAsync(
            context!.SosRequests,
            context.NearbyDepots,
            context.NearbyTeams,
            context.MultiDepotRecommended,
            request.ClusterId,
            cancellationToken))
        {
            if (evt.EventType == "result" && evt.Result is not null)
                aiResult = evt.Result;
            else if (evt.EventType == "error")
                hasError = true;

            yield return evt;
        }

        if (hasError)
            yield break;

        if (aiResult is not null && aiResult.IsSuccess && aiResult.SuggestionId.HasValue)
        {
            try
            {
                context!.Cluster.IsMissionCreated = true;
                await _sosClusterRepository.UpdateAsync(context.Cluster, cancellationToken);
                await _unitOfWork.SaveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update cluster mission-created flag for ClusterId={clusterId}", request.ClusterId);
            }
        }

        yield return new SseMissionEvent { EventType = "status", Data = "done" };
    }
}
