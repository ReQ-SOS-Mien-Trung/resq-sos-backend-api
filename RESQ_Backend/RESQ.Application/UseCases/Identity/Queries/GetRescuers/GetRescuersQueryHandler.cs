using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuers;

public class GetRescuersQueryHandler(
    IUserRepository userRepository,
    IRescuerScoreRepository rescuerScoreRepository,
    IRescuerScoreVisibilityConfigRepository rescuerScoreVisibilityConfigRepository)
    : IRequestHandler<GetRescuersQuery, PagedResult<GetRescuersItemResponse>>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRescuerScoreRepository _rescuerScoreRepository = rescuerScoreRepository;
    private readonly IRescuerScoreVisibilityConfigRepository _rescuerScoreVisibilityConfigRepository = rescuerScoreVisibilityConfigRepository;

    public async Task<PagedResult<GetRescuersItemResponse>> Handle(GetRescuersQuery request, CancellationToken cancellationToken)
    {
        var paged = await _userRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            roleId: 3,
            isBanned: request.IsBanned,
            search: request.Search,
            isEligible: true,
            rescuerType: request.RescuerType,
            cancellationToken: cancellationToken);

        var minimumEvaluationCount = (await _rescuerScoreVisibilityConfigRepository.GetAsync(cancellationToken))?.MinimumEvaluationCount ?? 0;
        var rescuerScores = await _rescuerScoreRepository.GetVisibleByRescuerIdsAsync(
            paged.Items.Select(x => x.Id),
            minimumEvaluationCount,
            cancellationToken);

        var items = paged.Items.Select(u => new GetRescuersItemResponse
        {
            Id = u.Id,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Username = u.Username,
            Phone = u.Phone,
            Email = u.Email,
            RescuerType = u.RescuerType?.ToString(),
            AvatarUrl = u.AvatarUrl,
            IsEmailVerified = u.IsEmailVerified,
            IsEligibleRescuer = u.IsEligibleRescuer,
            RescuerStep = u.RescuerStep,
            IsBanned = u.IsBanned,
            BannedAt = u.BannedAt,
            BanReason = u.BanReason,
            Address = u.Address,
            Ward = u.Ward,
            Province = u.Province,
            CreatedAt = u.CreatedAt,
            RescuerScore = RescuerScoreDto.FromModel(
                rescuerScores.TryGetValue(u.Id, out var rescuerScore) ? rescuerScore : null)
        }).ToList();

        return new PagedResult<GetRescuersItemResponse>(items, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }
}
