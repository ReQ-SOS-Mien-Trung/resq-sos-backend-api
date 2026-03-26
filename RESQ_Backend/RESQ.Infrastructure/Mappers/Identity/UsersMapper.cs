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
                Email = model.Email,
                IsEmailVerified = model.IsEmailVerified,
                AvatarUrl = model.AvatarUrl,
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
                IsBanned = model.IsBanned,
                BannedBy = model.BannedBy,
                BannedAt = model.BannedAt,
                BanReason = model.BanReason
            };

            // Only create RescuerProfile for rescuer role (RoleId = 3) or applicants (RescuerType != null)
            if (model.RoleId == 3 || model.RescuerType != null)
            {
                entity.RescuerProfile = new RescuerProfile
                {
                    UserId = model.Id,
                    RescuerType = model.RescuerType?.ToString(),
                    IsEligibleRescuer = model.IsEligibleRescuer,
                    Step = model.RescuerStep,
                    ApprovedBy = model.ApprovedBy,
                    ApprovedAt = model.ApprovedAt
                };
            }

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
                RescuerType = Enum.TryParse<RESQ.Domain.Enum.Identity.RescuerType>(entity.RescuerProfile?.RescuerType, ignoreCase: true, out var type) ? type : null,
                Email = entity.Email,
                IsEmailVerified = entity.IsEmailVerified,
                IsEligibleRescuer = entity.RescuerProfile?.IsEligibleRescuer ?? false,
                RescuerStep = entity.RescuerProfile?.Step ?? 0,
                AvatarUrl = entity.AvatarUrl,
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
                ApprovedBy = entity.RescuerProfile?.ApprovedBy,
                ApprovedAt = entity.RescuerProfile?.ApprovedAt,
                IsBanned = entity.IsBanned,
                BannedBy = entity.BannedBy,
                BannedAt = entity.BannedAt,
                BanReason = entity.BanReason
            };
        }
    }
}
