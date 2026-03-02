using NetTopologySuite.Geometries;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Mappers.Identity
{
    public static class UsersMapper
    {
        public static User ToEntity(UserModel model)
        {
            var entity = new User
            {
                Id = model.Id,
                RoleId = model.RoleId,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Username = model.Username,
                Phone = model.Phone,
                Password = model.Password,
                RescuerType = model.RescuerType,
                Email = model.Email,
                IsEmailVerified = model.IsEmailVerified,
                IsOnboarded = model.IsOnboarded,
                IsEligibleRescuer = model.IsEligibleRescuer,
                EmailVerificationToken = model.EmailVerificationToken,
                EmailVerificationTokenExpiry = model.EmailVerificationTokenExpiry,
                PasswordResetToken = model.PasswordResetToken,
                PasswordResetTokenExpiry = model.PasswordResetTokenExpiry,
                RefreshToken = model.RefreshToken,
                RefreshTokenExpiry = model.RefreshTokenExpiry,
                Address = model.Address,
                Ward = model.Ward,
                Province = model.Province,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt,
                ApprovedBy = model.ApprovedBy,
                ApprovedAt = model.ApprovedAt
            };

            // Convert latitude/longitude to Point
            if (model.Latitude.HasValue && model.Longitude.HasValue)
            {
                entity.Location = new Point(model.Longitude.Value, model.Latitude.Value) { SRID = 4326 };
            }

            return entity;
        }

        public static UserModel ToModel(User entity)
        {
            return new UserModel
            {
                Id = entity.Id,
                RoleId = entity.RoleId,
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                Username = entity.Username,
                Phone = entity.Phone,
                Password = entity.Password,
                RescuerType = entity.RescuerType,
                Email = entity.Email,
                IsEmailVerified = entity.IsEmailVerified,
                IsOnboarded = entity.IsOnboarded,
                IsEligibleRescuer = entity.IsEligibleRescuer,
                EmailVerificationToken = entity.EmailVerificationToken,
                EmailVerificationTokenExpiry = entity.EmailVerificationTokenExpiry,
                PasswordResetToken = entity.PasswordResetToken,
                PasswordResetTokenExpiry = entity.PasswordResetTokenExpiry,
                RefreshToken = entity.RefreshToken,
                RefreshTokenExpiry = entity.RefreshTokenExpiry,
                Address = entity.Address,
                Ward = entity.Ward,
                Province = entity.Province,
                Latitude = entity.Location?.Y,
                Longitude = entity.Location?.X,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ApprovedBy = entity.ApprovedBy,
                ApprovedAt = entity.ApprovedAt
            };
        }
    }
}
