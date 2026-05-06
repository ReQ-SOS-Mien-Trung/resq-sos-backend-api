using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateUserAbilities
{
    public class UpdateUserAbilitiesResponse
    {
        public Guid UserId { get; set; }
        public List<UserAbilityModel> Abilities { get; set; } = [];
        public string Message { get; set; } = null!;
    }
}
