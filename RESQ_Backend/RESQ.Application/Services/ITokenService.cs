using RESQ.Application.UseCases.Users.Dtos;
using RESQ.Domain.Entities.Users;

namespace RESQ.Application.Services
{
    public interface ITokenService
    {
        Task<AuthResultDto> GenerateTokensAsync(UserModel user);
        string GenerateAccessToken(UserModel user);
        string GenerateRefreshToken();
    }
}
