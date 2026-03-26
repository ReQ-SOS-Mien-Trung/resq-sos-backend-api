using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Identity.Queries.GetCurrentUser
{
    public class GetCurrentUserQueryHandler(
        IUserRepository userRepository,
        IRescuerApplicationRepository rescuerApplicationRepository,
        IPermissionRepository permissionRepository,
        ILogger<GetCurrentUserQueryHandler> logger,
        IDepotInventoryRepository depotInventoryRepository,
        IDepotRepository depotRepository
    ) : IRequestHandler<GetCurrentUserQuery, GetCurrentUserResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
        private readonly IPermissionRepository _permissionRepository = permissionRepository;
        private readonly ILogger<GetCurrentUserQueryHandler> _logger = logger;
        private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
        private readonly IDepotRepository _depotRepository = depotRepository;

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

            int? depotId = null;
            string? depotName = null;
            var managedDepotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(user.Id, cancellationToken);
            if (managedDepotId.HasValue)
            {
                depotId = managedDepotId.Value;
                var depot = await _depotRepository.GetByIdAsync(managedDepotId.Value, cancellationToken);
                depotName = depot?.Name;
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
                DepotId = depotId,
                DepotName = depotName
            };
        }
    }
}
