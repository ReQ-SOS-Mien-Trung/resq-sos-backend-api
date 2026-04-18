namespace RESQ.Application.UseCases.Identity.Commands.ReviewRescuerApplication
{
    public class ReviewRescuerApplicationResponse
    {
        public int ApplicationId { get; set; }
        public Guid UserId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime ReviewedAt { get; set; }
        public Guid ReviewedBy { get; set; }
        public string Message { get; set; } = null!;
    }
}
