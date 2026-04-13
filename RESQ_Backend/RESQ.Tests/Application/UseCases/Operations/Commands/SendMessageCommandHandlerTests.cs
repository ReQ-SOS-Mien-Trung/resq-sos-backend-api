using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.SendMessage;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RESQ.Tests.Application.UseCases.Operations.Commands;

/// <summary>
/// FE-02/FE-06 – Rescuer Chat / Mission Coordination: SendMessage handler tests.
/// Covers: Hazard Reporting & In-App Chat (FE-02), Real-Time Mission Notifications (FE-06).
/// </summary>
public class SendMessageCommandHandlerTests
{
    private static readonly Guid SenderId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    [Fact]
    public async Task Handle_Throws_ForbiddenException_WhenSenderIsNotParticipant()
    {
        var stub = new StubConversationRepository { IsParticipantResult = false };
        var handler = BuildHandler(stub);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(BuildCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Throws_BadRequestException_WhenContentIsEmpty()
    {
        var stub = new StubConversationRepository { IsParticipantResult = true };
        var handler = BuildHandler(stub);

        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(BuildCommand(content: ""), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Throws_BadRequestException_WhenContentIsWhitespace()
    {
        var stub = new StubConversationRepository { IsParticipantResult = true };
        var handler = BuildHandler(stub);

        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(BuildCommand(content: "   "), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SavesMessage_AndReturnsResponse_WhenValid()
    {
        var stub = new StubConversationRepository
        {
            IsParticipantResult = true,
            SavedMessage = new MessageModel
            {
                Id = 42,
                ConversationId = 1,
                SenderId = SenderId,
                SenderName = "TestUser",
                Content = "Xin chào",
                MessageType = MessageType.UserMessage,
                CreatedAt = DateTime.UtcNow
            },
            ConversationResult = new ConversationModel
            {
                Id = 1,
                Participants =
                [
                    new ConversationParticipantModel { UserId = SenderId },
                    new ConversationParticipantModel { UserId = OtherUserId }
                ]
            }
        };
        var firebase = new RecordingFirebaseService();
        var handler = BuildHandler(stub, firebase);

        var response = await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Equal(42, response.Id);
        Assert.Equal(1, response.ConversationId);
        Assert.Equal("Xin chào", response.Content);
        Assert.Equal(MessageType.UserMessage, response.MessageType);
    }

    [Fact]
    public async Task Handle_SendsPushNotification_ToOtherParticipants()
    {
        var stub = new StubConversationRepository
        {
            IsParticipantResult = true,
            SavedMessage = new MessageModel
            {
                Id = 1,
                ConversationId = 1,
                SenderId = SenderId,
                SenderName = "Sender",
                Content = "Help!",
                MessageType = MessageType.UserMessage,
                CreatedAt = DateTime.UtcNow
            },
            ConversationResult = new ConversationModel
            {
                Id = 1,
                Participants =
                [
                    new ConversationParticipantModel { UserId = SenderId },
                    new ConversationParticipantModel { UserId = OtherUserId }
                ]
            }
        };
        var firebase = new RecordingFirebaseService();
        var handler = BuildHandler(stub, firebase);

        await handler.Handle(BuildCommand(content: "Help!"), CancellationToken.None);

        Assert.Single(firebase.NotifiedUserIds);
        Assert.Contains(OtherUserId, firebase.NotifiedUserIds);
        Assert.DoesNotContain(SenderId, firebase.NotifiedUserIds);
    }

    [Fact]
    public async Task Handle_DoesNotPush_WhenConversationIsNull()
    {
        var stub = new StubConversationRepository
        {
            IsParticipantResult = true,
            SavedMessage = new MessageModel
            {
                Id = 1,
                SenderId = SenderId,
                Content = "test",
                MessageType = MessageType.UserMessage,
                CreatedAt = DateTime.UtcNow
            },
            ConversationResult = null
        };
        var firebase = new RecordingFirebaseService();
        var handler = BuildHandler(stub, firebase);

        await handler.Handle(BuildCommand(), CancellationToken.None);

        Assert.Empty(firebase.NotifiedUserIds);
    }

    // -- Helpers --

    private static SendMessageCommand BuildCommand(
        int conversationId = 1,
        Guid? senderId = null,
        string content = "Xin chào",
        MessageType messageType = MessageType.UserMessage)
        => new(conversationId, senderId ?? SenderId, content, messageType);

    private static SendMessageCommandHandler BuildHandler(
        StubConversationRepository? conversationRepo = null,
        RecordingFirebaseService? firebase = null)
        => new(
            conversationRepo ?? new StubConversationRepository(),
            firebase ?? new RecordingFirebaseService(),
            NullLogger<SendMessageCommandHandler>.Instance);

    // -- Stubs --

    private sealed class RecordingFirebaseService : IFirebaseService
    {
        public List<Guid> NotifiedUserIds { get; } = [];

        public Task<FirebasePhoneTokenInfo> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new FirebasePhoneTokenInfo { Uid = "stub" });

        public Task<FirebaseGoogleUserInfo> VerifyGoogleIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new FirebaseGoogleUserInfo { Uid = "stub", Email = "stub@example.com" });

        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type = "general", CancellationToken cancellationToken = default)
        {
            NotifiedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type, Dictionary<string, string> data, CancellationToken cancellationToken = default)
        {
            NotifiedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public Task SendToTopicAsync(string topic, string title, string body, string type = "general", CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendToTopicAsync(string topic, string title, string body, Dictionary<string, string> data, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeToUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UnsubscribeFromUserTopicAsync(string fcmToken, Guid userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubConversationRepository : IConversationRepository
    {
        public bool IsParticipantResult { get; set; }
        public MessageModel? SavedMessage { get; set; }
        public ConversationModel? ConversationResult { get; set; }

        public Task<bool> IsParticipantAsync(int conversationId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(IsParticipantResult);

        public Task<MessageModel> SendMessageAsync(int conversationId, Guid? senderId, string content, MessageType messageType = MessageType.UserMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(SavedMessage ?? new MessageModel { Id = 1, Content = content, SenderId = senderId, MessageType = messageType, CreatedAt = DateTime.UtcNow });

        public Task<ConversationModel?> GetByIdAsync(int conversationId, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationResult);

        // -- Unused but required by interface --

        public Task<ConversationModel> GetOrCreateForVictimAsync(Guid victimId, CancellationToken cancellationToken = default)
            => Task.FromResult(new ConversationModel());

        public Task<IEnumerable<ConversationModel>> GetVictimConversationsAsync(Guid victimId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<ConversationModel>());

        public Task<ConversationModel?> GetByVictimIdAsync(Guid victimId, CancellationToken cancellationToken = default)
            => Task.FromResult<ConversationModel?>(null);

        public Task<IEnumerable<ConversationModel>> GetAllByMissionIdForUserAsync(int missionId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<ConversationModel>());

        public Task UpdateStatusAsync(int conversationId, ConversationStatus status, string? selectedTopic = null, int? linkedSosRequestId = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddCoordinatorAsync(int conversationId, Guid coordinatorId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveCoordinatorAsync(int conversationId, Guid coordinatorId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IEnumerable<ConversationModel>> GetConversationsWaitingForCoordinatorAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<ConversationModel>());

        public Task<IEnumerable<MessageModel>> GetMessagesAsync(int conversationId, int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<MessageModel>());
    }
}
