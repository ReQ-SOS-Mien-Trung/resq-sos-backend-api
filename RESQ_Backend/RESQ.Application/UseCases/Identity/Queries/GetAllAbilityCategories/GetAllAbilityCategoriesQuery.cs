using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetAllAbilityCategories;

public record GetAllAbilityCategoriesQuery : IRequest<GetAllAbilityCategoriesResponse>;
