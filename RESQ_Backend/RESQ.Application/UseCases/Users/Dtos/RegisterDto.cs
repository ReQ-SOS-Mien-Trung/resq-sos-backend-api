namespace RESQ.Application.UseCases.Users.Dtos
{
    public class RegisterDto
    {
        // Victim registers with phone and 6-digit PIN only
        public string? Username { get; set; }
        public string Password { get; set; } = null!;
        public string? FullName { get; set; }
        public string Phone { get; set; } = null!;
    }
}
