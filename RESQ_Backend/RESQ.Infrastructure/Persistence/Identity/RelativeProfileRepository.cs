using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Mappers.Identity;

namespace RESQ.Infrastructure.Persistence.Identity
{
    public class RelativeProfileRepository : IRelativeProfileRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        public RelativeProfileRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<UserRelativeProfileModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var repo = _unitOfWork.GetRepository<UserRelativeProfile>();
            var entities = await repo.GetAllByPropertyAsync(e => e.UserId == userId);
            return entities
                .OrderByDescending(e => e.UpdatedAt)
                .ThenBy(e => e.DisplayName)
                .Select(UserRelativeProfileMapper.ToModel)
                .ToList();
        }

        public async Task<UserRelativeProfileModel?> GetByIdAsync(Guid profileId, Guid userId, CancellationToken cancellationToken = default)
        {
            var repo = _unitOfWork.GetRepository<UserRelativeProfile>();
            var entity = await repo.GetByPropertyAsync(e => e.Id == profileId && e.UserId == userId, tracked: false);
            return entity == null ? null : UserRelativeProfileMapper.ToModel(entity);
        }

        public async Task<UserRelativeProfileModel> CreateAsync(UserRelativeProfileModel model, CancellationToken cancellationToken = default)
        {
            var repo = _unitOfWork.GetRepository<UserRelativeProfile>();
            var entity = UserRelativeProfileMapper.ToEntity(model);
            await repo.AddAsync(entity);
            await _unitOfWork.SaveAsync();
            return UserRelativeProfileMapper.ToModel(entity);
        }

        public async Task<UserRelativeProfileModel> UpdateAsync(UserRelativeProfileModel model, CancellationToken cancellationToken = default)
        {
            var repo = _unitOfWork.GetRepository<UserRelativeProfile>();
            var entity = await repo.GetByPropertyAsync(e => e.Id == model.Id && e.UserId == model.UserId, tracked: true);
            if (entity == null)
                throw new NotFoundException($"Relative profile {model.Id} not found for user {model.UserId}.");

            UserRelativeProfileMapper.UpdateEntity(entity, model);
            await repo.UpdateAsync(entity);
            await _unitOfWork.SaveAsync();
            return UserRelativeProfileMapper.ToModel(entity);
        }

        public async Task DeleteAsync(Guid profileId, Guid userId, CancellationToken cancellationToken = default)
        {
            var repo = _unitOfWork.GetRepository<UserRelativeProfile>();
            var entity = await repo.GetByPropertyAsync(e => e.Id == profileId && e.UserId == userId, tracked: true);
            if (entity != null)
            {
                await repo.DeleteAsyncById(profileId);
                await _unitOfWork.SaveAsync();
            }
        }

        public async Task<(int Created, int Updated, int Deleted)> ReplaceAllForUserAsync(
            Guid userId,
            IList<UserRelativeProfileModel> profiles,
            CancellationToken cancellationToken = default)
        {
            int created = 0, updated = 0, deleted = 0;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var repo = _unitOfWork.GetRepository<UserRelativeProfile>();

                // Load existing profiles for this user
                var existing = await repo.GetAllByPropertyAsync(e => e.UserId == userId);
                var existingById = existing.ToDictionary(e => e.Id);
                var existingIds = existing.Select(e => e.Id).ToHashSet();

                // Guard: check if any incoming id belongs to a different user
                var incomingIds = profiles.Select(p => p.Id).ToList();
                if (incomingIds.Count > 0)
                {
                    var conflictingEntities = await repo.GetAllByPropertyAsync(
                        e => incomingIds.Contains(e.Id) && e.UserId != userId);

                    if (conflictingEntities.Count > 0)
                    {
                        var conflictId = conflictingEntities[0].Id;
                        throw new ConflictException(
                            $"Profile id {conflictId} already belongs to another user.");
                    }
                }

                var incomingIdSet = incomingIds.ToHashSet();

                // Upsert
                foreach (var model in profiles)
                {
                    model.UserId = userId;
                    if (existingById.TryGetValue(model.Id, out var existingEntity))
                    {
                        UserRelativeProfileMapper.UpdateEntity(existingEntity, model);
                        await repo.UpdateAsync(existingEntity);
                        updated++;
                    }
                    else
                    {
                        var newEntity = UserRelativeProfileMapper.ToEntity(model);
                        await repo.AddAsync(newEntity);
                        created++;
                    }
                }

                // Delete records no longer in the payload
                foreach (var existingId in existingIds)
                {
                    if (!incomingIdSet.Contains(existingId))
                    {
                        await repo.DeleteAsyncById(existingId);
                        deleted++;
                    }
                }

                await _unitOfWork.SaveAsync();
            });

            return (created, updated, deleted);
        }
    }
}
