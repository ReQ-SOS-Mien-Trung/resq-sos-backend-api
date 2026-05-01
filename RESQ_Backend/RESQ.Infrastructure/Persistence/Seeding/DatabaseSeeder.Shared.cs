using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.System;

namespace RESQ.Infrastructure.Persistence.Seeding;

public sealed partial class DatabaseSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static Guid StableGuid(string value)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }

    private static DateTime VnToUtc(DateTime vietnamLocal)
    {
        return DateTime.SpecifyKind(vietnamLocal - TimeSpan.FromHours(7), DateTimeKind.Utc);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private static Point Point(double longitude, double latitude)
    {
        return new Point(longitude, latitude) { SRID = 4326 };
    }

    private static Point? OffsetPoint(Point? point, double latOffset, double lonOffset)
    {
        if (point is null)
        {
            return null;
        }

        return Point(point.X + lonOffset, point.Y + latOffset);
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static DateTime RandomEventLocal(DemoSeedContext seed, int index)
    {
        var yearBucket = index % 10;
        var year = yearBucket < 2 ? 2023 : yearBucket < 5 ? 2024 : yearBucket < 8 ? 2025 : 2026;
        var seasonBucket = index % 20;
        int month;
        if (seasonBucket < 13 && year < 2026)
        {
            month = 9 + index % 4;
        }
        else if (seasonBucket < 17)
        {
            month = 1 + index % 3;
        }
        else
        {
            month = year == 2023 ? 4 + index % 5 : 4 + index % 5;
            if (year == 2026)
            {
                month = 4;
            }
        }

        if (year == 2023 && month < 4)
        {
            month = 4;
        }
        if (year == 2026 && month > 4)
        {
            month = 4;
        }

        var maxDay = year == 2026 && month == 4 ? 16 : DateTime.DaysInMonth(year, month);
        var minDay = year == 2023 && month == 4 ? 16 : 1;
        var day = minDay + index % (maxDay - minDay + 1);
        return new DateTime(year, month, day, index % 24, (index * 7) % 60, 0, DateTimeKind.Unspecified);
    }

    private static DateTime RandomEventUtc(DemoSeedContext seed, int index) => VnToUtc(RandomEventLocal(seed, index));

    private static bool IsRecentOpenSosStatus(string? status) => status is "Pending" or "Assigned" or "InProgress" or "Incident";

    private static (DateTime CreatedAt, DateTime ReceivedAt, DateTime? ReviewedAt, DateTime LastUpdatedAt) BuildRecentOpenSosTimeline(
        DateTime anchorUtc,
        int primaryIndex,
        int secondaryIndex,
        string status,
        bool onBehalf)
    {
        if (!IsRecentOpenSosStatus(status))
        {
            throw new InvalidOperationException($"Status '{status}' does not use recent open SOS timeline.");
        }

        var seed = StableGuid($"recent-open-sos-{status}-{primaryIndex}-{secondaryIndex}-{(onBehalf ? 1 : 0)}");
        var createdHoursAgo = status switch
        {
            "Pending" => DeterministicRange(seed, 0, 2.5, 23.5),
            "Assigned" => DeterministicRange(seed, 0, 4.0, 21.0),
            "InProgress" => DeterministicRange(seed, 0, 5.5, 18.0),
            _ => DeterministicRange(seed, 0, 6.0, 14.0)
        };

        var createdAt = TrimUtcToMinute(anchorUtc.AddHours(-createdHoursAgo));
        var receivedAt = TrimUtcToMinute(createdAt.AddMinutes(onBehalf ? 2 : 0));

        if (status == "Pending")
        {
            var pendingLastUpdatedAt = TrimUtcToMinute(createdAt.AddMinutes(DeterministicRange(seed, 4, 12, 55)));
            return (createdAt, receivedAt, null, MinUtc(pendingLastUpdatedAt, anchorUtc.AddMinutes(-1)));
        }

        var reviewedAt = TrimUtcToMinute(createdAt.AddMinutes(DeterministicRange(seed, 4, 8, 45)));
        var followUpMinutes = status switch
        {
            "Assigned" => DeterministicRange(seed, 8, 18, 95),
            "InProgress" => DeterministicRange(seed, 8, 55, 220),
            _ => DeterministicRange(seed, 8, 70, 260)
        };
        var lastUpdatedAtCandidate = TrimUtcToMinute(reviewedAt.AddMinutes(followUpMinutes));
        var lastUpdatedAt = MinUtc(lastUpdatedAtCandidate, anchorUtc.AddMinutes(-1));
        if (lastUpdatedAt < reviewedAt)
        {
            lastUpdatedAt = reviewedAt;
        }

        return (createdAt, receivedAt, reviewedAt, lastUpdatedAt);
    }

    private static (DateTime CreatedAt, DateTime ReceivedAt, DateTime? ReviewedAt, DateTime LastUpdatedAt) BuildClampedHistoricalSosTimeline(
        DateTime createdAtCandidate,
        TimeSpan receivedOffset,
        TimeSpan? reviewedOffset,
        TimeSpan lastUpdatedOffset,
        DateTime anchorUtc)
    {
        var createdAt = ClampHistoricalUtc(createdAtCandidate, anchorUtc);
        var receivedAt = ClampHistoricalUtc(createdAtCandidate.Add(receivedOffset), createdAt, anchorUtc);
        DateTime? reviewedAt = reviewedOffset.HasValue
            ? ClampHistoricalUtc(createdAtCandidate.Add(reviewedOffset.Value), receivedAt, anchorUtc)
            : null;
        var lastUpdatedLowerBound = reviewedAt ?? receivedAt;
        var lastUpdatedAt = ClampHistoricalUtc(createdAtCandidate.Add(lastUpdatedOffset), lastUpdatedLowerBound, anchorUtc);
        return (createdAt, receivedAt, reviewedAt, lastUpdatedAt);
    }

    private static DateTime TrimUtcToMinute(DateTime value) =>
        new DateTime(value.Ticks - value.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);

    private static DateTime ClampHistoricalUtc(DateTime candidateUtc, DateTime anchorUtc) =>
        candidateUtc <= anchorUtc ? candidateUtc : anchorUtc;

    private static DateTime ClampHistoricalUtc(DateTime candidateUtc, DateTime floorUtc, DateTime anchorUtc)
    {
        var capped = ClampHistoricalUtc(candidateUtc, anchorUtc);
        return capped < floorUtc ? floorUtc : capped;
    }

    private static DateTime? ClampHistoricalUtc(DateTime? candidateUtc, DateTime floorUtc, DateTime anchorUtc) =>
        candidateUtc.HasValue ? ClampHistoricalUtc(candidateUtc.Value, floorUtc, anchorUtc) : null;

    private static DateTime MinUtc(DateTime left, DateTime right) => left <= right ? left : right;

    private static SeedArea Area(int index)
    {
        var areas = new[]
        {
            new SeedArea("HUE", "Thừa Thiên Huế", "Phú Hội", "Lê Lợi, Huế", 16.4637, 107.5962),
            new SeedArea("HUE", "Thừa Thiên Huế", "Hương Sơ", "Nguyễn Văn Linh, Huế", 16.4952, 107.5860),
            new SeedArea("DNG", "Đà Nẵng", "Hải Châu", "2 Tháng 9, Đà Nẵng", 16.0471, 108.2188),
            new SeedArea("QTR", "Quảng Trị", "Đông Hà", "Lê Duẩn, Đông Hà", 16.8175, 107.1003),
            new SeedArea("QNM", "Quảng Nam", "Tam Kỳ", "Hùng Vương, Tam Kỳ", 15.5736, 108.4740),
            new SeedArea("QNM", "Quảng Nam", "Hội An", "Cửa Đại, Hội An", 15.8801, 108.3380),
            new SeedArea("QNG", "Quảng Ngãi", "Trần Phú", "Quang Trung, Quảng Ngãi", 15.1214, 108.8044)
        };
        return areas[index % areas.Length];
    }

    private static IReadOnlyList<SeedCoordinate> GetCuratedBulkSosAnchors(SeedArea area)
    {
        return (area.Code, area.Ward) switch
        {
            ("HUE", "Phú Hội") =>
            [
                new SeedCoordinate(16.466942, 107.593184),
                new SeedCoordinate(16.465718, 107.599862),
                new SeedCoordinate(16.463981, 107.603104),
                new SeedCoordinate(16.461447, 107.598756),
                new SeedCoordinate(16.459832, 107.594227),
                new SeedCoordinate(16.458214, 107.601335),
                new SeedCoordinate(16.456973, 107.589684),
                new SeedCoordinate(16.468256, 107.588927)
            ],
            ("HUE", "Hương Sơ") =>
            [
                new SeedCoordinate(16.499812, 107.582644),
                new SeedCoordinate(16.498276, 107.589401),
                new SeedCoordinate(16.496145, 107.592338),
                new SeedCoordinate(16.493447, 107.588924),
                new SeedCoordinate(16.491318, 107.583571),
                new SeedCoordinate(16.489642, 107.590811),
                new SeedCoordinate(16.487925, 107.586374),
                new SeedCoordinate(16.500386, 107.586922)
            ],
            ("DNG", "Hải Châu") =>
            [
                new SeedCoordinate(16.050284, 108.214973),
                new SeedCoordinate(16.048617, 108.221556),
                new SeedCoordinate(16.046851, 108.225348),
                new SeedCoordinate(16.044902, 108.220217),
                new SeedCoordinate(16.042731, 108.216094),
                new SeedCoordinate(16.045588, 108.212347),
                new SeedCoordinate(16.051936, 108.219487),
                new SeedCoordinate(16.047934, 108.228204)
            ],
            ("QTR", "Đông Hà") =>
            [
                new SeedCoordinate(16.821476, 107.096412),
                new SeedCoordinate(16.820138, 107.103754),
                new SeedCoordinate(16.817462, 107.107118),
                new SeedCoordinate(16.814808, 107.102671),
                new SeedCoordinate(16.812943, 107.098384),
                new SeedCoordinate(16.815562, 107.094945),
                new SeedCoordinate(16.819947, 107.090853),
                new SeedCoordinate(16.823114, 107.100226)
            ],
            ("QNM", "Tam Kỳ") =>
            [
                new SeedCoordinate(15.577812, 108.469826),
                new SeedCoordinate(15.576203, 108.476915),
                new SeedCoordinate(15.573941, 108.480144),
                new SeedCoordinate(15.571225, 108.476492),
                new SeedCoordinate(15.569438, 108.472318),
                new SeedCoordinate(15.571987, 108.467834),
                new SeedCoordinate(15.575112, 108.463925),
                new SeedCoordinate(15.578431, 108.473781)
            ],
            ("QNM", "Hội An") =>
            [
                new SeedCoordinate(15.884216, 108.334882),
                new SeedCoordinate(15.882807, 108.341245),
                new SeedCoordinate(15.880352, 108.344627),
                new SeedCoordinate(15.877966, 108.340982),
                new SeedCoordinate(15.876184, 108.336771),
                new SeedCoordinate(15.878415, 108.332684),
                new SeedCoordinate(15.881924, 108.329415),
                new SeedCoordinate(15.885142, 108.338904)
            ],
            ("QNG", "Trần Phú") =>
            [
                new SeedCoordinate(15.125116, 108.800712),
                new SeedCoordinate(15.123728, 108.807194),
                new SeedCoordinate(15.121335, 108.810456),
                new SeedCoordinate(15.118914, 108.806973),
                new SeedCoordinate(15.117103, 108.802624),
                new SeedCoordinate(15.119684, 108.798935),
                new SeedCoordinate(15.122447, 108.795682),
                new SeedCoordinate(15.126024, 108.804173)
            ],
            _ => throw new InvalidOperationException($"Chưa cấu hình curated anchor cho area {area.Code}/{area.Ward}.")
        };
    }

    private static IReadOnlyList<SeedCoordinate> BuildClusterScatterPoints(SeedArea area, int clusterIndex, int count)
    {
        var anchors = GetCuratedBulkSosAnchors(area);
        var anchorSeed = StableGuid($"cluster-anchor-{clusterIndex}-{area.Code}-{area.Ward}");
        var anchorIndex = (DeterministicIndex(anchorSeed, anchors.Count) + clusterIndex % anchors.Count) % anchors.Count;
        var anchor = anchors[anchorIndex];

        var templates = count switch
        {
            1 => SinglePointClusterScatterTemplates,
            2 => TwoPointClusterScatterTemplates,
            3 => ThreePointClusterScatterTemplates,
            4 => FourPointClusterScatterTemplates,
            _ => throw new InvalidOperationException($"Không hỗ trợ {count} SOS trong một cluster demo.")
        };
        var templateSeed = StableGuid($"cluster-template-{clusterIndex}-{count}-{area.Code}-{area.Ward}");
        var templateIndex = (DeterministicIndex(templateSeed, templates.Length) + clusterIndex % templates.Length) % templates.Length;
        var template = templates[templateIndex];
        var latJitterAmplitude = count switch
        {
            1 => 0.00018,
            2 => 0.00024,
            _ => 0.00035
        };
        var lonJitterAmplitude = count switch
        {
            1 => 0.00022,
            2 => 0.00030,
            _ => 0.00042
        };

        return Enumerable.Range(0, count)
            .Select(pointIndex =>
            {
                var jitterSeed = StableGuid($"cluster-point-{clusterIndex}-{pointIndex}-{area.Code}-{area.Ward}");
                var latJitter = DeterministicRange(jitterSeed, 0, -latJitterAmplitude, latJitterAmplitude);
                var lonJitter = DeterministicRange(jitterSeed, 4, -lonJitterAmplitude, lonJitterAmplitude);
                return new SeedCoordinate(
                    anchor.Lat + template[pointIndex].Lat + latJitter,
                    anchor.Lon + template[pointIndex].Lon + lonJitter);
            })
            .ToArray();
    }

    private static int DeterministicIndex(Guid seed, int count)
    {
        var bytes = seed.ToByteArray();
        return (int)(BitConverter.ToUInt32(bytes, 0) % count);
    }

    private static double DeterministicRange(Guid seed, int byteOffset, double min, double max)
    {
        var bytes = seed.ToByteArray();
        var offset = Math.Clamp(byteOffset, 0, bytes.Length - sizeof(uint));
        var ratio = BitConverter.ToUInt32(bytes, offset) / (double)uint.MaxValue;
        return min + (max - min) * ratio;
    }

    private static readonly SeedCoordinate[][] SinglePointClusterScatterTemplates =
    [
        [new SeedCoordinate(0.00042, -0.00028)],
        [new SeedCoordinate(-0.00031, 0.00047)],
        [new SeedCoordinate(0.00018, 0.00039)],
        [new SeedCoordinate(-0.00046, -0.00012)]
    ];

    private static readonly SeedCoordinate[][] TwoPointClusterScatterTemplates =
    [
        [
            new SeedCoordinate(0.00074, -0.00041),
            new SeedCoordinate(-0.00028, 0.00063)
        ],
        [
            new SeedCoordinate(-0.00061, -0.00022),
            new SeedCoordinate(0.00047, 0.00058)
        ],
        [
            new SeedCoordinate(0.00032, -0.00079),
            new SeedCoordinate(-0.00054, 0.00035)
        ],
        [
            new SeedCoordinate(-0.00072, 0.00048),
            new SeedCoordinate(0.00026, -0.00057)
        ]
    ];

    private static readonly SeedCoordinate[][] ThreePointClusterScatterTemplates =
    [
        [
            new SeedCoordinate(0.0018, -0.0011),
            new SeedCoordinate(-0.0007, 0.0016),
            new SeedCoordinate(0.0003, -0.0022)
        ],
        [
            new SeedCoordinate(0.0012, 0.0017),
            new SeedCoordinate(-0.0014, -0.0006),
            new SeedCoordinate(0.0005, -0.0018)
        ],
        [
            new SeedCoordinate(0.0009, -0.0019),
            new SeedCoordinate(-0.0016, 0.0008),
            new SeedCoordinate(0.0017, 0.0011)
        ],
        [
            new SeedCoordinate(0.0015, 0.0006),
            new SeedCoordinate(-0.0009, -0.0017),
            new SeedCoordinate(-0.0018, 0.0014)
        ]
    ];

    private static readonly SeedCoordinate[][] FourPointClusterScatterTemplates =
    [
        [
            new SeedCoordinate(0.0019, -0.0012),
            new SeedCoordinate(-0.0006, 0.0017),
            new SeedCoordinate(0.0004, -0.0021),
            new SeedCoordinate(-0.0017, 0.0009)
        ],
        [
            new SeedCoordinate(0.0013, 0.0018),
            new SeedCoordinate(-0.0015, -0.0007),
            new SeedCoordinate(0.0006, -0.0019),
            new SeedCoordinate(-0.0004, 0.0022)
        ],
        [
            new SeedCoordinate(0.0021, 0.0005),
            new SeedCoordinate(-0.0008, -0.0018),
            new SeedCoordinate(-0.0019, 0.0012),
            new SeedCoordinate(0.0003, -0.0024)
        ],
        [
            new SeedCoordinate(0.0011, -0.0020),
            new SeedCoordinate(-0.0017, 0.0006),
            new SeedCoordinate(0.0018, 0.0014),
            new SeedCoordinate(-0.0005, 0.0020)
        ]
    ];

    private static (string Last, string First) VietnameseName(int index)
    {
        var lastNames = new[] { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý" };
        var firstNames = new[]
        {
            "Anh Tuấn", "Khánh Vy", "Minh Châu", "Quang Hải", "Thảo Nguyên", "Hoài Nam", "Thanh Hằng", "Đức Anh", "Mai Lan", "Gia Huy",
            "Hồng Nhung", "Bảo Trâm", "Văn Đức", "Thanh Tâm", "Nhật Minh", "Phương Linh", "Mạnh Hùng", "Diệu Anh", "Quốc Bảo", "Ngọc Hà"
        };
        return (lastNames[index % lastNames.Length], firstNames[index % firstNames.Length]);
    }

    private static string FullName(User user) => $"{user.LastName} {user.FirstName}".Trim();

    private static string Slug(string value)
    {
        var normalized = value.ToLowerInvariant()
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace("đ", "d", StringComparison.Ordinal);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch == '-')
            {
                builder.Append(ch);
            }
        }
        return builder.ToString();
    }

    private static IEnumerable<ServiceZone> ServiceZones(DateTime now)
        => ServiceZoneSeedData.CreateZones(now);

    private static IReadOnlyList<DocumentFileType> DocumentFileTypes(DateTime now) =>
    [
        new DocumentFileType
        {
            Id = 1,
            Code = "WATER_SAFETY_CERT",
            Name = "Chứng chỉ an toàn dưới nước",
            Description = "Chứng chỉ xác nhận khả năng bơi lội, sinh tồn và an toàn môi trường nước cơ bản.",
            IsActive = true,
            DocumentFileTypeCategoryId = 1,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 2,
            Code = "WATER_RESCUE_CERT",
            Name = "Chứng chỉ cứu hộ dưới nước",
            Description = "Chứng chỉ nghiệp vụ cứu hộ, cứu nạn chuyên nghiệp dưới nước, dòng chảy xiết.",
            IsActive = true,
            DocumentFileTypeCategoryId = 1,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 3,
            Code = "TECHNICAL_RESCUE_CERT",
            Name = "Chứng chỉ cứu hộ kỹ thuật",
            Description = "Chứng chỉ nghiệp vụ sử dụng thiết bị chuyên dụng, cứu hộ không gian hẹp, sập đổ, dùng dây thừng.",
            IsActive = true,
            DocumentFileTypeCategoryId = 1,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 4,
            Code = "DISASTER_RESPONSE_CERT",
            Name = "Chứng chỉ ứng phó thiên tai",
            Description = "Chứng chỉ hoàn thành khóa huấn luyện phản ứng nhanh, điều phối và ứng phó thảm họa/thiên tai.",
            IsActive = true,
            DocumentFileTypeCategoryId = 1,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 5,
            Code = "BASIC_FIRST_AID_CERT",
            Name = "Chứng chỉ Sơ cấp cứu cơ bản",
            Description = "Chứng chỉ hoàn thành các khóa đào tạo sơ cấp cứu ban đầu, hô hấp nhân tạo, dành cho tình nguyện viên và nhân viên y tế nền tảng.",
            IsActive = true,
            DocumentFileTypeCategoryId = 2,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 6,
            Code = "NURSING_PRACTICE_LICENSE",
            Name = "Chứng chỉ hành nghề Điều dưỡng",
            Description = "Giấy phép hành nghề điều dưỡng, y tá do cơ quan có thẩm quyền cấp, chứng minh năng lực thực hành lâm sàng và chăm sóc người bệnh.",
            IsActive = true,
            DocumentFileTypeCategoryId = 2,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 7,
            Code = "MOTORCYCLE_LICENSE",
            Name = "Giấy phép lái xe máy",
            Description = "Bằng lái xe mô tô 2 bánh (Hạng A1, A2...).",
            IsActive = true,
            DocumentFileTypeCategoryId = 3,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 8,
            Code = "CAR_TRUCK_LICENSE",
            Name = "Giấy phép lái xe ô tô / tải",
            Description = "Bằng lái xe ô tô, xe bán tải, xe tải hạng nặng (Hạng B1, B2, C, D...).",
            IsActive = true,
            DocumentFileTypeCategoryId = 3,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 9,
            Code = "OTHER",
            Name = "Khác",
            Description = "Khác",
            IsActive = true,
            DocumentFileTypeCategoryId = 4,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 10,
            Code = "PARAMEDIC_EMT_CERT",
            Name = "Chứng chỉ Cấp cứu ngoại viện",
            Description = "Chứng chỉ chuyên môn dành cho lực lượng cấp cứu tiền viện (115/EMT), chuyên gia xử lý chấn thương và duy trì sự sống trực tiếp tại hiện trường.",
            IsActive = true,
            DocumentFileTypeCategoryId = 2,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 11,
            Code = "MEDICAL_DOCTOR_LICENSE",
            Name = "Chứng chỉ hành nghề Bác sĩ",
            Description = "Giấy phép hành nghề khám, chữa bệnh cấp cho Bác sĩ. Thể hiện thẩm quyền cao nhất trong chẩn đoán, phân loại mức độ nguy kịch và ra y lệnh.",
            IsActive = true,
            DocumentFileTypeCategoryId = 2,
            CreatedAt = now,
            UpdatedAt = now
        },
        new DocumentFileType
        {
            Id = 12,
            Code = "INLAND_WATERWAY_LICENSE",
            Name = "Bằng lái phương tiện thủy",
            Description = "Chứng chỉ/Bằng lái phương tiện thủy nội địa dành cho người điều khiển Ca nô, xuồng máy có động cơ.",
            IsActive = true,
            DocumentFileTypeCategoryId = 3,
            CreatedAt = now,
            UpdatedAt = now
        }
    ];
}
