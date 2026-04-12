using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailable;

public class SetAssemblyPointUnavailableCommandHandler(
    IAssemblyPointRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<SetAssemblyPointUnavailableCommandHandler> logger)
    : IRequestHandler<SetAssemblyPointUnavailableCommand, SetAssemblyPointUnavailableResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<SetAssemblyPointUnavailableCommandHandler> _logger = logger;

    public async Task<SetAssemblyPointUnavailableResponse> Handle(SetAssemblyPointUnavailableCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SetAssemblyPointUnavailable: Id={Id}", request.Id);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy điểm tập kết");

        // Domain enforces: chỉ Active hoặc Overloaded → Unavailable
        assemblyPoint.ChangeStatus(AssemblyPointStatus.Unavailable);

        await _repository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("AssemblyPoint set to Unavailable: Id={Id}", request.Id);

        return new SetAssemblyPointUnavailableResponse
        {
            Id = assemblyPoint.Id,
            Status = assemblyPoint.Status.ToString(),
            Message = "Điểm tập kết đang trong trạng thái bảo trì."
        };
    }
}

