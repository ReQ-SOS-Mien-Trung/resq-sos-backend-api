using RESQ.Domain.Models;
using RESQ.Domain.Entities;

namespace RESQ.Infrastructure.Mappers.Users
{
    public static class UsersMapper
    {
        public static UserModel ToDomain(this User db)
        {
            if (db == null) return null!;
            return new UserModel
            {
                Id = db.Id,
                RoleId = db.RoleId,
                FullName = db.FullName,
                Username = db.Username,
                Phone = db.Phone,
                Password = db.Password,
                CreatedAt = db.CreatedAt,
                UpdatedAt = db.UpdatedAt,
                RefreshToken = db.RefreshToken,
                RefreshTokenExpiry = db.RefreshTokenExpiry
            };
        }

        public static void UpdateDb(this User db, UserModel model)
        {
            db.RoleId = model.RoleId;
            db.FullName = model.FullName;
            db.Username = model.Username;
            db.Phone = model.Phone;
            db.Password = model.Password;
            db.UpdatedAt = model.UpdatedAt;
            db.RefreshToken = model.RefreshToken;
            db.RefreshTokenExpiry = model.RefreshTokenExpiry;
        }
    }
}
