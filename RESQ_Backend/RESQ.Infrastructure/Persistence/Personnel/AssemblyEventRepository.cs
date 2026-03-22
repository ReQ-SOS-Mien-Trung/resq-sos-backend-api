using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.UseCases.Personnel.Queries.GetCheckedInRescuers;
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
        var hasActive = await _unitOfWork.GetRepository<AssemblyEvent>().AsQueryable()
            .AnyAsync(e => e.AssemblyPointId == assemblyPointId && e.Status != completedStatus, cancellationToken);

        if (hasActive)
            throw new InvalidOperationException(
                "Điểm tập kết này đã có sự kiện tập trung đang hoạt động. Vui lòng hoàn tất sự kiện hiện tại trước.");

        var entity = new AssemblyEvent
        {
            AssemblyPointId = assemblyPointId,
            AssemblyDate = assemblyDate,
            Status = AssemblyEventStatus.Scheduled.ToString(),
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
        var existing = await _unitOfWork.GetRepository<AssemblyParticipant>().AsQueryable()
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
        var participant = await _unitOfWork.GetRepository<AssemblyParticipant>().AsQueryable(tracked: true)
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
        return await _unitOfWork.GetRepository<AssemblyParticipant>().AsQueryable()
            .AnyAsync(p => p.AssemblyEventId == eventId && p.RescuerId == rescuerId && p.IsCheckedIn, cancellationToken);
    }

    public async Task<PagedResult<CheckedInRescuerDto>> GetCheckedInRescuersAsync(
        int eventId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        // Load event to get EventDateTime for IsEarly/IsLate computation
        var assemblyEvent = await _unitOfWork.GetRepository<AssemblyEvent>().AsQueryable()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        var eventDateTime = assemblyEvent?.AssemblyDate;

        var query = _unitOfWork.GetRepository<AssemblyParticipant>().AsQueryable()
            .Where(p => p.AssemblyEventId == eventId && p.IsCheckedIn)
            .Join(_unitOfWork.GetRepository<User>().AsQueryable(), p => p.RescuerId, u => u.Id, (p, u) => new { Participant = p, User = u })
            .OrderByDescending(x => x.Participant.CheckInTime);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = items.Select(x => x.User.Id).ToList();

        // Kiểm tra rescuer đã ở trong team active chưa
        var disbandedStatus = RescueTeamStatus.Disbanded.ToString();
        var acceptedStatus = TeamMemberStatus.Accepted.ToString();

        var usersInTeam = await _unitOfWork.GetRepository<RescueTeamMember>().AsQueryable()
            .Where(m => userIds.Contains(m.UserId)
                && m.Status == acceptedStatus
                && m.Team!.Status != disbandedStatus)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Lấy top abilities
        var allAbilities = await _unitOfWork.GetRepository<UserAbility>().AsQueryable()
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
            AvatarUrl = x.User.AvatarUrl,
            RescuerType = x.User.RescuerType,
            CheckedInAt = x.Participant.CheckInTime ?? DateTime.MinValue,
            IsInTeam = usersInTeam.Contains(x.User.Id),
            IsEarly = eventDateTime.HasValue && x.Participant.CheckInTime.HasValue && x.Participant.CheckInTime.Value < eventDateTime.Value,
            IsLate = eventDateTime.HasValue && x.Participant.CheckInTime.HasValue && x.Participant.CheckInTime.Value > eventDateTime.Value,
            TopAbilities = abilitiesDict.TryGetValue(x.User.Id, out var abs) ? abs : new()
        }).ToList();

        return new PagedResult<CheckedInRescuerDto>(dtos, total, pageNumber, pageSize);
    }

    public async Task<(int EventId, string Status)?> GetActiveEventByAssemblyPointAsync(
        int assemblyPointId, CancellationToken cancellationToken = default)
    {
        var completedStatus = AssemblyEventStatus.Completed.ToString();

        var evt = await _unitOfWork.GetRepository<AssemblyEvent>().AsQueryable()
            .Where(e => e.AssemblyPointId == assemblyPointId && e.Status != completedStatus)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (evt == null) return null;
        return (evt.Id, evt.Status);
    }

    public async Task UpdateEventStatusAsync(int eventId, string status,
        CancellationToken cancellationToken = default)
    {
        var evt = await _unitOfWork.GetRepository<AssemblyEvent>().AsQueryable(tracked: true)
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (evt != null)
        {
            evt.Status = status;
            evt.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task StartGatheringAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var evt = await _unitOfWork.GetRepository<AssemblyEvent>().AsQueryable(tracked: true)
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy sự kiện tập trung id = {eventId}");

        if (evt.Status != AssemblyEventStatus.Scheduled.ToString())
            throw new InvalidOperationException(
                $"Không thể bắt đầu tập trung. Trạng thái hiện tại: {evt.Status}. Yêu cầu: Scheduled.");

        evt.Status = AssemblyEventStatus.Gathering.ToString();
        evt.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<(int EventId, int AssemblyPointId, string Status, DateTime AssemblyDate)?> GetEventByIdAsync(
        int eventId, CancellationToken cancellationToken = default)
    {
        var evt = await _unitOfWork.GetRepository<AssemblyEvent>().AsQueryable()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (evt == null) return null;
        return (evt.Id, evt.AssemblyPointId, evt.Status, evt.AssemblyDate);
    }
}
