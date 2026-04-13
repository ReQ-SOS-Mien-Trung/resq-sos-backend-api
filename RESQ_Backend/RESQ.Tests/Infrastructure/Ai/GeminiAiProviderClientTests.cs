using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Services.Ai;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Infrastructure.Ai;

public class GeminiAiProviderClientTests
{
    [Fact]
    public async Task CompleteAsync_ShouldFormatGeminiRequest_AndPreserveNativeFunctionCallPart()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "candidates": [
                        {
                          "content": {
                            "parts": [
                              {
                                "functionCall": {
                                  "name": "searchInventory",
                                  "args": {
                                    "category": "Nước"
                                  }
                                },
                                "thoughtSignature": "sig-step-1"
                              }
                            ]
                          },
                          "finishReason": "STOP"
                        }
                      ]
                    }
                    """)
            });
        var client = new GeminiAiProviderClient(new FakeHttpClientFactory(handler), NullLogger<GeminiAiProviderClient>.Instance);

        var response = await client.CompleteAsync(CreateGeminiRequest([AiChatMessage.User("analyze this")], "system-instruction"));

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("https://gemini.example/models/gemini-2.5-flash:generateContent?key=gemini-key", handler.LastRequest?.RequestUri?.ToString());

        var payload = handler.GetLastJsonBody();
        Assert.Equal("system-instruction", payload.GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal("user", payload.GetProperty("contents")[0].GetProperty("role").GetString());
        Assert.Equal("searchInventory", payload.GetProperty("tools")[0].GetProperty("functionDeclarations")[0].GetProperty("name").GetString());

        var toolCall = Assert.Single(response.ToolCalls);
        Assert.Equal("searchInventory", toolCall.Name);
        Assert.Equal("Nước", toolCall.Arguments.GetProperty("category").GetString());
        Assert.True(toolCall.NativeFunctionCallPart.HasValue);
        Assert.Equal("sig-step-1", toolCall.NativeFunctionCallPart.Value.GetProperty("thoughtSignature").GetString());
        Assert.Equal("searchInventory", toolCall.NativeFunctionCallPart.Value.GetProperty("functionCall").GetProperty("name").GetString());
        Assert.Equal("STOP", response.FinishReason);
    }

    [Fact]
    public async Task CompleteAsync_ShouldReplayStoredFunctionCallPartWithSignature_AndFunctionResponse()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            CreateGeminiHttpResponse(
                """
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "functionCall": {
                              "name": "searchInventory",
                              "args": {
                                "category": "medical"
                              }
                            },
                            "thoughtSignature": "sig-step-1"
                          }
                        ]
                      },
                      "finishReason": "STOP"
                    }
                  ]
                }
                """),
            CreateGeminiHttpResponse(
                """
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "text": "done"
                          }
                        ]
                      },
                      "finishReason": "STOP"
                    }
                  ]
                }
                """)
        ]);

        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = new GeminiAiProviderClient(new FakeHttpClientFactory(handler), NullLogger<GeminiAiProviderClient>.Instance);

        var firstResponse = await client.CompleteAsync(CreateGeminiRequest([AiChatMessage.User("plan rescue")], "system-instruction"));
        var firstToolCall = Assert.Single(firstResponse.ToolCalls);
        var toolResult = JsonSerializer.SerializeToElement(new
        {
            items = new[] { "bandage" }
        });

        await client.CompleteAsync(CreateGeminiRequest(
        [
            AiChatMessage.User("plan rescue"),
            AiChatMessage.Assistant(firstResponse.Text, firstResponse.ToolCalls),
            AiChatMessage.Tool(firstToolCall.Id, firstToolCall.Name, toolResult)
        ], "system-instruction"));

        var payload = handler.GetLastJsonBody();
        var contents = payload.GetProperty("contents");
        var replayedAssistantPart = contents[1].GetProperty("parts")[0];
        var replayedToolResponse = contents[2].GetProperty("parts")[0].GetProperty("functionResponse");

        Assert.Equal("sig-step-1", replayedAssistantPart.GetProperty("thoughtSignature").GetString());
        Assert.Equal("searchInventory", replayedAssistantPart.GetProperty("functionCall").GetProperty("name").GetString());
        Assert.Equal("medical", replayedAssistantPart.GetProperty("functionCall").GetProperty("args").GetProperty("category").GetString());
        Assert.Equal("searchInventory", replayedToolResponse.GetProperty("name").GetString());
        Assert.Equal("bandage", replayedToolResponse.GetProperty("response").GetProperty("items")[0].GetString());
    }

    [Fact]
    public async Task CompleteAsync_ShouldReplayAllPriorFunctionCallSignaturesAcrossMultipleSteps()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            CreateGeminiHttpResponse(
                """
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "functionCall": {
                              "name": "searchInventory",
                              "args": {
                                "category": "medical"
                              }
                            },
                            "thoughtSignature": "sig-step-1a"
                          },
                          {
                            "functionCall": {
                              "name": "getTeams",
                              "args": {
                                "radiusKm": 5
                              }
                            },
                            "thought_signature": "sig-step-1b"
                          }
                        ]
                      },
                      "finishReason": "STOP"
                    }
                  ]
                }
                """),
            CreateGeminiHttpResponse(
                """
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "functionCall": {
                              "name": "confirmPlan",
                              "args": {
                                "teamId": 7
                              }
                            },
                            "thoughtSignature": "sig-step-2"
                          }
                        ]
                      },
                      "finishReason": "STOP"
                    }
                  ]
                }
                """),
            CreateGeminiHttpResponse(
                """
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "text": "final plan"
                          }
                        ]
                      },
                      "finishReason": "STOP"
                    }
                  ]
                }
                """)
        ]);

        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = new GeminiAiProviderClient(new FakeHttpClientFactory(handler), NullLogger<GeminiAiProviderClient>.Instance);

        var step1Response = await client.CompleteAsync(CreateGeminiRequest([AiChatMessage.User("plan rescue")], "system-instruction"));
        var step1ToolCalls = step1Response.ToolCalls;

        var step2Messages = new List<AiChatMessage>
        {
            AiChatMessage.User("plan rescue"),
            AiChatMessage.Assistant(step1Response.Text, step1ToolCalls),
            AiChatMessage.Tool(step1ToolCalls[0].Id, step1ToolCalls[0].Name, JsonSerializer.SerializeToElement(new { depots = 2 })),
            AiChatMessage.Tool(step1ToolCalls[1].Id, step1ToolCalls[1].Name, JsonSerializer.SerializeToElement(new { teams = 3 }))
        };

        var step2Response = await client.CompleteAsync(CreateGeminiRequest(step2Messages, "system-instruction"));
        var step2ToolCall = Assert.Single(step2Response.ToolCalls);

        var step3Messages = new List<AiChatMessage>(step2Messages)
        {
            AiChatMessage.Assistant(step2Response.Text, step2Response.ToolCalls),
            AiChatMessage.Tool(step2ToolCall.Id, step2ToolCall.Name, JsonSerializer.SerializeToElement(new { approved = true }))
        };

        await client.CompleteAsync(CreateGeminiRequest(step3Messages, "system-instruction"));

        var payload = handler.GetLastJsonBody();
        var contents = payload.GetProperty("contents");
        var firstAssistantParts = contents[1].GetProperty("parts");
        var secondAssistantParts = contents[4].GetProperty("parts");

        Assert.Equal(2, firstAssistantParts.GetArrayLength());
        Assert.Equal("sig-step-1a", firstAssistantParts[0].GetProperty("thoughtSignature").GetString());
        Assert.Equal("searchInventory", firstAssistantParts[0].GetProperty("functionCall").GetProperty("name").GetString());
        Assert.Equal("sig-step-1b", firstAssistantParts[1].GetProperty("thought_signature").GetString());
        Assert.Equal("getTeams", firstAssistantParts[1].GetProperty("functionCall").GetProperty("name").GetString());
        Assert.Equal("sig-step-2", secondAssistantParts[0].GetProperty("thoughtSignature").GetString());
        Assert.Equal("confirmPlan", secondAssistantParts[0].GetProperty("functionCall").GetProperty("name").GetString());
    }

    private static AiCompletionRequest CreateGeminiRequest(IReadOnlyList<AiChatMessage> messages, string systemPrompt)
    {
        return new AiCompletionRequest
        {
            Provider = AiProvider.Gemini,
            Model = "gemini-2.5-flash",
            ApiUrl = "https://gemini.example/models/{0}:generateContent?key={1}",
            ApiKey = "gemini-key",
            SystemPrompt = systemPrompt,
            Temperature = 0.3,
            MaxTokens = 512,
            Messages = messages,
            Tools =
            [
                CreateToolDefinition("searchInventory", new
                {
                    category = new { type = "string" }
                }),
                CreateToolDefinition("getTeams", new
                {
                    radiusKm = new { type = "number" }
                }),
                CreateToolDefinition("confirmPlan", new
                {
                    teamId = new { type = "integer" }
                })
            ]
        };
    }

    private static AiToolDefinition CreateToolDefinition(string name, object properties)
    {
        return new AiToolDefinition
        {
            Name = name,
            Description = $"Tool {name}",
            Parameters = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties
            })
        };
    }

    private static HttpResponseMessage CreateGeminiHttpResponse(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        };
    }
}
