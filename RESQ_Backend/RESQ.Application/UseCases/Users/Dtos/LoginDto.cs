namespace RESQ.Application.UseCases.Users.Dtos
{
    public class LoginDto
    {
        // For victims login by phone, other roles by username
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string Password { get; set; } = null!;
    }
}

