using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;

namespace RESQ.Application.UseCases.Identity.Commands.ReplaceRescuerDocuments
{
    public class ReplaceRescuerDocumentsResponse
    {
        public int ApplicationId { get; set; }
        public Guid UserId { get; set; }
        public int DocumentCount { get; set; }
        public string Message { get; set; } = null!;
        public List<RescuerApplicationDocumentDto> Documents { get; set; } = new();
    }
}
