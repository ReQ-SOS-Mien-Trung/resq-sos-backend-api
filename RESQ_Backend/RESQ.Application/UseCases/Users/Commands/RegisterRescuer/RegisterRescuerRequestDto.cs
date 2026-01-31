namespace RESQ.Application.UseCases.Users.Commands.RegisterRescuer
{
    public class RegisterRescuerRequestDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? FullName { get; set; }
    }
}
