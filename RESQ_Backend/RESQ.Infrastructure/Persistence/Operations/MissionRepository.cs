using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Mappers.Operations;

namespace RESQ.Infrastructure.Persistence.Operations;

public class MissionRepository(IUnitOfWork unitOfWork) : IMissionRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<MissionModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Mission>()
            .GetByPropertyAsync(x => x.Id == id, tracked: false, includeProperties: "MissionActivities");

        return entity is null ? null : MissionMapper.ToDomain(entity);
    }

    public async Task<IEnumerable<MissionModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<Mission>()
            .GetAllByPropertyAsync(filter: null, includeProperties: "MissionActivities");

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(MissionMapper.ToDomain);
    }

    public async Task<IEnumerable<MissionModel>> GetByClusterIdAsync(int clusterId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.GetRepository<Mission>()
            .GetAllByPropertyAsync(x => x.ClusterId == clusterId, includeProperties: "MissionActivities");

        return entities
            .OrderByDescending(x => x.CreatedAt)
            .Select(MissionMapper.ToDomain);
    }

    public async Task<int> CreateAsync(MissionModel mission, Guid coordinatorId, CancellationToken cancellationToken = default)
    {
        // 1. Create mission entity
        var entity = MissionMapper.ToEntity(mission);
        await _unitOfWork.GetRepository<Mission>().AddAsync(entity);
        await _unitOfWork.SaveAsync();

        // 2. Create activities
        if (mission.Activities.Count > 0)
        {
            var activityRepo = _unitOfWork.GetRepository<MissionActivity>();
            foreach (var actModel in mission.Activities)
            {
                actModel.MissionId = entity.Id;
                var actEntity = MissionActivityMapper.ToEntity(actModel);
                actEntity.MissionId = entity.Id;
                actEntity.AssignedAt = DateTime.UtcNow;
                await activityRepo.AddAsync(actEntity);
            }
            await _unitOfWork.SaveAsync();
        }

        // 3. Auto-create conversation for coordinator + victims from cluster
        await CreateConversationForMissionAsync(entity.Id, entity.ClusterId, coordinatorId);

        return entity.Id;
    }

    public async Task UpdateAsync(MissionModel mission, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Mission>()
            .GetByPropertyAsync(x => x.Id == mission.Id, tracked: true);

        if (entity is null) return;

        entity.MissionType = mission.MissionType;
        entity.PriorityScore = mission.PriorityScore;
        entity.StartTime = mission.StartTime;
        entity.ExpectedEndTime = mission.ExpectedEndTime;

        await _unitOfWork.GetRepository<Mission>().UpdateAsync(entity);
    }

    public async Task UpdateStatusAsync(int missionId, string status, bool isCompleted, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.GetRepository<Mission>()
            .GetByPropertyAsync(x => x.Id == missionId, tracked: true);

        if (entity is null) return;

        entity.Status = status;
        entity.IsCompleted = isCompleted;
        if (isCompleted)
            entity.CompletedAt = DateTime.UtcNow;

        await _unitOfWork.GetRepository<Mission>().UpdateAsync(entity);
    }

    // -----------------------------------------------------------------------
    private async Task CreateConversationForMissionAsync(int missionId, int? clusterId, Guid coordinatorId)
    {
        var conversationRepo = _unitOfWork.GetRepository<Conversation>();
        var participantRepo = _unitOfWork.GetRepository<ConversationParticipant>();

        // Create the conversation
        var conversation = new Conversation
        {
            MissionId = missionId
        };
        await conversationRepo.AddAsync(conversation);
        await _unitOfWork.SaveAsync();

        var participantIds = new HashSet<Guid>();

        // Add coordinator
        participantIds.Add(coordinatorId);
        await participantRepo.AddAsync(new ConversationParticipant
        {
            ConversationId = conversation.Id,
            UserId = coordinatorId,
            RoleInConversation = "coordinator",
            JoinedAt = DateTime.UtcNow
        });

        // Add victims from cluster's SOS requests
        if (clusterId.HasValue)
        {
            var sosRequests = await _unitOfWork.GetRepository<SosRequest>()
                .GetAllByPropertyAsync(x => x.ClusterId == clusterId.Value);

            foreach (var sos in sosRequests)
            {
                if (sos.UserId.HasValue && !participantIds.Contains(sos.UserId.Value))
                {
                    participantIds.Add(sos.UserId.Value);
                    await participantRepo.AddAsync(new ConversationParticipant
                    {
                        ConversationId = conversation.Id,
                        UserId = sos.UserId.Value,
                        RoleInConversation = "victim",
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _unitOfWork.SaveAsync();
    }
}
