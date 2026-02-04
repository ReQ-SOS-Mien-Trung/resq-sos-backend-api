using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.ChangeAssemblyPointStatus;

public class ChangeAssemblyPointStatusCommandHandler(
    IAssemblyPointRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<ChangeAssemblyPointStatusCommandHandler> logger)
    : IRequestHandler<ChangeAssemblyPointStatusCommand, ChangeAssemblyPointStatusResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ChangeAssemblyPointStatusCommandHandler> _logger = logger;

    public async Task<ChangeAssemblyPointStatusResponse> Handle(ChangeAssemblyPointStatusCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling ChangeAssemblyPointStatusCommand for Id={Id} to Status={Status}", request.Id, request.Status);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (assemblyPoint == null)
        {
            throw new NotFoundException("Không tìm thấy điểm tập kết");
        }

        // Apply change (Domain Layer enforces invariants)
        assemblyPoint.ChangeStatus(request.Status);

        await _repository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("AssemblyPoint status updated successfully: Id={Id}", request.Id);

        return new ChangeAssemblyPointStatusResponse
        {
            Id = assemblyPoint.Id,
            Status = assemblyPoint.Status.ToString(),
            Message = "Cập nhật trạng thái điểm tập kết thành công."
        };
    }
}
