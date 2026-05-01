using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private const int UnassignedRescuerCount = 40;
    private const int EligibleAssignedRescuerCount = 128;
    private const int HueStadiumCheckedInStandbyRescuerCount = 10;
    private const int HueStadiumReserveTeamCount = 2;
    private const int HueStadiumReserveTeamMemberCount = 6;
    private const string HueStadiumReserveTeamCodePrefix = "RT-HUE-TD-AV";

    private async Task SeedPersonnelAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var points = new[]
        {
            ("AP-HUE-TD-241015", "Sân vận động Tự Do (Thừa Thiên Huế)", 16.46751083681696, 107.59761456770599, "Available", 20, "https://res.cloudinary.com/dezgwdrfs/image/upload/v1774499522/SVDTD_TTH_sqdeoa.jpg"),
            ("AP-HUE-02", "Trường THPT chuyên Quốc Học Huế", 16.460016626853086, 107.58329401836049, "Available", (int?)null, (string?)null),
            ("AP-HUE-03", "Nhà văn hóa Quảng Điền", 16.5790, 107.5128, "Unavailable", (int?)null, (string?)null),
            ("AP-DNG-01", "Cung thể thao Tiên Sơn", 16.0471, 108.2188, "Available", (int?)null, (string?)null),
            ("AP-DNG-02", "Trung tâm Hòa Vang", 15.9886, 108.1210, "Available", (int?)null, (string?)null),
            ("AP-QTR-01", "Nhà văn hóa Đông Hà", 16.8175, 107.1003, "Available", (int?)null, (string?)null),
            ("AP-QTR-02", "Trường THPT Hải Lăng", 16.6766, 107.2284, "Closed", (int?)null, (string?)null),
            ("AP-QNM-01", "Trung tâm Tam Kỳ", 15.5736, 108.4740, "Available", (int?)null, (string?)null),
            ("AP-QNM-02", "Điểm tập kết Hội An", 15.8801, 108.3380, "Created", (int?)null, (string?)null),
            ("AP-QNG-01", "Trung tâm Quảng Ngãi", 15.1214, 108.8044, "Available", (int?)null, (string?)null)
        };

        foreach (var (code, name, lat, lon, status, maxCapacity, imageUrl) in points)
        {
            seed.AssemblyPoints.Add(new AssemblyPoint
            {
                Code = code,
                Name = name,
                MaxCapacity = maxCapacity ?? 90 + seed.AssemblyPoints.Count * 15,
                Status = status,
                Location = Point(lon, lat),
                CreatedAt = seed.StartUtc.AddDays(seed.AssemblyPoints.Count * 12),
                UpdatedAt = seed.AnchorUtc.AddDays(-seed.AssemblyPoints.Count),
                ImageUrl = imageUrl ?? $"https://cdn.resq.vn/assembly/{code.ToLowerInvariant()}.jpg",
                StatusReason = status == "Unavailable" ? "Đang sửa mái che và máy phát điện" : null,
                StatusChangedAt = seed.AnchorUtc.AddDays(-10 + seed.AssemblyPoints.Count),
                StatusChangedBy = seed.Coordinators[seed.AssemblyPoints.Count % seed.Coordinators.Count].Id
            });
        }

        _db.AssemblyPoints.AddRange(seed.AssemblyPoints);
        await _db.SaveChangesAsync(cancellationToken);

        var deployableRescuers = seed.Rescuers.Take(seed.Rescuers.Count - UnassignedRescuerCount).ToList();
        var standbyRescuers = seed.Rescuers.Skip(deployableRescuers.Count).ToList();
        var standbyRescuerIds = standbyRescuers.Select(r => r.Id).ToHashSet();

        for (var i = 0; i < deployableRescuers.Count; i++)
        {
            deployableRescuers[i].AssemblyPointId = seed.AssemblyPoints[i % seed.AssemblyPoints.Count].Id;
        }

        var profiles = seed.Rescuers.Select((user, index) =>
        {
            var isApprovedRescuer = index < EligibleAssignedRescuerCount || standbyRescuerIds.Contains(user.Id);
            return new RescuerProfile
            {
                UserId = user.Id,
                RescuerType = index % 4 == 0 ? "Core" : "Volunteer",
                IsEligibleRescuer = isApprovedRescuer,
                Step = isApprovedRescuer ? 3 : 0,
                ApprovedBy = isApprovedRescuer ? seed.Admins[0].Id : null,
                ApprovedAt = null
            };
        }).ToList();

        _db.RescuerProfiles.AddRange(profiles);
        await _db.SaveChangesAsync(cancellationToken);

        var applications = new List<RescuerApplication>();
        var profilesByUserId = profiles.ToDictionary(profile => profile.UserId);
        var approvedRescuerIds = profiles
            .Where(profile => profile.IsEligibleRescuer)
            .Select(profile => profile.UserId)
            .ToHashSet();
        var approvedApplicationUsers = seed.Rescuers
            .Where(rescuer => approvedRescuerIds.Contains(rescuer.Id))
            .ToList();
        var nonApprovedApplicationUsers = seed.Rescuers
            .Where(rescuer => !approvedRescuerIds.Contains(rescuer.Id))
            .ToList();

        var applicationIndex = 0;
        foreach (var user in approvedApplicationUsers)
        {
            var rescuerNumber = seed.Rescuers.IndexOf(user) + 1;
            var submitted = IsRecentRescuerNumber(rescuerNumber)
                ? user.CreatedAt!.Value.AddHours(4 + RecentRescuerIndex(rescuerNumber) % 12)
                : seed.StartUtc.AddDays(50 + applicationIndex * 3);
            var reviewedAt = IsRecentRescuerNumber(rescuerNumber)
                ? RecentRescuerApprovedAt(seed, user.CreatedAt, RecentRescuerIndex(rescuerNumber))
                : submitted.AddDays(2 + applicationIndex % 4);
            if (reviewedAt <= submitted)
            {
                reviewedAt = submitted.AddDays(1);
            }

            applications.Add(new RescuerApplication
            {
                UserId = user.Id,
                Status = "Approved",
                SubmittedAt = submitted,
                ReviewedAt = reviewedAt,
                ReviewedBy = seed.Admins[0].Id,
                AdminNote = "Đủ hồ sơ và đã xác minh kỹ năng cơ bản"
            });

            profilesByUserId[user.Id].ApprovedAt = reviewedAt;
            profilesByUserId[user.Id].ApprovedBy = seed.Admins[0].Id;
            applicationIndex++;
        }

        foreach (var user in nonApprovedApplicationUsers.Take(5))
        {
            var submitted = user.CreatedAt!.Value.AddDays(2 + applicationIndex % 4);
            applications.Add(new RescuerApplication
            {
                UserId = user.Id,
                Status = "Pending",
                SubmittedAt = submitted,
                AdminNote = null
            });

            profilesByUserId[user.Id].Step = 1;
            applicationIndex++;
        }

        foreach (var user in nonApprovedApplicationUsers.Skip(5).Take(5))
        {
            var submitted = user.CreatedAt!.Value.AddDays(2 + applicationIndex % 4);
            applications.Add(new RescuerApplication
            {
                UserId = user.Id,
                Status = "Rejected",
                SubmittedAt = submitted,
                ReviewedAt = submitted.AddDays(2 + applicationIndex % 3),
                ReviewedBy = seed.Admins[0].Id,
                AdminNote = "Thiếu giấy tờ xác minh"
            });

            profilesByUserId[user.Id].Step = 1;
            applicationIndex++;
        }

        _db.RescuerApplications.AddRange(applications);
        await _db.SaveChangesAsync(cancellationToken);

        var documents = new List<RescuerApplicationDocument>();
        foreach (var application in applications)
        {
            var typeIds = new[] { 9, 5, 1 + application.Id % 4 };
            foreach (var typeId in typeIds)
            {
                documents.Add(new RescuerApplicationDocument
                {
                    ApplicationId = application.Id,
                    FileTypeId = typeId,
                    FileUrl = $"https://cdn.resq.vn/docs/application-{application.Id}-{typeId}.pdf",
                    UploadedAt = application.SubmittedAt?.AddMinutes(typeId * 7)
                });
            }
        }

        _db.RescuerApplicationDocuments.AddRange(documents);

        var scores = deployableRescuers.Take(72).Select((rescuer, index) =>
        {
            var a = 6.5m + (index % 30) / 10m;
            var b = 6.2m + (index % 25) / 10m;
            var c = 6.0m + (index % 28) / 10m;
            var d = 6.4m + (index % 24) / 10m;
            var e = 6.3m + (index % 26) / 10m;
            return new RescuerScore
            {
                UserId = rescuer.Id,
                ResponseTimeScore = a,
                RescueEffectivenessScore = b,
                DecisionHandlingScore = c,
                SafetyMedicalSkillScore = d,
                TeamworkCommunicationScore = e,
                OverallAverageScore = Math.Round((a + b + c + d + e) / 5m, 2),
                EvaluationCount = index % 26,
                CreatedAt = seed.StartUtc.AddDays(100 + index),
                UpdatedAt = seed.AnchorUtc.AddDays(-index % 40)
            };
        }).ToList();
        _db.RescuerScores.AddRange(scores);

        await SeedAssemblyEventsAsync(seed, cancellationToken);
        await SeedRescueTeamsAsync(seed, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAssemblyEventsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var deployableRescuers = GetDeployableRescuers(seed);
        var standbyRescuers = seed.Rescuers.Skip(deployableRescuers.Count).ToList();
        var events = new List<AssemblyEvent>();
        var hueStadium = GetHueStadiumAssemblyPoint(seed);
        AssemblyEvent? activeHueEvent = null;

        if (hueStadium is not null)
        {
            var assemblyDate = TrimUtcToMinute(seed.AnchorUtc.AddMinutes(-30));
            var checkInDeadline = assemblyDate.AddMinutes(45);
            activeHueEvent = new AssemblyEvent
            {
                AssemblyPointId = hueStadium.Id,
                AssemblyDate = assemblyDate,
                Status = "Gathering",
                CreatedBy = seed.Coordinators[0].Id,
                CreatedAt = seed.AnchorUtc.AddHours(-2),
                UpdatedAt = seed.AnchorUtc.AddMinutes(-5),
                CheckInDeadline = checkInDeadline
            };
            events.Add(activeHueEvent);

            foreach (var rescuer in standbyRescuers.Take(HueStadiumCheckedInStandbyRescuerCount))
            {
                rescuer.AssemblyPointId = hueStadium.Id;
            }
        }

        for (var i = 0; i < 44; i++)
        {
            var plannedAssemblyDate = RandomEventUtc(seed, i).AddHours(6 + i % 3);
            var plannedCheckInDeadline = plannedAssemblyDate.AddMinutes(45);
            var status = plannedCheckInDeadline <= seed.AnchorUtc
                ? "Completed"
                : "Gathering";
            var assemblyDate = status == "Gathering"
                ? TrimUtcToMinute(seed.AnchorUtc.AddMinutes(-(26 + i % 15)))
                : plannedAssemblyDate;
            var checkInDeadline = assemblyDate.AddMinutes(45);
            events.Add(new AssemblyEvent
            {
                AssemblyPointId = seed.AssemblyPoints[i % seed.AssemblyPoints.Count].Id,
                AssemblyDate = assemblyDate,
                Status = status,
                CreatedBy = seed.Coordinators[i % seed.Coordinators.Count].Id,
                CreatedAt = assemblyDate.AddHours(-8),
                UpdatedAt = status == "Completed"
                    ? ClampHistoricalUtc(assemblyDate.AddHours(8), assemblyDate, seed.AnchorUtc)
                    : ClampHistoricalUtc(seed.AnchorUtc.AddMinutes(-(10 + i % 5)), assemblyDate, seed.AnchorUtc),
                CheckInDeadline = checkInDeadline
            });
        }

        _db.AssemblyEvents.AddRange(events);
        await _db.SaveChangesAsync(cancellationToken);

        var participants = new List<AssemblyParticipant>();
        if (activeHueEvent is not null)
        {
            foreach (var (rescuer, index) in standbyRescuers.Take(HueStadiumCheckedInStandbyRescuerCount).Select((rescuer, index) => (rescuer, index)))
            {
                participants.Add(new AssemblyParticipant
                {
                    AssemblyEventId = activeHueEvent.Id,
                    RescuerId = rescuer.Id,
                    Status = "CheckedIn",
                    IsCheckedIn = true,
                    CheckInTime = activeHueEvent.AssemblyDate.AddMinutes(5 + index * 2),
                    IsCheckedOut = false,
                    CheckOutTime = null
                });
            }
        }

        foreach (var assemblyEvent in events)
        {
            if (activeHueEvent is not null && assemblyEvent.Id == activeHueEvent.Id)
            {
                continue;
            }

            for (var i = 0; i < 7; i++)
            {
                var rescuer = deployableRescuers[(assemblyEvent.Id * 11 + i) % deployableRescuers.Count];
                var absent = (assemblyEvent.Id + i) % 10 == 0;
                var late = (assemblyEvent.Id + i) % 6 == 0;
                participants.Add(new AssemblyParticipant
                {
                    AssemblyEventId = assemblyEvent.Id,
                    RescuerId = rescuer.Id,
                    Status = absent ? "Absent" : "CheckedIn",
                    IsCheckedIn = !absent,
                    CheckInTime = absent
                        ? null
                        : ClampHistoricalUtc(
                            assemblyEvent.AssemblyDate.AddMinutes(
                                assemblyEvent.Status == "Gathering"
                                    ? late ? 35 : 12 + i * 2
                                    : late ? 55 : 20 + i),
                            assemblyEvent.AssemblyDate,
                            seed.AnchorUtc),
                    IsCheckedOut = !absent && assemblyEvent.Status == "Completed",
                    CheckOutTime = !absent && assemblyEvent.Status == "Completed"
                        ? ClampHistoricalUtc(assemblyEvent.AssemblyDate.AddHours(8), assemblyEvent.AssemblyDate, seed.AnchorUtc)
                        : null
                });
            }
        }

        _db.AssemblyParticipants.AddRange(participants);
    }

    private async Task SeedRescueTeamsAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var teamRescuers = GetTeamRosterRescuers(seed);
        var statuses = new[]
        {
            "Available", "Available", "Gathering", "Available", "Gathering",
            "Available", "Gathering", "Available", "Available", "Stuck",
            "Available", "Gathering", "Available", "Gathering", "Available",
            "Available", "Available", "Unavailable", "Disbanded", "Disbanded"
        };
        var types = new[] { "Mixed", "Rescue", "Medical", "Transportation" };

        for (var i = 0; i < 20; i++)
        {
            seed.RescueTeams.Add(new RescueTeam
            {
                AssemblyPointId = seed.AssemblyPoints[i % seed.AssemblyPoints.Count].Id,
                ManagedBy = seed.Coordinators[i % seed.Coordinators.Count].Id,
                Code = $"RT-{Area(i).Code}-{i + 1:00}",
                Name = $"Đội {TeamName(i)} {i + 1}",
                TeamType = types[i % types.Length],
                Status = statuses[i],
                MaxMembers = i >= 17 ? 10 : 8,
                Reason = statuses[i] == "Unavailable" ? "Bảo dưỡng thiết bị và nghỉ luân phiên" : null,
                AssemblyDate = RandomEventUtc(seed, i + 80),
                CreatedAt = seed.StartUtc.AddDays(120 + i),
                UpdatedAt = seed.AnchorUtc.AddDays(-i),
                DisbandAt = statuses[i] == "Disbanded" ? seed.AnchorUtc.AddDays(-50 + i) : null
            });
        }

        _db.RescueTeams.AddRange(seed.RescueTeams);
        await _db.SaveChangesAsync(cancellationToken);

        var memberIndex = 0;
        for (var teamIndex = 0; teamIndex < 20; teamIndex++)
        {
            var team = seed.RescueTeams[teamIndex];
            var count = team.MaxMembers; // Luôn lấp đầy đội theo MaxMembers
            for (var i = 0; i < count; i++)
            {
                var rescuer = teamIndex < 18
                    ? teamRescuers[memberIndex++ % teamRescuers.Count]
                    : teamRescuers[(teamIndex * 13 + i) % teamRescuers.Count];
                var invitedAt = (team.CreatedAt ?? seed.StartUtc).AddHours(2 + i);
                seed.RescueTeamMembers.Add(new RescueTeamMember
                {
                    TeamId = team.Id,
                    UserId = rescuer.Id,
                    Status = "Accepted",
                    InvitedAt = invitedAt,
                    RespondedAt = invitedAt.AddMinutes(10 + i * 3),
                    IsLeader = i == 0,
                    RoleInTeam = i == 0 ? "Leader" : TeamMemberRole(i, team.TeamType),
                    CheckedIn = team.Status != "Disbanded"
                });
            }
        }

        await AddHueStadiumAvailableReserveTeamsAsync(seed, teamRescuers, memberIndex, cancellationToken);

        _db.RescueTeamMembers.AddRange(seed.RescueTeamMembers);
    }

    private async Task AddHueStadiumAvailableReserveTeamsAsync(
        DemoSeedContext seed,
        IReadOnlyList<User> deployableRescuers,
        int usedDeployableRescuerCount,
        CancellationToken cancellationToken)
    {
        var hueStadium = GetHueStadiumAssemblyPoint(seed)
            ?? throw new InvalidOperationException("Không tìm thấy điểm tập kết Sân vận động Tự Do trong demo seed.");
        var requiredMemberCount = HueStadiumReserveTeamCount * HueStadiumReserveTeamMemberCount;
        var assignedRescuerIds = seed.RescueTeamMembers.Select(member => member.UserId).ToHashSet();
        var reserveRescuers = deployableRescuers
            .Skip(usedDeployableRescuerCount)
            .Concat(seed.Rescuers
                .Skip(deployableRescuers.Count)
                .Where(rescuer => rescuer.AssemblyPointId == hueStadium.Id))
            .Where(rescuer => !assignedRescuerIds.Contains(rescuer.Id))
            .Take(requiredMemberCount)
            .ToList();

        if (reserveRescuers.Count < requiredMemberCount)
        {
            throw new InvalidOperationException("Không đủ rescuer khả dụng để tạo 2 team Available tại Sân vận động Tự Do.");
        }

        foreach (var rescuer in reserveRescuers)
        {
            rescuer.AssemblyPointId = hueStadium.Id;
        }

        var reserveTeamTypes = new[] { "Mixed", "Rescue" };
        var reserveTeamNames = new[] { "Đội thường trực Tự Do 1", "Đội cơ động Tự Do 2" };
        var reserveTeams = new List<RescueTeam>();
        for (var i = 0; i < HueStadiumReserveTeamCount; i++)
        {
            reserveTeams.Add(new RescueTeam
            {
                AssemblyPointId = hueStadium.Id,
                ManagedBy = seed.Coordinators[i % seed.Coordinators.Count].Id,
                Code = $"{HueStadiumReserveTeamCodePrefix}-{i + 1:00}",
                Name = reserveTeamNames[i],
                TeamType = reserveTeamTypes[i],
                Status = "Available",
                MaxMembers = 6,
                AssemblyDate = seed.AnchorUtc.AddHours(-(i + 1)),
                CreatedAt = seed.AnchorUtc.AddDays(-(i + 1)),
                UpdatedAt = seed.AnchorUtc.AddMinutes(-(10 + i))
            });
        }

        _db.RescueTeams.AddRange(reserveTeams);
        await _db.SaveChangesAsync(cancellationToken);
        seed.RescueTeams.AddRange(reserveTeams);

        for (var teamIndex = 0; teamIndex < reserveTeams.Count; teamIndex++)
        {
            var team = reserveTeams[teamIndex];
            var members = reserveRescuers
                .Skip(teamIndex * HueStadiumReserveTeamMemberCount)
                .Take(HueStadiumReserveTeamMemberCount)
                .ToList();

            for (var memberPosition = 0; memberPosition < members.Count; memberPosition++)
            {
                var rescuer = members[memberPosition];
                var invitedAt = (team.CreatedAt ?? seed.StartUtc).AddHours(2 + memberPosition);
                seed.RescueTeamMembers.Add(new RescueTeamMember
                {
                    TeamId = team.Id,
                    UserId = rescuer.Id,
                    Status = "Accepted",
                    InvitedAt = invitedAt,
                    RespondedAt = invitedAt.AddMinutes(10 + memberPosition * 3),
                    IsLeader = memberPosition == 0,
                    RoleInTeam = memberPosition == 0 ? "Leader" : TeamMemberRole(memberPosition, team.TeamType),
                    CheckedIn = true
                });
            }
        }
    }


    private static string TeamName(int index)
    {
        var names = new[] { "Hương Giang", "Bạch Mã", "Sơn Trà", "Hải Vân", "Thạch Hãn", "Thu Bồn", "Trà Khúc", "Phú Bài" };
        return names[index % names.Length];
    }

    private static string TeamMemberRole(int index, string? teamType)
    {
        if (teamType == "Medical")
        {
            return index % 2 == 0 ? "Medic" : "Support";
        }

        if (teamType == "Transportation")
        {
            return index % 2 == 0 ? "Driver" : "Loader";
        }

        return index % 3 == 0 ? "Navigator" : "Rescuer";
    }

    private static List<User> GetDeployableRescuers(DemoSeedContext seed) =>
        seed.Rescuers.Take(seed.Rescuers.Count - UnassignedRescuerCount).ToList();

    private static List<User> GetTeamRosterRescuers(DemoSeedContext seed)
    {
        var hueLeadRescuers = seed.Rescuers
            .Skip(180)
            .Take(8);
        var approvedAssignedRescuers = seed.Rescuers
            .Take(EligibleAssignedRescuerCount);
        var activeStandbyRescuers = seed.Rescuers
            .Skip(170);

        return hueLeadRescuers
            .Concat(approvedAssignedRescuers)
            .Concat(activeStandbyRescuers)
            .DistinctBy(rescuer => rescuer.Id)
            .ToList();
    }

    private static bool IsHueStadiumReserveTeam(RescueTeam team) =>
        team.Code?.StartsWith(HueStadiumReserveTeamCodePrefix, StringComparison.Ordinal) == true;

    private static AssemblyPoint? GetHueStadiumAssemblyPoint(DemoSeedContext seed) =>
        seed.AssemblyPoints.FirstOrDefault(point =>
            string.Equals(point.Code, "AP-HUE-TD-241015", StringComparison.Ordinal)
            || string.Equals(point.Name, "Sân vận động Tự Do (Thừa Thiên Huế)", StringComparison.Ordinal));
}
