using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.System;

using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Identity.Queries.GetCurrentUser
{
    public class GetCurrentUserQueryHandler(
        IUserRepository userRepository,
        IRescuerApplicationRepository rescuerApplicationRepository,
        IPermissionRepository permissionRepository,
        ILogger<GetCurrentUserQueryHandler> logger,
        IDepotInventoryRepository depotInventoryRepository,
        IDepotRepository depotRepository,
        IRescuerScoreRepository rescuerScoreRepository,
        IRescuerScoreVisibilityConfigRepository rescuerScoreVisibilityConfigRepository,
        IManagerDepotAccessService managerDepotAccessService
    ) : IRequestHandler<GetCurrentUserQuery, GetCurrentUserResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly IPermissionRepository _permissionRepository = permissionRepository;
        private readonly ILogger<GetCurrentUserQueryHandler> _logger = logger;
        private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
        private readonly IDepotRepository _depotRepository = depotRepository;
        private readonly IRescuerScoreRepository _rescuerScoreRepository = rescuerScoreRepository;
        private readonly IRescuerScoreVisibilityConfigRepository _rescuerScoreVisibilityConfigRepository = rescuerScoreVisibilityConfigRepository;
        private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

        public async Task<GetCurrentUserResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting current user info for UserId={userId}", request.UserId);

            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("User not found for UserId={userId}", request.UserId);
                throw new NotFoundException($"Không tìm thấy người dùng với ID: {request.UserId}");
            }

            // Load rescuer application documents if the user has a rescuer application
            var documents = new List<RescuerDocumentDto>();
            var application = await _rescuerApplicationRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (application is not null)
            {
                var appDocuments = await _rescuerApplicationRepository.GetDocumentsByApplicationIdAsync(application.Id, cancellationToken);
                documents = appDocuments.Select(d => new RescuerDocumentDto
                {
                    Id = d.Id,
                    ApplicationId = d.ApplicationId,
                    FileUrl = d.FileUrl,
                    FileTypeId = d.FileTypeId,
                    FileTypeCode = d.FileTypeCode,
                    FileTypeName = d.FileTypeName,
                    UploadedAt = d.UploadedAt
                }).ToList();
            }

            _logger.LogInformation("Successfully retrieved user info for UserId={userId}", request.UserId);

            var permissions = await _permissionRepository.GetEffectivePermissionCodesAsync(user.Id, user.RoleId, cancellationToken);

            var managedDepotsData = await _managerDepotAccessService.GetManagedDepotsAsync(user.Id, cancellationToken);
            
            RescuerScoreDto? rescuerScoreDto = null;
            if (user.RescuerStep > 0)
            {
                var minimumEvaluationCount = (await _rescuerScoreVisibilityConfigRepository.GetAsync(cancellationToken))?.MinimumEvaluationCount ?? 0;
                var rescuerScore = await _rescuerScoreRepository.GetVisibleByRescuerIdAsync(request.UserId, minimumEvaluationCount, cancellationToken);
                if (rescuerScore is not null)
                {
                    rescuerScoreDto = new RescuerScoreDto
                    {
                        ResponseTimeScore = rescuerScore.ResponseTimeScore,
                        RescueEffectivenessScore = rescuerScore.RescueEffectivenessScore,
                        DecisionHandlingScore = rescuerScore.DecisionHandlingScore,
                        SafetyMedicalSkillScore = rescuerScore.SafetyMedicalSkillScore,
                        TeamworkCommunicationScore = rescuerScore.TeamworkCommunicationScore,
                        OverallAverageScore = rescuerScore.OverallAverageScore,
                        EvaluationCount = rescuerScore.EvaluationCount,
                        UpdatedAt = rescuerScore.UpdatedAt
                    };
                }
            }

            return new GetCurrentUserResponse
            {
                Id = user.Id,
                RoleId = user.RoleId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Username = user.Username,
                Phone = user.Phone,
                RescuerType = user.RescuerType?.ToString(),
                Email = user.Email,
                IsEmailVerified = user.IsEmailVerified,
                IsEligibleRescuer = user.IsEligibleRescuer,
                RescuerStep = user.RescuerStep,
                AvatarUrl = user.AvatarUrl,
                Latitude = user.Latitude,
                Longitude = user.Longitude,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                ApprovedBy = user.ApprovedBy,
                ApprovedAt = user.ApprovedAt,
                RescuerApplicationDocuments = documents,
                Permissions = permissions,
                ManagedDepots = managedDepotsData,
                RescuerScore = rescuerScoreDto
            };
        }
    }
}
