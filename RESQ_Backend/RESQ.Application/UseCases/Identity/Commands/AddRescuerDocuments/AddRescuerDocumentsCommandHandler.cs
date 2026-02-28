using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.AddRescuerDocuments
{
    public class AddRescuerDocumentsCommandHandler(
        IRescuerApplicationRepository rescuerApplicationRepository,
        IUnitOfWork unitOfWork,
        ILogger<AddRescuerDocumentsCommandHandler> logger
    ) : IRequestHandler<AddRescuerDocumentsCommand, AddRescuerDocumentsResponse>
    {
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<AddRescuerDocumentsCommandHandler> _logger = logger;

        public async Task<AddRescuerDocumentsResponse> Handle(AddRescuerDocumentsCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Adding documents for UserId={UserId}", request.UserId);

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
                FileTypeId = doc.FileTypeId,
                UploadedAt = DateTime.UtcNow
            }).ToList();

            await _rescuerApplicationRepository.AddDocumentsAsync(application.Id, documentModels, cancellationToken);
            await _unitOfWork.SaveAsync();

            _logger.LogInformation("Documents added: ApplicationId={ApplicationId}, Count={Count}", application.Id, documentModels.Count);

            return new AddRescuerDocumentsResponse
            {
                ApplicationId = application.Id,
                UserId = request.UserId,
                DocumentCount = documentModels.Count,
                Message = "Thêm tài liệu thành công.",
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
