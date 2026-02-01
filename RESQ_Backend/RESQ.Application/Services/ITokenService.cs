using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Services
{
    public interface ITokenService
    {
        string GenerateAccessToken(UserModel user);
        string GenerateRefreshToken();
        bool ValidateRefreshToken(string refreshToken);
    }
}
