using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Exceptions;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateDepot;

public class CreateDepotCommandHandler(
    IDepotRepository depotRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateDepotCommandHandler> logger) 
    : IRequestHandler<CreateDepotCommand, CreateDepotResponse>
{
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreateDepotCommandHandler> _logger = logger;

    public async Task<CreateDepotResponse> Handle(CreateDepotCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CreateDepotCommand for Name={name}", request.Name);
        
        var existing = await _depotRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing != null)
        {
            throw new DepotNameDuplicatedException(request.Name);
        }

        // Validate manager nếu có
        if (request.ManagerId.HasValue && request.ManagerId.Value != Guid.Empty)
        {
            var manager = await userRepository.GetByIdAsync(request.ManagerId.Value, cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy người dùng với ID = {request.ManagerId.Value}");

            if (manager.RoleId != 4)
                throw new BadRequestException(
                    $"Người dùng {manager.LastName} {manager.FirstName} không có vai trò Quản lý kho (Manager).");
        }

        var location = new GeoLocation(request.Latitude, request.Longitude);

        var depot = DepotModel.Create(
            request.Name,
            request.Address,
            location,
            request.Capacity,
            request.WeightCapacity,
            request.ManagerId,
            request.ImageUrl
        );

        await _depotRepository.CreateAsync(depot, cancellationToken);
        var succeedCount = await _unitOfWork.SaveAsync();
        
        if (succeedCount < 1) 
            throw new CreateFailedException("Kho");

        var addedDepot = await _depotRepository.GetByNameAsync(request.Name, cancellationToken); 
        if (addedDepot is null) 
            throw new CreateFailedException("Kho");

        return new CreateDepotResponse
        {
            Id = addedDepot.Id,
            Name = addedDepot.Name,
            Address = addedDepot.Address,
            Latitude = addedDepot.Location?.Latitude,
            Longitude = addedDepot.Location?.Longitude,
            Capacity = addedDepot.Capacity,
            CurrentUtilization = addedDepot.CurrentUtilization,
            WeightCapacity = addedDepot.WeightCapacity,
            CurrentWeightUtilization = addedDepot.CurrentWeightUtilization,
            Status = addedDepot.Status.ToString(),
            DepotManagerId = addedDepot.CurrentManagerId,
            LastUpdatedAt = addedDepot.LastUpdatedAt
        };
    }
}
