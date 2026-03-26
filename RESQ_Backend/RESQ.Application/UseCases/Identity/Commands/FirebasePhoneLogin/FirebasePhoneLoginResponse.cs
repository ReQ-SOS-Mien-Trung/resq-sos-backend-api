namespace RESQ.Application.UseCases.Identity.Commands.FirebasePhoneLogin;

public class FirebasePhoneLoginResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public Guid UserId { get; set; }
    public string? Phone { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? RoleId { get; set; }
    public bool IsNewUser { get; set; }
}
