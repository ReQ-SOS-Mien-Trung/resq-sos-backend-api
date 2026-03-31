using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetUserById;

public class GetUserByIdQueryHandler(
    IUserRepository userRepository,
    IAbilityRepository abilityRepository,
    IRescuerApplicationRepository rescuerApplicationRepository,
    IRescuerScoreRepository rescuerScoreRepository
) : IRequestHandler<GetUserByIdQuery, GetUserByIdResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IAbilityRepository _abilityRepository = abilityRepository;
    private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;
    private readonly IRescuerScoreRepository _rescuerScoreRepository = rescuerScoreRepository;

    public async Task<GetUserByIdResponse> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy user với ID {request.UserId}");

        var userAbilities = await _abilityRepository.GetUserAbilitiesAsync(user.Id, cancellationToken);

        var documents = new List<RescuerDocumentDto>();
        var application = await _rescuerApplicationRepository.GetByUserIdAsync(user.Id, cancellationToken);
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

        var rescuerScore = await _rescuerScoreRepository.GetByRescuerIdAsync(user.Id, cancellationToken);

        return new GetUserByIdResponse
        {
            Id = user.Id,
            RoleId = user.RoleId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            Phone = user.Phone,
            Email = user.Email,
            RescuerType = user.RescuerType?.ToString(),
            AvatarUrl = user.AvatarUrl,
            IsEmailVerified = user.IsEmailVerified,
            IsEligibleRescuer = user.IsEligibleRescuer,
            RescuerStep = user.RescuerStep,
            IsBanned = user.IsBanned,
            BannedBy = user.BannedBy,
            BannedAt = user.BannedAt,
            BanReason = user.BanReason,
            Address = user.Address,
            Ward = user.Ward,
            Province = user.Province,
            Latitude = user.Latitude,
            Longitude = user.Longitude,
            ApprovedBy = user.ApprovedBy,
            ApprovedAt = user.ApprovedAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            RescuerScore = rescuerScore is null ? null : RescuerScoreDto.FromModel(rescuerScore),
            Abilities = userAbilities.Select(a => new UserAbilityDto
            {
                AbilityId = a.AbilityId,
                Code = a.AbilityCode,
                Description = a.AbilityDescription,
                Level = a.Level,
                CategoryCode = a.CategoryCode
            }).ToList(),
            RescuerApplicationDocuments = documents
        };
    }
}
