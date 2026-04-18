using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;
using RESQ.Application.Services.Ai;
using RESQ.Application.UseCases.Emergency.Shared;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.System;

namespace RESQ.Infrastructure.Services;

public partial class RescueMissionSuggestionService
{
    private const int MaxPipelineToolTurns = 8;
    private const string DraftSuggestionPhase = "Draft";
    private const string ValidatedSuggestionPhase = "Validated";
    private const string ExecutionSuggestionPhase = "Execution";

    private static readonly JsonSerializerOptions PipelineJsonDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        AllowTrailingCommas = true
    };

    private sealed class MissionSuggestionPipelineFallbackException(string message, Exception? innerException = null)
        : Exception(message, innerException);

    private sealed record PromptStageResult(
        string ModelName,
        string ResponseText,
        long LatencyMs);

    private async IAsyncEnumerable<SseMissionEvent> GeneratePipelineSuggestionStreamAsync(
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots,
        IReadOnlyCollection<AgentTeamInfo> nearbyTeams,
        bool isMultiDepotRecommended,
        int? clusterId,
        int? suggestionId,
        MissionSuggestionMetadata metadata,
        AiConfigModel aiConfig,
        MissionSuggestionExecutionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        MissionRequirementsFragment requirements;
        MissionDepotFragment depot;
        MissionTeamFragment team;

        yield return Status("requirements");
        try
        {
            var stage = await ExecuteSingleStageAsync(
                PromptType.MissionRequirementsAssessment,
                BuildStageUserMessage(
                    null,
                    new Dictionary<string, string>
                    {
                        ["sos_requests_data"] = BuildSosRequestsData(sosRequests),
                        ["total_count"] = sosRequests.Count.ToString()
                    },
                    "Analyze SOS requests and return JSON for mission requirements only."),
                "No tools are available. Return JSON only.",
                aiConfig,
                options,
                cancellationToken);

            requirements = DeserializePipelineFragment<MissionRequirementsFragment>(stage.ResponseText);
            ValidateRequirementsFragment(requirements, sosRequests);

            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "requirements",
                "completed",
                PromptType.MissionRequirementsAssessment,
                stage.ModelName,
                stage.LatencyMs,
                SerializePipelineFragment(requirements),
                null,
                "running",
                cancellationToken);
        }
        catch (Exception ex)
        {
            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "requirements",
                "failed",
                PromptType.MissionRequirementsAssessment,
                error: ex.Message,
                pipelineStatus: "fallback",
                cancellationToken: cancellationToken);
            throw new MissionSuggestionPipelineFallbackException("Requirements stage failed.", ex);
        }

        yield return Status("depot");
        try
        {
            var stage = await ExecuteToolStageAsync(
                PromptType.MissionDepotPlanning,
                BuildStageUserMessage(
                    null,
                    new Dictionary<string, string>
                    {
                        ["sos_requests_data"] = BuildSosRequestsData(sosRequests),
                        ["requirements_fragment"] = SerializePipelineFragment(requirements),
                        ["single_depot_required"] = bool.TrueString,
                        ["eligible_depot_count"] = (nearbyDepots?.Count ?? 0).ToString()
                    },
                    "Plan depot collection and delivery fragments. Use only inventory lookup results. Choose exactly one depot for the whole mission. Do not split supplies across multiple depots. Search both relief stock and transport or reusable equipment from inventory when the plan needs vehicles or field gear. If the chosen depot lacks stock, keep the one-depot plan and fill needs_additional_depot plus supply_shortages."),
                "Only searchInventory is available. It is already scoped to eligible depots for this cluster. If a depot-backed vehicle or reusable item is selected, keep it inside COLLECT_SUPPLIES and RETURN_SUPPLIES with depot and item identifiers; do not demote it to resources[]. This stage only suggests the plan and does not reserve inventory. Do not invent depot_id or item_id. Return JSON only.",
                BuildAllowedTools("searchInventory"),
                nearbyDepots,
                nearbyTeams,
                aiConfig,
                options,
                cancellationToken);

            depot = DeserializePipelineFragment<MissionDepotFragment>(stage.ResponseText);
            ValidateDepotFragment(depot);

            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "depot",
                "completed",
                PromptType.MissionDepotPlanning,
                stage.ModelName,
                stage.LatencyMs,
                SerializePipelineFragment(depot),
                null,
                "running",
                cancellationToken);
        }
        catch (Exception ex)
        {
            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "depot",
                "failed",
                PromptType.MissionDepotPlanning,
                error: ex.Message,
                pipelineStatus: "fallback",
                cancellationToken: cancellationToken);
            throw new MissionSuggestionPipelineFallbackException("Depot stage failed.", ex);
        }

        yield return Status("team");
        try
        {
            var stage = await ExecuteToolStageAsync(
                PromptType.MissionTeamPlanning,
                BuildStageUserMessage(
                    null,
                    new Dictionary<string, string>
                    {
                        ["sos_requests_data"] = BuildSosRequestsData(sosRequests),
                        ["requirements_fragment"] = SerializePipelineFragment(requirements),
                        ["depot_fragment"] = SerializePipelineFragment(depot),
                        ["nearby_team_count"] = nearbyTeams.Count.ToString()
                    },
                    "Assign nearby teams and add rescue or medical activities as JSON fragments."),
                "Only getTeams and getAssemblyPoints are available. Do not invent team_id or assembly_point_id. Return JSON only.",
                BuildAllowedTools("getTeams", "getAssemblyPoints"),
                nearbyDepots,
                nearbyTeams,
                aiConfig,
                options,
                cancellationToken);

            team = DeserializePipelineFragment<MissionTeamFragment>(stage.ResponseText);
            ValidateTeamFragment(team);

            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "team",
                "completed",
                PromptType.MissionTeamPlanning,
                stage.ModelName,
                stage.LatencyMs,
                SerializePipelineFragment(team),
                null,
                "running",
                cancellationToken);
        }
        catch (Exception ex)
        {
            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "team",
                "failed",
                PromptType.MissionTeamPlanning,
                error: ex.Message,
                pipelineStatus: "fallback",
                cancellationToken: cancellationToken);
            throw new MissionSuggestionPipelineFallbackException("Team stage failed.", ex);
        }

        yield return Status("assemble");

        var draftBody = AssembleDraftBody(requirements, depot, team);
        var draftJson = SerializeMissionDraftBody(draftBody);
        var draftActivities = draftBody.Activities
            .Select(MapDraftActivityToSuggestedActivity)
            .ToList();

        await SavePipelineStageSnapshotAsync(
            suggestionId,
            metadata,
            "assemble",
            "completed",
            outputJson: draftJson,
            pipelineStatus: "running",
            cancellationToken: cancellationToken);

        yield return Status("validate");

        var finalResultSource = "validated";
        RescueMissionSuggestionResult result;

        try
        {
            var stage = await ExecuteSingleStageAsync(
                PromptType.MissionPlanValidation,
                BuildStageUserMessage(
                    null,
                    new Dictionary<string, string>
                    {
                        ["sos_requests_data"] = BuildSosRequestsData(sosRequests),
                        ["mission_draft_body"] = draftJson
                    },
                    "Rewrite the assembled mission draft as the final mission JSON schema. Preserve the single selected depot, needs_additional_depot, and supply_shortages fields. Preserve any inventory-backed transport or reusable equipment inside supplies_to_collect. Keep the JSON contract unchanged."),
                "No tools are available. Return the full mission JSON only. Do not introduce a second depot. Do not add warnings[] or any new warning schema.",
                aiConfig,
                options,
                cancellationToken);

            result = ParseMissionSuggestion(stage.ResponseText);
            result.IsSuccess = true;
            result.ModelName = stage.ModelName;
            result.RawAiResponse = stage.ResponseText;

            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "validate",
                "completed",
                PromptType.MissionPlanValidation,
                stage.ModelName,
                stage.LatencyMs,
                ExtractJsonPayload(stage.ResponseText),
                null,
                "completed",
                cancellationToken,
                "validated");
        }
        catch (Exception ex)
        {
            finalResultSource = "draft";
            result = MapDraftBodyToResult(draftBody, draftJson);
            result.NeedsManualReview = true;
            result.SpecialNotes = AppendSpecialNote(
                result.SpecialNotes,
                "Final validation failed. Please review the assembled mission draft manually.");

            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "validate",
                "failed",
                PromptType.MissionPlanValidation,
                error: ex.Message,
                pipelineStatus: "completed",
                cancellationToken: cancellationToken,
                finalResultSource: finalResultSource);
        }

        await FinalizeSuggestionResultAsync(
            result,
            sosRequests,
            nearbyDepots,
            nearbyTeams,
            isMultiDepotRecommended,
            clusterId,
            suggestionId,
            metadata,
            draftActivities,
            finalResultSource,
            options,
            cancellationToken);

        yield return new SseMissionEvent
        {
            EventType = "result",
            Result = result
        };
    }

    private async Task<PromptStageResult> ExecuteSingleStageAsync(
        PromptType promptType,
        string userMessage,
        string systemAppendix,
        AiConfigModel aiConfig,
        MissionSuggestionExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var (prompt, settings) = await GetStagePromptAsync(promptType, aiConfig, options, cancellationToken);
        var providerClient = _aiProviderClientFactory.GetClient(settings.Provider);
        var stopwatch = Stopwatch.StartNew();

        var response = await SendAiRequestWithRetriesAsync(
            providerClient,
            new AiCompletionRequest
            {
                Provider = settings.Provider,
                Model = settings.Model,
                ApiUrl = settings.ApiUrl,
                ApiKey = settings.ApiKey,
                SystemPrompt = BuildStageSystemPrompt(prompt.SystemPrompt, systemAppendix),
                Temperature = settings.Temperature,
                MaxTokens = Math.Max(settings.MaxTokens, 8192),
                Timeout = TimeSpan.FromSeconds(120),
                Messages = [AiChatMessage.User(BuildStageUserMessage(prompt.UserPromptTemplate, new Dictionary<string, string>(), userMessage))]
            },
            promptType.ToString(),
            cancellationToken);

        stopwatch.Stop();
        ThrowIfPromptResponseInvalid(response, promptType, allowToolCalls: false);

        return new PromptStageResult(
            settings.Model,
            response.Text?.Trim() ?? string.Empty,
            Math.Max(stopwatch.ElapsedMilliseconds, response.LatencyMs));
    }

    private async Task<PromptStageResult> ExecuteToolStageAsync(
        PromptType promptType,
        string userMessage,
        string systemAppendix,
        IReadOnlyList<AiToolDefinition> tools,
        IReadOnlyCollection<DepotSummary>? nearbyDepots,
        IReadOnlyCollection<AgentTeamInfo> nearbyTeams,
        AiConfigModel aiConfig,
        MissionSuggestionExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var (prompt, settings) = await GetStagePromptAsync(promptType, aiConfig, options, cancellationToken);
        var providerClient = _aiProviderClientFactory.GetClient(settings.Provider);
        var messages = new List<AiChatMessage>
        {
            AiChatMessage.User(BuildStageUserMessage(prompt.UserPromptTemplate, new Dictionary<string, string>(), userMessage))
        };

        var stopwatch = Stopwatch.StartNew();
        string? finalText = null;

        for (var turn = 0; turn < MaxPipelineToolTurns; turn++)
        {
            var response = await SendAiRequestWithRetriesAsync(
                providerClient,
                new AiCompletionRequest
                {
                    Provider = settings.Provider,
                    Model = settings.Model,
                    ApiUrl = settings.ApiUrl,
                    ApiKey = settings.ApiKey,
                    SystemPrompt = BuildStageSystemPrompt(prompt.SystemPrompt, systemAppendix),
                    Temperature = settings.Temperature,
                    MaxTokens = Math.Max(settings.MaxTokens, 16384),
                    Timeout = TimeSpan.FromSeconds(120),
                    Messages = messages,
                    Tools = tools
                },
                $"{promptType}:{turn + 1}",
                cancellationToken);

            ThrowIfPromptResponseInvalid(response, promptType, allowToolCalls: true);
            messages.Add(AiChatMessage.Assistant(response.Text, response.ToolCalls));

            if (response.ToolCalls.Count == 0)
            {
                finalText = response.Text;
                break;
            }

            foreach (var toolCall in response.ToolCalls)
            {
                JsonElement toolResult;
                try
                {
                    toolResult = await ExecuteToolAsync(toolCall.Name, toolCall.Arguments, nearbyDepots, nearbyTeams, cancellationToken);
                }
                catch (Exception ex)
                {
                    toolResult = JsonSerializer.SerializeToElement(new { error = ex.Message });
                }

                messages.Add(AiChatMessage.Tool(toolCall.Id, toolCall.Name, toolResult));
            }
        }

        stopwatch.Stop();

        if (string.IsNullOrWhiteSpace(finalText))
            throw new InvalidOperationException($"Stage '{promptType}' did not return a final JSON payload.");

        return new PromptStageResult(
            settings.Model,
            finalText.Trim(),
            stopwatch.ElapsedMilliseconds);
    }

    private async Task<(PromptModel Prompt, AiPromptExecutionSettings Settings)> GetStagePromptAsync(
        PromptType promptType,
        AiConfigModel aiConfig,
        MissionSuggestionExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var prompt = options.PromptOverride?.PromptType == promptType
            ? options.PromptOverride
            : await _promptRepository.GetActiveByTypeAsync(promptType, cancellationToken);

        if (prompt is null)
            throw new MissionSuggestionPipelineFallbackException($"Missing active prompt '{promptType}'.");

        var settings = _settingsResolver.Resolve(aiConfig);

        return (prompt, settings);
    }

    private async Task<AiCompletionResponse> SendAiRequestWithRetriesAsync(
        IAiProviderClient providerClient,
        AiCompletionRequest request,
        string stageName,
        CancellationToken cancellationToken)
    {
        AiCompletionResponse? response = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            response = await providerClient.CompleteAsync(request, cancellationToken);

            if (response.HttpStatusCode != 503)
                return response;

            if (attempt < 2)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 2));
                _logger.LogWarning(
                    "Mission pipeline stage {stageName} received 503 from provider, retrying in {delaySeconds}s",
                    stageName,
                    (int)delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        return response ?? new AiCompletionResponse
        {
            HttpStatusCode = 500,
            ErrorBody = $"Stage '{stageName}' returned no response."
        };
    }

    private static void ThrowIfPromptResponseInvalid(
        AiCompletionResponse response,
        PromptType promptType,
        bool allowToolCalls)
    {
        if (response.HttpStatusCode is >= 400)
            throw new InvalidOperationException(
                $"AI returned HTTP {(response.HttpStatusCode ?? 0)} for stage '{promptType}'. {response.ErrorBody}");

        if (!string.IsNullOrWhiteSpace(response.BlockReason)
            && !string.Equals(response.BlockReason, "BLOCK_REASON_UNSPECIFIED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"AI blocked stage '{promptType}' with reason '{response.BlockReason}'.");
        }

        if (allowToolCalls)
        {
            if (string.IsNullOrWhiteSpace(response.Text) && response.ToolCalls.Count == 0)
                throw new InvalidOperationException($"AI returned empty content for stage '{promptType}'.");

            return;
        }

        if (string.IsNullOrWhiteSpace(response.Text))
            throw new InvalidOperationException($"AI returned empty content for stage '{promptType}'.");
    }

    private static string BuildStageUserMessage(
        string? template,
        IReadOnlyDictionary<string, string> replacements,
        string fallbackInstructions)
    {
        var message = string.IsNullOrWhiteSpace(template)
            ? fallbackInstructions
            : template.Trim();

        foreach (var pair in replacements)
            message = message.Replace($"{{{{{pair.Key}}}}}", pair.Value, StringComparison.Ordinal);

        if (replacements.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(template) && !string.IsNullOrWhiteSpace(fallbackInstructions))
                return $"{message.Trim()}\n\n{fallbackInstructions.Trim()}".Trim();

            return message.Trim();
        }

        var contextBlock = string.Join(
            "\n\n",
            replacements.Select(pair => $"{pair.Key.ToUpperInvariant()}:\n{pair.Value}"));

        return string.IsNullOrWhiteSpace(contextBlock)
            ? message.Trim()
            : $"{message.Trim()}\n\n{contextBlock}".Trim();
    }

    private static string BuildStageSystemPrompt(string? systemPrompt, string appendix)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
            return appendix.Trim();

        if (string.IsNullOrWhiteSpace(appendix))
            return systemPrompt.Trim();

        return $"{systemPrompt.Trim()}\n\n{appendix.Trim()}";
    }

    private static IReadOnlyList<AiToolDefinition> BuildAllowedTools(params string[] toolNames)
    {
        var allowed = new HashSet<string>(toolNames, StringComparer.OrdinalIgnoreCase);
        return BuildToolDefinitions()
            .Where(tool => allowed.Contains(tool.Name))
            .ToList();
    }

    private static T DeserializePipelineFragment<T>(string rawResponse)
    {
        var json = ExtractJsonPayload(rawResponse);
        var result = JsonSerializer.Deserialize<T>(json, PipelineJsonDeserializeOptions);

        if (result is null)
            throw new InvalidOperationException($"Could not parse pipeline fragment '{typeof(T).Name}'.");

        return result;
    }

    private static string ExtractJsonPayload(string rawResponse)
    {
        var cleaned = rawResponse.Trim();

        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = cleaned.IndexOf('\n');
            if (firstLineEnd >= 0)
                cleaned = cleaned[(firstLineEnd + 1)..];

            var fenceEnd = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0)
                cleaned = cleaned[..fenceEnd];
        }

        cleaned = cleaned.Trim();

        var objectStart = cleaned.IndexOf('{');
        var objectEnd = cleaned.LastIndexOf('}');
        if (objectStart >= 0 && objectEnd > objectStart)
            return cleaned[objectStart..(objectEnd + 1)];

        throw new InvalidOperationException("Response does not contain a JSON object.");
    }

    private static void ValidateRequirementsFragment(
        MissionRequirementsFragment fragment,
        IReadOnlyCollection<SosRequestSummary> sosRequests)
    {
        if (fragment.SosRequirements.Count == 0)
            throw new InvalidOperationException("Requirements fragment must contain at least one SOS requirement.");

        var knownSosIds = sosRequests.Select(sos => sos.Id).ToHashSet();
        foreach (var sosRequirement in fragment.SosRequirements)
        {
            if (!knownSosIds.Contains(sosRequirement.SosRequestId))
                throw new InvalidOperationException($"Requirements fragment references unknown SOS #{sosRequirement.SosRequestId}.");
        }
    }

    private static void ValidateDepotFragment(MissionDepotFragment fragment)
    {
        foreach (var activity in fragment.Activities)
        {
            if (string.IsNullOrWhiteSpace(activity.ActivityKey))
                throw new InvalidOperationException("Depot activity is missing activity_key.");

            if (!string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Depot fragment activity '{activity.ActivityKey}' has invalid type '{activity.ActivityType}'.");
            }
        }

        var activityDepotIds = fragment.Activities
            .Where(activity => activity.DepotId.HasValue)
            .Select(activity => activity.DepotId!.Value);
        var shortageDepotIds = fragment.SupplyShortages
            .Where(shortage => shortage.SelectedDepotId.HasValue)
            .Select(shortage => shortage.SelectedDepotId!.Value);
        var distinctDepotIds = activityDepotIds
            .Concat(shortageDepotIds)
            .Distinct()
            .ToList();

        if (distinctDepotIds.Count > 1)
            throw new InvalidOperationException("Depot fragment must use exactly one depot for the whole mission draft.");
    }

    private static void ValidateTeamFragment(MissionTeamFragment fragment)
    {
        var duplicateKey = fragment.ActivityAssignments
            .GroupBy(assignment => assignment.ActivityKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1);

        if (duplicateKey is not null)
            throw new InvalidOperationException($"Team fragment contains duplicate activity assignment key '{duplicateKey.Key}'.");
    }

    private static MissionDraftBody AssembleDraftBody(
        MissionRequirementsFragment requirements,
        MissionDepotFragment depot,
        MissionTeamFragment team)
    {
        var assignmentLookup = team.ActivityAssignments
            .Where(assignment => !string.IsNullOrWhiteSpace(assignment.ActivityKey))
            .ToDictionary(assignment => assignment.ActivityKey, StringComparer.OrdinalIgnoreCase);

        var draftActivities = new List<MissionDraftActivityDto>();

        foreach (var activity in depot.Activities.OrderBy(item => item.Step))
        {
            var draftActivity = MapActivityFragmentToDraft(activity);
            if (assignmentLookup.TryGetValue(activity.ActivityKey, out var assignment))
            {
                draftActivity.ExecutionMode = assignment.ExecutionMode ?? draftActivity.ExecutionMode;
                draftActivity.RequiredTeamCount = assignment.RequiredTeamCount ?? draftActivity.RequiredTeamCount;
                draftActivity.CoordinationGroupKey = assignment.CoordinationGroupKey ?? draftActivity.CoordinationGroupKey;
                draftActivity.CoordinationNotes = assignment.CoordinationNotes ?? draftActivity.CoordinationNotes;
                draftActivity.SuggestedTeam = CloneSuggestedTeam(assignment.SuggestedTeam) ?? draftActivity.SuggestedTeam;
            }

            draftActivities.Add(draftActivity);
        }

        draftActivities.AddRange(
            team.AdditionalActivities
                .OrderBy(item => item.Step)
                .Select(MapActivityFragmentToDraft));

        for (var index = 0; index < draftActivities.Count; index++)
            draftActivities[index].Step = index + 1;

        return new MissionDraftBody
        {
            MissionTitle = requirements.SuggestedMissionTitle,
            MissionType = requirements.SuggestedMissionType,
            PriorityScore = requirements.SuggestedPriorityScore,
            SeverityLevel = requirements.SuggestedSeverityLevel,
            OverallAssessment = requirements.OverallAssessment,
            Activities = draftActivities,
            Resources = requirements.SuggestedResources,
            SuggestedTeam = CloneSuggestedTeam(team.SuggestedTeam),
            EstimatedDuration = requirements.EstimatedDuration,
            SpecialNotes = JoinNotes(requirements.SpecialNotes, depot.SpecialNotes, team.SpecialNotes),
            NeedsAdditionalDepot = depot.NeedsAdditionalDepot || requirements.NeedsAdditionalDepot,
            SupplyShortages = depot.SupplyShortages.Count > 0
                ? depot.SupplyShortages.Select(CloneSupplyShortage).ToList()
                : requirements.SupplyShortages.Select(CloneSupplyShortage).ToList(),
            ConfidenceScore = CalculateDraftConfidence(
                requirements.ConfidenceScore,
                depot.ConfidenceScore,
                team.ConfidenceScore)
        };
    }

    private static MissionDraftActivityDto MapActivityFragmentToDraft(MissionActivityFragment activity)
    {
        return new MissionDraftActivityDto
        {
            Step = activity.Step,
            ActivityType = activity.ActivityType,
            Description = activity.Description,
            Priority = activity.Priority,
            EstimatedTime = activity.EstimatedTime,
            ExecutionMode = activity.ExecutionMode,
            RequiredTeamCount = activity.RequiredTeamCount,
            CoordinationGroupKey = activity.CoordinationGroupKey,
            CoordinationNotes = activity.CoordinationNotes,
            SosRequestId = activity.SosRequestId,
            DepotId = activity.DepotId,
            DepotName = activity.DepotName,
            DepotAddress = activity.DepotAddress,
            DepotLatitude = activity.DepotLatitude,
            DepotLongitude = activity.DepotLongitude,
            AssemblyPointId = activity.AssemblyPointId,
            AssemblyPointName = activity.AssemblyPointName,
            AssemblyPointLatitude = activity.AssemblyPointLatitude,
            AssemblyPointLongitude = activity.AssemblyPointLongitude,
            SuppliesToCollect = activity.SuppliesToCollect?.Select(CloneSupply).ToList(),
            SuggestedTeam = CloneSuggestedTeam(activity.SuggestedTeam)
        };
    }

    private static string SerializeMissionDraftBody(MissionDraftBody draftBody)
    {
        var payload = new
        {
            mission_title = draftBody.MissionTitle,
            mission_type = draftBody.MissionType,
            priority_score = draftBody.PriorityScore,
            severity_level = draftBody.SeverityLevel,
            overall_assessment = draftBody.OverallAssessment,
            activities = draftBody.Activities.Select(activity => new
            {
                step = activity.Step,
                activity_type = activity.ActivityType,
                description = activity.Description,
                priority = activity.Priority,
                estimated_time = activity.EstimatedTime,
                execution_mode = activity.ExecutionMode,
                required_team_count = activity.RequiredTeamCount,
                coordination_group_key = activity.CoordinationGroupKey,
                coordination_notes = activity.CoordinationNotes,
                sos_request_id = activity.SosRequestId,
                depot_id = activity.DepotId,
                depot_name = activity.DepotName,
                depot_address = activity.DepotAddress,
                depot_latitude = activity.DepotLatitude,
                depot_longitude = activity.DepotLongitude,
                assembly_point_id = activity.AssemblyPointId,
                assembly_point_name = activity.AssemblyPointName,
                assembly_point_latitude = activity.AssemblyPointLatitude,
                assembly_point_longitude = activity.AssemblyPointLongitude,
                supplies_to_collect = activity.SuppliesToCollect?.Select(supply => new
                {
                    item_id = supply.ItemId,
                    item_name = supply.ItemName,
                    quantity = supply.Quantity,
                    unit = supply.Unit
                }).ToList(),
                suggested_team = activity.SuggestedTeam is null ? null : new
                {
                    team_id = activity.SuggestedTeam.TeamId,
                    team_name = activity.SuggestedTeam.TeamName,
                    team_type = activity.SuggestedTeam.TeamType,
                    reason = activity.SuggestedTeam.Reason,
                    assembly_point_id = activity.SuggestedTeam.AssemblyPointId,
                    assembly_point_name = activity.SuggestedTeam.AssemblyPointName,
                    latitude = activity.SuggestedTeam.Latitude,
                    longitude = activity.SuggestedTeam.Longitude,
                    distance_km = activity.SuggestedTeam.DistanceKm
                }
            }).ToList(),
            resources = draftBody.Resources.Select(resource => new
            {
                resource_type = resource.ResourceType,
                description = resource.Description,
                quantity = resource.Quantity,
                priority = resource.Priority
            }).ToList(),
            suggested_team = draftBody.SuggestedTeam is null ? null : new
            {
                team_id = draftBody.SuggestedTeam.TeamId,
                team_name = draftBody.SuggestedTeam.TeamName,
                team_type = draftBody.SuggestedTeam.TeamType,
                reason = draftBody.SuggestedTeam.Reason,
                assembly_point_id = draftBody.SuggestedTeam.AssemblyPointId,
                assembly_point_name = draftBody.SuggestedTeam.AssemblyPointName,
                latitude = draftBody.SuggestedTeam.Latitude,
                longitude = draftBody.SuggestedTeam.Longitude,
                distance_km = draftBody.SuggestedTeam.DistanceKm
            },
            estimated_duration = draftBody.EstimatedDuration,
            special_notes = draftBody.SpecialNotes,
            needs_additional_depot = draftBody.NeedsAdditionalDepot,
            supply_shortages = draftBody.SupplyShortages.Select(shortage => new
            {
                sos_request_id = shortage.SosRequestId,
                item_id = shortage.ItemId,
                item_name = shortage.ItemName,
                unit = shortage.Unit,
                selected_depot_id = shortage.SelectedDepotId,
                selected_depot_name = shortage.SelectedDepotName,
                needed_quantity = shortage.NeededQuantity,
                available_quantity = shortage.AvailableQuantity,
                missing_quantity = shortage.MissingQuantity,
                notes = shortage.Notes
            }).ToList(),
            confidence_score = draftBody.ConfidenceScore
        };

        return JsonSerializer.Serialize(payload, _jsonOpts);
    }

    private static RescueMissionSuggestionResult MapDraftBodyToResult(
        MissionDraftBody draftBody,
        string rawAiResponse)
    {
        var result = ParseMissionSuggestion(SerializeMissionDraftBody(draftBody));
        result.IsSuccess = true;
        result.RawAiResponse = rawAiResponse;
        return result;
    }

    private async Task FinalizeSuggestionResultAsync(
        RescueMissionSuggestionResult result,
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots,
        IReadOnlyCollection<AgentTeamInfo> nearbyTeams,
        bool isMultiDepotRecommended,
        int? clusterId,
        int? suggestionId,
        MissionSuggestionMetadata? metadata,
        List<SuggestedActivityDto>? draftActivities,
        string finalResultSource,
        MissionSuggestionExecutionOptions options,
        CancellationToken cancellationToken)
    {
        await ApplySharedPostProcessingAsync(
            result,
            sosRequests,
            nearbyDepots,
            nearbyTeams,
            isMultiDepotRecommended,
            cancellationToken);

        if (draftActivities is { Count: > 0 })
        {
            var sosLookup = sosRequests.ToDictionary(sos => sos.Id);
            BackfillSosRequestIds(draftActivities, sosRequests);
            EnrichVictimTargets(draftActivities, sosLookup);
        }

        var effectiveMetadata = metadata ?? CreateSuggestionMetadataForLegacy();
        effectiveMetadata.OverallAssessment = result.OverallAssessment;
        effectiveMetadata.EstimatedDuration = result.EstimatedDuration;
        effectiveMetadata.SpecialNotes = result.SpecialNotes;
        effectiveMetadata.MixedRescueReliefWarning = result.MixedRescueReliefWarning;
        effectiveMetadata.NeedsManualReview = result.NeedsManualReview;
        effectiveMetadata.LowConfidenceWarning = result.LowConfidenceWarning;
        effectiveMetadata.NeedsAdditionalDepot = result.NeedsAdditionalDepot;
        effectiveMetadata.SupplyShortages = result.SupplyShortages;
        effectiveMetadata.SuggestedResources = result.SuggestedResources;
        effectiveMetadata.SuggestedSeverityLevel = result.SuggestedSeverityLevel;
        effectiveMetadata.SuggestedMissionType = result.SuggestedMissionType;
        effectiveMetadata.RawAiResponse = result.RawAiResponse;

        if (effectiveMetadata.Pipeline is not null)
        {
            effectiveMetadata.Pipeline.PipelineStatus = "completed";
            effectiveMetadata.Pipeline.FinalResultSource = finalResultSource;
        }

        if (!options.PersistSuggestion)
        {
            result.SuggestionId = null;
            result.PipelineMetadata = effectiveMetadata.Pipeline;
            return;
        }

        result.SuggestionId = await PersistSuggestionAsync(
            clusterId,
            suggestionId,
            result,
            effectiveMetadata,
            draftActivities,
            finalResultSource,
            cancellationToken);
    }

    private async Task ApplySharedPostProcessingAsync(
        RescueMissionSuggestionResult result,
        List<SosRequestSummary> sosRequests,
        List<DepotSummary>? nearbyDepots,
        IReadOnlyCollection<AgentTeamInfo> nearbyTeams,
        bool isMultiDepotRecommended,
        CancellationToken cancellationToken)
    {
        var sosLookup = sosRequests.ToDictionary(sos => sos.Id);
        NormalizeActivitySequence(result.SuggestedActivities, sosLookup);
        BackfillItemIds(result.SuggestedActivities, nearbyDepots ?? []);
        await BackfillInventoryBackedItemIdsAsync(result.SuggestedActivities, cancellationToken);
        BackfillSosRequestIds(result.SuggestedActivities, sosRequests);
        await EnrichActivitiesWithAssemblyPointsAsync(result, sosLookup, cancellationToken);
        await EnsureReusableReturnActivitiesAsync(result.SuggestedActivities, cancellationToken);
        await BackfillDestinationInfoAsync(result.SuggestedActivities, nearbyDepots ?? [], sosRequests, cancellationToken);
        BackfillShortageItemIds(result.SupplyShortages, nearbyDepots ?? []);
        NormalizeSupplyShortages(result);
        ApplySingleDepotConstraint(result);
        RescueMissionSuggestionReviewHelper.ApplyNearbyTeamConstraints(result, nearbyTeams);
        EnsureReturnAssemblyPointActivities(result);
        EnrichVictimTargets(result.SuggestedActivities, sosLookup);
        ApplyMixedRescueReliefSafetyNote(result);
        NormalizeMixedRescueReliefWarning(result, allowFallbackFromSpecialNotes: !string.IsNullOrWhiteSpace(result.MixedRescueReliefWarning));
        NormalizeEstimatedDurations(result);

        if (result.ConfidenceScore < LowConfidenceThreshold)
        {
            result.NeedsManualReview = true;
            result.LowConfidenceWarning =
                $"AI chi dat do tu tin {result.ConfidenceScore:P0} (nguong {LowConfidenceThreshold:P0}). " +
                "Dieu phoi vien nen kiem tra lai ke hoach.";
        }

        result.IsSuccess = true;
        result.MultiDepotRecommended = false;
    }

    private static void NormalizeMixedRescueReliefWarning(
        RescueMissionSuggestionResult result,
        bool allowFallbackFromSpecialNotes)
    {
        var normalized = MissionSuggestionWarningHelper.NormalizeMixedRescueReliefWarning(
            result.SpecialNotes,
            result.MixedRescueReliefWarning,
            allowFallbackFromSpecialNotes);

        result.SpecialNotes = string.IsNullOrWhiteSpace(normalized.SpecialNotes)
            ? null
            : normalized.SpecialNotes;
        result.MixedRescueReliefWarning = normalized.MixedRescueReliefWarning;
    }

    private async Task<int?> PersistSuggestionAsync(
        int? clusterId,
        int? suggestionId,
        RescueMissionSuggestionResult result,
        MissionSuggestionMetadata metadata,
        List<SuggestedActivityDto>? draftActivities,
        string finalResultSource,
        CancellationToken cancellationToken)
    {
        if (!clusterId.HasValue)
            return suggestionId;

        var activities = new List<ActivityAiSuggestionModel>();
        if (string.Equals(finalResultSource, "legacy", StringComparison.OrdinalIgnoreCase))
        {
            activities.Add(BuildActivitySuggestionModel(
                clusterId.Value,
                result.ModelName,
                result.SuggestedMissionType,
                ExecutionSuggestionPhase,
                result.SuggestedActivities,
                result.ConfidenceScore));
        }
        else
        {
            if (draftActivities is { Count: > 0 })
            {
                activities.Add(BuildActivitySuggestionModel(
                    clusterId.Value,
                    result.ModelName,
                    result.SuggestedMissionType,
                    DraftSuggestionPhase,
                    draftActivities,
                    result.ConfidenceScore));
            }

            if (string.Equals(finalResultSource, "validated", StringComparison.OrdinalIgnoreCase)
                && result.SuggestedActivities.Count > 0)
            {
                activities.Add(BuildActivitySuggestionModel(
                    clusterId.Value,
                    result.ModelName,
                    result.SuggestedMissionType,
                    ValidatedSuggestionPhase,
                    result.SuggestedActivities,
                    result.ConfidenceScore));
            }
        }

        var missionModel = new MissionAiSuggestionModel
        {
            Id = suggestionId ?? 0,
            ClusterId = clusterId.Value,
            ModelName = result.ModelName,
            AnalysisType = "RescueMissionSuggestion",
            SuggestedMissionTitle = result.SuggestedMissionTitle,
            SuggestedMissionType = result.SuggestedMissionType,
            SuggestedPriorityScore = result.SuggestedPriorityScore,
            SuggestedSeverityLevel = result.SuggestedSeverityLevel,
            ConfidenceScore = result.ConfidenceScore,
            Metadata = JsonSerializer.Serialize(metadata, _jsonOpts),
            CreatedAt = suggestionId.HasValue ? null : DateTime.UtcNow,
            Activities = activities
        };

        if (suggestionId.HasValue)
        {
            await _missionAiSuggestionRepository.UpdateAsync(missionModel, cancellationToken);
            return suggestionId;
        }

        return await _missionAiSuggestionRepository.CreateAsync(missionModel, cancellationToken);
    }

    private static ActivityAiSuggestionModel BuildActivitySuggestionModel(
        int clusterId,
        string? modelName,
        string? missionType,
        string phase,
        List<SuggestedActivityDto> activities,
        double confidenceScore)
    {
        return new ActivityAiSuggestionModel
        {
            ClusterId = clusterId,
            ModelName = modelName,
            ActivityType = missionType ?? "RescueActivities",
            SuggestionPhase = phase,
            SuggestedActivities = JsonSerializer.Serialize(activities, _jsonOpts),
            ConfidenceScore = confidenceScore,
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task<int?> EnsureSuggestionRecordAsync(
        int? clusterId,
        MissionSuggestionMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!clusterId.HasValue)
            return null;

        var placeholder = new MissionAiSuggestionModel
        {
            ClusterId = clusterId.Value,
            AnalysisType = "RescueMissionSuggestion",
            Metadata = JsonSerializer.Serialize(metadata, _jsonOpts),
            CreatedAt = DateTime.UtcNow
        };

        return await _missionAiSuggestionRepository.CreateAsync(placeholder, cancellationToken);
    }

    private async Task SaveSuggestionMetadataAsync(
        int? suggestionId,
        MissionSuggestionMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!suggestionId.HasValue)
            return;

        await _missionAiSuggestionRepository.SavePipelineSnapshotAsync(
            suggestionId.Value,
            metadata,
            cancellationToken);
    }

    private async Task SavePipelineStageSnapshotAsync(
        int? suggestionId,
        MissionSuggestionMetadata metadata,
        string stageName,
        string status,
        PromptType? promptType = null,
        string? modelName = null,
        long? responseTimeMs = null,
        string? outputJson = null,
        string? error = null,
        string? pipelineStatus = null,
        CancellationToken cancellationToken = default,
        string? finalResultSource = null)
    {
        metadata.Pipeline ??= new MissionSuggestionPipelineMetadata
        {
            ExecutionMode = "pipeline"
        };

        metadata.Pipeline.Stages[stageName] = new MissionSuggestionStageSnapshot
        {
            Status = status,
            PromptType = promptType?.ToString(),
            ModelName = modelName,
            ResponseTimeMs = responseTimeMs,
            OutputJson = outputJson,
            Error = error,
            UpdatedAtUtc = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(pipelineStatus))
            metadata.Pipeline.PipelineStatus = pipelineStatus;

        if (!string.IsNullOrWhiteSpace(finalResultSource))
            metadata.Pipeline.FinalResultSource = finalResultSource;

        await SaveSuggestionMetadataAsync(suggestionId, metadata, cancellationToken);
    }

    private static MissionSuggestionMetadata CreateSuggestionMetadataForPipeline()
    {
        return new MissionSuggestionMetadata
        {
            Pipeline = new MissionSuggestionPipelineMetadata
            {
                ExecutionMode = "pipeline",
                PipelineStatus = "running"
            }
        };
    }

    private static MissionSuggestionMetadata CreateSuggestionMetadataForLegacy()
    {
        return new MissionSuggestionMetadata
        {
            Pipeline = new MissionSuggestionPipelineMetadata
            {
                ExecutionMode = "legacy",
                PipelineStatus = "completed",
                FinalResultSource = "legacy"
            }
        };
    }

    private static string SerializePipelineFragment<T>(T fragment) =>
        JsonSerializer.Serialize(fragment, _jsonOpts);

    private static double CalculateDraftConfidence(params double[] scores)
    {
        var validScores = scores.Where(score => score > 0).ToList();
        if (validScores.Count == 0)
            return 0;

        return Math.Round(validScores.Average(), 2);
    }

    private static SuggestedActivityDto MapDraftActivityToSuggestedActivity(MissionDraftActivityDto activity)
    {
        return new SuggestedActivityDto
        {
            Step = activity.Step,
            ActivityType = activity.ActivityType ?? string.Empty,
            Description = activity.Description ?? string.Empty,
            Priority = activity.Priority,
            EstimatedTime = activity.EstimatedTime,
            ExecutionMode = activity.ExecutionMode,
            RequiredTeamCount = activity.RequiredTeamCount,
            CoordinationGroupKey = activity.CoordinationGroupKey,
            CoordinationNotes = activity.CoordinationNotes,
            SosRequestId = activity.SosRequestId,
            DepotId = activity.DepotId,
            DepotName = activity.DepotName,
            DepotAddress = activity.DepotAddress,
            AssemblyPointId = activity.AssemblyPointId,
            AssemblyPointName = activity.AssemblyPointName,
            AssemblyPointLatitude = activity.AssemblyPointLatitude,
            AssemblyPointLongitude = activity.AssemblyPointLongitude,
            SuppliesToCollect = activity.SuppliesToCollect?.Select(CloneSupply).ToList(),
            SuggestedTeam = CloneSuggestedTeam(activity.SuggestedTeam),
            DestinationName = string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                    ? activity.DepotName
                    : activity.AssemblyPointName,
            DestinationLatitude = string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                    ? activity.DepotLatitude
                    : activity.AssemblyPointLatitude,
            DestinationLongitude = string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.ActivityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                    ? activity.DepotLongitude
                    : activity.AssemblyPointLongitude
        };
    }

    private static string? JoinNotes(params string?[] notes)
    {
        var values = notes
            .Where(note => !string.IsNullOrWhiteSpace(note))
            .Select(note => note!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return values.Count == 0 ? null : string.Join(Environment.NewLine, values);
    }

    private static string AppendSpecialNote(string? existing, string note)
    {
        if (string.IsNullOrWhiteSpace(existing))
            return note;

        if (existing.Contains(note, StringComparison.Ordinal))
            return existing;

        return $"{existing.TrimEnd()}{Environment.NewLine}{note}";
    }
}
