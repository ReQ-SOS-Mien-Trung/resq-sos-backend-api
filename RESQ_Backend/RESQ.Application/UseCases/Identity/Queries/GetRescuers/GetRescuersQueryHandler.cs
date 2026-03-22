using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuers;

public class GetRescuersQueryHandler(
    IUserRepository userRepository,
    IAbilityRepository abilityRepository,
    IRescuerApplicationRepository rescuerApplicationRepository)
    : IRequestHandler<GetRescuersQuery, PagedResult<GetRescuersItemResponse>>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IAbilityRepository _abilityRepository = abilityRepository;
    private readonly IRescuerApplicationRepository _rescuerApplicationRepository = rescuerApplicationRepository;

    public async Task<PagedResult<GetRescuersItemResponse>> Handle(GetRescuersQuery request, CancellationToken cancellationToken)
    {
        var paged = await _userRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            roleId: 3,
            isBanned: request.IsBanned,
            search: request.Search,
            isEligible: true,
            cancellationToken: cancellationToken);

        var items = new List<GetRescuersItemResponse>();
        foreach (var u in paged.Items)
        {
            var abilities = await _abilityRepository.GetUserAbilitiesAsync(u.Id, cancellationToken);
            var latestApp = await _rescuerApplicationRepository.GetLatestByUserIdAsync(u.Id, cancellationToken);

            items.Add(new GetRescuersItemResponse
            {
                Id = u.Id,
                RoleId = u.RoleId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Username = u.Username,
                Phone = u.Phone,
                Email = u.Email,
                RescuerType = u.RescuerType?.ToString(),
                AvatarUrl = u.AvatarUrl,
                IsEmailVerified = u.IsEmailVerified,
                IsOnboarded = u.IsOnboarded,
                IsEligibleRescuer = u.IsEligibleRescuer,
                IsBanned = u.IsBanned,
                BannedBy = u.BannedBy,
                BannedAt = u.BannedAt,
                BanReason = u.BanReason,
                Address = u.Address,
                Ward = u.Ward,
                Province = u.Province,
                Latitude = u.Latitude,
                Longitude = u.Longitude,
                ApprovedBy = u.ApprovedBy,
                ApprovedAt = u.ApprovedAt,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                Abilities = abilities.Select(a => new RescuerAbilityDto
                {
                    AbilityId = a.AbilityId,
                    Code = a.AbilityCode,
                    Description = a.AbilityDescription,
                    Level = a.Level,
                    SubgroupId = a.SubgroupId,
                    SubgroupCode = a.SubgroupCode,
                    SubgroupDescription = a.SubgroupDescription,
                    CategoryId = a.CategoryId,
                    CategoryCode = a.CategoryCode,
                    CategoryDescription = a.CategoryDescription
                }).ToList(),
                CertificateDocuments = latestApp?.Documents.Select(d => new RescuerCertificateDocumentDto
                {
                    Id = d.Id,
                    FileUrl = d.FileUrl,
                    FileTypeId = d.FileTypeId,
                    FileTypeCode = d.FileTypeCode,
                    FileTypeName = d.FileTypeName,
                    UploadedAt = d.UploadedAt
                }).ToList() ?? new()
            });
        }

        return new PagedResult<GetRescuersItemResponse>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }
}
