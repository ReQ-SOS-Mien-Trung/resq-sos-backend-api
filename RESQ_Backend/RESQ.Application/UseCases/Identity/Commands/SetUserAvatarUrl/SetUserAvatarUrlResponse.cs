namespace RESQ.Application.UseCases.Identity.Commands.SetUserAvatarUrl
{
    public class SetUserAvatarUrlResponse
    {
        public Guid UserId { get; set; }
        public string? AvatarUrl { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
