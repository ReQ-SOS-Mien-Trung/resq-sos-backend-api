using RESQ.Domain.Entities.Personnel;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Mappers.Personnel;

public static class FreeRescuerMapper
{
    public static FreeRescuerModel ToModel(User user)
    {
        return new FreeRescuerModel
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            RescuerType = user.RescuerProfile?.RescuerType,
            Address = user.Address,
            Ward = user.Ward,
            Province = user.Province
        };
    }
}
