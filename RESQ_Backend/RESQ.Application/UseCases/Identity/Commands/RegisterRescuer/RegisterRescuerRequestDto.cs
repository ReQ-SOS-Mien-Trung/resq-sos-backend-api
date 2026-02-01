namespace RESQ.Application.UseCases.Identity.Commands.RegisterRescuer
{
    public class RegisterRescuerRequestDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
