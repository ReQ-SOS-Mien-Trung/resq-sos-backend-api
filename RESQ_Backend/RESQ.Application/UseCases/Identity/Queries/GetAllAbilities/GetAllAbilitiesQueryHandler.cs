using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetAllAbilities;

public class GetAllAbilitiesQueryHandler(
    IAbilityRepository repository,
    ILogger<GetAllAbilitiesQueryHandler> logger)
    : IRequestHandler<GetAllAbilitiesQuery, GetAllAbilitiesResponse>
{
    private readonly IAbilityRepository _repository = repository;
    private readonly ILogger<GetAllAbilitiesQueryHandler> _logger = logger;

    public async Task<GetAllAbilitiesResponse> Handle(GetAllAbilitiesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {handler} - retrieving all abilities", nameof(GetAllAbilitiesQueryHandler));

        var abilities = await _repository.GetAllAbilitiesAsync(cancellationToken);

        var dtos = abilities.Select(a => new AbilityDto
        {
            Id = a.Id,
            Code = a.Code,
            Description = a.Description,
            AbilityCategoryId = a.AbilityCategoryId,
            AbilityCategory = a.AbilityCategory is not null
                ? new AbilityCategoryDto
                {
                    Id = a.AbilityCategory.Id,
                    Code = a.AbilityCategory.Code,
                    Description = a.AbilityCategory.Description
                }
                : null
        }).ToList();

        _logger.LogInformation("{handler} - retrieved {count} abilities", nameof(GetAllAbilitiesQueryHandler), dtos.Count);

        return new GetAllAbilitiesResponse { Items = dtos };
    }
}
