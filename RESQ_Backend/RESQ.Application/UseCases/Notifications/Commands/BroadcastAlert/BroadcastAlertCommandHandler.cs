using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Notifications.Commands.BroadcastAlert;

public class BroadcastAlertCommandHandler(
    IFirebaseService firebaseService,
    ILogger<BroadcastAlertCommandHandler> logger
) : IRequestHandler<BroadcastAlertCommand, BroadcastAlertResponse>
{
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<BroadcastAlertCommandHandler> _logger = logger;

    public async Task<BroadcastAlertResponse> Handle(BroadcastAlertCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Broadcasting alert '{Title}' to all_users topic by {SentBy}",
            request.Title, request.SentByUserId);

        await _firebaseService.SendToTopicAsync(
            "all_users",
            request.Title,
            request.Body,
            request.Type,
            cancellationToken);

        _logger.LogInformation("Broadcast alert '{Title}' sent to FCM topic all_users", request.Title);
        return new BroadcastAlertResponse(1);
    }
}
