using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private async Task SeedChatAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var sosByVictim = seed.SosRequests
            .Where(s => s.UserId.HasValue)
            .GroupBy(s => s.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());

        for (var i = 0; i < 140; i++)
        {
            var victim = seed.Victims[i];
            var sos = sosByVictim.GetValueOrDefault(victim.Id) ?? seed.SosRequests[i % seed.SosRequests.Count];
            var mission = seed.Missions.FirstOrDefault(m => m.ClusterId == sos.ClusterId);
            var status = i < 20 ? "AiAssist" : i < 50 ? "WaitingCoordinator" : i < 95 ? "CoordinatorActive" : "Closed";
            var conversationCreatedAt = ClampHistoricalUtc(
                (sos.CreatedAt ?? seed.StartUtc).AddMinutes(8),
                sos.CreatedAt ?? seed.StartUtc,
                seed.AnchorUtc);
            seed.Conversations.Add(new Conversation
            {
                VictimId = victim.Id,
                MissionId = i % 3 == 0 ? mission?.Id : null,
                Status = status,
                SelectedTopic = status == "AiAssist" ? "SosRequestSupport" : "Cần cập nhật ETA và vật phẩm",
                LinkedSosRequestId = sos.Id,
                CreatedAt = conversationCreatedAt,
                UpdatedAt = ClampHistoricalUtc(
                    conversationCreatedAt.AddHours(status == "Closed" ? 9 : 1),
                    conversationCreatedAt,
                    seed.AnchorUtc)
            });
        }

        _db.Conversations.AddRange(seed.Conversations);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var conversation in seed.Conversations)
        {
            var victim = seed.Victims.First(v => v.Id == conversation.VictimId);
            var coordinator = seed.Coordinators[conversation.Id % seed.Coordinators.Count];
            _db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = victim.Id,
                RoleInConversation = "Victim",
                JoinedAt = conversation.CreatedAt,
                LeftAt = conversation.Status == "Closed" ? conversation.UpdatedAt : null
            });
            _db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = coordinator.Id,
                RoleInConversation = "Coordinator",
                JoinedAt = ClampHistoricalUtc(
                    conversation.CreatedAt?.AddMinutes(conversation.Status == "WaitingCoordinator" ? 30 : 3),
                    conversation.CreatedAt ?? seed.StartUtc,
                    seed.AnchorUtc),
                LeftAt = conversation.Status == "Closed" ? conversation.UpdatedAt : null
            });
        }

        var messages = new List<Message>();
        for (var conversationIndex = 0; conversationIndex < seed.Conversations.Count; conversationIndex++)
        {
            var conversation = seed.Conversations[conversationIndex];
            var victim = seed.Victims.First(v => v.Id == conversation.VictimId);
            var coordinator = seed.Coordinators[conversationIndex % seed.Coordinators.Count];
            var count = 13 + (conversationIndex < 80 ? 1 : 0);
            for (var i = 0; i < count; i++)
            {
                var messageType = i == 1 ? "AiMessage" : i % 7 == 0 ? "SystemMessage" : "UserMessage";
                messages.Add(new Message
                {
                    ConversationId = conversation.Id,
                    SenderId = messageType == "SystemMessage" ? null : messageType == "AiMessage" ? null : i % 2 == 0 ? victim.Id : coordinator.Id,
                    Content = ChatMessage(i, conversation.Status),
                    MessageType = messageType,
                    CreatedAt = ClampHistoricalUtc(
                        (conversation.CreatedAt ?? seed.StartUtc).AddMinutes(i * 4),
                        conversation.CreatedAt ?? seed.StartUtc,
                        seed.AnchorUtc)
                });
            }
        }

        _db.Messages.AddRange(messages);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSupplyRequestsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var inProgressStatuses = new[]
        {
            ("Pending", "WaitingForApproval"),
            ("Accepted", "Approved"),
            ("Preparing", "Approved"),
            ("Shipping", "InTransit")
        };
        const int depotOneTwoRequestCount = 24;
        const int depotOneTwoIncompleteRequestCount = 12;
        var completedStatus = ("Completed", "Received");
        var completedOnlyDepots = seed.Depots
            .Skip(2)
            .Where(depot => !IsDepotClosureTestCandidate(depot))
            .ToList();

        for (var i = 0; i < 95; i++)
        {
            Depot requesting;
            Depot source;
            (string SourceStatus, string RequestingStatus) status;

            if (i < depotOneTwoRequestCount)
            {
                requesting = seed.Depots[i % 2];
                source = seed.Depots[(i + 1) % 2];
                status = i < depotOneTwoIncompleteRequestCount
                    ? inProgressStatuses[i % inProgressStatuses.Length]
                    : completedStatus;
            }
            else
            {
                var completedIndex = i - depotOneTwoRequestCount;
                requesting = completedOnlyDepots[completedIndex % completedOnlyDepots.Count];
                source = completedOnlyDepots[(completedIndex + 2) % completedOnlyDepots.Count];
                status = completedStatus;
            }

            var created = RandomEventUtc(seed, i + 220);
            var timeline = BuildSupplyRequestTimeline(created, status.SourceStatus, seed.AnchorUtc);
            var sourceManager = seed.Managers[(source.Id - 1) % seed.Managers.Count];
            var requestingManager = seed.Managers[(requesting.Id - 1) % seed.Managers.Count];
            seed.SupplyRequests.Add(new DepotSupplyRequest
            {
                RequestingDepotId = requesting.Id,
                SourceDepotId = source.Id,
                Note = SupplyRequestNote(i),
                PriorityLevel = status.SourceStatus == "Pending"
                    ? "Urgent"
                    : status.SourceStatus is "Accepted" or "Preparing" or "Shipping"
                        ? "High"
                        : i % 5 == 0 ? "High" : "Medium",
                SourceStatus = status.SourceStatus,
                RequestingStatus = status.RequestingStatus,
                RejectedReason = null,
                RequestedBy = requestingManager.Id,
                CreatedAt = created,
                AutoRejectAt = status.SourceStatus == "Pending" ? created.AddHours(i % 3 == 0 ? 2 : 6) : null,
                HighEscalationNotified = status.SourceStatus is "Accepted" or "Preparing" or "Shipping" or "Pending",
                HighEscalationNotifiedAt = timeline.HighEscalationNotifiedAt,
                UrgentEscalationNotified = status.SourceStatus == "Pending",
                UrgentEscalationNotifiedAt = timeline.UrgentEscalationNotifiedAt,
                RespondedAt = timeline.RespondedAt,
                ShippedAt = timeline.ShippedAt,
                CompletedAt = timeline.CompletedAt,
                UpdatedAt = timeline.UpdatedAt,
                AcceptedBy = status.SourceStatus is "Accepted" or "Preparing" or "Shipping" or "Completed" ? sourceManager.Id : null,
                RejectedBy = null,
                PreparedBy = status.SourceStatus is "Preparing" or "Shipping" or "Completed" ? sourceManager.Id : null,
                ShippedBy = status.SourceStatus is "Shipping" or "Completed" ? sourceManager.Id : null,
                CompletedBy = status.SourceStatus == "Completed" ? sourceManager.Id : null,
                ConfirmedBy = status.SourceStatus == "Completed" ? requestingManager.Id : null
            });
        }

        _db.DepotSupplyRequests.AddRange(seed.SupplyRequests);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var request in seed.SupplyRequests)
        {
            var itemCount = request.Id % 2 == 0 ? 3 : 2;
            for (var j = 0; j < itemCount; j++)
            {
                var item = seed.ItemModels[(request.Id * 3 + j) % seed.ItemModels.Count];
                _db.DepotSupplyRequestItems.Add(new DepotSupplyRequestItem
                {
                    DepotSupplyRequestId = request.Id,
                    ItemModelId = item.Id,
                    Quantity = item.ItemType == "Reusable" ? 2 + j : 60 + j * 40 + request.Id % 30
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static (
        DateTime? HighEscalationNotifiedAt,
        DateTime? UrgentEscalationNotifiedAt,
        DateTime? RespondedAt,
        DateTime? ShippedAt,
        DateTime? CompletedAt,
        DateTime UpdatedAt)
        BuildSupplyRequestTimeline(DateTime createdAt, string sourceStatus, DateTime anchorUtc)
    {
        DateTime? highEscalationNotifiedAt = sourceStatus is "Accepted" or "Preparing" or "Shipping" or "Pending"
            ? ClampHistoricalUtc(createdAt.AddMinutes(60), createdAt, anchorUtc)
            : null;
        DateTime? urgentEscalationNotifiedAt = sourceStatus == "Pending"
            ? ClampHistoricalUtc(createdAt.AddMinutes(25), createdAt, anchorUtc)
            : null;
        DateTime? respondedAt = sourceStatus == "Pending"
            ? null
            : ClampHistoricalUtc(createdAt.AddMinutes(30), createdAt, anchorUtc);
        DateTime? shippedAt = sourceStatus is "Shipping" or "Completed"
            ? ClampHistoricalUtc(createdAt.AddHours(3), respondedAt ?? createdAt, anchorUtc)
            : null;
        DateTime? completedAt = sourceStatus == "Completed"
            ? ClampHistoricalUtc(createdAt.AddHours(7), shippedAt ?? respondedAt ?? createdAt, anchorUtc)
            : null;
        var updatedAtCandidate = sourceStatus switch
        {
            "Completed" => createdAt.AddHours(7),
            "Shipping" => createdAt.AddHours(3),
            _ => createdAt.AddHours(1)
        };
        var updatedAtLowerBound = completedAt ?? shippedAt ?? respondedAt ?? highEscalationNotifiedAt ?? urgentEscalationNotifiedAt ?? createdAt;
        var updatedAt = ClampHistoricalUtc(updatedAtCandidate, updatedAtLowerBound, anchorUtc);
        return (highEscalationNotifiedAt, urgentEscalationNotifiedAt, respondedAt, shippedAt, completedAt, updatedAt);
    }


    private static string ChatMessage(int index, string? status)
    {
        var active = status == "CoordinatorActive";
        var messages = new[]
        {
            "Hệ thống đã ghi nhận yêu cầu hỗ trợ.",
            "Tôi đã đọc thông tin SOS, bạn hãy giữ điện thoại khô và bật âm lượng.",
            "Nhà em còn một bà cụ không đi lại được, nước đang lên nhanh.",
            active ? "Đội cứu hộ đang di chuyển từ điểm tập kết gần nhất." : "Tôi đang chờ điều phối viên phản hồi.",
            "Nếu có thể, hãy tập trung mọi người ở vị trí cao nhất trong nhà.",
            "Gia đình còn nước uống khoảng nửa ngày.",
            "Đã bổ sung nhu cầu nước uống và thuốc vào ghi chú mission.",
            "Có trẻ nhỏ nên cần áo phao cỡ nhỏ khi tiếp cận.",
            "Tín hiệu hơi yếu, tôi sẽ gửi vị trí lại.",
            "Đã nhận vị trí, sai số khoảng dưới 30m.",
            "Khi thấy đội cứu hộ, hãy dùng đèn pin hoặc khăn sáng màu để báo hiệu.",
            "Cảm ơn, gia đình sẽ chờ ở tầng hai.",
            "Cuộc hội thoại được lưu để điều phối tiếp theo.",
            "Ảnh hiện trường: https://cdn.resq.vn/chat/flood-demo.jpg"
        };
        return messages[index % messages.Length];
    }

    private static string SupplyRequestNote(int index)
    {
        var notes = new[]
        {
            "Thiếu nước uống và thuốc hạ sốt cho đợt lũ Quảng Điền.",
            "Cần bổ sung áo phao, dây cứu sinh cho đội xuồng.",
            "Kho địa phương gần cạn lương khô và sữa trẻ em.",
            "Xin điều chuyển bộ đàm và pin dự phòng trước bão.",
            "Cần máy phát điện và đèn sạc cho điểm tránh trú."
        };
        return notes[index % notes.Length];
    }
}
