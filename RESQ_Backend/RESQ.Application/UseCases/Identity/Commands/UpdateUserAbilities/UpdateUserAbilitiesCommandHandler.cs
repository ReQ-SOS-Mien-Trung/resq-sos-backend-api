using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateUserAbilities
{
    public class UpdateUserAbilitiesCommandHandler(
        IUserRepository userRepository,
        IAbilityRepository abilityRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateUserAbilitiesCommandHandler> logger
    ) : IRequestHandler<UpdateUserAbilitiesCommand, UpdateUserAbilitiesResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IAbilityRepository _abilityRepository = abilityRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<UpdateUserAbilitiesCommandHandler> _logger = logger;

        public async Task<UpdateUserAbilitiesResponse> Handle(UpdateUserAbilitiesCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling UpdateUserAbilitiesCommand CallerUserId={callerId} TargetUserId={targetId}",
                request.CallerUserId, request.TargetUserId);

            var caller = await _userRepository.GetByIdAsync(request.CallerUserId, cancellationToken);
            if (caller is null)
            {
                _logger.LogWarning("Update abilities failed: Caller not found CallerUserId={callerId}", request.CallerUserId);
                throw new NotFoundException("User", request.CallerUserId);
            }

            var target = await _userRepository.GetByIdAsync(request.TargetUserId, cancellationToken);
            if (target is null)
            {
                _logger.LogWarning("Update abilities failed: Target user not found TargetUserId={targetId}", request.TargetUserId);
                throw new NotFoundException("User", request.TargetUserId);
            }

            // Authorization: non-admin users can only update their own abilities
            if (caller.RoleId != RoleConstants.Admin && request.CallerUserId != request.TargetUserId)
            {
                _logger.LogWarning("Update abilities forbidden: CallerUserId={callerId} cannot update TargetUserId={targetId}",
                    request.CallerUserId, request.TargetUserId);
                throw new ForbiddenException();
            }

            var abilityModels = request.Abilities.Select(a => new UserAbilityModel
            {
                UserId = request.TargetUserId,
                AbilityId = a.AbilityId,
                Level = a.Level
            }).ToList();

            await _abilityRepository.SaveUserAbilitiesAsync(request.TargetUserId, abilityModels, cancellationToken);

            await _unitOfWork.SaveAsync();

            var savedAbilities = await _abilityRepository.GetUserAbilitiesAsync(request.TargetUserId, cancellationToken);

            _logger.LogInformation("User abilities updated successfully: TargetUserId={targetId} Count={count}",
                request.TargetUserId, savedAbilities.Count);

            return new UpdateUserAbilitiesResponse
            {
                UserId = request.TargetUserId,
                Abilities = savedAbilities,
                Message = "Cập nhật abilities thành công."
            };
        }
    }
}
