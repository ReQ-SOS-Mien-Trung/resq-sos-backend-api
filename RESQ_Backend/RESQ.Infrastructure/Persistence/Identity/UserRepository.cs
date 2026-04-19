using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Mappers.Identity;
using RESQ.Domain.Enum.Identity;

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
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Id == id, includeProperties: "RescuerProfile");
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            var idList = ids as IReadOnlyList<Guid> ?? ids.ToList();
            var entities = await _unitOfWork.GetRepository<User>()
                .AsQueryable(tracked: false)
                .Where(u => idList.Contains(u.Id))
                .ToListAsync(cancellationToken);
            return entities.Select(UsersMapper.ToModel).ToList();
        }

        public async Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Email == email, includeProperties: "RescuerProfile");
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<UserModel?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Phone == phone, includeProperties: "RescuerProfile");
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<UserModel?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Username == username, includeProperties: "RescuerProfile");
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<UserModel?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.EmailVerificationToken == token, includeProperties: "RescuerProfile");
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task<UserModel?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.PasswordResetToken == token, includeProperties: "RescuerProfile");
            return entity is null ? null : UsersMapper.ToModel(entity);
        }

        public async Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Id == user.Id, includeProperties: "RescuerProfile");
            if (entity is not null)
            {
                // Update identity info
                entity.FirstName = user.FirstName;
                entity.LastName = user.LastName;
                entity.Email = user.Email;
                entity.Phone = user.Phone;
                entity.Password = user.Password;

                // Update status flags
                entity.IsEmailVerified = user.IsEmailVerified;
                entity.AvatarUrl = user.AvatarUrl;

                // Update rescuer profile - only for rescuer role (RoleId = 3) or users applying (RescuerType != null)
                if (user.RoleId == 3 || user.RescuerType != null)
                {
                    if (entity.RescuerProfile is null)
                    {
                        entity.RescuerProfile = new RescuerProfile { UserId = entity.Id };
                    }
                    entity.RescuerProfile.RescuerType = user.RescuerType?.ToString();
                    entity.RescuerProfile.IsEligibleRescuer = user.IsEligibleRescuer;
                    entity.RescuerProfile.Step = user.RescuerStep;
                }

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
                if (entity.RescuerProfile is not null)
                {
                    entity.RescuerProfile.ApprovedBy = user.ApprovedBy;
                    entity.RescuerProfile.ApprovedAt = user.ApprovedAt;
                }

                // Update ban info
                entity.IsBanned = user.IsBanned;
                entity.BannedBy = user.BannedBy;
                entity.BannedAt = user.BannedAt;
                entity.BanReason = user.BanReason;

                await _unitOfWork.GetRepository<User>().UpdateAsync(entity);
            }
        }

        public async Task<PagedResult<UserModel>> GetPagedAsync(int pageNumber, int pageSize, int? roleId = null, bool? isBanned = null, string? search = null, int? excludeRoleId = null, bool? isEligible = null, RescuerType? rescuerType = null, CancellationToken cancellationToken = default)
        {
            var rescuerTypeString = rescuerType?.ToString();
            var paged = await _unitOfWork.GetRepository<User>().GetPagedAsync(
                pageNumber,
                pageSize,
                filter: u =>
                    (roleId == null || u.RoleId == roleId) &&
                    (excludeRoleId == null || u.RoleId != excludeRoleId) &&
                    (isBanned == null || u.IsBanned == isBanned) &&
                    (isEligible == null || (u.RescuerProfile != null && u.RescuerProfile.IsEligibleRescuer == isEligible)) &&
                    (rescuerTypeString == null || (u.RescuerProfile != null && u.RescuerProfile.RescuerType == rescuerTypeString)) &&
                    (search == null || (u.Phone != null && u.Phone.ToLower().Contains(search.ToLower())) ||
                     (u.Email != null && u.Email.ToLower().Contains(search.ToLower())) ||
                     (u.FirstName != null && u.FirstName.ToLower().Contains(search.ToLower())) ||
                     (u.LastName != null && u.LastName.ToLower().Contains(search.ToLower()))),
                orderBy: q => q.OrderByDescending(u => u.CreatedAt),
                includeProperties: "RescuerProfile"
            );

            var models = paged.Items.Select(UsersMapper.ToModel).ToList();
            return new PagedResult<UserModel>(models, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }

        public async Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.GetRepository<User>()
                .AsQueryable(tracked: false)
                .Where(u => u.RoleId == 1 && !u.IsBanned)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Guid>> GetActiveCoordinatorUserIdsAsync(CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.GetRepository<User>()
                .AsQueryable(tracked: false)
                .Where(u => u.RoleId == 2 && !u.IsBanned)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.GetRepository<User>()
                .AsQueryable(tracked: false)
                .Where(u => u.RoleId == 4 && !u.IsBanned)
                .Where(u => excludeDepotId == null || !u.DepotManagers.Any(dm => dm.DepotId == excludeDepotId && dm.UnassignedAt == null))
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Select(u => new AvailableManagerDto
                {
                    Id       = u.Id,
                    FullName = (u.LastName + " " + u.FirstName).Trim(),
                    Email    = u.Email,
                    Phone    = u.Phone,
                    AssignedDepotsCount = u.DepotManagers.Count(dm => dm.UnassignedAt == null)
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<PagedResult<UserModel>> GetPagedForPermissionAsync(
            int pageNumber,
            int pageSize,
            int? roleId = null,
            string? name = null,
            string? phone = null,
            string? email = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToLower();
            var normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLower();

            var paged = await _unitOfWork.GetRepository<User>().GetPagedAsync(
                pageNumber,
                pageSize,
                filter: u =>
                    // Loại trừ user bị ban
                    !u.IsBanned &&
                    // Loại trừ rescuer (RoleId=3) chưa được kích hoạt - các role khác không cần profile
                    (u.RoleId != 3 || (u.RescuerProfile != null && u.RescuerProfile.IsEligibleRescuer)) &&
                    (roleId == null || u.RoleId == roleId) &&
                    (normalizedName == null ||
                     (u.FirstName != null && u.FirstName.ToLower().Contains(normalizedName)) ||
                     (u.LastName != null && u.LastName.ToLower().Contains(normalizedName)) ||
                     (((u.FirstName ?? "") + " " + (u.LastName ?? "")).ToLower().Contains(normalizedName)) ||
                     (((u.LastName ?? "") + " " + (u.FirstName ?? "")).ToLower().Contains(normalizedName))) &&
                    (normalizedPhone == null || (u.Phone != null && u.Phone.Contains(normalizedPhone))) &&
                    (normalizedEmail == null || (u.Email != null && u.Email.ToLower().Contains(normalizedEmail))),
                orderBy: q => q.OrderByDescending(u => u.CreatedAt),
                includeProperties: "RescuerProfile"
            );

            var models = paged.Items.Select(UsersMapper.ToModel).ToList();
            return new PagedResult<UserModel>(models, paged.TotalCount, paged.PageNumber, paged.PageSize);
        }
    }
}
