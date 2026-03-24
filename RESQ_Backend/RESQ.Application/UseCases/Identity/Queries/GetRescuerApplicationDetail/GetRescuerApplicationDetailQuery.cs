using MediatR;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerApplicationDetail
{
    public record GetRescuerApplicationDetailQuery(int Id) : IRequest<RescuerApplicationDto?>;
}
