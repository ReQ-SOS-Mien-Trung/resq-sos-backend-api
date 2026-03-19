using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Personnel.Queries.GetRescuers;

public record GetRescuersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    bool? HasAssemblyPoint = null,
    bool? HasTeam = null,
    RescuerType? RescuerType = null,
    string? AbilitySubgroupCode = null,
    string? AbilityCategoryCode = null) : IRequest<PagedResult<RescuerDto>>;
