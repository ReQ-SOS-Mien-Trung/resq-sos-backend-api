using RESQ.Domain.Models;
using RESQ.Application.UseCases.Users.Dtos;
using System.Threading.Tasks;

namespace RESQ.Application.Services
{
    public interface ITokenService
    {
        Task<AuthResultDto> GenerateTokensAsync(UserModel user);
        string GenerateAccessToken(UserModel user);
        string GenerateRefreshToken();
    }
}
