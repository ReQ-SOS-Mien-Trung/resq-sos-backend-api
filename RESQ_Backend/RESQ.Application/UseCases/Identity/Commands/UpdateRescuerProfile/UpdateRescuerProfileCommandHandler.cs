using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile
{
    public class UpdateRescuerProfileCommandHandler(
        IUserRepository userRepository,
        IRescuerApplicationRepository rescuerApplicationRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateRescuerProfileCommandHandler> logger
    ) : IRequestHandler<UpdateRescuerProfileCommand, UpdateRescuerProfileResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
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
            user.Phone = request.Phone;
            user.Address = request.Address;
            user.Ward = request.Ward;
            user.District = request.District;
            user.Province = request.Province;
            user.Latitude = request.Latitude;
            user.Longitude = request.Longitude;
            user.IsOnboarded = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);

            // Update documents if provided
            var documentDtos = new List<RescuerApplicationDocumentDto>();
            if (request.Documents is not null)
            {
                // Find the user's latest pending application to attach documents to
                var application = await _rescuerApplicationRepository.GetPendingByUserIdAsync(request.UserId, cancellationToken);
                if (application is null)
                {
                    // If no pending application, check for any existing application
                    application = await _rescuerApplicationRepository.GetByUserIdAsync(request.UserId, cancellationToken);
                }

                if (application is not null)
                {
                    var documentModels = request.Documents.Select(doc => new RescuerApplicationDocumentModel
                    {
                        ApplicationId = application.Id,
                        FileUrl = doc.FileUrl,
                        FileType = doc.FileType ?? "Other",
                        UploadedAt = DateTime.UtcNow
                    }).ToList();

                    // Replace old documents with new ones
                    await _rescuerApplicationRepository.ReplaceDocumentsAsync(application.Id, documentModels, cancellationToken);

                    _logger.LogInformation("Documents updated for ApplicationId={applicationId}, Count={count}", application.Id, documentModels.Count);

                    // Build response document list
                    documentDtos = documentModels.Select(d => new RescuerApplicationDocumentDto
                    {
                        FileUrl = d.FileUrl,
                        FileType = d.FileType,
                        UploadedAt = d.UploadedAt
                    }).ToList();
                }
                else
                {
                    _logger.LogWarning("No rescuer application found for UserId={userId}, documents not updated", request.UserId);
                }
            }

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
                Phone = user.Phone,
                Address = user.Address,
                Ward = user.Ward,
                District = user.District,
                Province = user.Province,
                Latitude = user.Latitude,
                Longitude = user.Longitude,
                IsOnboarded = user.IsOnboarded,
                UpdatedAt = user.UpdatedAt ?? DateTime.UtcNow,
                Message = "Cập nhật thông tin cá nhân thành công.",
                Documents = documentDtos
            };
        }
    }
}
