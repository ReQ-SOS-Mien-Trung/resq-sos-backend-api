using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
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
                    "Plan depot collection and delivery fragments. Use only inventory lookup results. Choose exactly one depot for the whole mission. Do not split supplies across multiple depots. Search both relief stock and transport or reusable equipment from inventory when the plan needs vehicles or field gear. If SOS context mentions flooding, isolation, or evacuation, you must also search transportation/rescue inventory before finalizing the depot plan. Batch nearby SOS into route-friendly collect/deliver fragments when the same depot can serve them safely. If the chosen depot lacks stock, keep the one-depot plan and fill needs_additional_depot plus supply_shortages."),
                "Only searchInventory is available. It is already scoped to eligible depots for this cluster and returns only decision fields, not image URLs or raw lot/serial data. If a depot-backed vehicle or reusable item is selected, keep it inside COLLECT_SUPPLIES and RETURN_SUPPLIES with depot and item identifiers; do not demote it to resources[]. When searchInventory returns a matching boat, vehicle, or rescue equipment item, put that real inventory item into supplies_to_collect instead of leaving it as a generic resource. This stage only suggests the plan and does not reserve inventory. Do not invent depot_id or item_id. Every DELIVER_SUPPLIES that comes from the chosen depot must keep depot_id/depot_name/depot_address and the concrete supplies_to_collect list. If an urgent rescue route needs depot-backed gear or supplies before field execution, you may create COLLECT_SUPPLIES before the rescue branch. Return JSON only.",
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
                    "Assign nearby teams, add rescue/medical/evacuate activities as JSON fragments, and decide the exact final activity order. A mission may use many teams, but each individual activity must remain SingleTeam."),
                "Only getTeams and getAssemblyPoints are available. Do not invent team_id or assembly_point_id. Do not use SplitAcrossTeams, MultiTeam, or required_team_count > 1 on any activity or assignment. You may keep coordination_group_key only as a route-ordering hint, not as a multi-team split signal. Every additional activity should include activity_key when available. Return ordered_activity_keys when possible; if omitted, backend will keep the combined depot/team activity order. Non-urgent mixed routes may do COLLECT->DELIVER before rescue. Urgent rescue routes should still prioritize rescue work before unrelated work, but depot-backed COLLECT_SUPPLIES or DELIVER_SUPPLIES may appear before rescue when the same route must bring items or equipment to the scene, including for nearby urgent SOS handled in one combined route. A DELIVER_SUPPLIES activity must stay on the same route/team as the COLLECT_SUPPLIES that gathered its depot-backed supplies; cross-team inventory handoff is unsupported. Do not assign one team to COLLECT and another team to DELIVER the same collected supplies. Return JSON only.",
                BuildAllowedTools("getTeams", "getAssemblyPoints"),
                nearbyDepots,
                nearbyTeams,
                aiConfig,
                options,
                cancellationToken);

            team = DeserializePipelineFragment<MissionTeamFragment>(stage.ResponseText);
            ValidateTeamFragment(team, depot);

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

        NormalizePipelineSuggestedResources(root);
        NormalizePipelineSosRequirements(root);
        NormalizePipelineActivities(root, "activities");
        NormalizePipelineActivityAssignments(root);
        NormalizePipelineActivities(root, "additional_activities");
        NormalizePipelineOrderedActivityKeys(root);
        NormalizePipelineSupplyShortages(root);
        NormalizePipelineWarningRelatedSosIds(root);
        NormalizePipelineTopLevelSuggestedTeam(root);
        return root.ToJsonString();
    }

    private static void NormalizePipelineSuggestedResources(JsonObject root)
    {
        if (!root.TryGetPropertyValue("suggested_resources", out var node) || node is null)
            return;

        var normalized = new JsonArray();
        foreach (var entry in CoerceNodeToArray(node))
        {
            switch (entry)
            {
                case JsonObject obj:
                    normalized.Add(obj.DeepClone());
                    break;
                case JsonValue value when value.TryGetValue(out string? label) && !string.IsNullOrWhiteSpace(label):
                    normalized.Add(new JsonObject
                    {
                        ["resource_type"] = "EQUIPMENT",
                        ["description"] = label.Trim(),
                        ["quantity"] = 1,
                        ["priority"] = null
                    });
                    break;
            }
        }

        root["suggested_resources"] = normalized;
    }

    private static void NormalizePipelineSosRequirements(JsonObject root)
    {
        if (!root.TryGetPropertyValue("sos_requirements", out var node) || node is null)
            return;

        var normalized = new JsonArray();
        foreach (var entry in CoerceNodeToArray(node))
        {
            switch (entry)
            {
                case JsonObject obj:
                    NormalizePipelineRequiredSupplies(obj);
                    NormalizePipelineRequiredTeams(obj);
                    normalized.Add(obj.DeepClone());
                    break;
                case JsonValue value:
                {
                    var sosId = ReadIntNode(value);
                    if (sosId is > 0)
                    {
                        normalized.Add(new JsonObject
                        {
                            ["sos_request_id"] = sosId.Value,
                            ["required_supplies"] = new JsonArray(),
                            ["required_teams"] = new JsonArray()
                        });
                    }

                    break;
                }
            }
        }

        root["sos_requirements"] = normalized;
    }

    private static void NormalizePipelineActivities(JsonObject root, string propertyName)
    {
        if (!root.TryGetPropertyValue(propertyName, out var node) || node is null)
            return;

        var normalized = new JsonArray();
        foreach (var entry in CoerceNodeToArray(node))
        {
            if (entry is not JsonObject obj)
                continue;

            NormalizePipelineSuppliesToCollect(obj);
            NormalizeNestedSuggestedTeam(obj);
            normalized.Add(obj.DeepClone());
        }

        root[propertyName] = normalized;
    }

    private static void NormalizePipelineActivityAssignments(JsonObject root)
    {
        if (!root.TryGetPropertyValue("activity_assignments", out var node) || node is null)
            return;

        var normalized = new JsonArray();
        foreach (var entry in CoerceNodeToArray(node))
        {
            if (entry is not JsonObject obj)
                continue;

            NormalizeNestedSuggestedTeam(obj);
            normalized.Add(obj.DeepClone());
        }

        root["activity_assignments"] = normalized;
    }

    private static void NormalizePipelineOrderedActivityKeys(JsonObject root)
    {
        if (!root.TryGetPropertyValue("ordered_activity_keys", out var node) || node is null)
            return;

        var normalized = new JsonArray();
        foreach (var entry in CoerceNodeToArray(node))
        {
            if (entry is not JsonValue value)
                continue;

            if (value.TryGetValue(out string? key) && !string.IsNullOrWhiteSpace(key))
                normalized.Add(key.Trim());
        }

        root["ordered_activity_keys"] = normalized;
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

    private static void NormalizePipelineTopLevelSuggestedTeam(JsonObject root)
    {
        if (!root.TryGetPropertyValue("suggested_team", out var suggestedTeamNode))
            return;

        if (suggestedTeamNode is null or JsonObject)
            return;

        root["suggested_team"] = null;
    }

    private static JsonArray CoerceNodeToArray(JsonNode node)
    {
        return node switch
        {
            JsonArray array => array,
            JsonObject obj => [(JsonNode)obj.DeepClone()],
            JsonValue value => [JsonNode.Parse(value.ToJsonString())!],
            _ => []
        };
    }

    private static void NormalizePipelineRequiredSupplies(JsonObject source)
    {
        if (!source.TryGetPropertyValue("required_supplies", out var node) || node is null)
            return;

        var normalized = new JsonArray();
        foreach (var entry in CoerceNodeToArray(node))
        {
            switch (entry)
            {
                case JsonObject obj:
                    normalized.Add(obj.DeepClone());
                    break;
                case JsonValue value when value.TryGetValue(out string? label) && !string.IsNullOrWhiteSpace(label):
                    normalized.Add(new JsonObject
                    {
                        ["item_name"] = label.Trim(),
                        ["quantity"] = 1,
                        ["unit"] = null,
                        ["category"] = null,
                        ["notes"] = null
                    });
                    break;
            }
        }

        source["required_supplies"] = normalized;
    }

    private static void NormalizePipelineRequiredTeams(JsonObject source)
    {
        if (!source.TryGetPropertyValue("required_teams", out var node) || node is null)
            return;

        var normalized = new JsonArray();
        foreach (var entry in CoerceNodeToArray(node))
        {
            switch (entry)
            {
                case JsonObject obj:
                    normalized.Add(obj.DeepClone());
                    break;
                case JsonValue value when value.TryGetValue(out string? label) && !string.IsNullOrWhiteSpace(label):
                    normalized.Add(new JsonObject
                    {
                        ["team_type"] = label.Trim(),
                        ["quantity"] = 1,
                        ["reason"] = null
                    });
                    break;
            }
        }

        source["required_teams"] = normalized;
    }

    private static void NormalizePipelineSuppliesToCollect(JsonObject source)
    {
        if (!source.TryGetPropertyValue("supplies_to_collect", out var node) || node is null)
            return;

        var normalized = new JsonArray();
        foreach (var entry in CoerceNodeToArray(node))
        {
            switch (entry)
            {
                case JsonObject obj:
                    normalized.Add(obj.DeepClone());
                    break;
                case JsonValue value when value.TryGetValue(out string? label) && !string.IsNullOrWhiteSpace(label):
                    normalized.Add(new JsonObject
                    {
                        ["item_name"] = label.Trim(),
                        ["quantity"] = 1,
                        ["unit"] = null
                    });
                    break;
            }
        }

        source["supplies_to_collect"] = normalized;
    }

    private static void NormalizeNestedSuggestedTeam(JsonObject source)
    {
        if (!source.TryGetPropertyValue("suggested_team", out var suggestedTeamNode))
            return;

        if (suggestedTeamNode is null or JsonObject)
            return;

        source["suggested_team"] = null;
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
        var requirementLookup = fragment.SosRequirements
            .Where(requirement => requirement.SosRequestId > 0)
            .GroupBy(requirement => requirement.SosRequestId)
            .ToDictionary(group => group.Key, group => group.First());

        fragment.SosRequirements = sosRequests
            .Select(sos =>
            {
                if (requirementLookup.TryGetValue(sos.Id, out var existing))
                {
                    existing.RequiredSupplies ??= [];
                    existing.RequiredTeams ??= [];
                    return existing;
                }

                return new MissionSosRequirementFragment
                {
                    SosRequestId = sos.Id,
                    Summary = string.IsNullOrWhiteSpace(sos.RawMessage) ? $"SOS #{sos.Id}" : sos.RawMessage.Trim(),
                    Priority = SosRequestAiAnalysisHelper.ResolveSuggestedPriority(sos.AiAnalysis, sos.PriorityLevel)
                        ?? sos.PriorityLevel
                        ?? "Medium",
                    NeedsImmediateSafeTransfer = sos.AiAnalysis?.NeedsImmediateSafeTransfer,
                    UrgentRescueRequiresImmediateSafeTransfer = sos.AiAnalysis?.NeedsImmediateSafeTransfer,
                    CanWaitForCombinedMission = sos.AiAnalysis?.CanWaitForCombinedMission,
                    HandlingReason = sos.AiAnalysis?.HandlingReason,
                    RequiredSupplies = [],
                    RequiredTeams = []
                };
            })
            .ToList();

        if (fragment.SplitClusterRecommended && string.IsNullOrWhiteSpace(fragment.SplitClusterReason))
            fragment.SplitClusterReason = "AI recommended cluster split but did not provide a specific reason.";
    }

    private static void ValidateDepotFragment(MissionDepotFragment fragment)
    {
        var selectedDepotId = fragment.Activities
            .Where(activity => activity.DepotId is > 0)
            .Select(activity => activity.DepotId)
            .Concat(fragment.SupplyShortages.Where(shortage => shortage.SelectedDepotId is > 0).Select(shortage => shortage.SelectedDepotId))
            .FirstOrDefault();
        var selectedDepotName = fragment.Activities
            .Select(activity => activity.DepotName)
            .Concat(fragment.SupplyShortages.Select(shortage => shortage.SelectedDepotName))
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        var selectedDepotAddress = fragment.Activities
            .Select(activity => activity.DepotAddress)
            .FirstOrDefault(address => !string.IsNullOrWhiteSpace(address));

        var normalizedActivities = new List<MissionActivityFragment>();
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sequence = 1;

        foreach (var activity in fragment.Activities.OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue))
        {
            activity.ActivityKey = EnsureUniqueActivityKey(
                activity.ActivityKey,
                activity.ActivityType,
                activity.SosRequestId,
                ref sequence,
                usedKeys);

            if (selectedDepotId is > 0 && IsSupplyPipelineActivity(activity.ActivityType))
            {
                activity.DepotId ??= selectedDepotId;
                activity.DepotName ??= selectedDepotName;
                activity.DepotAddress ??= selectedDepotAddress;
            }

            if (IsSupplyPipelineActivity(activity.ActivityType))
            {
                activity.SuppliesToCollect = activity.SuppliesToCollect?
                    .Where(supply => supply.Quantity > 0 && !string.IsNullOrWhiteSpace(supply.ItemName))
                    .ToList();

                if (activity.DepotId is null || activity.SuppliesToCollect is not { Count: > 0 })
                    continue;
            }

            normalizedActivities.Add(activity);
        }

        fragment.Activities = normalizedActivities;

        if (selectedDepotId is not > 0)
            return;

        foreach (var activity in fragment.Activities.Where(activity => IsSupplyPipelineActivity(activity.ActivityType)))
        {
            activity.DepotId = selectedDepotId;
            activity.DepotName ??= selectedDepotName;
            activity.DepotAddress ??= selectedDepotAddress;
        }

        foreach (var shortage in fragment.SupplyShortages)
        {
            shortage.SelectedDepotId ??= selectedDepotId;
            shortage.SelectedDepotName ??= selectedDepotName;
        }
    }

    private static void ValidateTeamFragment(MissionTeamFragment fragment, MissionDepotFragment depot)
    {
        var depotKeys = depot.Activities
            .Select(activity => activity.ActivityKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToList();

        var usedKeys = new HashSet<string>(depotKeys, StringComparer.OrdinalIgnoreCase);
        var sequence = 1;
        var additionalKeys = new List<string>();
        foreach (var activity in fragment.AdditionalActivities.OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue))
        {
            activity.ActivityKey = EnsureUniqueActivityKey(
                activity.ActivityKey,
                activity.ActivityType,
                activity.SosRequestId,
                ref sequence,
                usedKeys);
            additionalKeys.Add(activity.ActivityKey);
        }

        var allKeys = depotKeys
            .Concat(additionalKeys)
            .ToList();

        fragment.ActivityAssignments = fragment.ActivityAssignments
            .Where(assignment => !string.IsNullOrWhiteSpace(assignment.ActivityKey))
            .Where(assignment => allKeys.Contains(assignment.ActivityKey, StringComparer.OrdinalIgnoreCase))
            .GroupBy(assignment => assignment.ActivityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (allKeys.Count == 0)
            return;

        var normalizedOrder = fragment.OrderedActivityKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Where(key => allKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedOrder.Count == 0)
        {
            normalizedOrder = depot.Activities
                .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
                .Select(activity => activity.ActivityKey)
                .Concat(fragment.AdditionalActivities
                    .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
                    .Select(activity => activity.ActivityKey))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        foreach (var key in allKeys)
        {
            if (!normalizedOrder.Contains(key, StringComparer.OrdinalIgnoreCase))
                normalizedOrder.Add(key);
        }

        fragment.OrderedActivityKeys = normalizedOrder;
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
            var draftActivity = MapActivityFragmentToDraft(activity);
            if (assignmentLookup.TryGetValue(activity.ActivityKey, out var assignment))
            {
                draftActivity.ExecutionMode = assignment.ExecutionMode ?? draftActivity.ExecutionMode;
                draftActivity.RequiredTeamCount = assignment.RequiredTeamCount ?? draftActivity.RequiredTeamCount;
                draftActivity.CoordinationGroupKey = assignment.CoordinationGroupKey ?? draftActivity.CoordinationGroupKey;
                draftActivity.CoordinationNotes = assignment.CoordinationNotes ?? draftActivity.CoordinationNotes;
                draftActivity.SuggestedTeam = CloneSuggestedTeam(assignment.SuggestedTeam) ?? draftActivity.SuggestedTeam;
            }

            activityLookup[activity.ActivityKey] = draftActivity;
        }

        foreach (var activity in team.AdditionalActivities.OrderBy(item => item.Step))
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

            activityLookup[activity.ActivityKey] = draftActivity;
        }

        var orderedKeys = team.OrderedActivityKeys
            .Where(activityKey => activityLookup.ContainsKey(activityKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedKeys.Count == 0)
        {
            orderedKeys = depot.Activities
                .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
                .Select(activity => activity.ActivityKey)
                .Concat(team.AdditionalActivities
                    .OrderBy(activity => activity.Step > 0 ? activity.Step : int.MaxValue)
                    .Select(activity => activity.ActivityKey))
                .Where(activityKey => activityLookup.ContainsKey(activityKey))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        foreach (var key in activityLookup.Keys)
        {
            if (!orderedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                orderedKeys.Add(key);
        }

        var draftActivities = orderedKeys
            .Select(activityKey => activityLookup[activityKey])
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

    private static bool IsSupplyPipelineActivity(string? activityType) =>
        string.Equals(activityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activityType, "DELIVER_SUPPLIES", StringComparison.OrdinalIgnoreCase);

    private static string EnsureUniqueActivityKey(
        string? activityKey,
        string? activityType,
        int? sosRequestId,
        ref int sequence,
        ISet<string> usedKeys)
    {
        var candidate = string.IsNullOrWhiteSpace(activityKey)
            ? BuildGeneratedActivityKey(activityType, sosRequestId, sequence++)
            : activityKey.Trim();
        var resolved = candidate;
        var suffix = 2;

        while (!usedKeys.Add(resolved))
            resolved = $"{candidate}-{suffix++}";

        return resolved;
    }

    private static string BuildGeneratedActivityKey(string? activityType, int? sosRequestId, int sequence)
    {
        var typeToken = string.IsNullOrWhiteSpace(activityType)
            ? "activity"
            : activityType.Trim().ToLowerInvariant().Replace('_', '-');
        var sosToken = sosRequestId is > 0 ? sosRequestId.Value.ToString() : "auto";
        return $"{typeToken}-{sosToken}-{sequence}";
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
        await EnsureInventoryBackedTransportSuppliesAsync(result, sosRequests, nearbyDepots ?? [], cancellationToken);
        NormalizeActivitySequence(result.SuggestedActivities, sosLookup);
        BackfillSosRequestIds(result.SuggestedActivities, sosRequests);
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
