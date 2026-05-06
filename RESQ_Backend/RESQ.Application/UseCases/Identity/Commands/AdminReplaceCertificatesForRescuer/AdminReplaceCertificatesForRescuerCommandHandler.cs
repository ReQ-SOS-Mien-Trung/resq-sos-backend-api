using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.AdminReplaceCertificatesForRescuer
{
    public class AdminReplaceCertificatesForRescuerCommandHandler(
        IUserRepository userRepository,
        IRescuerApplicationRepository rescuerApplicationRepository,
        IUnitOfWork unitOfWork,
        ILogger<AdminReplaceCertificatesForRescuerCommandHandler> logger
    ) : IRequestHandler<AdminReplaceCertificatesForRescuerCommand, AdminReplaceCertificatesForRescuerResponse>
    {
        public async Task<AdminReplaceCertificatesForRescuerResponse> Handle(AdminReplaceCertificatesForRescuerCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Admin replacing certificates for UserId={UserId}", request.UserId);

            var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null) throw new NotFoundException("Người dùng", request.UserId);

            var application = await rescuerApplicationRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (application is null) throw new NotFoundException("Không tìm thấy đơn đăng ký rescuer cho người dùng này");

            var documentModels = request.Documents.Select(doc => new RescuerApplicationDocumentModel
            {
                ApplicationId = application.Id,
                FileUrl = doc.FileUrl,
                FileTypeId = doc.FileTypeId,
                UploadedAt = DateTime.UtcNow
            }).ToList();

            await rescuerApplicationRepository.ReplaceDocumentsAsync(application.Id, documentModels, cancellationToken);
            await unitOfWork.SaveAsync();

            return new AdminReplaceCertificatesForRescuerResponse
            {
                ApplicationId = application.Id,
                UserId = request.UserId,
                DocumentCount = documentModels.Count,
                Message = "Cập nhật chứng chỉ cho rescuer thành công.",
                Documents = documentModels.Select(d => new RescuerApplicationDocumentDto { FileUrl = d.FileUrl, FileTypeId = d.FileTypeId, UploadedAt = d.UploadedAt }).ToList()
            };
        }
    }
}
