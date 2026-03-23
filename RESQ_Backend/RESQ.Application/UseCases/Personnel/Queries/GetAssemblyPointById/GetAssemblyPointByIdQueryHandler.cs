using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointById;

public class GetAssemblyPointByIdQueryHandler(
    IAssemblyPointRepository repository,
    IAssemblyEventRepository assemblyEventRepository,
    ILogger<GetAssemblyPointByIdQueryHandler> logger)
    : IRequestHandler<GetAssemblyPointByIdQuery, AssemblyPointDto>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IAssemblyEventRepository _assemblyEventRepository = assemblyEventRepository;
    private readonly ILogger<GetAssemblyPointByIdQueryHandler> _logger = logger;

    public async Task<AssemblyPointDto> Handle(GetAssemblyPointByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetAssemblyPointByIdQuery for Id={Id}", request.Id);

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (entity == null)
        {
            throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.Id}");
        }

        var teamsDict = await _repository.GetTeamsByAssemblyPointIdsAsync([entity.Id], cancellationToken);
        var teams = teamsDict.TryGetValue(entity.Id, out var t) ? t : [];

        var activeEvent = await _assemblyEventRepository.GetActiveEventByAssemblyPointAsync(entity.Id, cancellationToken);

        return new AssemblyPointDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Latitude = entity.Location?.Latitude,
            Longitude = entity.Location?.Longitude,
            MaxCapacity = entity.MaxCapacity,
            Status = entity.Status.ToString(),
            LastUpdatedAt = entity.UpdatedAt,
            HasActiveEvent = activeEvent != null,
            Teams = teams
        };
    }
}
