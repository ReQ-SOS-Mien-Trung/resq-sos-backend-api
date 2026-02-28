using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.ReplaceRescuerDocuments
{
    public class ReplaceRescuerDocumentsCommandHandler(
        IRescuerApplicationRepository rescuerApplicationRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReplaceRescuerDocumentsCommandHandler> logger
    ) : IRequestHandler<ReplaceRescuerDocumentsCommand, ReplaceRescuerDocumentsResponse>
    {
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<ReplaceRescuerDocumentsCommandHandler> _logger = logger;

        public async Task<ReplaceRescuerDocumentsResponse> Handle(ReplaceRescuerDocumentsCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Replacing documents for UserId={UserId}", request.UserId);

            // Find the user's latest pending application
            var application = await _rescuerApplicationRepository.GetPendingByUserIdAsync(request.UserId, cancellationToken);
            if (application is null)
            {
                // If no pending, check for any existing application
                application = await _rescuerApplicationRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            }

            if (application is null)
            {
                throw new NotFoundException("Không tìm thấy đơn đăng ký rescuer cho người dùng này");
            }

            var documentModels = request.Documents.Select(doc => new RescuerApplicationDocumentModel
            {
                ApplicationId = application.Id,
                FileUrl = doc.FileUrl,
                FileType = doc.FileType,
                UploadedAt = DateTime.UtcNow
            }).ToList();

            // Replace old documents with new ones
            await _rescuerApplicationRepository.ReplaceDocumentsAsync(application.Id, documentModels, cancellationToken);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("Documents replaced: ApplicationId={ApplicationId}, Count={Count}", application.Id, documentModels.Count);

            return new ReplaceRescuerDocumentsResponse
            {
                ApplicationId = application.Id,
                UserId = request.UserId,
                DocumentCount = documentModels.Count,
                Message = "Cập nhật tài liệu thành công.",
                Documents = documentModels.Select(d => new RescuerApplicationDocumentDto
                {
                    FileUrl = d.FileUrl,
                    FileType = d.FileType,
                    UploadedAt = d.UploadedAt
                }).ToList()
            };
        }
    }
}
