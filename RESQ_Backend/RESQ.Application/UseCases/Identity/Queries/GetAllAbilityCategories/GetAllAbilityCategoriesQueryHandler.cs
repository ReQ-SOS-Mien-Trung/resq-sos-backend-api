using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetAllAbilityCategories;

public class GetAllAbilityCategoriesQueryHandler(
    IAbilityCategoryRepository repository,
    ILogger<GetAllAbilityCategoriesQueryHandler> logger)
    : IRequestHandler<GetAllAbilityCategoriesQuery, GetAllAbilityCategoriesResponse>
{
    private readonly IAbilityCategoryRepository _repository = repository;
    private readonly ILogger<GetAllAbilityCategoriesQueryHandler> _logger = logger;

    public async Task<GetAllAbilityCategoriesResponse> Handle(GetAllAbilityCategoriesQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {handler} - retrieving all ability categories", nameof(GetAllAbilityCategoriesQueryHandler));

        var categories = await _repository.GetAllAsync(cancellationToken);

        var dtos = categories.Select(c => new AbilityCategoryItemDto
        {
            Id = c.Id,
            Code = c.Code,
            Description = c.Description
        }).ToList();

        _logger.LogInformation("{handler} - retrieved {count} ability categories", nameof(GetAllAbilityCategoriesQueryHandler), dtos.Count);

        return new GetAllAbilityCategoriesResponse { Items = dtos };
    }
}
