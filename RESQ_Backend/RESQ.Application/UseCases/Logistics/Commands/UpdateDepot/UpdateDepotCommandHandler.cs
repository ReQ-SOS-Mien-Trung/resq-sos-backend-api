using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics.Exceptions;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

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

        var depot = await _depotRepository.GetByIdAsync(request.Id, cancellationToken);
        if (depot == null)
        {
            throw new NotFoundException("Không tìm thấy kho cứu trợ");
        }

        // 1. Validate Duplicate Name (excluding current record)
        if (!string.Equals(depot.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var existingName = await _depotRepository.GetByNameAsync(request.Name, cancellationToken);
            if (existingName != null && existingName.Id != request.Id)
            {
                throw new DepotNameDuplicatedException(request.Name);
            }
        }

        // 2. Update Domain Model
        var location = new GeoLocation(request.Latitude, request.Longitude);

        depot.UpdateDetails(
            request.Name,
            request.Address,
            location,
            request.Capacity
        );

        await _depotRepository.UpdateAsync(depot, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Updated depot successfully: Id={Id}", request.Id);
    }
}
