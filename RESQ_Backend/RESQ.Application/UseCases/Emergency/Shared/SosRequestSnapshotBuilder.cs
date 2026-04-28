using System.Text.Json;
using RESQ.Application.Common;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Emergency.Queries;
using RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;
using RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Shared;

public sealed class SosRequestSnapshotBuilder(
    ISosRequestRepository sosRequestRepository,
    ISosRequestCompanionRepository companionRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    ISosRuleEvaluationRepository sosRuleEvaluationRepository,
    ISosAiAnalysisRepository sosAiAnalysisRepository,
    IUserRepository userRepository) : ISosRequestSnapshotBuilder
{
    public async Task<SosRequestDetailDto?> BuildAsync(
        int sosRequestId,
        CancellationToken cancellationToken = default)
    {
        var sosRequest = await sosRequestRepository.GetByIdAsync(sosRequestId, cancellationToken);
        return sosRequest is null
            ? null
            : await BuildAsync(sosRequest, cancellationToken);
    }

    public async Task<SosRequestDetailDto> BuildAsync(
        SosRequestModel sosRequest,
        CancellationToken cancellationToken = default)
    {
        var victimUpdateLookup = await sosRequestUpdateRepository.GetLatestVictimUpdatesBySosRequestIdsAsync(
            [sosRequest.Id],
            cancellationToken);
        victimUpdateLookup.TryGetValue(sosRequest.Id, out var latestVictimUpdate);
        var effectiveSosRequest = SosRequestVictimUpdateOverlay.Apply(sosRequest, latestVictimUpdate);

        var incidentLookup = await sosRequestUpdateRepository.GetIncidentHistoryBySosRequestIdsAsync(
            [sosRequest.Id],
            cancellationToken);
        incidentLookup.TryGetValue(sosRequest.Id, out var incidents);
        var latestIncident = incidents?.FirstOrDefault();

        var ruleEvaluation = await sosRuleEvaluationRepository.GetBySosRequestIdAsync(
            sosRequest.Id,
            cancellationToken);
        var aiAnalyses = await sosAiAnalysisRepository.GetAllBySosRequestIdAsync(
            sosRequest.Id,
            cancellationToken);
        var latestAiAnalysis = aiAnalyses
            .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();

        var companions = await BuildCompanionsAsync(sosRequest.Id, cancellationToken);

        return new SosRequestDetailDto
        {
            Id = effectiveSosRequest.Id,
            PacketId = effectiveSosRequest.PacketId,
            ClusterId = effectiveSosRequest.ClusterId,
            UserId = effectiveSosRequest.UserId,
            SosType = effectiveSosRequest.SosType,
            RawMessage = effectiveSosRequest.RawMessage,
            StructuredData = SosStructuredDataParser.Parse(effectiveSosRequest.StructuredData),
            NetworkMetadata = ParseJson<SosNetworkMetadataDto>(effectiveSosRequest.NetworkMetadata),
            SenderInfo = ParseJson<SosSenderInfoDto>(effectiveSosRequest.SenderInfo),
            ReporterInfo = SosStructuredDataParser.ParseReporterInfo(
                effectiveSosRequest.ReporterInfo,
                effectiveSosRequest.SenderInfo),
            VictimInfo = ParseJson<SosVictimInfoDto>(effectiveSosRequest.VictimInfo),
            IsSentOnBehalf = effectiveSosRequest.IsSentOnBehalf,
            OriginId = effectiveSosRequest.OriginId,
            Status = effectiveSosRequest.Status.ToString(),
            PriorityLevel = effectiveSosRequest.PriorityLevel?.ToString(),
            Latitude = effectiveSosRequest.Location?.Latitude,
            Longitude = effectiveSosRequest.Location?.Longitude,
            LocationAccuracy = effectiveSosRequest.LocationAccuracy,
            Timestamp = effectiveSosRequest.Timestamp,
            CreatedAt = effectiveSosRequest.CreatedAt,
            ReceivedAt = effectiveSosRequest.ReceivedAt,
            LastUpdatedAt = effectiveSosRequest.LastUpdatedAt,
            ReviewedAt = effectiveSosRequest.ReviewedAt,
            ReviewedById = effectiveSosRequest.ReviewedById,
            CreatedByCoordinatorId = effectiveSosRequest.CreatedByCoordinatorId,
            Evaluation = new SosRequestDetailEvaluationDto
            {
                RuleEvaluation = SosEvaluationViewFactory.CreateRuleEvaluation(ruleEvaluation),
                AiAnalysis = SosRequestDetailAiAnalysisMapper.Map(latestAiAnalysis)
            },
            LatestIncidentNote = latestIncident?.Note,
            LatestIncidentAt = latestIncident?.CreatedAt,
            IncidentHistory = incidents?.Select(x => new SosIncidentNoteDto
            {
                Id = x.Id,
                TeamIncidentId = x.TeamIncidentId,
                MissionId = x.MissionId,
                MissionTeamId = x.MissionTeamId,
                MissionActivityId = x.MissionActivityId,
                IncidentScope = x.IncidentScope,
                Note = x.Note,
                ReportedById = x.ReportedById,
                CreatedAt = x.CreatedAt,
                TeamName = x.TeamName,
                ActivityType = x.ActivityType
            }).ToList(),
            Companions = companions
        };
    }

    private async Task<List<CompanionResultDto>?> BuildCompanionsAsync(
        int sosRequestId,
        CancellationToken cancellationToken)
    {
        var companionRecords = await companionRepository.GetBySosRequestIdAsync(
            sosRequestId,
            cancellationToken);
        if (companionRecords.Count == 0)
            return null;

        var users = await userRepository.GetByIdsAsync(
            companionRecords.Select(c => c.UserId),
            cancellationToken);
        var userMap = users.ToDictionary(u => u.Id);

        return companionRecords.Select(c =>
        {
            userMap.TryGetValue(c.UserId, out var user);
            return new CompanionResultDto
            {
                UserId = c.UserId,
                FullName = user is null ? null : $"{user.LastName} {user.FirstName}".Trim(),
                Phone = c.PhoneNumber ?? user?.Phone,
                AddedAt = c.AddedAt
            };
        }).ToList();
    }

    private static T? ParseJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}
