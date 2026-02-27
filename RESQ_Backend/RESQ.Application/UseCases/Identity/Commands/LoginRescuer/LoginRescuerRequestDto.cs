namespace RESQ.Application.UseCases.Identity.Commands.LoginRescuer
{
    public class LoginRescuerRequestDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
