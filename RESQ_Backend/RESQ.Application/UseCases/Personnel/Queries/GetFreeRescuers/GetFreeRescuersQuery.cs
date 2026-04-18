using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Personnel.Queries.GetFreeRescuers;

public record GetFreeRescuersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? FirstName = null,
    string? LastName = null,
    string? Phone = null,
    string? Email = null,
    RescuerType? RescuerType = null) : IRequest<PagedResult<FreeRescuerDto>>;
