using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.SaveRescuerAbilities;

public class SaveRescuerAbilitiesCommandHandler(
    IAbilityRepository abilityRepository,
    IUnitOfWork unitOfWork,
    ILogger<SaveRescuerAbilitiesCommandHandler> logger)
    : IRequestHandler<SaveRescuerAbilitiesCommand, SaveRescuerAbilitiesResponse>
{
    private readonly IAbilityRepository _abilityRepository = abilityRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<SaveRescuerAbilitiesCommandHandler> _logger = logger;

    public async Task<SaveRescuerAbilitiesResponse> Handle(SaveRescuerAbilitiesCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {handler} for UserId={userId} with {count} abilities",
            nameof(SaveRescuerAbilitiesCommandHandler), request.UserId, request.Abilities.Count);

        // Validate that all ability IDs exist
        var allAbilities = await _abilityRepository.GetAllAbilitiesAsync(cancellationToken);
        var validAbilityIds = allAbilities.Select(a => a.Id).ToHashSet();

        var invalidIds = request.Abilities
            .Where(a => !validAbilityIds.Contains(a.AbilityId))
            .Select(a => a.AbilityId)
            .ToList();

        if (invalidIds.Count > 0)
        {
            throw new BadRequestException($"Các ability ID không hợp lệ: {string.Join(", ", invalidIds)}");
        }

        // Map to domain models
        var userAbilities = request.Abilities.Select(a => new UserAbilityModel
        {
            UserId = request.UserId,
            AbilityId = a.AbilityId,
            Level = a.Level
        }).ToList();

        // Save (replace all existing abilities for this user)
        await _abilityRepository.SaveUserAbilitiesAsync(request.UserId, userAbilities, cancellationToken);
        var savedCount = await _unitOfWork.SaveAsync();

        // Retrieve saved abilities to return
        var savedAbilities = await _abilityRepository.GetUserAbilitiesAsync(request.UserId, cancellationToken);

        var response = new SaveRescuerAbilitiesResponse
        {
            UserId = request.UserId,
            SavedCount = savedAbilities.Count,
            Abilities = savedAbilities.Select(ua => new SavedAbilityDto
            {
                AbilityId = ua.AbilityId,
                Code = ua.AbilityCode ?? string.Empty,
                Description = ua.AbilityDescription,
                Level = ua.Level
            }).ToList()
        };

        _logger.LogInformation("{handler} - saved {count} abilities for UserId={userId}",
            nameof(SaveRescuerAbilitiesCommandHandler), response.SavedCount, request.UserId);

        return response;
    }
}
