using Microsoft.EntityFrameworkCore;
using RESQ.Application.Repositories.Operations;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Application.Repositories.Base;

namespace RESQ.Infrastructure.Persistence.Operations;

public class ConversationRepository(IUnitOfWork unitOfWork) : IConversationRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    // --- Victim conversation -------------------------------------------------

    public async Task<ConversationModel> GetOrCreateForVictimAsync(
        Guid victimId, CancellationToken cancellationToken = default)
    {
        // Chỉ lấy conversation đang ở trạng thái AiAssist (chưa chọn chủ đề).
        // Nếu không tìm thấy (victim đã chọn chủ đề trước đó), tạo conversation mới.
        // Điều này đảm bảo mỗi chủ đề chat là một đoạn hội thoại riêng biệt.
        var entity = await _unitOfWork.SetTracked<Conversation>()
            .Include(c => c.ConversationParticipants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(
                c => c.VictimId == victimId && c.Status == nameof(ConversationStatus.AiAssist),
                cancellationToken);

        if (entity == null)
        {
            entity = new Conversation
            {
                VictimId = victimId,
                Status = nameof(ConversationStatus.AiAssist),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.GetRepository<Conversation>().AddAsync(entity);

            var participant = new ConversationParticipant
            {
                Conversation = entity,
                UserId = victimId,
                RoleInConversation = "Victim",
                JoinedAt = DateTime.UtcNow
            };
            await _unitOfWork.GetRepository<ConversationParticipant>().AddAsync(participant);

            await _unitOfWork.SaveAsync();

            // Reload with navigation
            entity = await _unitOfWork.SetTracked<Conversation>()
                .Include(c => c.ConversationParticipants)
                    .ThenInclude(p => p.User)
                .FirstAsync(c => c.Id == entity.Id, cancellationToken);
        }

        return ToConversationModel(entity);
    }

    public async Task<IEnumerable<ConversationModel>> GetVictimConversationsAsync(
        Guid victimId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.Set<Conversation>()
            .Where(c => c.VictimId == victimId)
            .Include(c => c.ConversationParticipants)
                .ThenInclude(p => p.User)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToConversationModel);
    }

    public async Task<ConversationModel?> GetByVictimIdAsync(
        Guid victimId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Set<Conversation>()
            .Include(c => c.ConversationParticipants)
                .ThenInclude(p => p.User)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(c => c.VictimId == victimId, cancellationToken);

        return entity == null ? null : ToConversationModel(entity);
    }

    public async Task<ConversationModel?> GetByIdAsync(
        int conversationId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Set<Conversation>()
            .Include(c => c.ConversationParticipants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        return entity == null ? null : ToConversationModel(entity);
    }

    // --- Legacy: mission-based -----------------------------------------------

    public async Task<IEnumerable<ConversationModel>> GetAllByMissionIdForUserAsync(
        int missionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.Set<Conversation>()
            .Where(c => c.MissionId == missionId &&
                        c.ConversationParticipants.Any(p => p.UserId == userId))
            .Include(c => c.ConversationParticipants)
                .ThenInclude(p => p.User)
            .ToListAsync(cancellationToken);

        return entities.Select(ToConversationModel);
    }

    // --- Status transitions --------------------------------------------------

    public async Task UpdateStatusAsync(
        int conversationId,
        ConversationStatus status,
        string? selectedTopic = null,
        int? linkedSosRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.SetTracked<Conversation>()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (entity == null) return;

        entity.Status = status.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        if (selectedTopic != null)
            entity.SelectedTopic = selectedTopic;

        if (linkedSosRequestId.HasValue)
            entity.LinkedSosRequestId = linkedSosRequestId;

        await _unitOfWork.SaveAsync();
    }

    // --- Participants --------------------------------------------------------

    public async Task<bool> IsParticipantAsync(
        int conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<ConversationParticipant>()
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId, cancellationToken);
    }

    public async Task AddCoordinatorAsync(
        int conversationId, Guid coordinatorId, CancellationToken cancellationToken = default)
    {
        var alreadyJoined = await _unitOfWork.Set<ConversationParticipant>()
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == coordinatorId, cancellationToken);

        if (!alreadyJoined)
        {
            await _unitOfWork.GetRepository<ConversationParticipant>().AddAsync(new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = coordinatorId,
                RoleInConversation = "Coordinator",
                JoinedAt = DateTime.UtcNow
            });
            await _unitOfWork.SaveAsync();
        }
    }

    public async Task RemoveCoordinatorAsync(
        int conversationId, Guid coordinatorId, CancellationToken cancellationToken = default)
    {
        var participant = await _unitOfWork.SetTracked<ConversationParticipant>()
            .FirstOrDefaultAsync(
                p => p.ConversationId == conversationId && p.UserId == coordinatorId,
                cancellationToken);

        if (participant != null)
        {
            await _unitOfWork.GetRepository<ConversationParticipant>().DeleteAsync(participant.Id);
            await _unitOfWork.SaveAsync();
        }
    }

    public async Task<IEnumerable<ConversationModel>> GetConversationsWaitingForCoordinatorAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _unitOfWork.Set<Conversation>()
            .Where(c => c.Status == nameof(ConversationStatus.WaitingCoordinator))
            .Include(c => c.ConversationParticipants)
                .ThenInclude(p => p.User)
            .OrderBy(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToConversationModel);
    }

    // --- Messages ------------------------------------------------------------

    public async Task<IEnumerable<MessageModel>> GetMessagesAsync(
        int conversationId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var messages = await _unitOfWork.Set<Message>()
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender)
            .OrderBy(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return messages.Select(ToMessageModel);
    }

    public async Task<MessageModel> SendMessageAsync(
        int conversationId,
        Guid? senderId,
        string content,
        MessageType messageType = MessageType.UserMessage,
        CancellationToken cancellationToken = default)
    {
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            MessageType = messageType.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.GetRepository<Message>().AddAsync(message);
        await _unitOfWork.SaveAsync();

        User? sender = null;
        if (senderId.HasValue)
        {
            sender = await _unitOfWork.Set<User>()
                .FirstOrDefaultAsync(u => u.Id == senderId.Value, cancellationToken);
        }

        return new MessageModel
        {
            Id = message.Id,
            ConversationId = conversationId,
            SenderId = senderId,
            SenderName = sender is not null
                ? $"{sender.LastName} {sender.FirstName}".Trim()
                : null,
            Content = content,
            MessageType = messageType,
            CreatedAt = message.CreatedAt
        };
    }

    // --- Helpers -------------------------------------------------------------

    private static ConversationModel ToConversationModel(Conversation entity) => new()
    {
        Id = entity.Id,
        VictimId = entity.VictimId,
        MissionId = entity.MissionId,
        Status = Enum.TryParse<ConversationStatus>(entity.Status, out var s) ? s : ConversationStatus.AiAssist,
        SelectedTopic = entity.SelectedTopic,
        LinkedSosRequestId = entity.LinkedSosRequestId,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        Participants = entity.ConversationParticipants.Select(p => new ConversationParticipantModel
        {
            Id = p.Id,
            ConversationId = p.ConversationId,
            UserId = p.UserId,
            UserName = p.User is not null
                ? $"{p.User.LastName} {p.User.FirstName}".Trim()
                : null,
            RoleInConversation = p.RoleInConversation,
            JoinedAt = p.JoinedAt
        }).ToList()
    };

    private static MessageModel ToMessageModel(Message m) => new()
    {
        Id = m.Id,
        ConversationId = m.ConversationId,
        SenderId = m.SenderId,
        SenderName = m.Sender is not null
            ? $"{m.Sender.LastName} {m.Sender.FirstName}".Trim()
            : null,
        Content = m.Content,
        MessageType = Enum.TryParse<MessageType>(m.MessageType, out var mt) ? mt : MessageType.UserMessage,
        CreatedAt = m.CreatedAt
    };
}

