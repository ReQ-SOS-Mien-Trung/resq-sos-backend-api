namespace RESQ.Application.UseCases.Identity.Commands.LoginRescuer
{
    public class LoginRescuerResponse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public Guid UserId { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public int? RoleId { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsOnboarded { get; set; }
    }
}
