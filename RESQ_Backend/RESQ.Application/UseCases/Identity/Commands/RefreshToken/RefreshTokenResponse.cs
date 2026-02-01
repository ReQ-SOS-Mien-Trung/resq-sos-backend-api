namespace RESQ.Application.UseCases.Identity.Commands.RefreshToken
{
    public class RefreshTokenResponse
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = "Bearer";
    }
}
