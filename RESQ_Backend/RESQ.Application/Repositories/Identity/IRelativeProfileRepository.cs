using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Repositories.Identity
{
    public interface IRelativeProfileRepository
    {
        Task<List<UserRelativeProfileModel>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserRelativeProfileModel?> GetByIdAsync(Guid profileId, Guid userId, CancellationToken cancellationToken = default);
        Task<UserRelativeProfileModel> CreateAsync(UserRelativeProfileModel model, CancellationToken cancellationToken = default);
        Task<UserRelativeProfileModel> UpdateAsync(UserRelativeProfileModel model, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid profileId, Guid userId, CancellationToken cancellationToken = default);
        Task<(int Created, int Updated, int Deleted)> ReplaceAllForUserAsync(Guid userId, IList<UserRelativeProfileModel> profiles, CancellationToken cancellationToken = default);
    }
}
