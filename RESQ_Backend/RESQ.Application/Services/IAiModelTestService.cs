namespace RESQ.Application.Services;

/// <summary>
/// Service để kiểm tra kết nối và hoạt động của AI model
/// </summary>
public interface IAiModelTestService
{
    /// <summary>
    /// Gửi một tin nhắn test đến AI API để xác nhận model hoạt động đúng.
    /// </summary>
    Task<AiModelTestResult> TestModelAsync(
        string model,
        string apiUrlTemplate,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken = default);
}

public class AiModelTestResult
{
    public bool IsSuccess { get; set; }
    public string Model { get; set; } = string.Empty;
    public string? AiResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public int? HttpStatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
}
