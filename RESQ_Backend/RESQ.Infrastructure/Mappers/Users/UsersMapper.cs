using RESQ.Domain.Entities;
using RESQ.Infrastructure.Entities;

namespace RESQ.Infrastructure.Mappers.Users
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
