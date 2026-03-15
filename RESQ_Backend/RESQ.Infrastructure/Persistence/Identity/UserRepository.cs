using NetTopologySuite.Geometries;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Mappers.Identity;

namespace RESQ.Infrastructure.Persistence.Identity
{
    public class UserRepository(IUnitOfWork unitOfWork) : IUserRepository
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        public async Task CreateAsync(UserModel user, CancellationToken cancellationToken = default)
        {
            var entity = UsersMapper.ToEntity(user);
            await _unitOfWork.GetRepository<User>().AddAsync(entity);
        }

        public async Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Id == id);
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Email == email);
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<UserModel?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Phone == phone);
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<UserModel?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Username == username);
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<UserModel?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.EmailVerificationToken == token);
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<UserModel?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.PasswordResetToken == token);
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Id == user.Id);
            if (entity is not null)
            {
                // Update identity info
                entity.FirstName = user.FirstName;
                entity.LastName = user.LastName;
                entity.Email = user.Email;
                entity.Phone = user.Phone;
                entity.Password = user.Password;
                entity.RescuerType = user.RescuerType.ToString();

                // Update status flags
                entity.IsEmailVerified = user.IsEmailVerified;
                entity.IsOnboarded = user.IsOnboarded;
                entity.IsEligibleRescuer = user.IsEligibleRescuer;
                entity.AvatarUrl = user.AvatarUrl;

                // Update tokens
                entity.EmailVerificationToken = user.EmailVerificationToken;
                entity.EmailVerificationTokenExpiry = user.EmailVerificationTokenExpiry;
                entity.PasswordResetToken = user.PasswordResetToken;
                entity.PasswordResetTokenExpiry = user.PasswordResetTokenExpiry;
                entity.RefreshToken = user.RefreshToken;
                entity.RefreshTokenExpiry = user.RefreshTokenExpiry;

                // Update location and address
                entity.Address = user.Address;
                entity.Ward = user.Ward;
                entity.Province = user.Province;

                if (user.Latitude.HasValue && user.Longitude.HasValue)
                {
                    entity.Location = new Point(user.Longitude.Value, user.Latitude.Value) { SRID = 4326 };
                }
                else
                {
                    entity.Location = null;
                }

                // Update metadata
                entity.UpdatedAt = DateTime.UtcNow;
                entity.ApprovedBy = user.ApprovedBy;
                entity.ApprovedAt = user.ApprovedAt;

                // Update ban info
                entity.IsBanned = user.IsBanned;
                entity.BannedBy = user.BannedBy;
                entity.BannedAt = user.BannedAt;
                entity.BanReason = user.BanReason;

                await _unitOfWork.GetRepository<User>().UpdateAsync(entity);
            }
        }

        public async Task<PagedResult<UserModel>> GetPagedAsync(int pageNumber, int pageSize, int? roleId = null, bool? isBanned = null, string? search = null, int? excludeRoleId = null, bool? isEligible = null, CancellationToken cancellationToken = default)
        {
            var paged = await _unitOfWork.GetRepository<User>().GetPagedAsync(
                pageNumber,
                pageSize,
                filter: u =>
                    (roleId == null || u.RoleId == roleId) &&
                    (excludeRoleId == null || u.RoleId != excludeRoleId) &&
                    (isBanned == null || u.IsBanned == isBanned) &&
                    (isEligible == null || u.IsEligibleRescuer == isEligible) &&
                    (search == null || (u.Phone != null && u.Phone.Contains(search)) ||
                     (u.Email != null && u.Email.Contains(search)) ||
                     (u.FirstName != null && u.FirstName.Contains(search)) ||
                     (u.LastName != null && u.LastName.Contains(search))),
                orderBy: q => q.OrderByDescending(u => u.CreatedAt)
            );

            var models = paged.Items.Select(UsersMapper.ToModel).ToList();
            return new PagedResult<UserModel>(models, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<PagedResult<UserModel>> GetPagedForPermissionAsync(
            int pageNumber, int pageSize,
            int? roleId = null, string? search = null,
            CancellationToken cancellationToken = default)
        {
            var paged = await _unitOfWork.GetRepository<User>().GetPagedAsync(
                pageNumber,
                pageSize,
                filter: u =>
                    // Loại trừ user bị ban
                    !u.IsBanned &&
                    // Loại trừ volunteer chưa kích hoạt (cả 2 cờ đều false)
                    (u.IsEligibleRescuer || u.IsOnboarded) &&
                    (roleId == null || u.RoleId == roleId) &&
                    (search == null ||
                     (u.Phone != null && u.Phone.Contains(search)) ||
                     (u.Email != null && u.Email.Contains(search)) ||
                     (u.FirstName != null && u.FirstName.Contains(search)) ||
                     (u.LastName != null && u.LastName.Contains(search))),
                orderBy: q => q.OrderByDescending(u => u.CreatedAt)
            );

            var models = paged.Items.Select(UsersMapper.ToModel).ToList();
            return new PagedResult<UserModel>(models, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }
    }
}
