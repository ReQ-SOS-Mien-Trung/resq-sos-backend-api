using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private const int TotalRescuerCount = 200;
    private const int RecentRescuerCount = 20;

    private async Task SeedIdentityAsync(DemoSeedContext seed, CancellationToken cancellationToken)
    {
        var users = new List<User>();
        users.Add(CreateUser("admin", 1, 1, "Nguyễn", "Minh Tuấn", SeedConstants.AdminPasswordHash, Area(0), seed));

        for (var i = 0; i < 5; i++)
        {
            var name = VietnameseName(i + 3);
            users.Add(CreateUser($"coord{i + 1:00}", 2, i + 1, name.Last, name.First, SeedConstants.CoordinatorPasswordHash, Area(i), seed));
        }

        for (var i = 0; i < 9; i++)
        {
            var name = VietnameseName(i + 20);
            users.Add(CreateUser($"manager{i + 1:00}", 4, i + 1, name.Last, name.First, SeedConstants.ManagerPasswordHash, Area(i + 2), seed));
        }

        for (var i = 0; i < TotalRescuerCount; i++)
        {
            var name = VietnameseName(i + 40);
            var rescuerNumber = i + 1;
            var user = CreateUser($"rescuer{rescuerNumber:000}", 3, rescuerNumber, name.Last, name.First, SeedConstants.RescuerPasswordHash, Area(i), seed);
            user.IsEmailVerified = true;
            if (IsRecentRescuerNumber(rescuerNumber))
            {
                var recentIndex = RecentRescuerIndex(rescuerNumber);
                var createdAt = RecentRescuerCreatedAt(seed, recentIndex);
                user.CreatedAt = createdAt;
                user.UpdatedAt = createdAt.AddHours(8 + recentIndex % 18);
            }

            users.Add(user);
        }

        for (var i = 0; i < 140; i++)
        {
            var name = VietnameseName(i + 150);
            users.Add(CreateUser($"victim{i + 1:000}", 5, i + 1, name.Last, name.First, SeedConstants.VictimPasswordHash, Area(i + 4), seed));
        }

        users[^1].IsBanned = true;
        users[^1].BannedBy = users[0].Id;
        users[^1].BannedAt = seed.AnchorUtc.AddDays(-20);
        users[^1].BanReason = "Tạo nhiều SOS thử nghiệm sai sự thật";
        users[^2].IsBanned = true;
        users[^2].BannedBy = users[0].Id;
        users[^2].BannedAt = seed.AnchorUtc.AddDays(-48);
        users[^2].BanReason = "Spam chat hỗ trợ";

        var demoVictim = CreateDemoVictimWithPin(seed);
        users.Add(demoVictim);

        _db.Users.AddRange(users);
        await _db.SaveChangesAsync(cancellationToken);

        seed.Admins.Add(users[0]);
        seed.Coordinators.AddRange(users.Where(u => u.RoleId == 2));
        seed.Managers.AddRange(users.Where(u => u.RoleId == 4));
        seed.Rescuers.AddRange(users.Where(u => u.RoleId == 3));
        seed.Victims.AddRange(users.Where(u => u.RoleId == 5));

        _db.UserRelativeProfiles.AddRange(CreateDemoVictimRelativeProfiles(demoVictim.Id, seed));
        await _db.SaveChangesAsync(cancellationToken);

        var abilities = await _db.Abilities.OrderBy(a => a.Id).ToListAsync(cancellationToken);
        var userAbilities = new List<UserAbility>();
        foreach (var rescuer in seed.Rescuers)
        {
            var index = seed.Rescuers.IndexOf(rescuer);
            var abilityCount = 2 + index % 5;
            for (var i = 0; i < abilityCount; i++)
            {
                var ability = abilities[(index * 3 + i) % abilities.Count];
                userAbilities.Add(new UserAbility
                {
                    UserId = rescuer.Id,
                    AbilityId = ability.Id,
                    Level = 2 + (index + i) % 4
                });
            }
        }

        _db.UserAbilities.AddRange(userAbilities);
        await _db.SaveChangesAsync(cancellationToken);
    }


    private static User CreateUser(string username, int roleId, int number, string lastName, string firstName, string password, SeedArea area, DemoSeedContext seed)
    {
        var rolePrefix = roleId switch
        {
            1 => "admin",
            2 => "coord",
            3 => "rescuer",
            4 => "manager",
            _ => "victim"
        };
        var location = Point(area.Lon + (number % 7 - 3) * 0.002, area.Lat + (number % 5 - 2) * 0.002);
        return new User
        {
            Id = StableGuid($"user-{rolePrefix}-{number:000}"),
            RoleId = roleId,
            FirstName = firstName,
            LastName = lastName,
            Username = username,
            Phone = Phone(roleId, number),
            Password = password,
            Email = $"{username}@resq.vn",
            IsEmailVerified = number % 17 != 0,
            AvatarUrl = $"https://i.pravatar.cc/160?u={username}",
            Location = location,
            Address = $"{10 + number % 90} {area.Address}",
            Ward = area.Ward,
            Province = area.Province,
            CreatedAt = seed.StartUtc.AddDays(number * 3 % 900),
            UpdatedAt = seed.AnchorUtc.AddDays(-(number % 60)),
            IsBanned = false
        };
    }

    private static User CreateDemoVictimWithPin(DemoSeedContext seed)
    {
        var area = Area(0);
        var user = CreateUser(
            "victim.demo.374745872",
            5,
            999,
            "Huỳnh",
            "Kim Cương",
            SeedConstants.DemoVictimPinPasswordHash,
            area,
            seed);

        user.Phone = "+84374745872";
        user.Email = "victim.demo.374745872@resq.vn";
        user.Address = "32 Nguyễn Huệ, phường Phú Hội, Huế";
        user.Ward = "Phú Hội";
        user.Province = "Thừa Thiên Huế";
        user.Location = Point(107.5948, 16.4642);
        user.CreatedAt = new DateTime(2026, 4, 18, 10, 45, 0, DateTimeKind.Utc);
        user.UpdatedAt = new DateTime(2026, 4, 18, 10, 53, 8, DateTimeKind.Utc);
        user.IsEmailVerified = true;

        return user;
    }

    private static IEnumerable<UserRelativeProfile> CreateDemoVictimRelativeProfiles(Guid userId, DemoSeedContext seed)
    {
        var createdAt = new DateTime(2026, 4, 18, 10, 53, 8, DateTimeKind.Utc);
        var relatives = new[]
        {
            new RelativeProfileSeed(
                "Châu",
                "+84972513978",
                "ELDERLY",
                "FEMALE",
                ["me_gia", "can_diu", "uu_tien_so_tan"],
                "Mẹ 72 tuổi, huyết áp cao, hay đau khớp gối.",
                "Cần người dìu khi đi bộ xa hoặc leo cầu thang.",
                "Ăn mềm, hạn chế muối và đường.",
                Json(new
                {
                    bloodType = "UNKNOWN",
                    allergyDetails = "Dị ứng nhẹ với một số thuốc giảm đau nhóm NSAID.",
                    allergyOptions = new[] { "MEDICATION" },
                    medicalDevices = new[] { "WALKING_CANE" },
                    medicalHistory = new[] { "BONE_FRACTURE", "JOINT_PAIN" },
                    mobilityStatus = "NEEDS_ASSISTANCE",
                    specialSituation = new
                    {
                        isSenior = true,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = new[] { "HYPERTENSION", "DIABETES" },
                    otherMedicalDevice = "",
                    longTermMedications = new[] { "Thuốc huyết áp buổi sáng", "Thuốc tiểu đường sau ăn" },
                    hasLongTermMedication = true,
                    medicalHistoryDetails = "Từng gãy xương cổ tay phải, đi lại chậm khi trời mưa.",
                    otherChronicCondition = ""
                })),
            new RelativeProfileSeed(
                "An",
                "+84908112233",
                "ADULT",
                "FEMALE",
                ["vo", "lien_he_chinh", "di_chuyen_duoc"],
                "Sức khỏe ổn định, có tiền sử hen nhẹ khi lạnh.",
                "Cần mang theo thuốc xịt hen dự phòng.",
                "Không ăn hải sản sống.",
                Json(new
                {
                    bloodType = "O",
                    allergyDetails = "Dị ứng hải sản sống.",
                    allergyOptions = new[] { "FOOD" },
                    medicalDevices = Array.Empty<string>(),
                    medicalHistory = new[] { "ASTHMA" },
                    mobilityStatus = "NORMAL",
                    specialSituation = new
                    {
                        isSenior = false,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = Array.Empty<string>(),
                    otherMedicalDevice = "",
                    longTermMedications = new[] { "Thuốc xịt hen dự phòng" },
                    hasLongTermMedication = true,
                    medicalHistoryDetails = "Hen nhẹ, thường xuất hiện khi thời tiết lạnh hoặc ẩm.",
                    otherChronicCondition = ""
                })),
            new RelativeProfileSeed(
                "Thảo",
                "+84933668120",
                "ADULT",
                "FEMALE",
                ["chi_gai", "biet_so_cuu", "co_the_ho_tro"],
                "Chị gái sống gần nhà, có thể hỗ trợ chăm sóc người già.",
                null,
                "Không ăn cay.",
                Json(new
                {
                    bloodType = "B",
                    allergyDetails = "",
                    allergyOptions = Array.Empty<string>(),
                    medicalDevices = Array.Empty<string>(),
                    medicalHistory = Array.Empty<string>(),
                    mobilityStatus = "NORMAL",
                    specialSituation = new
                    {
                        isSenior = false,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = Array.Empty<string>(),
                    otherMedicalDevice = "",
                    longTermMedications = Array.Empty<string>(),
                    hasLongTermMedication = false,
                    medicalHistoryDetails = "",
                    otherChronicCondition = ""
                })),
            new RelativeProfileSeed(
                "Khoa",
                "+84911224567",
                "ADULT",
                "MALE",
                ["em_trai", "can_lien_lac", "di_chuyen_duoc"],
                "Em trai thường đi làm xa, cần báo sớm khi có sơ tán.",
                "Cần hỗ trợ định vị nếu mất sóng điện thoại.",
                null,
                Json(new
                {
                    bloodType = "A",
                    allergyDetails = "",
                    allergyOptions = new[] { "DUST" },
                    medicalDevices = Array.Empty<string>(),
                    medicalHistory = new[] { "MIGRAINE" },
                    mobilityStatus = "NORMAL",
                    specialSituation = new
                    {
                        isSenior = false,
                        isPregnant = false,
                        isYoungChild = false,
                        hasDisability = false
                    },
                    chronicConditions = Array.Empty<string>(),
                    otherMedicalDevice = "",
                    longTermMedications = Array.Empty<string>(),
                    hasLongTermMedication = false,
                    medicalHistoryDetails = "Đôi khi đau nửa đầu khi thiếu ngủ.",
                    otherChronicCondition = ""
                }))
        };

        return relatives.Select((relative, index) => new UserRelativeProfile
        {
            Id = StableGuid($"demo-victim-relative-{index + 1}"),
            UserId = userId,
            DisplayName = relative.DisplayName,
            PhoneNumber = relative.PhoneNumber,
            PersonType = relative.PersonType,
            RelationGroup = "gia_dinh",
            Gender = relative.Gender,
            TagsJson = Json(relative.Tags),
            MedicalBaselineNote = relative.MedicalBaselineNote,
            SpecialNeedsNote = relative.SpecialNeedsNote,
            SpecialDietNote = relative.SpecialDietNote,
            MedicalProfileJson = relative.MedicalProfileJson,
            ProfileUpdatedAt = createdAt.AddMinutes(index),
            CreatedAt = createdAt.AddSeconds(index * 12),
            UpdatedAt = createdAt.AddSeconds(index * 12 + 4)
        });
    }

    private static bool IsRecentRescuerNumber(int number) =>
        number > TotalRescuerCount - RecentRescuerCount;

    private static int RecentRescuerIndex(int number) =>
        number - (TotalRescuerCount - RecentRescuerCount) - 1;

    private static DateTime RecentRescuerCreatedAt(DemoSeedContext seed, int recentIndex)
    {
        var anchorVietnamDate = seed.AnchorUtc.AddHours(7).Date;
        var dayOffset = -29 + recentIndex * 27 / Math.Max(1, RecentRescuerCount - 1);
        var localCreatedAt = anchorVietnamDate
            .AddDays(dayOffset)
            .AddHours(8 + recentIndex % 10)
            .AddMinutes(recentIndex * 17 % 60);

        return VnToUtc(localCreatedAt);
    }

    private static DateTime RecentRescuerApprovedAt(DemoSeedContext seed, DateTime? createdAt, int recentIndex)
    {
        var approvedAt = (createdAt ?? RecentRescuerCreatedAt(seed, recentIndex))
            .AddDays(1 + recentIndex % 3)
            .AddHours(2);

        return approvedAt <= seed.AnchorUtc
            ? approvedAt
            : seed.AnchorUtc.AddHours(-(recentIndex % 12 + 1));
    }

    private static string Phone(int roleId, int number)
    {
        var prefix = roleId switch
        {
            1 => 900,
            2 => 901,
            3 => 902,
            4 => 903,
            5 => 904,
            _ => 905
        };
        return roleId == 5
            ? $"+84{prefix}{number:000000}"
            : $"0{prefix}{number:000000}";
    }
}
