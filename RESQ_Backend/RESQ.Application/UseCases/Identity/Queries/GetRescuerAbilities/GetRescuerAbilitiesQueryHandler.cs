using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerAbilities;

public class GetRescuerAbilitiesQueryHandler(
    IAbilityRepository repository,
    ILogger<GetRescuerAbilitiesQueryHandler> logger)
    : IRequestHandler<GetRescuerAbilitiesQuery, GetRescuerAbilitiesResponse>
{
    private readonly IAbilityRepository _repository = repository;
    private readonly ILogger<GetRescuerAbilitiesQueryHandler> _logger = logger;

    public async Task<GetRescuerAbilitiesResponse> Handle(GetRescuerAbilitiesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {handler} for UserId={userId}", nameof(GetRescuerAbilitiesQueryHandler), request.UserId);

        var userAbilities = await _repository.GetUserAbilitiesAsync(request.UserId, cancellationToken);

        var dtos = userAbilities.Select(ua => new RescuerAbilityDto
        {
            AbilityId = ua.AbilityId,
            Code = ua.AbilityCode ?? string.Empty,
            Description = ua.AbilityDescription,
            Level = ua.Level
        }).ToList();

        _logger.LogInformation("{handler} - found {count} abilities for UserId={userId}",
            nameof(GetRescuerAbilitiesQueryHandler), dtos.Count, request.UserId);

        return new GetRescuerAbilitiesResponse
        {
            UserId = request.UserId,
            Abilities = dtos
        };
    }
}
