namespace RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication
{
    public class SubmitRescuerApplicationResponse
    {
        public int ApplicationId { get; set; }
        public Guid UserId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime SubmittedAt { get; set; }
        public string Message { get; set; } = null!;
        public int DocumentCount { get; set; }
    }
}
