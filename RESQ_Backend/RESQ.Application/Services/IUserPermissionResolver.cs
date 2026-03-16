namespace RESQ.Application.Services;

public interface IUserPermissionResolver
{
    Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}