using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;

namespace RESQ.Application.UseCases.Operations.Queries.GetConversationByMission;

public class GetConversationByMissionQueryHandler(
    IConversationRepository conversationRepository,
    ILogger<GetConversationByMissionQueryHandler> logger
) : IRequestHandler<GetConversationByMissionQuery, GetConversationByMissionResponse>
{
    private readonly IConversationRepository _conversationRepository = conversationRepository;
    private readonly ILogger<GetConversationByMissionQueryHandler> _logger = logger;

    public async Task<GetConversationByMissionResponse> Handle(
        GetConversationByMissionQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting conversations for MissionId={missionId}, RequesterId={requesterId}",
            request.MissionId, request.RequesterId);

        // Lấy tất cả conversations mà requester tham gia trong mission này
        var conversations = (await _conversationRepository.GetAllByMissionIdForUserAsync(
            request.MissionId, request.RequesterId, cancellationToken)).ToList();

        if (conversations.Count == 0)
            throw new NotFoundException(
                $"Không tìm thấy conversation nào cho mission ID: {request.MissionId} hoặc bạn không phải là thành viên.");

        return new GetConversationByMissionResponse
        {
            MissionId = request.MissionId,
            Conversations = conversations.Select(c =>
            {
                var coordinator = c.Participants
                    .FirstOrDefault(p => p.RoleInConversation == "coordinator");
                var victim = c.Participants
                    .FirstOrDefault(p => p.RoleInConversation == "victim");

                return new ConversationItemDto
                {
                    ConversationId = c.Id,
                    Coordinator = coordinator is null ? null : new ParticipantDto
                    {
                        Id = coordinator.Id,
                        UserId = coordinator.UserId,
                        UserName = coordinator.UserName,
                        RoleInConversation = coordinator.RoleInConversation,
                        JoinedAt = coordinator.JoinedAt
                    },
                    Victim = victim is null ? null : new ParticipantDto
                    {
                        Id = victim.Id,
                        UserId = victim.UserId,
                        UserName = victim.UserName,
                        RoleInConversation = victim.RoleInConversation,
                        JoinedAt = victim.JoinedAt
                    }
                };
            }).ToList()
        };
    }
}
