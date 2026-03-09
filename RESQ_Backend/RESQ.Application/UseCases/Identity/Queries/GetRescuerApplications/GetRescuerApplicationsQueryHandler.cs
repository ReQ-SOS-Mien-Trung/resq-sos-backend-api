using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications
{
    public class GetRescuerApplicationsQueryHandler(
        IRescuerApplicationRepository rescuerApplicationRepository,
        IAbilityRepository abilityRepository,
        ILogger<GetRescuerApplicationsQueryHandler> logger
    ) : IRequestHandler<GetRescuerApplicationsQuery, PagedResult<RescuerApplicationDto>>
    {
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly IAbilityRepository _abilityRepository = abilityRepository;
        private readonly ILogger<GetRescuerApplicationsQueryHandler> _logger = logger;

        public async Task<PagedResult<RescuerApplicationDto>> Handle(GetRescuerApplicationsQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting rescuer applications: Page={Page}, Size={Size}, Status={Status}, Name={Name}, Email={Email}, Phone={Phone}",
                request.PageNumber, request.PageSize, request.Status, request.Name, request.Email, request.Phone);

            var result = await _rescuerApplicationRepository.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                request.Status,
                request.Name,
                request.Email,
                request.Phone,
                cancellationToken
            );

            foreach (var app in result.Items)
            {
                var userAbilities = await _abilityRepository.GetUserAbilitiesAsync(app.UserId, cancellationToken);
                app.Abilities = userAbilities.Select(a => new RescuerApplicationAbilityDto
                {
                    AbilityId = a.AbilityId,
                    Code = a.AbilityCode,
                    Description = a.AbilityDescription,
                    Level = a.Level
                }).ToList();
            }

            return result;
        }
    }
}
