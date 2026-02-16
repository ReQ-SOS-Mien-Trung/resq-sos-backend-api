using MediatR;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerAbilities;

public record GetRescuerAbilitiesQuery(Guid UserId) : IRequest<GetRescuerAbilitiesResponse>;
