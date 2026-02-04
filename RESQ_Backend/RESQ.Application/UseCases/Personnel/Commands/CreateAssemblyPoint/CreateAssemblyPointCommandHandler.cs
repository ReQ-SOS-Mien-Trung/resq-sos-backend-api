using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Entities.Personnel.ValueObjects;
using RESQ.Domain.Entities.Personnel;
using RESQ.Domain.Entities.Personnel.Exceptions;
using System.Text.RegularExpressions;

namespace RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;

public class CreateAssemblyPointCommandHandler(
    IAssemblyPointRepository repository, 
    IUnitOfWork unitOfWork, 
    ILogger<CreateAssemblyPointCommandHandler> logger) 
    : IRequestHandler<CreateAssemblyPointCommand, CreateAssemblyPointResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreateAssemblyPointCommandHandler> _logger = logger;

    public async Task<CreateAssemblyPointResponse> Handle(CreateAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CreateAssemblyPointCommand for Name={name}", request.Name);

        // 1. Validate Duplicate Name
        var existingName = await _repository.GetByNameAsync(request.Name, cancellationToken);
        if (existingName != null)
        {
            throw new AssemblyPointNameDuplicatedException(request.Name);
        }

        // 2. Generate Code Automatically
        // Rule: AP-{first 3 letters of name}-{timestamp}
        string generatedCode = GenerateCode(request.Name);

        // 3. Create Domain Model using Personnel GeoLocation
        var location = new GeoLocation(request.Latitude, request.Longitude);

        var assemblyPoint = AssemblyPointModel.Create(
            generatedCode,
            request.Name,
            request.CapacityTeams,
            location
        );

        await _repository.CreateAsync(assemblyPoint, cancellationToken);
        var succeedCount = await _unitOfWork.SaveAsync();

        if (succeedCount < 1)
            throw new CreateFailedException("Điểm tập kết");

        // 4. Retrieve the created entity to get the generated ID
        // The local 'assemblyPoint' model does not have the ID because it's not tracked by EF.
        // We query by the unique Code we just generated.
        var createdEntity = await _repository.GetByCodeAsync(generatedCode, cancellationToken);

        if (createdEntity is null) 
            throw new CreateFailedException("Điểm tập kết");

        return new CreateAssemblyPointResponse
        {
            Id = createdEntity.Id,
            Code = createdEntity.Code,
            Name = createdEntity.Name,
            CapacityTeams = createdEntity.CapacityTeams,
            Status = createdEntity.Status.ToString()
        };
    }

    private static string GenerateCode(string name)
    {
        // Get first 3 letters, remove special chars/spaces, ensure uppercase
        var cleanName = Regex.Replace(name, "[^a-zA-Z0-9]", "");
        var prefixName = cleanName.Length >= 3 ? cleanName[..3] : cleanName.PadRight(3, 'X');
        var timestamp = DateTime.UtcNow.ToString("yyMMddHHmmss");
        
        return $"AP-{prefixName.ToUpper()}-{timestamp}";
    }
}
