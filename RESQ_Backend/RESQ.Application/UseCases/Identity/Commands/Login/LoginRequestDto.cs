namespace RESQ.Application.UseCases.Identity.Commands.Login
{
    public class LoginRequestDto
    {
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string Password { get; set; }
    }
}
