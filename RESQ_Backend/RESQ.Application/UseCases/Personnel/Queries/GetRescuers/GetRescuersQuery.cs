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
    string? AbilityCategoryCode = null,
    /// <summary>Tìm kiếm theo firstName, lastName, phone hoặc email (OR logic).</summary>
    string? Search = null,
    /// <summary>Lọc theo danh sách mã điểm tập kết (OR logic). Null = không lọc.</summary>
    List<string>? AssemblyPointCodes = null) : IRequest<PagedResult<RescuerDto>>;
