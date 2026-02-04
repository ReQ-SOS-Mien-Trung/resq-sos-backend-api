using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;

public class GetAssemblyPointByIdQueryHandler(
    IAssemblyPointRepository repository, 
    ILogger<GetAssemblyPointByIdQueryHandler> logger)
    : IRequestHandler<GetAssemblyPointByIdQuery, AssemblyPointDto>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly ILogger<GetAssemblyPointByIdQueryHandler> _logger = logger;

    public async Task<AssemblyPointDto> Handle(GetAssemblyPointByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetAssemblyPointByIdQuery for Id={Id}", request.Id);

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity == null)
        {
            throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.Id}");
        }

        return new AssemblyPointDto
        {
            Id = entity.Id,
            Code = entity.Code, // Added
            Name = entity.Name,
            Latitude = entity.Location?.Latitude,
            Longitude = entity.Location?.Longitude,
            CapacityTeams = entity.CapacityTeams,
            Status = entity.Status.ToString(),
            LastUpdatedAt = entity.UpdatedAt
        };
    }
}
