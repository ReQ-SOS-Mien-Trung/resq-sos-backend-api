using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication
{
    public class SubmitRescuerApplicationCommandHandler(
        IUserRepository userRepository,
        IRescuerApplicationRepository rescuerApplicationRepository,
        RESQ.Application.Services.IAdminRealtimeHubService adminRealtimeHubService,
        IUnitOfWork unitOfWork,
        ILogger<SubmitRescuerApplicationCommandHandler> logger
    ) : IRequestHandler<SubmitRescuerApplicationCommand, SubmitRescuerApplicationResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly RESQ.Application.Services.IAdminRealtimeHubService _adminRealtimeHubService = adminRealtimeHubService;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<SubmitRescuerApplicationCommandHandler> _logger = logger;

        public async Task<SubmitRescuerApplicationResponse> Handle(SubmitRescuerApplicationCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing rescuer application for UserId={UserId}", request.UserId);

            // 1. Verify user exists
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                throw new NotFoundException("Người dùng", request.UserId);
            }

            // 2. Check if user already has a pending application
            var existingApplication = await _rescuerApplicationRepository.GetPendingByUserIdAsync(request.UserId, cancellationToken);
            if (existingApplication is not null)
            {
                throw new ConflictException("Bạn đã có đơn đăng ký đang chờ xét duyệt");
            }

            // 3. Update user profile info
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.Phone = request.Phone;
            user.Address = request.Address;
            user.Ward = request.Ward;
            user.Province = request.Province;
            user.Latitude = request.Latitude;
            user.Longitude = request.Longitude;
            if (!Enum.TryParse<RescuerType>(request.RescuerType, ignoreCase: true, out var parsedRescuerType))
            {
                throw new BadRequestException("Loại rescuer không hợp lệ");
            }
            user.RescuerType = parsedRescuerType;
            user.RescuerStep = 1;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);

            // 4. Create rescuer application
            var application = new RescuerApplicationModel
            {
                UserId = request.UserId,
                Status = RescuerApplicationStatus.Pending,
                SubmittedAt = DateTime.UtcNow,
                AdminNote = request.Note
            };

            var applicationId = await _rescuerApplicationRepository.CreateAsync(application, cancellationToken);

            // 5. Save all changes
            await _unitOfWork.SaveAsync();
            await _adminRealtimeHubService.PushRescuerApplicationUpdateAsync(
                new RESQ.Application.Common.Models.AdminRescuerApplicationRealtimeUpdate
                {
                    EntityId = applicationId,
                    EntityType = "RescuerApplication",
                    ApplicationId = applicationId,
                    UserId = request.UserId,
                    ReviewedBy = null,
                    Action = "Submitted",
                    Status = RescuerApplicationStatus.Pending.ToString(),
                    ChangedAt = DateTime.UtcNow
                },
                cancellationToken);

            _logger.LogInformation("Rescuer application submitted successfully: ApplicationId={ApplicationId}, UserId={UserId}",
                applicationId, request.UserId);

            return new SubmitRescuerApplicationResponse
            {
                ApplicationId = applicationId,
                UserId = request.UserId,
                Status = RescuerApplicationStatus.Pending.ToString(),
                SubmittedAt = application.SubmittedAt ?? DateTime.UtcNow,
                Message = "Đơn đăng ký đã được gửi thành công. Vui lòng đợi quản trị viên xét duyệt."
            };
        }
    }
}
