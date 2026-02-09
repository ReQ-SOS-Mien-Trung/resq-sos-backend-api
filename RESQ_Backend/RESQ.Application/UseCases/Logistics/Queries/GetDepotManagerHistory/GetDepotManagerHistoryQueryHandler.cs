using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotManagerHistory;

public class GetDepotManagerHistoryQueryHandler(
    IDepotRepository depotRepository,
    ILogger<GetDepotManagerHistoryQueryHandler> logger)
    : IRequestHandler<GetDepotManagerHistoryQuery, PagedResult<DepotManagerHistoryDto>>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly ILogger<GetDepotManagerHistoryQueryHandler> _logger = logger;

    public async Task<PagedResult<DepotManagerHistoryDto>> Handle(GetDepotManagerHistoryQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetDepotManagerHistoryQuery for DepotId={DepotId}, Page={Page}", request.DepotId, request.PageNumber);

        var depot = await _depotRepository.GetByIdAsync(request.DepotId, cancellationToken);
        if (depot == null)
        {
            throw new NotFoundException($"Không tìm thấy kho cứu trợ với id = {request.DepotId}");
        }

        // Get all history items as queryable or list to perform pagination in memory
        // Since ManagerHistory is loaded with the Aggregate Root, we paginate the list in memory.
        var allHistory = depot.ManagerHistory
            .Select(h => new DepotManagerHistoryDto
            {
                UserId = h.UserId,
                FullName = h.FullName,
                Email = h.Email,
                Phone = h.Phone,
                AssignedAt = h.AssignedAt,
                UnassignedAt = h.UnassignedAt,
                IsCurrent = h.IsActive()
            })
            .OrderByDescending(h => h.AssignedAt)
            .ToList();

        var totalCount = allHistory.Count;
        
        var pagedItems = allHistory
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<DepotManagerHistoryDto>(
            pagedItems, 
            totalCount, 
            request.PageNumber, 
            request.PageSize);
    }
}
