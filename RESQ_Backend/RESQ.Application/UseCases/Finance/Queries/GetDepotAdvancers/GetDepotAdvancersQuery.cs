using MediatR;
using RESQ.Application.Common.Models;
using System;

namespace RESQ.Application.UseCases.Finance.Queries.GetDepotAdvancers;

public record GetDepotAdvancersQuery(
    Guid UserId,
    int PageNumber = 1,
    int PageSize = 10, int? DepotId = null) : IRequest<PagedResult<DepotAdvancerDto>>;
