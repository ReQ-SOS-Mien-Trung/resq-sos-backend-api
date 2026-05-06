namespace RESQ.Application.UseCases.Identity.Commands.UpdateUserAbilities
{
    public class AbilityEntryDto
    {
        public int AbilityId { get; set; }
        public int? Level { get; set; }
    }

    public class UpdateUserAbilitiesRequestDto
    {
        public List<AbilityEntryDto> Abilities { get; set; } = [];
    }
}
