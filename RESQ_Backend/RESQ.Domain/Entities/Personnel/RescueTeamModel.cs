using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Personnel;
using System.Text.RegularExpressions;

namespace RESQ.Domain.Entities.Personnel;

public class RescueTeamModel
{
    public int Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public RescueTeamType TeamType { get; private set; }
    public RescueTeamStatus Status { get; private set; }
    public int AssemblyPointId { get; private set; }
    public string? AssemblyPointName { get; private set; }
    public Guid ManagedBy { get; private set; }
    public int MaxMembers { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DisbandAt { get; private set; }

    private readonly List<RescueTeamMemberModel> _members = new();
    public IReadOnlyCollection<RescueTeamMemberModel> Members => _members.AsReadOnly();

    protected RescueTeamModel() { }

    /// <summary>
    /// Tạo đội cứu hộ từ danh sách rescuer đã check-in tại điểm tập kết. Status = Gathering.
    /// </summary>
    public static RescueTeamModel Create(string name, RescueTeamType type, int assemblyPointId, Guid managedBy, int maxMembers = 8)
    {
        if (maxMembers is < 6 or > 8)
            throw new RescueTeamBusinessRuleException("Số lượng thành viên tối đa phải từ 6 đến 8 người.");

        var cleanName = Regex.Replace(name, "[^a-zA-Z0-9]", "");
        var prefix = cleanName.Length >= 3 ? cleanName[..3] : cleanName.PadRight(3, 'X');
        var code = $"RT-{prefix.ToUpper()}-{DateTime.UtcNow:yyMMddHHmmss}";

        return new RescueTeamModel
        {
            Code = code,
            Name = name,
            TeamType = type,
            Status = RescueTeamStatus.Gathering,
            AssemblyPointId = assemblyPointId,
            ManagedBy = managedBy,
            MaxMembers = maxMembers,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void LoadAssemblyPointName(string name)
    {
        AssemblyPointName = name;
    }

    /// <summary>
    /// Thêm thành viên đã check-in tại điểm tập kết vào đội. Member ở trạng thái Accepted ngay.
    /// </summary>
    public void AddMember(Guid userId, bool isLeader, string rescuerType, string? roleInTeam)
    {
        if (Status != RescueTeamStatus.Gathering)
            throw new RescueTeamBusinessRuleException("Chỉ có thể thêm thành viên khi đội đang ở trạng thái Gathering.");

        if (_members.Count(m => m.Status != TeamMemberStatus.Removed) >= MaxMembers)
            throw new RescueTeamBusinessRuleException("Đội đã đạt giới hạn thành viên tối đa.");

        if (isLeader && _members.Any(m => m.IsLeader && m.Status != TeamMemberStatus.Removed))
            throw new RescueTeamBusinessRuleException("Đội đã có đội trưởng.");

        if (_members.Any(m => m.UserId == userId && m.Status != TeamMemberStatus.Removed))
            throw new RescueTeamBusinessRuleException("Thành viên này đã có trong đội.");

        _members.Add(RescueTeamMemberModel.Create(userId, isLeader, rescuerType, roleInTeam));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Xóa thành viên khỏi đội. Chỉ thực hiện được khi đội đang Gathering hoặc Unavailable.
    /// </summary>
    public void RemoveMember(Guid userId)
    {
        if (Status is not (RescueTeamStatus.Gathering or RescueTeamStatus.Unavailable))
            throw new RescueTeamBusinessRuleException("Chỉ có thể xóa thành viên khi đội đang ở trạng thái Gathering hoặc Unavailable.");

        var member = _members.FirstOrDefault(m => m.UserId == userId && m.Status != TeamMemberStatus.Removed)
            ?? throw new TeamMemberDomainException("Không tìm thấy thành viên trong đội.");

        member.Remove();
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Mission lifecycle ───────────────────────────────────────────

    public void AssignMission() => ChangeStatus(RescueTeamStatus.Available, RescueTeamStatus.Assigned);
    public void CancelMission() => ChangeStatus(RescueTeamStatus.Assigned, RescueTeamStatus.Available);
    public void StartMission() => ChangeStatus(RescueTeamStatus.Assigned, RescueTeamStatus.OnMission);
    public void FinishMission() => ChangeStatus(RescueTeamStatus.OnMission, RescueTeamStatus.Available);
    public void ReportIncident() => ChangeStatus(RescueTeamStatus.OnMission, RescueTeamStatus.Stuck);
    public void SetUnavailable() => ChangeStatus(RescueTeamStatus.Available, RescueTeamStatus.Unavailable);

    public void ResolveIncident(bool hasInjuredMember)
    {
        if (Status != RescueTeamStatus.Stuck)
            throw new InvalidTeamTransitionException(Status, hasInjuredMember ? RescueTeamStatus.Unavailable : RescueTeamStatus.Available);

        Status = hasInjuredMember ? RescueTeamStatus.Unavailable : RescueTeamStatus.Available;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Leader xác nhận đội đã tập hợp đủ và sẵn sàng nhận nhiệm vụ. Gathering → Available.
    /// </summary>
    public void SetAvailableByLeader(Guid leaderUserId)
    {
        if (Status != RescueTeamStatus.Gathering)
            throw new InvalidTeamTransitionException(Status, RescueTeamStatus.Available);

        var leader = _members.FirstOrDefault(m => m.IsLeader && m.UserId == leaderUserId && m.Status == TeamMemberStatus.Accepted)
            ?? throw new RescueTeamBusinessRuleException("Chỉ đội trưởng mới có thể đặt trạng thái Available.");

        Status = RescueTeamStatus.Available;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disband()
    {
        if (Status is not (RescueTeamStatus.Available or RescueTeamStatus.Unavailable or RescueTeamStatus.Gathering))
            throw new InvalidTeamTransitionException(Status, RescueTeamStatus.Disbanded);

        Status = RescueTeamStatus.Disbanded;
        DisbandAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gán đội vào một điểm tập kết. Đội không thể được gán nếu đã Disbanded.
    /// </summary>
    public void AssignToAssemblyPoint(int assemblyPointId)
    {
        if (Status == RescueTeamStatus.Disbanded)
            throw new RescueTeamBusinessRuleException("Không thể gán đội đã giải tán vào điểm tập kết.");

        AssemblyPointId = assemblyPointId;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Private ─────────────────────────────────────────────────────

    private void ChangeStatus(RescueTeamStatus expectedCurrent, RescueTeamStatus nextStatus)
    {
        if (Status != expectedCurrent)
            throw new InvalidTeamTransitionException(Status, nextStatus);

        Status = nextStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetId(int id) => Id = id;
    public void LoadMembers(IEnumerable<RescueTeamMemberModel> members) => _members.AddRange(members);
}
