using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.UpdateSosRequestVictim;

public class UpdateSosRequestVictimCommandHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRequestCompanionRepository companionRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ISosRuleEvaluationRepository sosRuleEvaluationRepository,
    ISosPriorityEvaluationService priorityEvaluationService,
    ISosAiAnalysisQueue sosAiAnalysisQueue,
    ISosRequestRealtimeHubService sosRequestRealtimeHubService,
    IUnitOfWork unitOfWork
) : IRequestHandler<UpdateSosRequestVictimCommand, UpdateSosRequestVictimResponse>
{
    public async Task<UpdateSosRequestVictimResponse> Handle(UpdateSosRequestVictimCommand request, CancellationToken cancellationToken)
    {
        var sos = await sosRequestRepository.GetByIdAsync(request.SosRequestId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy SOS request với ID: {request.SosRequestId}");

        var isOwner = sos.UserId == request.ReporterUserId;
        if (!isOwner)
        {
            var isCompanion = await companionRepository.IsCompanionAsync(request.SosRequestId, request.ReporterUserId, cancellationToken);
            if (!isCompanion)
            {
                throw new ForbiddenException("Bạn không có quyền cập nhật SOS request này.");
            }
        }

        SosRequestVictimMutationGuard.EnsureCanUpdate(sos);

        var updatedAt = DateTime.UtcNow;
        var trimmedRawMessage = request.RawMessage.Trim();
        var effectiveStructuredData = request.StructuredData ?? sos.StructuredData;
        var effectiveSosType = request.SosType ?? sos.SosType;
        var contentChanged =
            !string.Equals(trimmedRawMessage, sos.RawMessage?.Trim(), StringComparison.Ordinal) ||
            !string.Equals(effectiveStructuredData, sos.StructuredData, StringComparison.Ordinal) ||
            !string.Equals(effectiveSosType, sos.SosType, StringComparison.OrdinalIgnoreCase);

        sos.PacketId = request.PacketId ?? sos.PacketId;
        sos.Location = request.Location;
        sos.LocationAccuracy = request.LocationAccuracy ?? sos.LocationAccuracy;
        sos.SosType = effectiveSosType;
        sos.RawMessage = trimmedRawMessage;
        sos.StructuredData = effectiveStructuredData;
        sos.NetworkMetadata = request.NetworkMetadata ?? sos.NetworkMetadata;
        sos.SenderInfo = request.SenderInfo ?? sos.SenderInfo;
        sos.VictimInfo = request.VictimInfo ?? sos.VictimInfo;
        sos.ReporterInfo = request.ReporterInfo ?? sos.ReporterInfo;
        sos.IsSentOnBehalf = request.IsSentOnBehalf ?? sos.IsSentOnBehalf;
        sos.OriginId = request.OriginId ?? sos.OriginId;
        sos.Timestamp = request.Timestamp ?? sos.Timestamp;
        sos.CreatedAt = request.ClientCreatedAt ?? sos.CreatedAt;
        sos.LastUpdatedAt = updatedAt;

        var victimUpdate = new SosRequestVictimUpdateModel
        {
            SosRequestId = sos.Id,
            PacketId = sos.PacketId,
            Location = sos.Location,
            LocationAccuracy = sos.LocationAccuracy,
            SosType = sos.SosType,
            RawMessage = sos.RawMessage,
            StructuredData = sos.StructuredData,
            NetworkMetadata = sos.NetworkMetadata,
            SenderInfo = sos.SenderInfo,
            VictimInfo = sos.VictimInfo,
            ReporterInfo = sos.ReporterInfo,
            IsSentOnBehalf = sos.IsSentOnBehalf,
            OriginId = sos.OriginId,
            Timestamp = sos.Timestamp,
            ClientCreatedAt = sos.CreatedAt,
            UpdatedByUserId = request.ReporterUserId,
            UpdatedAt = updatedAt,
            UpdatedByMode = isOwner ? "Owner" : "Companion"
        };

        var evaluation = await priorityEvaluationService.EvaluateAsync(
            sos.Id,
            effectiveStructuredData,
            effectiveSosType,
            cancellationToken);

        await sosRequestUpdateRepository.AddVictimUpdateAsync(victimUpdate, cancellationToken);
        await sosRuleEvaluationRepository.CreateAsync(evaluation, cancellationToken);
        sos.SetPriorityLevel(evaluation.PriorityLevel);
        sos.SetPriorityScore(evaluation.TotalScore);
        sos.LastUpdatedAt = updatedAt;
        await sosRequestRepository.UpdateAsync(sos, cancellationToken);
        await unitOfWork.SaveAsync();

        if (contentChanged)
        {
            await sosAiAnalysisQueue.QueueAsync(SosAiAnalysisTask.Create(
                sos.Id,
                effectiveStructuredData,
                trimmedRawMessage,
                effectiveSosType,
                evaluation));
        }

        await sosRequestRealtimeHubService.PushSosRequestUpdateAsync(
            sos.Id,
            "VictimUpdated",
            cancellationToken: cancellationToken);

        return new UpdateSosRequestVictimResponse
        {
            SosRequestId = sos.Id,
            UpdatedAt = updatedAt
        };
    }
}
