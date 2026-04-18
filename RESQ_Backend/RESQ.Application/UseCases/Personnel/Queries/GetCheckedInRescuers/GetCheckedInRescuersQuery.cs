using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;

public record GetCheckedInRescuersQuery(
    int AssemblyEventId,
    int PageNumber = 1,
    int PageSize = 10,
    RescuerType? RescuerType = null,
    string? AbilitySubgroupCode = null,
    string? AbilityCategoryCode = null,
    /// <summary>Tìm kiếm theo firstName, lastName, phone hoặc email (OR logic).</summary>
    string? Search = null) : IRequest<PagedResult<CheckedInRescuerDto>>;
