using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.ReviewRescuerApplication
{
    public class ReviewRescuerApplicationCommandHandler(
        IRescuerApplicationRepository rescuerApplicationRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReviewRescuerApplicationCommandHandler> logger
    ) : IRequestHandler<ReviewRescuerApplicationCommand, ReviewRescuerApplicationResponse>
    {
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<ReviewRescuerApplicationCommandHandler> _logger = logger;

        public async Task<ReviewRescuerApplicationResponse> Handle(ReviewRescuerApplicationCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Reviewing rescuer application: ApplicationId={ApplicationId}, IsApproved={IsApproved}, ReviewedBy={ReviewedBy}",
                request.ApplicationId, request.IsApproved, request.ReviewedBy);

            // 1. Get the application
            var application = await _rescuerApplicationRepository.GetByIdAsync(request.ApplicationId, cancellationToken);
            if (application is null)
            {
                throw new NotFoundException("Đơn đăng ký", request.ApplicationId);
            }

            // 2. Check if already reviewed
            if (application.Status != RescuerApplicationStatus.Pending)
            {
                throw new ConflictException($"Đơn đăng ký đã được xử lý với trạng thái: {application.Status}");
            }

            // 3. Update application status
            var newStatus = request.IsApproved ? RescuerApplicationStatus.Approved : RescuerApplicationStatus.Rejected;
            application.Status = newStatus;
            application.ReviewedAt = DateTime.UtcNow;
            application.ReviewedBy = request.ReviewedBy;
            application.AdminNote = request.AdminNote;

            await _rescuerApplicationRepository.UpdateAsync(application, cancellationToken);

            // 4. If approved, update user's IsEligibleRescuer flag
            if (request.IsApproved && application.UserId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(application.UserId.Value, cancellationToken);
                if (user is not null)
                {
                    // 1. Chuyển RoleId sang Rescuer (3) để được cấp quyền cứu hộ
                    user.RoleId = 3;
                    user.IsEligibleRescuer = true;
                    user.RescuerStep = 3; // Hoàn thành onboard/duyệt
                    user.ApprovedBy = request.ReviewedBy;
                    user.ApprovedAt = DateTime.UtcNow;
                    user.UpdatedAt = DateTime.UtcNow;

                    await _userRepository.UpdateAsync(user, cancellationToken);
                }
            }

            // 5. Save all changes
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("Rescuer application reviewed: ApplicationId={ApplicationId}, Status={Status}",
                request.ApplicationId, newStatus);

            return new ReviewRescuerApplicationResponse
            {
                ApplicationId = application.Id,
                UserId = application.UserId ?? Guid.Empty,
                Status = newStatus.ToString(),
                ReviewedAt = application.ReviewedAt ?? DateTime.UtcNow,
                ReviewedBy = request.ReviewedBy,
                Message = request.IsApproved
                    ? "Đơn đăng ký đã được phê duyệt thành công"
                    : "Đơn đăng ký đã bị từ chối"
            };
        }
    }
}
