using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetCurrentUser
{
    public class GetCurrentUserQueryHandler(
        IUserRepository userRepository,
        ILogger<GetCurrentUserQueryHandler> logger
    ) : IRequestHandler<GetCurrentUserQuery, GetCurrentUserResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ILogger<GetCurrentUserQueryHandler> _logger = logger;

        public async Task<GetCurrentUserResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting current user info for UserId={userId}", request.UserId);

            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("User not found for UserId={userId}", request.UserId);
                throw new NotFoundException($"Không tìm thấy người dùng với ID: {request.UserId}");
            }

            _logger.LogInformation("Successfully retrieved user info for UserId={userId}", request.UserId);

            return new GetCurrentUserResponse
            {
                Id = user.Id,
                RoleId = user.RoleId,
                FullName = user.FullName,
                Username = user.Username,
                Phone = user.Phone,
                RescuerType = user.RescuerType,
                Email = user.Email,
                IsEmailVerified = user.IsEmailVerified,
                IsOnboarded = user.IsOnboarded,
                IsEligibleRescuer = user.IsEligibleRescuer,
                Latitude = user.Latitude,
                Longitude = user.Longitude,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                ApprovedBy = user.ApprovedBy,
                ApprovedAt = user.ApprovedAt
            };
        }
    }
}
