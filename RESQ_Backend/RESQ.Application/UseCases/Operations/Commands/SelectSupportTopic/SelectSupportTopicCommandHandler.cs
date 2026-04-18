using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Queries;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.SelectSupportTopic;

public class SelectSupportTopicCommandHandler(
    IConversationRepository conversationRepository,
    ISosRequestRepository sosRequestRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    IChatSupportAiService chatSupportAiService,
    ILogger<SelectSupportTopicCommandHandler> logger
) : IRequestHandler<SelectSupportTopicCommand, SelectSupportTopicResponse>
{
    public async Task<SelectSupportTopicResponse> Handle(
        SelectSupportTopicCommand request,
        CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.GetByIdAsync(
            request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Conversation {request.ConversationId} không tồn tại.");

        // Ensure it belongs to the victim
        if (conversation.VictimId != request.VictimId)
            throw new ForbiddenException("Bạn không có quyền thao tác với conversation này.");

        logger.LogInformation(
            "Victim {VictimId} chọn topic '{Topic}' trong conversation {ConvId}",
            request.VictimId, request.TopicKey, request.ConversationId);

        string aiMessage;
        List<SosRequestDto>? sosRequestDtos = null;
        ConversationStatus newStatus;

        if (request.TopicKey == "SosRequestSupport")
        {
            // AI truy vấn SOS requests của victim
            var sosRequests = (await sosRequestRepository.GetByUserIdAsync(
                request.VictimId, cancellationToken)).ToList();
            var victimUpdateLookup = await sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync(
                sosRequests.Select(x => x.Id),
                cancellationToken);
            var effectiveSosRequests = sosRequests.Select(sos =>
            {
                victimUpdateLookup.TryGetValue(sos.Id, out var latestVictimUpdate);
                return SosRequestVictimUpdateOverlay.Apply(sos, latestVictimUpdate);
            }).ToList();

            aiMessage = await chatSupportAiService.FormatSosRequestListMessageAsync(
                effectiveSosRequests, cancellationToken);

            // Chuyển sang WaitingCoordinator chỉ khi có SOS request để hỗ trợ
            newStatus = effectiveSosRequests.Count > 0
                ? ConversationStatus.WaitingCoordinator
                : ConversationStatus.AiAssist;

            sosRequestDtos = effectiveSosRequests
                .Where(r => r.Status != RESQ.Domain.Enum.Emergency.SosRequestStatus.Cancelled
                         && r.Status != RESQ.Domain.Enum.Emergency.SosRequestStatus.Resolved)
                .Select(r => new SosRequestDto
                {
                    Id = r.Id,
                    PacketId = r.PacketId,
                    ClusterId = r.ClusterId,
                    UserId = r.UserId,
                    SosType = r.SosType,
                    RawMessage = r.RawMessage,
                    OriginId = r.OriginId,
                    Status = r.Status.ToString(),
                    PriorityLevel = r.PriorityLevel?.ToString(),
                    Latitude = r.Location?.Latitude,
                    Longitude = r.Location?.Longitude,
                    LocationAccuracy = r.LocationAccuracy,
                    Timestamp = r.Timestamp,
                    CreatedAt = r.CreatedAt,
                    ReceivedAt = r.ReceivedAt,
                    LastUpdatedAt = r.LastUpdatedAt
                })
                .ToList();
        }
        else
        {
            // Các topic khác → thông báo chờ coordinator
            aiMessage = $"Bạn đã chọn: **{request.TopicKey}**. " +
                        "Một Coordinator sẽ tham gia hỗ trợ bạn trong thời gian sớm nhất. " +
                        "Vui lòng mô tả thêm vấn đề của bạn.";
            newStatus = ConversationStatus.WaitingCoordinator;
        }

        // Cập nhật trạng thái conversation
        await conversationRepository.UpdateStatusAsync(
            request.ConversationId,
            newStatus,
            selectedTopic: request.TopicKey,
            cancellationToken: cancellationToken);

        // Lưu tin nhắn AI vào conversation
        await conversationRepository.SendMessageAsync(
            request.ConversationId,
            senderId: null,
            content: aiMessage,
            messageType: MessageType.AiMessage,
            cancellationToken: cancellationToken);

        return new SelectSupportTopicResponse
        {
            ConversationId = request.ConversationId,
            Status = newStatus,
            TopicKey = request.TopicKey,
            AiResponseMessage = aiMessage,
            SosRequests = sosRequestDtos
        };
    }
}
