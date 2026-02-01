namespace RESQ.Application.UseCases.Identity.Commands.VerifyEmail
{
    public class VerifyEmailRequestDto
    {
        public string Token { get; set; } = null!;
    }
}
