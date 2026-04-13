using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Services.Ai;
using RESQ.Domain.Enum.System;
using RESQ.Infrastructure.Services.Ai;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Infrastructure.Ai;

public class OpenRouterAiProviderClientTests
{
    [Fact]
    public async Task CompleteAsync_ShouldSendOpenAiCompatiblePayload_AndMapToolCalls()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "",
                            "tool_calls": [
                              {
                                "id": "call_1",
                                "type": "function",
                                "function": {
                                  "name": "searchInventory",
                                  "arguments": "{\"category\":\"Y tế\"}"
                                }
                              }
                            ]
                          },
                          "finish_reason": "tool_calls"
                        }
                      ]
                    }
                    """)
            });
        var client = new OpenRouterAiProviderClient(new FakeHttpClientFactory(handler), NullLogger<OpenRouterAiProviderClient>.Instance);

        var response = await client.CompleteAsync(new AiCompletionRequest
        {
            Provider = AiProvider.OpenRouter,
            Model = "openai/gpt-4o-mini",
            ApiUrl = "https://openrouter.example/chat/completions",
            ApiKey = "openrouter-key",
            SystemPrompt = "system-prompt",
            Temperature = 0.2,
            MaxTokens = 1024,
            Messages = [AiChatMessage.User("plan rescue")],
            Tools = [new AiToolDefinition
            {
                Name = "searchInventory",
                Description = "Search inventory",
                Parameters = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        category = new { type = "string" }
                    }
                })
            }]
        });

        Assert.Equal("https://openrouter.example/chat/completions", handler.LastRequest?.RequestUri?.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "openrouter-key"), handler.LastRequest?.Headers.Authorization);

        var payload = handler.GetLastJsonBody();
        Assert.Equal("system", payload.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("system-prompt", payload.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("searchInventory", payload.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("auto", payload.GetProperty("tool_choice").GetString());

        var toolCall = Assert.Single(response.ToolCalls);
        Assert.Equal("call_1", toolCall.Id);
        Assert.Equal("searchInventory", toolCall.Name);
        Assert.Equal("Y tế", toolCall.Arguments.GetProperty("category").GetString());
        Assert.Equal("tool_calls", response.FinishReason);
    }
}
