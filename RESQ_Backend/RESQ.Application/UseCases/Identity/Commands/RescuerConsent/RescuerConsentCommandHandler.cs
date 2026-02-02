using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.RescuerConsent
{
    public class RescuerConsentCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<RescuerConsentCommandHandler> logger
    ) : IRequestHandler<RescuerConsentCommand, RescuerConsentResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<RescuerConsentCommandHandler> _logger = logger;

        public async Task<RescuerConsentResponse> Handle(RescuerConsentCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling RescuerConsentCommand for UserId={userId}", request.UserId);

            // Get user by ID
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("Rescuer consent failed: User not found UserId={userId}", request.UserId);
                throw new NotFoundException("User", request.UserId);
            }

            // Check if all consent fields are true
            var isEligible = request.AgreeMedicalFitness 
                && request.AgreeLegalResponsibility 
                && request.AgreeTraining 
                && request.AgreeCodeOfConduct;

            if (!isEligible)
            {
                _logger.LogWarning("Rescuer consent failed: Not all consents agreed UserId={userId}", request.UserId);
                return new RescuerConsentResponse
                {
                    UserId = user.Id,
                    IsEligibleRescuer = false,
                    AcceptedAt = DateTime.UtcNow,
                    Message = "Bạn cần đồng ý tất cả các điều khoản để trở thành người cứu hộ."
                };
            }

            // Update user's IsEligibleRescuer to true
            user.IsEligibleRescuer = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            var succeedCount = await _unitOfWork.SaveAsync();

            if (succeedCount < 1)
            {
                throw new CreateFailedException("RescuerConsent");
            }

            _logger.LogInformation("Rescuer consent accepted successfully: UserId={userId}", user.Id);

            return new RescuerConsentResponse
            {
                UserId = user.Id,
                IsEligibleRescuer = true,
                AcceptedAt = DateTime.UtcNow,
                Message = "Bạn đã đồng ý tất cả các điều khoản và đủ điều kiện trở thành người cứu hộ."
            };
        }
    }
}
