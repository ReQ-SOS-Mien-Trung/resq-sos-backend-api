using RESQ.Domain.Entities;

namespace RESQ.Application.Services
{
    public interface ITokenService
    {
        string GenerateAccessToken(UserModel user);
        string GenerateRefreshToken();
        bool ValidateRefreshToken(string refreshToken);
    }
}
