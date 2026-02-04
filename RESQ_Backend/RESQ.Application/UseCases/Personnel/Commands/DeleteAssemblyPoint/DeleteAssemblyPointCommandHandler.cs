using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.DeleteAssemblyPoint;

public class DeleteAssemblyPointCommandHandler(
    IAssemblyPointRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<DeleteAssemblyPointCommandHandler> logger)
    : IRequestHandler<DeleteAssemblyPointCommand>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<DeleteAssemblyPointCommandHandler> _logger = logger;

    public async Task Handle(DeleteAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling DeleteAssemblyPointCommand for Id={Id}", request.Id);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (assemblyPoint == null)
        {
            throw new NotFoundException("Không tìm thấy điểm tập kết");
        }

        await _repository.DeleteAsync(request.Id, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Deleted AssemblyPoint successfully: Id={Id}", request.Id);
    }
}