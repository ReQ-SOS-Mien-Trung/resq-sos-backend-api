using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;

public class UpdateDepotCommandHandler(
    IDepotRepository depotRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateDepotCommandHandler> logger) : IRequestHandler<UpdateDepotCommand>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateDepotCommandHandler> _logger = logger;

    public async Task Handle(UpdateDepotCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling UpdateDepotCommand for Id={Id}", request.Id);

        // 1. Load Domain Model
        var depot = await _depotRepository.GetByIdAsync(request.Id, cancellationToken);
        if (depot == null)
        {
            // NotFoundException outputs: "Không tìm thấy thực thể "Kho" ({id})."
            throw new NotFoundException("Kho", request.Id);
        }

        // 2. Business Validation: Unique Name
        if (!string.Equals(depot.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var existingName = await _depotRepository.GetByNameAsync(request.Name, cancellationToken);
            if (existingName != null && existingName.Id != request.Id)
            {
                throw new ConflictException($"Kho với tên '{request.Name}' đã tồn tại.");
            }
        }

        // 3. Apply changes to Domain Model (Enforces Invariants)
        // Domain exceptions are already in Vietnamese (e.g., DepotCapacityExceededException)
        depot.UpdateDetails(
            request.Name,
            request.Address,
            request.Location,
            request.Capacity
        );

        // 4. Persist changes
        await _depotRepository.UpdateAsync(depot, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Updated depot successfully: Id={Id}", request.Id);
    }
}
