using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication
{
    public class SubmitRescuerApplicationCommandHandler(
        IUserRepository userRepository,
        IRescuerApplicationRepository rescuerApplicationRepository,
        IUnitOfWork unitOfWork,
        ILogger<SubmitRescuerApplicationCommandHandler> logger
    ) : IRequestHandler<SubmitRescuerApplicationCommand, SubmitRescuerApplicationResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
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
            user.FullName = request.FullName;
            user.Phone = request.Phone;
            user.Address = request.Address;
            user.Ward = request.Ward;
            user.City = request.City;
            user.RescuerType = request.RescuerType;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);

            // 4. Create rescuer application
            var application = new RescuerApplicationModel
            {
                UserId = request.UserId,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow,
                AdminNote = request.Note
            };

            var applicationId = await _rescuerApplicationRepository.CreateAsync(application, cancellationToken);

            // 5. Save document URLs if provided
            var documentCount = 0;
            if (request.Documents is not null && request.Documents.Count > 0)
            {
                var documentModels = request.Documents.Select(doc => new RescuerApplicationDocumentModel
                {
                    ApplicationId = applicationId,
                    FileUrl = doc.FileUrl,
                    FileType = doc.FileType ?? "Other",
                    UploadedAt = DateTime.UtcNow
                }).ToList();

                await _rescuerApplicationRepository.AddDocumentsAsync(applicationId, documentModels, cancellationToken);
                documentCount = documentModels.Count;
            }

            // 6. Save all changes
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("Rescuer application submitted successfully: ApplicationId={ApplicationId}, UserId={UserId}, Documents={DocumentCount}",
                applicationId, request.UserId, documentCount);

            return new SubmitRescuerApplicationResponse
            {
                ApplicationId = applicationId,
                UserId = request.UserId,
                Status = "Pending",
                SubmittedAt = application.SubmittedAt ?? DateTime.UtcNow,
                Message = "Đơn đăng ký đã được gửi thành công. Vui lòng đợi quản trị viên xét duyệt.",
                DocumentCount = documentCount
            };
        }
    }
}
