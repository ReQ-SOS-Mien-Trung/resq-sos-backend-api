using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile
{
    public class UpdateRescuerProfileCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateRescuerProfileCommandHandler> logger
    ) : IRequestHandler<UpdateRescuerProfileCommand, UpdateRescuerProfileResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<UpdateRescuerProfileCommandHandler> _logger = logger;

        public async Task<UpdateRescuerProfileResponse> Handle(UpdateRescuerProfileCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling UpdateRescuerProfileCommand for UserId={userId}", request.UserId);

            // Get user by ID
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("Update rescuer profile failed: User not found UserId={userId}", request.UserId);
                throw new NotFoundException("User", request.UserId);
            }

            // Update user profile information
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.FullName = string.IsNullOrWhiteSpace(request.LastName) 
                ? request.FirstName 
                : $"{request.LastName} {request.FirstName}";
            user.Phone = request.Phone;
            user.Address = request.Address;
            user.Ward = request.Ward;
            user.City = request.City;
            user.Latitude = request.Latitude;
            user.Longitude = request.Longitude;
            user.IsOnboarded = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            var succeedCount = await _unitOfWork.SaveAsync();

            if (succeedCount < 1)
            {
                throw new CreateFailedException("UpdateRescuerProfile");
            }

            _logger.LogInformation("Rescuer profile updated successfully: UserId={userId}", user.Id);

            return new UpdateRescuerProfileResponse
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Phone = user.Phone,
                Address = user.Address,
                Ward = user.Ward,
                City = user.City,
                Latitude = user.Latitude,
                Longitude = user.Longitude,
                IsOnboarded = user.IsOnboarded,
                UpdatedAt = user.UpdatedAt ?? DateTime.UtcNow,
                Message = "Cập nhật thông tin cá nhân thành công."
            };
        }
    }
}
