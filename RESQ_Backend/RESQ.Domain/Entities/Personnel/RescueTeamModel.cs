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
    public string? AssemblyPointName { get; private set; } // Added for display hydration
    public Guid ManagedBy { get; private set; }
    public int MaxMembers { get; private set; }
    public DateTime? AssemblyDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DisbandAt { get; private set; }

    private readonly List<RescueTeamMemberModel> _members = new();
    public IReadOnlyCollection<RescueTeamMemberModel> Members => _members.AsReadOnly();

    protected RescueTeamModel() { }

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
            Status = RescueTeamStatus.AwaitingAcceptance,
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

    public void AddMember(Guid userId, bool isLeader, string rescuerType, string? roleInTeam)
    {
        if (Status is not (RescueTeamStatus.AwaitingAcceptance or RescueTeamStatus.Unavailable))
            throw new RescueTeamBusinessRuleException("Chỉ có thể thêm thành viên khi đội đang ở trạng thái AwaitingAcceptance hoặc Unavailable.");

        if (_members.Count(m => m.Status is not (TeamMemberStatus.Declined or TeamMemberStatus.Removed)) >= MaxMembers)
            throw new RescueTeamBusinessRuleException("Đội đã đạt giới hạn thành viên tối đa.");

        if (isLeader && _members.Any(m => m.IsLeader && m.Status is not (TeamMemberStatus.Declined or TeamMemberStatus.Removed)))
            throw new RescueTeamBusinessRuleException("Đội đã có đội trưởng.");

        if (_members.Any(m => m.UserId == userId && m.Status != TeamMemberStatus.Declined && m.Status != TeamMemberStatus.Removed))
            throw new RescueTeamBusinessRuleException("Thành viên này đã có trong đội.");

        _members.Add(new RescueTeamMemberModel(userId, isLeader, rescuerType, roleInTeam));
        
        if (Status == RescueTeamStatus.Unavailable)
        {
            Status = RescueTeamStatus.AwaitingAcceptance;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveMember(Guid userId)
    {
        if (Status is not (RescueTeamStatus.AwaitingAcceptance or RescueTeamStatus.Unavailable))
            throw new RescueTeamBusinessRuleException("Chỉ có thể xóa thành viên khi đội đang ở trạng thái AwaitingAcceptance hoặc Unavailable.");

        var member = _members.FirstOrDefault(m => m.UserId == userId) 
            ?? throw new TeamMemberDomainException("Không tìm thấy thành viên trong đội.");

        member.Remove();
        
        EvaluateAcceptanceState();
        EvaluateCheckInState();
        UpdatedAt = DateTime.UtcNow;
    }

    public void AcceptInvitation(Guid userId)
    {
        var member = GetMember(userId);
        member.Accept();
        
        EvaluateAcceptanceState();
        UpdatedAt = DateTime.UtcNow;
    }

    public void DeclineInvitation(Guid userId)
    {
        var member = GetMember(userId);
        member.Decline();
        
        EvaluateAcceptanceState();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ScheduleAssembly(DateTime assemblyDate)
    {
        if (Status != RescueTeamStatus.Ready)
            throw new InvalidTeamTransitionException(Status, RescueTeamStatus.Gathering);

        Status = RescueTeamStatus.Gathering;
        AssemblyDate = assemblyDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MemberCheckIn(Guid userId)
    {
        if (Status != RescueTeamStatus.Gathering)
            throw new RescueTeamBusinessRuleException("Đội chưa trong trạng thái tập hợp (Gathering).");

        var member = GetMember(userId);
        member.CheckIn();
        
        EvaluateCheckInState();
        UpdatedAt = DateTime.UtcNow;
    }

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

    public void Disband()
    {
        if (Status is not (RescueTeamStatus.Available or RescueTeamStatus.Unavailable))
            throw new InvalidTeamTransitionException(Status, RescueTeamStatus.Disbanded);

        Status = RescueTeamStatus.Disbanded;
        DisbandAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EvaluateAcceptanceState()
    {
        if (Status == RescueTeamStatus.AwaitingAcceptance)
        {
            var activeMembers = _members.Where(m => m.Status is not (TeamMemberStatus.Declined or TeamMemberStatus.Removed)).ToList();
            if (activeMembers.Any() && activeMembers.All(m => m.Status == TeamMemberStatus.Accepted))
            {
                Status = RescueTeamStatus.Ready;
            }
        }
    }

    private void EvaluateCheckInState()
    {
        if (Status == RescueTeamStatus.Gathering)
        {
            var acceptedMembers = _members.Where(m => m.Status == TeamMemberStatus.Accepted).ToList();
            if (acceptedMembers.Any() && acceptedMembers.All(m => m.CheckedIn))
            {
                Status = RescueTeamStatus.Available;
            }
        }
    }

    private void ChangeStatus(RescueTeamStatus expectedCurrent, RescueTeamStatus nextStatus)
    {
        if (Status != expectedCurrent)
            throw new InvalidTeamTransitionException(Status, nextStatus);
        
        Status = nextStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    private RescueTeamMemberModel GetMember(Guid userId)
    {
        return _members.FirstOrDefault(m => m.UserId == userId) 
            ?? throw new TeamMemberDomainException("Không tìm thấy thành viên trong đội.");
    }

    public void SetId(int id) => Id = id;
    public void LoadMembers(IEnumerable<RescueTeamMemberModel> members) => _members.AddRange(members);
}
