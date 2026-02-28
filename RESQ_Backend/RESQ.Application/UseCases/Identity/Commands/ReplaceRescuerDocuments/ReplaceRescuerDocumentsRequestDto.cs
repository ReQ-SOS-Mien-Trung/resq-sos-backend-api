using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

namespace RESQ.Application.UseCases.Identity.Commands.ReplaceRescuerDocuments
{
    public class ReplaceRescuerDocumentsRequestDto
    {
        /// <summary>
        /// Danh sách URL tài liệu mới (thay thế toàn bộ documents cũ)
        /// </summary>
        public List<DocumentDto> Documents { get; set; } = new();
    }
}
