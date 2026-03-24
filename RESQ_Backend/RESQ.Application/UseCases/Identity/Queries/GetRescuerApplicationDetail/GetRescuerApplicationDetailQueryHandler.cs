using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerApplicationDetail
{
    public class GetRescuerApplicationDetailQueryHandler(
        IRescuerApplicationRepository rescuerApplicationRepository,
        IAbilityRepository abilityRepository,
        ILogger<GetRescuerApplicationDetailQueryHandler> logger
    ) : IRequestHandler<GetRescuerApplicationDetailQuery, RescuerApplicationDto?>
    {
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly IAbilityRepository _abilityRepository = abilityRepository;
        private readonly ILogger<GetRescuerApplicationDetailQueryHandler> _logger = logger;

        public async Task<RescuerApplicationDto?> Handle(GetRescuerApplicationDetailQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting rescuer application detail: Id={Id}", request.Id);

            var application = await _rescuerApplicationRepository.GetDetailByIdAsync(request.Id, cancellationToken);
            if (application is null)
                return null;

            var userAbilities = await _abilityRepository.GetUserAbilitiesAsync(application.UserId, cancellationToken);
            application.Abilities = userAbilities.Select(a => new RescuerApplicationAbilityDto
            {
                AbilityId = a.AbilityId,
                Code = a.AbilityCode,
                Description = a.AbilityDescription,
                Level = a.Level,
                SubgroupId = a.SubgroupId,
                SubgroupCode = a.SubgroupCode,
                SubgroupDescription = a.SubgroupDescription,
                CategoryId = a.CategoryId,
                CategoryCode = a.CategoryCode,
                CategoryDescription = a.CategoryDescription
            }).ToList();

            return application;
        }
    }
}
