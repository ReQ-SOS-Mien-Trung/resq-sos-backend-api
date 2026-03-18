namespace RESQ.Application.UseCases.Identity.Commands.Login
{
    public class LoginResonse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int? RoleId { get; set; }
        public List<string> Permissions { get; set; } = [];
        public int? DepotId { get; set; }
        public string? DepotName { get; set; }
    }
}
