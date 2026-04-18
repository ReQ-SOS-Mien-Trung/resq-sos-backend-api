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

        var latestVictimUpdateLookup = await sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync([sos.Id], cancellationToken);
        latestVictimUpdateLookup.TryGetValue(sos.Id, out var latestVictimUpdate);

        var currentView = SosRequestVictimUpdateOverlay.Apply(sos, latestVictimUpdate);
        var updatedAt = DateTime.UtcNow;
        var effectiveStructuredData = request.StructuredData ?? currentView.StructuredData;
        var effectiveSosType = request.SosType ?? currentView.SosType;
        var victimUpdate = new SosRequestVictimUpdateModel
        {
            SosRequestId = sos.Id,
            PacketId = request.PacketId ?? currentView.PacketId,
            Location = request.Location,
            LocationAccuracy = request.LocationAccuracy ?? currentView.LocationAccuracy,
            SosType = effectiveSosType,
            RawMessage = request.RawMessage.Trim(),
            StructuredData = effectiveStructuredData,
            NetworkMetadata = request.NetworkMetadata ?? currentView.NetworkMetadata,
            SenderInfo = request.SenderInfo ?? currentView.SenderInfo,
            VictimInfo = request.VictimInfo ?? currentView.VictimInfo,
            ReporterInfo = request.ReporterInfo ?? currentView.ReporterInfo,
            IsSentOnBehalf = request.IsSentOnBehalf ?? currentView.IsSentOnBehalf,
            OriginId = request.OriginId ?? currentView.OriginId,
            Timestamp = request.Timestamp ?? currentView.Timestamp,
            ClientCreatedAt = request.ClientCreatedAt ?? currentView.CreatedAt,
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

        return new UpdateSosRequestVictimResponse
        {
            SosRequestId = sos.Id,
            UpdatedAt = updatedAt
        };
    }
}
