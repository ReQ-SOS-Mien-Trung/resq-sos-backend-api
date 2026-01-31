namespace RESQ.Application.UseCases.Users.Commands.ResendVerificationEmail
{
    public class ResendVerificationEmailResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
    }
}
