using MediatR;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;

namespace RESQ.Application.UseCases.Identity.Queries.GetMyRescuerApplication
{
    public record GetMyRescuerApplicationQuery(Guid UserId) : IRequest<RescuerApplicationDto?>;
}
