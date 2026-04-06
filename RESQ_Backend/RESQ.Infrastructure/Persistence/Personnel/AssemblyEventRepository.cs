using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.Queries.GetAssemblyEvents;
using RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;
using RESQ.Application.UseCases.Personnel.Queries.GetMyAssemblyEvents;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Persistence.Personnel;

public class AssemblyEventRepository(IUnitOfWork unitOfWork) : IAssemblyEventRepository
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> CreateEventAsync(int assemblyPointId, DateTime assemblyDate, Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        // Rule: chỉ 1 active event (Status != Completed) per AP
        var completedStatus = AssemblyEventStatus.Completed.ToString();
        var hasActive = await _unitOfWork.Set<AssemblyEvent>()
            .AnyAsync(e => e.AssemblyPointId == assemblyPointId && e.Status != completedStatus, cancellationToken);

        if (hasActive)
            throw new InvalidOperationException(
                "Điểm tập kết này đã có sự kiện tập trung đang hoạt động. Vui lòng hoàn tất sự kiện hiện tại trước.");

        var entity = new AssemblyEvent
        {
            AssemblyPointId = assemblyPointId,
            AssemblyDate = assemblyDate,
            Status = AssemblyEventStatus.Gathering.ToString(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.GetRepository<AssemblyEvent>().AddAsync(entity);
        await _unitOfWork.SaveAsync();
        return entity.Id;
    }

    public async Task AssignParticipantsAsync(int eventId, List<Guid> rescuerIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Set<AssemblyParticipant>()
            .Where(p => p.AssemblyEventId == eventId)
            .Select(p => p.RescuerId)
            .ToListAsync(cancellationToken);

        var toAdd = rescuerIds.Except(existing).ToList();

        var participants = toAdd.Select(id => new AssemblyParticipant
        {
            AssemblyEventId = eventId,
            RescuerId = id,
            Status = AssemblyParticipantStatus.Assigned.ToString(),
            IsCheckedIn = false
        });

        await _unitOfWork.GetRepository<AssemblyParticipant>().AddRangeAsync(participants);
    }

    public async Task<bool> CheckInAsync(int eventId, Guid rescuerId,
        CancellationToken cancellationToken = default)
    {
        var participant = await _unitOfWork.SetTracked<AssemblyParticipant>()
            .FirstOrDefaultAsync(p => p.AssemblyEventId == eventId && p.RescuerId == rescuerId, cancellationToken);

        if (participant == null) return false;
        if (participant.IsCheckedIn) return true; // idempotent

        participant.IsCheckedIn = true;
        participant.CheckInTime = DateTime.UtcNow;
        participant.Status = AssemblyParticipantStatus.CheckedIn.ToString();
        return true;
    }

    public async Task<bool> IsParticipantCheckedInAsync(int eventId, Guid rescuerId,
        CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Set<AssemblyParticipant>()
            .AnyAsync(p => p.AssemblyEventId == eventId && p.RescuerId == rescuerId && p.IsCheckedIn, cancellationToken);
    }

    public async Task<PagedResult<CheckedInRescuerDto>> GetCheckedInRescuersAsync(
        int eventId, int pageNumber, int pageSize,
        RESQ.Domain.Enum.Identity.RescuerType? rescuerType = null,
        string? abilitySubgroupCode = null,
        string? abilityCategoryCode = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        // Load event to get EventDateTime for IsEarly/IsLate computation
        var assemblyEvent = await _unitOfWork.Set<AssemblyEvent>()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        var eventDateTime = assemblyEvent?.AssemblyDate;
        var rescuerTypeStr = rescuerType?.ToString();

        // Pre-filter user IDs by ability subgroup/category if needed
        IQueryable<Guid>? abilityFilteredUserIds = null;
        if (abilitySubgroupCode != null || abilityCategoryCode != null)
        {
            var abilityQuery = _unitOfWork.Set<UserAbility>();
            if (abilitySubgroupCode != null)
                abilityQuery = abilityQuery.Where(ua =>
                    ua.Ability.AbilitySubgroup != null &&
                    ua.Ability.AbilitySubgroup.Code == abilitySubgroupCode);
            if (abilityCategoryCode != null)
                abilityQuery = abilityQuery.Where(ua =>
                    ua.Ability.AbilitySubgroup != null &&
                    ua.Ability.AbilitySubgroup.AbilityCategory != null &&
                    ua.Ability.AbilitySubgroup.AbilityCategory.Code == abilityCategoryCode);
            abilityFilteredUserIds = abilityQuery.Select(ua => ua.UserId).Distinct();
        }

        var joinedQuery = _unitOfWork.Set<AssemblyParticipant>()
            .Where(p => p.AssemblyEventId == eventId && p.IsCheckedIn)
            .Join(
                _unitOfWork.Set<User>().Include(u => u.RescuerProfile),
                p => p.RescuerId,
                u => u.Id,
                (p, u) => new { Participant = p, User = u }
            );

        // Apply optional filters
        if (rescuerTypeStr != null)
            joinedQuery = joinedQuery.Where(x => x.User.RescuerProfile != null &&
                                                 x.User.RescuerProfile.RescuerType == rescuerTypeStr);

        // Filter: search (OR across firstName, lastName, phone, email)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            joinedQuery = joinedQuery.Where(x =>
                (x.User.FirstName != null && x.User.FirstName.ToLower().Contains(term)) ||
                (x.User.LastName  != null && x.User.LastName.ToLower().Contains(term))  ||
                (x.User.Phone     != null && x.User.Phone.ToLower().Contains(term))     ||
                (x.User.Email     != null && x.User.Email.ToLower().Contains(term)));
        }

        if (abilityFilteredUserIds != null)
            joinedQuery = joinedQuery.Where(x => abilityFilteredUserIds.Contains(x.User.Id));

        var query = joinedQuery.OrderByDescending(x => x.Participant.CheckInTime);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = items.Select(x => x.User.Id).ToList();

        // Kiểm tra rescuer đã ở trong team active chưa
        var disbandedStatus = RescueTeamStatus.Disbanded.ToString();
        var acceptedStatus = TeamMemberStatus.Accepted.ToString();

        var usersInTeam = await _unitOfWork.Set<RescueTeamMember>()
            .Where(m => userIds.Contains(m.UserId)
                && m.Status == acceptedStatus
                && m.Team!.Status != disbandedStatus)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Lấy top abilities
        var allAbilities = await _unitOfWork.Set<UserAbility>()
            .Where(ua => userIds.Contains(ua.UserId))
            .Include(ua => ua.Ability)
                .ThenInclude(a => a.AbilitySubgroup)
            .ToListAsync(cancellationToken);

        var abilitiesDict = allAbilities
            .GroupBy(ua => ua.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(ua => ua.Level)
                    .Take(3)
                    .Select(ua => ua.Ability?.AbilitySubgroup?.Description ?? ua.Ability?.Description ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList()
            );

        var dtos = items.Select(x => new CheckedInRescuerDto
        {
            UserId = x.User.Id,
            FirstName = x.User.FirstName,
            LastName = x.User.LastName,
            Phone = x.User.Phone,
            Email = x.User.Email,
            AvatarUrl = x.User.AvatarUrl,
            RescuerType = x.User.RescuerProfile?.RescuerType,
            CheckedInAt = (x.Participant.CheckInTime ?? DateTime.MinValue).ToVietnamTime(),
            IsInTeam = usersInTeam.Contains(x.User.Id),
            IsEarly = eventDateTime.HasValue && x.Participant.CheckInTime.HasValue && x.Participant.CheckInTime.Value < eventDateTime.Value,
            IsLate = eventDateTime.HasValue && x.Participant.CheckInTime.HasValue && x.Participant.CheckInTime.Value > eventDateTime.Value,
            TopAbilities = abilitiesDict.TryGetValue(x.User.Id, out var abs) ? abs : new()
        }).ToList();

        return new PagedResult<CheckedInRescuerDto>(dtos, total, pageNumber, pageSize);
    }

    public async Task<PagedResult<AssemblyEventListItemDto>> GetEventsByAssemblyPointAsync(
        int assemblyPointId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Set<AssemblyEvent>()
            .Where(e => e.AssemblyPointId == assemblyPointId)
            .OrderByDescending(e => e.AssemblyDate)
            .ThenByDescending(e => e.CreatedAt);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AssemblyEventListItemDto
            {
                EventId = e.Id,
                AssemblyPointId = e.AssemblyPointId,
                AssemblyDate = e.AssemblyDate,
                Status = e.Status,
                CreatedBy = e.CreatedBy,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                ParticipantCount = e.Participants.Count,
                CheckedInCount = e.Participants.Count(p => p.IsCheckedIn)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AssemblyEventListItemDto>(items, total, pageNumber, pageSize);
    }

    public async Task<(int EventId, string Status)?> GetActiveEventByAssemblyPointAsync(
        int assemblyPointId, CancellationToken cancellationToken = default)
    {
        var completedStatus = AssemblyEventStatus.Completed.ToString();

        var evt = await _unitOfWork.Set<AssemblyEvent>()
            .Where(e => e.AssemblyPointId == assemblyPointId && e.Status != completedStatus)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (evt == null) return null;
        return (evt.Id, evt.Status);
    }

    public async Task UpdateEventStatusAsync(int eventId, string status,
        CancellationToken cancellationToken = default)
    {
        var evt = await _unitOfWork.SetTracked<AssemblyEvent>()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (evt != null)
        {
            evt.Status = status;
            evt.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task StartGatheringAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var evt = await _unitOfWork.SetTracked<AssemblyEvent>()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy sự kiện tập trung id = {eventId}");

        // Idempotent: nếu đã Gathering thì không làm gì thêm
        if (evt.Status == AssemblyEventStatus.Gathering.ToString())
            return;

        if (evt.Status != AssemblyEventStatus.Scheduled.ToString())
            throw new InvalidOperationException(
                $"Không thể bắt đầu tập trung. Trạng thái hiện tại: {evt.Status}. Yêu cầu: Scheduled hoặc Gathering.");

        evt.Status = AssemblyEventStatus.Gathering.ToString();
        evt.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<(int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate)?> GetEventByIdAsync(
        int eventId, CancellationToken cancellationToken = default)
    {
        var evt = await _unitOfWork.Set<AssemblyEvent>()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (evt == null) return null;
        return (evt.Id, evt.AssemblyPointId, evt.Status, evt.AssemblyDate);
    }

    public async Task<PagedResult<MyAssemblyEventDto>> GetAssemblyEventsForRescuerAsync(
        Guid rescuerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var baseQuery = _unitOfWork.Set<AssemblyParticipant>()
            .Where(p => p.RescuerId == rescuerId)
            .Join(_unitOfWork.Set<AssemblyEvent>(),
                p => p.AssemblyEventId,
                e => e.Id,
                (p, e) => new { Participant = p, Event = e });

        var total = await baseQuery.CountAsync(cancellationToken);

        // Load raw data into memory to avoid EF translation issues with spatial .Y/.X on nullable geometry
        var rawItems = await baseQuery
            .OrderByDescending(x => x.Event.AssemblyDate)
            .ThenByDescending(x => x.Event.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Participant.IsCheckedIn,
                x.Participant.CheckInTime,
                x.Event.Id,
                x.Event.AssemblyPointId,
                x.Event.AssemblyDate,
                x.Event.Status,
                x.Event.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (rawItems.Count == 0)
            return new PagedResult<MyAssemblyEventDto>([], total, pageNumber, pageSize);

        var apIds = rawItems.Select(x => x.AssemblyPointId).Distinct().ToList();
        var assemblyPoints = await _unitOfWork.Set<AssemblyPoint>()
            .Where(ap => apIds.Contains(ap.Id))
            .ToListAsync(cancellationToken);

        var apDict = assemblyPoints.ToDictionary(ap => ap.Id);

        var items = rawItems.Select(x =>
        {
            apDict.TryGetValue(x.AssemblyPointId, out var ap);
            return new MyAssemblyEventDto
            {
                EventId = x.Id,
                AssemblyPointId = x.AssemblyPointId,
                AssemblyPointName = ap?.Name ?? string.Empty,
                AssemblyPointCode = ap?.Code,
                AssemblyPointStatus = ap?.Status,
                AssemblyPointMaxCapacity = ap?.MaxCapacity,
                AssemblyPointImageUrl = ap?.ImageUrl,
                AssemblyPointLatitude = ap?.Location != null ? ap.Location.Y : null,
                AssemblyPointLongitude = ap?.Location != null ? ap.Location.X : null,
                AssemblyDate = x.AssemblyDate,
                EventStatus = x.Status,
                IsCheckedIn = x.IsCheckedIn,
                CheckInTime = x.CheckInTime,
                CreatedAt = x.CreatedAt
            };
        }).ToList();

        return new PagedResult<MyAssemblyEventDto>(items, total, pageNumber, pageSize);
    }
}
