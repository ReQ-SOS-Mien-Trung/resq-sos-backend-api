using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Entities.Personnel.ValueObjects;
using RESQ.Domain.Entities.Personnel.Exceptions;

namespace RESQ.Application.UseCases.Personnel.Commands.UpdateAssemblyPoint;

public class UpdateAssemblyPointCommandHandler(
    IAssemblyPointRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateAssemblyPointCommandHandler> logger)
    : IRequestHandler<UpdateAssemblyPointCommand>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateAssemblyPointCommandHandler> _logger = logger;

    public async Task Handle(UpdateAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling UpdateAssemblyPointCommand for Id={Id}", request.Id);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (assemblyPoint == null)
        {
            throw new NotFoundException("Không tìm thấy điểm tập kết");
        }

        // 1. Validate Duplicate Name (excluding current record)
        if (!string.Equals(assemblyPoint.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var existingName = await _repository.GetByNameAsync(request.Name, cancellationToken);
            if (existingName != null && existingName.Id != request.Id)
            {
                throw new AssemblyPointNameDuplicatedException(request.Name);
            }
        }

        // 2. Update Domain Model
        var location = new GeoLocation(request.Latitude, request.Longitude);

        // We pass the existing assemblyPoint.Code to preserve immutability
        assemblyPoint.UpdateDetails(
            assemblyPoint.Code, 
            request.Name,
            request.CapacityTeams,
            location
        );

        await _repository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Updated AssemblyPoint successfully: Id={Id}", request.Id);
    }
}
