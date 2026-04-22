using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private sealed class MissionSuggestionPipelineState
    {
        public MissionRequirementsFragment? Requirements { get; set; }
        public MissionDepotFragment? Depot { get; set; }
        public MissionTeamFragment? Team { get; set; }
        public MissionDraftBody? DraftBody { get; set; }
        public List<SuggestedActivityDto>? DraftActivities { get; set; }
    }

    private sealed class MissionSuggestionPipelineFallbackException(
        string message,
        MissionSuggestionPipelineState? state = null,
        Exception? innerException = null)
        : Exception(message, innerException)
    {
        public MissionSuggestionPipelineState? State { get; } = state;
    }

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
        var pipelineState = new MissionSuggestionPipelineState();

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
                    "Analyze SOS requests and return JSON for mission requirements only. For every SOS requirement, decide urgent_rescue_requires_immediate_safe_transfer, can_wait_for_combined_mission, and requires_supply_before_rescue based on the full SOS cluster plus raw_message/structured_data/incident notes/AI analysis. Decide warning_level/warning_title/warning_message/warning_related_sos_ids/warning_reason based on the full SOS cluster, and keep the warning proportional to actual risk."),
                "No tools are available. Return JSON only. Include top-level warning_level = none|light|medium|strong, plus warning_title, warning_message, warning_related_sos_ids, warning_reason. For each sos_requirements item, always include urgent_rescue_requires_immediate_safe_transfer, can_wait_for_combined_mission, and requires_supply_before_rescue. Use requires_supply_before_rescue = true only when that SOS genuinely needs depot-backed items or equipment before rescue can start. Use light for low-risk follow-up notes, medium when coordinator should review, and strong when many SOS are critical/urgent, mixed rescue-relief is unsafe, or critical data/resources are missing.",
                aiConfig,
                options,
                cancellationToken);

            requirements = DeserializePipelineFragment<MissionRequirementsFragment>(stage.ResponseText);
            ValidateRequirementsFragment(requirements, sosRequests);
            pipelineState.Requirements = requirements;
            metadata.SplitClusterRecommended = requirements.SplitClusterRecommended;
            metadata.SplitClusterReason = requirements.SplitClusterReason;

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
                pipelineStatus: "failed",
                cancellationToken: cancellationToken);
            throw new MissionSuggestionPipelineFallbackException("Requirements stage failed.", pipelineState, ex);
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
                    "Plan depot collection and delivery fragments. Use only inventory lookup results. Choose exactly one depot for the whole mission. Do not split supplies across multiple depots. Search both relief stock and transport or reusable equipment from inventory when the plan needs vehicles or field gear. Batch nearby SOS into route-friendly collect/deliver fragments when the same depot can serve them safely. If the chosen depot lacks stock, keep the one-depot plan and fill needs_additional_depot plus supply_shortages."),
                "Only searchInventory is available. It is already scoped to eligible depots for this cluster and returns only decision fields, not image URLs or raw lot/serial data. If a depot-backed vehicle or reusable item is selected, keep it inside COLLECT_SUPPLIES and RETURN_SUPPLIES with depot and item identifiers; do not demote it to resources[]. This stage only suggests the plan and does not reserve inventory. Do not invent depot_id or item_id. Every DELIVER_SUPPLIES that comes from the chosen depot must keep depot_id/depot_name/depot_address and the concrete supplies_to_collect list. If an urgent rescue SOS requires depot-backed gear before rescue, you may create COLLECT_SUPPLIES for that SOS before the rescue branch. Return JSON only.",
                BuildAllowedTools("searchInventory"),
                nearbyDepots,
                nearbyTeams,
                aiConfig,
                options,
                cancellationToken);

            depot = DeserializePipelineFragment<MissionDepotFragment>(stage.ResponseText);
            ValidateDepotFragment(depot);
            pipelineState.Depot = depot;

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
                pipelineStatus: "failed",
                cancellationToken: cancellationToken);
            throw new MissionSuggestionPipelineFallbackException("Depot stage failed.", pipelineState, ex);
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
                    "Assign nearby teams, add rescue/medical/evacuate activities as JSON fragments, and decide the exact final activity order. A mission may use many teams, but each individual activity must remain SingleTeam."),
                "Only getTeams and getAssemblyPoints are available. Do not invent team_id or assembly_point_id. Do not use SplitAcrossTeams, MultiTeam, or required_team_count > 1 on any activity or assignment. You may keep coordination_group_key only as a route-ordering hint, not as a multi-team split signal. Every additional activity must include activity_key. Return ordered_activity_keys covering every depot activity key plus every additional activity key exactly once, in the final execution order. Non-urgent mixed routes may do COLLECT->DELIVER before rescue. Urgent rescue routes must do COLLECT(for that urgent SOS only when requires_supply_before_rescue=true)->RESCUE->EVACUATE before unrelated work. Return JSON only.",
                BuildAllowedTools("getTeams", "getAssemblyPoints"),
                nearbyDepots,
                nearbyTeams,
                aiConfig,
                options,
                cancellationToken);

            team = DeserializePipelineFragment<MissionTeamFragment>(stage.ResponseText);
            ValidateTeamFragment(team, depot);
            pipelineState.Team = team;

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
                pipelineStatus: "failed",
                cancellationToken: cancellationToken);
            throw new MissionSuggestionPipelineFallbackException("Team stage failed.", pipelineState, ex);
        }

        yield return Status("assemble");

        var draftBody = AssembleDraftBody(requirements, depot, team);
        var draftJson = SerializeMissionDraftBody(draftBody);
        var draftActivities = draftBody.Activities
            .Select(MapDraftActivityToSuggestedActivity)
            .ToList();
        var assembledDraftResult = MapDraftBodyToResult(draftBody, draftJson);
        var assembledDraftAssessment = AssessExecutableMissionResult(assembledDraftResult, sosRequests, draftActivities, requirements);
        if (!assembledDraftAssessment.IsExecutable)
        {
            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "assemble",
                "failed",
                outputJson: draftJson,
                error: assembledDraftAssessment.FailureReason,
                pipelineStatus: "failed",
                cancellationToken: cancellationToken);

            throw new MissionSuggestionPipelineFallbackException(
                $"Assemble stage failed: {assembledDraftAssessment.FailureReason ?? "Draft route is not executable."}",
                pipelineState);
        }

        pipelineState.DraftBody = draftBody;
        pipelineState.DraftActivities = draftActivities.Select(CloneActivity).ToList();

        await SavePipelineStageSnapshotAsync(
            suggestionId,
            metadata,
            "assemble",
            "completed",
            outputJson: draftJson,
            pipelineStatus: "running",
            cancellationToken: cancellationToken);

        yield return Status("validate");

        const string finalResultSource = "validated";
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
                    "Rewrite the assembled mission draft as the final mission JSON schema. Preserve the exact route order from ordered_activity_keys/activity_key, the single selected depot, needs_additional_depot, supply_shortages, warning_level, warning_title, warning_message, warning_related_sos_ids, and warning_reason fields. Preserve any inventory-backed transport or reusable equipment inside supplies_to_collect. Keep the JSON contract unchanged."),
                "No tools are available. Return the full mission JSON only. Do not introduce a second depot. Every activity must stay SingleTeam with required_team_count = 1. Do not change the route dependency COLLECT->DELIVER and do not reorder urgent rescue safe-transfer sequences. Preserve or improve the warning_level/warning_title/warning_message/warning_related_sos_ids/warning_reason fields so they still reflect the real cluster risk.",
                aiConfig,
                options,
                cancellationToken);

            result = ParseMissionSuggestion(stage.ResponseText);
            result.IsSuccess = true;
            result.ModelName = stage.ModelName;
            result.RawAiResponse = stage.ResponseText;

            var validatedAssessment = AssessExecutableMissionResult(result, sosRequests, draftActivities, requirements);
            if (!validatedAssessment.IsExecutable)
            {
                await SavePipelineStageSnapshotAsync(
                    suggestionId,
                    metadata,
                    "validate",
                    "failed",
                    PromptType.MissionPlanValidation,
                    stage.ModelName,
                    stage.LatencyMs,
                    ExtractJsonPayload(stage.ResponseText),
                    validatedAssessment.FailureReason,
                    "failed",
                    cancellationToken);

                throw new MissionSuggestionPipelineFallbackException(
                    $"Validation stage failed: {validatedAssessment.FailureReason ?? "Final mission JSON is not executable."}",
                    pipelineState);
            }

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
                finalResultSource);
        }
        catch (Exception ex)
        {
            await SavePipelineStageSnapshotAsync(
                suggestionId,
                metadata,
                "validate",
                "failed",
                PromptType.MissionPlanValidation,
                error: ex.Message,
                pipelineStatus: "failed",
                cancellationToken: cancellationToken);

            if (ex is MissionSuggestionPipelineFallbackException)
                throw;

            throw new MissionSuggestionPipelineFallbackException("Validation stage failed.", pipelineState, ex);
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
            CreateAiWarningDecision(requirements),
            requirements,
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
        var json = SanitizePipelineJsonPayload(ExtractJsonPayload(rawResponse));
        var result = JsonSerializer.Deserialize<T>(json, PipelineJsonDeserializeOptions);

        if (result is null)
            throw new InvalidOperationException($"Could not parse pipeline fragment '{typeof(T).Name}'.");

        return result;
    }

    private static string SanitizePipelineJsonPayload(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch
        {
            return json;
        }

        if (node is not JsonObject root)
            return json;

        NormalizePipelineSupplyShortages(root);
        NormalizePipelineWarningRelatedSosIds(root);
        return root.ToJsonString();
    }

    private static void NormalizePipelineSupplyShortages(JsonObject root)
    {
        if (!root.TryGetPropertyValue("supply_shortages", out var shortagesNode)
            || shortagesNode is null)
        {
            return;
        }

        JsonArray sourceArray = shortagesNode switch
        {
            JsonArray array => array,
            JsonObject singleObject => [singleObject],
            JsonValue value => [value],
            _ => []
        };

        var normalized = new JsonArray();
        foreach (var entry in sourceArray)
        {
            var normalizedEntry = NormalizePipelineSupplyShortageEntry(entry);
            if (normalizedEntry is not null)
                normalized.Add(normalizedEntry);
        }

        root["supply_shortages"] = normalized;
    }

    private static JsonObject? NormalizePipelineSupplyShortageEntry(JsonNode? entry)
    {
        return entry switch
        {
            JsonObject obj => NormalizePipelineSupplyShortageObject(obj),
            JsonValue value => BuildPipelineSupplyShortageFromScalar(value),
            _ => null
        };
    }

    private static JsonObject NormalizePipelineSupplyShortageObject(JsonObject source)
    {
        var itemName = ReadStringNode(source, "item_name")
            ?? ReadStringNode(source, "item")
            ?? ReadStringNode(source, "name")
            ?? "Thiếu vật phẩm chưa xác định";
        var unit = ReadStringNode(source, "unit");
        var neededQuantity = ReadIntNode(source, "needed_quantity")
            ?? ReadIntNode(source, "required_quantity")
            ?? ReadIntNode(source, "quantity")
            ?? Math.Max(ReadIntNode(source, "missing_quantity") ?? 1, 1);
        var availableQuantity = Math.Max(ReadIntNode(source, "available_quantity") ?? 0, 0);
        var missingQuantity = ReadIntNode(source, "missing_quantity")
            ?? Math.Max(neededQuantity - availableQuantity, neededQuantity > 0 ? neededQuantity : 1);

        return new JsonObject
        {
            ["sos_request_id"] = ReadIntNode(source, "sos_request_id"),
            ["item_id"] = ReadIntNode(source, "item_id"),
            ["item_name"] = itemName,
            ["unit"] = unit,
            ["selected_depot_id"] = ReadIntNode(source, "selected_depot_id"),
            ["selected_depot_name"] = ReadStringNode(source, "selected_depot_name"),
            ["needed_quantity"] = Math.Max(neededQuantity, 1),
            ["available_quantity"] = availableQuantity,
            ["missing_quantity"] = Math.Max(missingQuantity, 0),
            ["notes"] = ReadStringNode(source, "notes")
        };
    }

    private static JsonObject? BuildPipelineSupplyShortageFromScalar(JsonValue value)
    {
        if (!value.TryGetValue(out string? shortageLabel))
            return null;

        if (string.IsNullOrWhiteSpace(shortageLabel))
            return null;

        return new JsonObject
        {
            ["item_name"] = shortageLabel.Trim(),
            ["needed_quantity"] = 1,
            ["available_quantity"] = 0,
            ["missing_quantity"] = 1
        };
    }

    private static void NormalizePipelineWarningRelatedSosIds(JsonObject root)
    {
        if (!root.TryGetPropertyValue("warning_related_sos_ids", out var warningIdsNode)
            || warningIdsNode is null)
        {
            return;
        }

        JsonArray sourceArray = warningIdsNode switch
        {
            JsonArray array => array,
            JsonValue value => [value],
            _ => []
        };

        var normalized = new JsonArray();
        foreach (var entry in sourceArray)
        {
            var id = ReadIntNode(entry);
            if (id.HasValue && id.Value > 0)
                normalized.Add(id.Value);
        }

        root["warning_related_sos_ids"] = normalized;
    }

    private static int? ReadIntNode(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue(out int intValue))
                return intValue;

            if (value.TryGetValue(out string? stringValue)
                && int.TryParse(stringValue, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? ReadIntNode(JsonObject source, string propertyName) =>
        source.TryGetPropertyValue(propertyName, out var node) ? ReadIntNode(node) : null;

    private static string? ReadStringNode(JsonObject source, string propertyName)
    {
        if (!source.TryGetPropertyValue(propertyName, out var node) || node is null)
            return null;

        if (node is JsonValue value && value.TryGetValue(out string? text))
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        return null;
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

        if (fragment.SplitClusterRecommended && string.IsNullOrWhiteSpace(fragment.SplitClusterReason))
        {
            throw new InvalidOperationException(
                "Requirements fragment must provide split_cluster_reason when split_cluster_recommended is true.");
        }
    }

    private static void ValidateDepotFragment(MissionDepotFragment fragment)
    {
        var duplicateKey = fragment.Activities
            .GroupBy(activity => activity.ActivityKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1);

        if (duplicateKey is not null)
            throw new InvalidOperationException($"Depot fragment contains duplicate activity key '{duplicateKey.Key}'.");

        var collectedQuantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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

            if (activity.DepotId is null)
                throw new InvalidOperationException($"Depot fragment activity '{activity.ActivityKey}' is missing depot_id.");

            if (activity.SuppliesToCollect is not { Count: > 0 })
                throw new InvalidOperationException(
                    $"Depot fragment activity '{activity.ActivityKey}' must include supplies_to_collect.");

            if (string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var supply in activity.SuppliesToCollect.Where(supply => supply.Quantity > 0))
                {
                    var supplyKey = BuildSupplyLedgerKey(supply.ItemId, supply.ItemName);
                    collectedQuantities.TryGetValue(supplyKey, out var quantity);
                    collectedQuantities[supplyKey] = quantity + supply.Quantity;
                }

                continue;
            }

            foreach (var supply in activity.SuppliesToCollect.Where(supply => supply.Quantity > 0))
            {
                var supplyKey = BuildSupplyLedgerKey(supply.ItemId, supply.ItemName);
                collectedQuantities.TryGetValue(supplyKey, out var availableQuantity);
                if (availableQuantity < supply.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Depot fragment delivers '{supply.ItemName}' before a matching collect exists or exceeds collected quantity.");
                }

                collectedQuantities[supplyKey] = availableQuantity - supply.Quantity;
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

    private static void ValidateTeamFragment(
        MissionTeamFragment fragment,
        MissionDepotFragment depot)
    {
        var duplicateKey = fragment.ActivityAssignments
            .GroupBy(assignment => assignment.ActivityKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1);

        if (duplicateKey is not null)
            throw new InvalidOperationException($"Team fragment contains duplicate activity assignment key '{duplicateKey.Key}'.");

        var depotKeys = depot.Activities
            .Select(activity => activity.ActivityKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToList();

        var additionalKeys = new List<string>();
        foreach (var activity in fragment.AdditionalActivities)
        {
            if (string.IsNullOrWhiteSpace(activity.ActivityKey))
                throw new InvalidOperationException("Team fragment additional activity is missing activity_key.");

            if (string.Equals(activity.ActivityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity.ActivityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Team fragment additional activity '{activity.ActivityKey}' cannot be '{activity.ActivityType}'.");
            }

            additionalKeys.Add(activity.ActivityKey);
        }

        var duplicateActivityKey = depotKeys
            .Concat(additionalKeys)
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateActivityKey is not null)
            throw new InvalidOperationException(
                $"Team fragment references duplicate activity key '{duplicateActivityKey.Key}' across depot/additional activities.");

        var allKeys = depotKeys
            .Concat(additionalKeys)
            .ToList();

        foreach (var assignment in fragment.ActivityAssignments)
        {
            if (!allKeys.Contains(assignment.ActivityKey, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Team fragment assignment references unknown activity key '{assignment.ActivityKey}'.");
            }
        }

        if (allKeys.Count == 0)
            return;

        if (fragment.OrderedActivityKeys.Count == 0)
            throw new InvalidOperationException("Team fragment must return ordered_activity_keys for the full mission route.");

        var duplicateOrderedKey = fragment.OrderedActivityKeys
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1);

        if (duplicateOrderedKey is not null)
            throw new InvalidOperationException($"ordered_activity_keys contains duplicate key '{duplicateOrderedKey.Key}'.");

        var missingOrderedKeys = allKeys
            .Where(key => !fragment.OrderedActivityKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (missingOrderedKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"ordered_activity_keys is missing activity keys: {string.Join(", ", missingOrderedKeys)}.");
        }

        var unknownOrderedKeys = fragment.OrderedActivityKeys
            .Where(key => !allKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (unknownOrderedKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"ordered_activity_keys contains unknown keys: {string.Join(", ", unknownOrderedKeys)}.");
        }
    }

    private static MissionDraftBody AssembleDraftBody(
        MissionRequirementsFragment requirements,
        MissionDepotFragment depot,
        MissionTeamFragment team)
    {
        var assignmentLookup = team.ActivityAssignments
            .Where(assignment => !string.IsNullOrWhiteSpace(assignment.ActivityKey))
            .ToDictionary(assignment => assignment.ActivityKey, StringComparer.OrdinalIgnoreCase);

        var activityLookup = new Dictionary<string, MissionDraftActivityDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var activity in depot.Activities.OrderBy(item => item.Step))
        {
            activityLookup[activity.ActivityKey] = ApplyActivityAssignment(
                MapActivityFragmentToDraft(activity),
                assignmentLookup);
        }

        foreach (var activity in team.AdditionalActivities.OrderBy(item => item.Step))
        {
            activityLookup[activity.ActivityKey] = ApplyActivityAssignment(
                MapActivityFragmentToDraft(activity),
                assignmentLookup);
        }

        var draftActivities = team.OrderedActivityKeys
            .Select(activityKey =>
            {
                if (!activityLookup.TryGetValue(activityKey, out var activity))
                    throw new InvalidOperationException($"Could not assemble draft activity for key '{activityKey}'.");

                return activity;
            })
            .ToList();

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
            WarningLevel = requirements.WarningLevel,
            WarningTitle = requirements.WarningTitle,
            WarningMessage = requirements.WarningMessage,
            WarningRelatedSosIds = requirements.WarningRelatedSosIds.ToList(),
            WarningReason = requirements.WarningReason,
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
            ActivityKey = activity.ActivityKey,
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

    private static MissionDraftActivityDto ApplyActivityAssignment(
        MissionDraftActivityDto draftActivity,
        IReadOnlyDictionary<string, MissionActivityAssignmentFragment> assignmentLookup)
    {
        if (!assignmentLookup.TryGetValue(draftActivity.ActivityKey, out var assignment))
            return draftActivity;

        draftActivity.ExecutionMode = assignment.ExecutionMode ?? draftActivity.ExecutionMode;
        draftActivity.RequiredTeamCount = assignment.RequiredTeamCount ?? draftActivity.RequiredTeamCount;
        draftActivity.CoordinationGroupKey = assignment.CoordinationGroupKey ?? draftActivity.CoordinationGroupKey;
        draftActivity.CoordinationNotes = assignment.CoordinationNotes ?? draftActivity.CoordinationNotes;
        draftActivity.SuggestedTeam = CloneSuggestedTeam(assignment.SuggestedTeam) ?? draftActivity.SuggestedTeam;
        return draftActivity;
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
                activity_key = activity.ActivityKey,
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
            warning_level = draftBody.WarningLevel,
            warning_title = draftBody.WarningTitle,
            warning_message = draftBody.WarningMessage,
            warning_related_sos_ids = draftBody.WarningRelatedSosIds,
            warning_reason = draftBody.WarningReason,
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

    private static string BuildValidationFallbackNote(string failureReason)
    {
        return failureReason switch
        {
            var message when message.Contains("must include executable activities", StringComparison.OrdinalIgnoreCase)
                => "Final validation output omitted executable activities. Backend kept the assembled mission draft and marked it for manual review.",
            var message when message.Contains("must preserve both rescue and relief branches", StringComparison.OrdinalIgnoreCase)
                => "Final validation output dropped rescue or relief branches from the executable route. Backend kept the assembled mission draft and marked it for manual review.",
            _ => "Final validation failed. Please review the assembled mission draft manually."
        };
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
        AiWarningDecision? aiWarningFallback,
        MissionRequirementsFragment? routeRequirements,
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

        ApplyAiWarningDecision(result, aiWarningFallback);

        if (routeRequirements is not null)
        {
            var finalizedAssessment = AssessExecutableMissionResult(
                result,
                sosRequests,
                draftActivities,
                routeRequirements);

            if (!finalizedAssessment.IsExecutable)
            {
                await SavePipelineStageSnapshotAsync(
                    suggestionId,
                    metadata ?? CreateSuggestionMetadataForPipeline(),
                    "finalize",
                    "failed",
                    error: finalizedAssessment.FailureReason,
                    pipelineStatus: "failed",
                    cancellationToken: cancellationToken);

                throw new MissionSuggestionPipelineFallbackException(
                    $"Finalize stage failed: {finalizedAssessment.FailureReason ?? "Post-processed mission route is not executable."}");
            }
        }

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
        effectiveMetadata.SplitClusterRecommended =
            effectiveMetadata.SplitClusterRecommended || !string.IsNullOrWhiteSpace(result.MixedRescueReliefWarning);
        effectiveMetadata.SplitClusterReason ??= result.MixedRescueReliefWarning;
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
        ConvertUnresolvedSuppliesToShortages(result);
        await EnrichActivitiesWithAssemblyPointsAsync(result, sosLookup, cancellationToken);
        await EnsureReusableReturnActivitiesAsync(result.SuggestedActivities, cancellationToken);
        await HydrateSupplyPlanningSnapshotsAsync(result.SuggestedActivities, cancellationToken);
        await BackfillDestinationInfoAsync(result.SuggestedActivities, nearbyDepots ?? [], sosRequests, cancellationToken);
        BackfillShortageItemIds(result.SupplyShortages, nearbyDepots ?? []);
        ReconcileSupplyShortagesWithInventory(result.SupplyShortages, nearbyDepots ?? [], result.SuggestedActivities);
        NormalizeSupplyShortages(result);
        ApplySingleDepotConstraint(result);
        RescueMissionSuggestionReviewHelper.ApplyNearbyTeamConstraints(result, nearbyTeams);
        EnsureReturnAssemblyPointActivities(result);
        EnrichVictimTargets(result.SuggestedActivities, sosLookup);
        ApplyMixedRescueReliefSafetyNote(result, sosLookup);
        ApplyMixedMissionMissingAiAnalysisManualReview(result, sosLookup);
        NormalizeMixedRescueReliefWarning(result, allowFallbackFromSpecialNotes: !string.IsNullOrWhiteSpace(result.MixedRescueReliefWarning));
        NormalizeEstimatedDurations(result);
        StripSupplyPresentationFields(result.SuggestedActivities);

        if (result.ConfidenceScore < LowConfidenceThreshold)
        {
            result.NeedsManualReview = true;
            result.LowConfidenceWarning = AppendMultilineValue(
                result.LowConfidenceWarning,
                $"AI chi dat do tu tin {result.ConfidenceScore:P0} (nguong {LowConfidenceThreshold:P0}). " +
                "Dieu phoi vien nen kiem tra lai ke hoach.");
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

    private static void StripSupplyPresentationFields(IEnumerable<SuggestedActivityDto> activities)
    {
        foreach (var supply in activities.SelectMany(activity => activity.SuppliesToCollect ?? []))
            supply.ImageUrl = null;
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
        if (string.Equals(finalResultSource, "legacy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(finalResultSource, "salvaged", StringComparison.OrdinalIgnoreCase))
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
