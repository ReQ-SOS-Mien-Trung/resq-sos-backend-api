using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Services
{
    public interface ITokenService
    {
        string GenerateAccessToken(UserModel user);
        string GenerateRefreshToken();
        bool ValidateRefreshToken(string refreshToken);
        
        // New method to manually extract UserId from a token string
        Guid? GetUserIdFromToken(string token);
    }
}
