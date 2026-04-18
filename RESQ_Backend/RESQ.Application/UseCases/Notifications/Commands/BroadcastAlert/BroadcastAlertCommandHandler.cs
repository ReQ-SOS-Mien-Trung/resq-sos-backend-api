using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;
using System.Text.Json;

namespace RESQ.Application.UseCases.Notifications.Commands.BroadcastAlert;

public class BroadcastAlertCommandHandler(
    IFirebaseService firebaseService,
    ILogger<BroadcastAlertCommandHandler> logger
) : IRequestHandler<BroadcastAlertCommand, BroadcastAlertResponse>
{
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<BroadcastAlertCommandHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<BroadcastAlertResponse> Handle(BroadcastAlertCommand request, CancellationToken cancellationToken)
    {
        var firstAlert = request.ActiveAlerts?.FirstOrDefault();
        var title = firstAlert?.Title ?? "Cảnh báo khẩn";
        var body = firstAlert?.Description ?? string.Empty;
        var type = firstAlert?.EventType ?? "flood_alert";

        _logger.LogInformation("Broadcasting alert '{Title}' to all_users topic by {SentBy}", title, request.SentByUserId);

        var extraData = new Dictionary<string, string>
        {
            ["type"] = type,
            ["payload"] = JsonSerializer.Serialize(new
            {
                location = request.Location,
                active_alerts = request.ActiveAlerts
            }, _jsonOpts)
        };

        await _firebaseService.SendToTopicAsync("all_users", title, body, extraData, cancellationToken);

        _logger.LogInformation("Broadcast alert '{Title}' sent to FCM topic all_users", title);
        return new BroadcastAlertResponse(request.ActiveAlerts?.Count ?? 1);
    }
}
