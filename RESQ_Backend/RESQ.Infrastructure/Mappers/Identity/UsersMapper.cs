using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Mappers.Identity
{
    public static class UsersMapper
    {
        public static User ToEntity(UserModel model)
        {
            return new User
            {
                Id = model.Id,
                RoleId = model.RoleId,
                FullName = model.FullName,
                Username = model.Username,
                Email = model.Email,
                IsEmailVerified = model.IsEmailVerified,
                EmailVerificationToken = model.EmailVerificationToken,
                EmailVerificationTokenExpiry = model.EmailVerificationTokenExpiry,
                Phone = model.Phone,
                Password = model.Password,
                RefreshToken = model.RefreshToken,
                RefreshTokenExpiry = model.RefreshTokenExpiry,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt
            };
        }

        public static UserModel ToModel(User entity)
        {
            return new UserModel
            {
                Id = entity.Id,
                RoleId = entity.RoleId,
                FullName = entity.FullName,
                Username = entity.Username,
                Email = entity.Email,
                IsEmailVerified = entity.IsEmailVerified,
                EmailVerificationToken = entity.EmailVerificationToken,
                EmailVerificationTokenExpiry = entity.EmailVerificationTokenExpiry,
                Phone = entity.Phone,
                Password = entity.Password,
                RefreshToken = entity.RefreshToken,
                RefreshTokenExpiry = entity.RefreshTokenExpiry,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
