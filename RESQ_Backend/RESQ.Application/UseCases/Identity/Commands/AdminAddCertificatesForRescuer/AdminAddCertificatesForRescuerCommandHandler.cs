using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.AdminAddCertificatesForRescuer
{
    public class AdminAddCertificatesForRescuerCommandHandler(
        IUserRepository userRepository,
        IRescuerApplicationRepository rescuerApplicationRepository,
        IUnitOfWork unitOfWork,
        ILogger<AdminAddCertificatesForRescuerCommandHandler> logger
    ) : IRequestHandler<AdminAddCertificatesForRescuerCommand, AdminAddCertificatesForRescuerResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<AdminAddCertificatesForRescuerCommandHandler> _logger = logger;

        public async Task<AdminAddCertificatesForRescuerResponse> Handle(AdminAddCertificatesForRescuerCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Admin adding certificates for UserId={UserId}", request.UserId);

            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                throw new NotFoundException("Người dùng", request.UserId);
            }

            var application = await _rescuerApplicationRepository.GetByUserIdAsync(request.UserId, cancellationToken);

            if (application is null)
            {
                application = new RescuerApplicationModel
                {
                    UserId = request.UserId,
                    Status = RescuerApplicationStatus.Approved,
                    SubmittedAt = DateTime.UtcNow,
                    ReviewedAt = DateTime.UtcNow
                };

                var applicationId = await _rescuerApplicationRepository.CreateAsync(application, cancellationToken);
                application.Id = applicationId;

                _logger.LogInformation("Created new RescuerApplication (Approved) for core rescuer: ApplicationId={ApplicationId}, UserId={UserId}",
                    applicationId, request.UserId);
            }

            var documentModels = request.Documents.Select(doc => new RescuerApplicationDocumentModel
            {
                ApplicationId = application.Id,
                FileUrl = doc.FileUrl,
                FileTypeId = doc.FileTypeId,
                UploadedAt = DateTime.UtcNow
            }).ToList();

            await _rescuerApplicationRepository.AddDocumentsAsync(application.Id, documentModels, cancellationToken);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("Certificates added for rescuer: ApplicationId={ApplicationId}, UserId={UserId}, Count={Count}",
                application.Id, request.UserId, documentModels.Count);

            return new AdminAddCertificatesForRescuerResponse
            {
                ApplicationId = application.Id,
                UserId = request.UserId,
                DocumentCount = documentModels.Count,
                Message = "Thêm chứng chỉ cho rescuer thành công.",
                Documents = documentModels.Select(d => new RescuerApplicationDocumentDto
                {
                    FileUrl = d.FileUrl,
                    FileTypeId = d.FileTypeId,
                    UploadedAt = d.UploadedAt
                }).ToList()
            };
        }
    }
}
