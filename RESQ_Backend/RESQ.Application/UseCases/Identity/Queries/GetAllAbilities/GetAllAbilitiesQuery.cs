using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetAllAbilities;

public record GetAllAbilitiesQuery() : IRequest<GetAllAbilitiesResponse>;
