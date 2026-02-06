using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestCommandHandler(
    ISosRequestRepository sosRequestRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateSosRequestCommandHandler> logger
) : IRequestHandler<CreateSosRequestCommand, CreateSosRequestResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CreateSosRequestCommandHandler> _logger = logger;

    public async Task<CreateSosRequestResponse> Handle(CreateSosRequestCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CreateSosRequestCommand for UserId={userId}", request.UserId);

        var sosRequest = SosRequestModel.Create(request.UserId, request.Location, request.RawMessage);

        await _sosRequestRepository.CreateAsync(sosRequest, cancellationToken);
        var succeedCount = await _unitOfWork.SaveAsync();

        if (succeedCount < 1)
            throw new CreateFailedException("SosRequest");

        var created = (await _sosRequestRepository.GetByUserIdAsync(request.UserId, cancellationToken))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault(x => x.RawMessage == request.RawMessage);

        if (created is null)
            throw new CreateFailedException("SosRequest");

        return new CreateSosRequestResponse
        {
            Id = created.Id,
            UserId = created.UserId,
            RawMessage = created.RawMessage,
            Status = created.Status,
            Latitude = created.Location?.Latitude,
            Longitude = created.Location?.Longitude,
            CreatedAt = created.CreatedAt
        };
    }
}