using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications
{
    public class GetRescuerApplicationsQueryHandler(
        IRescuerApplicationRepository rescuerApplicationRepository,
        ILogger<GetRescuerApplicationsQueryHandler> logger
    ) : IRequestHandler<GetRescuerApplicationsQuery, PagedResult<RescuerApplicationDto>>
    {
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly ILogger<GetRescuerApplicationsQueryHandler> _logger = logger;

        public async Task<PagedResult<RescuerApplicationDto>> Handle(GetRescuerApplicationsQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting rescuer applications: Page={Page}, Size={Size}, Status={Status}",
                request.PageNumber, request.PageSize, request.Status);

            var result = await _rescuerApplicationRepository.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                request.Status,
                cancellationToken
            );

            return result;
        }
    }
}
