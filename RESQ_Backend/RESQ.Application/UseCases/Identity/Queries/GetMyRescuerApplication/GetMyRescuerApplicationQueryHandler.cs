using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;

namespace RESQ.Application.UseCases.Identity.Queries.GetMyRescuerApplication
{
    public class GetMyRescuerApplicationQueryHandler(
        IRescuerApplicationRepository rescuerApplicationRepository,
        ILogger<GetMyRescuerApplicationQueryHandler> logger
    ) : IRequestHandler<GetMyRescuerApplicationQuery, RescuerApplicationDto?>
    {
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly ILogger<GetMyRescuerApplicationQueryHandler> _logger = logger;

        public async Task<RescuerApplicationDto?> Handle(GetMyRescuerApplicationQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting rescuer application for UserId={UserId}", request.UserId);

            var result = await _rescuerApplicationRepository.GetLatestByUserIdAsync(request.UserId, cancellationToken);

            return result;
        }
    }
}
