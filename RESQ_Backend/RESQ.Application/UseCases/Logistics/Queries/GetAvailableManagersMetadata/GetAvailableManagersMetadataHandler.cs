using MediatR;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAvailableManagersMetadata;

public class GetAvailableManagersMetadataHandler
    : IRequestHandler<GetAvailableManagersMetadataQuery, List<AvailableManagerDto>>
{
    private readonly IUserRepository _userRepository;

    public GetAvailableManagersMetadataHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task<List<AvailableManagerDto>> Handle(
        GetAvailableManagersMetadataQuery request, CancellationToken cancellationToken)
        => _userRepository.GetAvailableManagersAsync(cancellationToken);
}
