namespace RESQ.Application.UseCases.Users.Commands.VerifyEmail
{
    public class VerifyEmailResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public string? Email { get; set; }
    }
}
