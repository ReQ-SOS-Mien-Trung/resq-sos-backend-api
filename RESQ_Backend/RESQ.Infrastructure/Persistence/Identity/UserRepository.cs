using NetTopologySuite.Geometries;
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

        public async Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<User>().GetByPropertyAsync(x => x.Id == user.Id);
            if (entity is not null)
            {
                // Update identity info
                entity.FullName = user.FullName;
                entity.FirstName = user.FirstName;
                entity.LastName = user.LastName;
                entity.Email = user.Email;
                entity.Phone = user.Phone;
                entity.Password = user.Password;
                entity.RescuerType = user.RescuerType;

                // Update status flags
                entity.IsEmailVerified = user.IsEmailVerified;
                entity.IsOnboarded = user.IsOnboarded;
                entity.IsEligibleRescuer = user.IsEligibleRescuer;

                // Update tokens
                entity.EmailVerificationToken = user.EmailVerificationToken;
                entity.EmailVerificationTokenExpiry = user.EmailVerificationTokenExpiry;
                entity.RefreshToken = user.RefreshToken;
                entity.RefreshTokenExpiry = user.RefreshTokenExpiry;

                // Update location and address
                entity.Address = user.Address;
                entity.Ward = user.Ward;
                entity.City = user.City;

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

                await _unitOfWork.GetRepository<User>().UpdateAsync(entity);
            }
        }
    }
}
