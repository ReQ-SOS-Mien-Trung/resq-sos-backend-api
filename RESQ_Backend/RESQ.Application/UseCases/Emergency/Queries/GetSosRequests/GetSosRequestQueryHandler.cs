using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;

public class GetSosRequestQueryHandler(
    ISosRequestRepository sosRequestRepository,
    ISosRequestCompanionRepository companionRepository,
    ISosRequestSnapshotBuilder snapshotBuilder,
    ILogger<GetSosRequestQueryHandler> logger
) : IRequestHandler<GetSosRequestQuery, GetSosRequestResponse>
{
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestCompanionRepository _companionRepository = companionRepository;
    private readonly ISosRequestSnapshotBuilder _snapshotBuilder = snapshotBuilder;
    private readonly ILogger<GetSosRequestQueryHandler> _logger = logger;

    public async Task<GetSosRequestResponse> Handle(GetSosRequestQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetSosRequestQuery Id={id} HasPrivilegedAccess={hasPrivilegedAccess}", request.Id, request.HasPrivilegedAccess);

        var sosRequest = await _sosRequestRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sosRequest is null)
            throw new NotFoundException("Không tìm thấy yêu cầu SOS");

        if (!request.HasPrivilegedAccess && sosRequest.UserId != request.RequestingUserId)
        {
            var isCompanion = await _companionRepository.IsCompanionAsync(request.Id, request.RequestingUserId, cancellationToken);
            if (!isCompanion)
                throw new ForbiddenException("Bạn không có quyền xem SOS request này");
        }

        return new GetSosRequestResponse
        {
            SosRequest = await _snapshotBuilder.BuildAsync(sosRequest, cancellationToken)
        };
    }
}
