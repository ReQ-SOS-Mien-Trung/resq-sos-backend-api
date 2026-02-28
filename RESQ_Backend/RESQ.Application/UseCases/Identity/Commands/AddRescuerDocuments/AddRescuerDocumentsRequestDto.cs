using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

namespace RESQ.Application.UseCases.Identity.Commands.AddRescuerDocuments
{
    public class AddRescuerDocumentsRequestDto
    {
        /// <summary>
        /// Danh sách URL tài liệu chứng minh (CMND, bằng cấp, chứng chỉ...) - đã upload lên cloud
        /// </summary>
        public List<DocumentDto> Documents { get; set; } = new();
    }
}
