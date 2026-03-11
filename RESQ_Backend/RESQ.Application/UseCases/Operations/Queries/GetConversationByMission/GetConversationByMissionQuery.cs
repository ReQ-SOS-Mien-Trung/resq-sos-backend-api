using MediatR;

namespace RESQ.Application.UseCases.Operations.Queries.GetConversationByMission;

public record GetConversationByMissionQuery(int MissionId, Guid RequesterId) : IRequest<GetConversationByMissionResponse>;
