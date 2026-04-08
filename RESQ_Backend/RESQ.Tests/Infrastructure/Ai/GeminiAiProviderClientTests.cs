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
    public async Task CompleteAsync_ShouldFormatGeminiRequest_AndMapToolCalls()
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
                                }
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

        var response = await client.CompleteAsync(new AiCompletionRequest
        {
            Provider = AiProvider.Gemini,
            Model = "gemini-2.5-flash",
            ApiUrl = "https://gemini.example/models/{0}:generateContent?key={1}",
            ApiKey = "gemini-key",
            SystemPrompt = "system-instruction",
            Temperature = 0.3,
            MaxTokens = 512,
            Messages = [AiChatMessage.User("analyze this")],
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

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("https://gemini.example/models/gemini-2.5-flash:generateContent?key=gemini-key", handler.LastRequest?.RequestUri?.ToString());

        var payload = handler.GetLastJsonBody();
        Assert.Equal("system-instruction", payload.GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal("user", payload.GetProperty("contents")[0].GetProperty("role").GetString());
        Assert.Equal("searchInventory", payload.GetProperty("tools")[0].GetProperty("functionDeclarations")[0].GetProperty("name").GetString());

        var toolCall = Assert.Single(response.ToolCalls);
        Assert.Equal("searchInventory", toolCall.Name);
        Assert.Equal("Nước", toolCall.Arguments.GetProperty("category").GetString());
        Assert.Equal("STOP", response.FinishReason);
    }
}