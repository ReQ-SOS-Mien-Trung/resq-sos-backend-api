namespace RESQ.Application.UseCases.Users.Commands.RefreshToken
{
    public class RefreshTokenRequestDto
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }
}
