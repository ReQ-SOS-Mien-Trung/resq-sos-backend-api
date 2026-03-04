using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.SetUserAvatarUrl
{
    public class SetUserAvatarUrlCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<SetUserAvatarUrlCommandHandler> logger
    ) : IRequestHandler<SetUserAvatarUrlCommand, SetUserAvatarUrlResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<SetUserAvatarUrlCommandHandler> _logger = logger;

        public async Task<SetUserAvatarUrlResponse> Handle(SetUserAvatarUrlCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Admin setting avatar for UserId={UserId}", request.UserId);

            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("Set avatar failed: User not found UserId={UserId}", request.UserId);
                throw new NotFoundException("User", request.UserId);
            }

            user.AvatarUrl = request.AvatarUrl;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            var savedCount = await _unitOfWork.SaveAsync();

            if (savedCount < 1)
            {
                throw new CreateFailedException("SetUserAvatarUrl");
            }

            _logger.LogInformation("Avatar updated successfully for UserId={UserId}", request.UserId);

            return new SetUserAvatarUrlResponse
            {
                UserId = user.Id,
                AvatarUrl = user.AvatarUrl,
                Message = "Cập nhật avatar thành công."
            };
        }
    }
}
