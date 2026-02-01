namespace RESQ.Application.UseCases.Users.Commands.GoogleLogin
{
    public class GoogleLoginResponse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public int? RoleId { get; set; }
        public bool IsNewUser { get; set; }
    }
}
