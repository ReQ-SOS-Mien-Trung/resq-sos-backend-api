namespace RESQ.Application.UseCases.Users.Commands.Register
{
    public class RegisterRequestDto
    {
        public string Phone { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
