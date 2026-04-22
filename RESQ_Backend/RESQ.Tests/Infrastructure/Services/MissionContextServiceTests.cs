using System.Reflection;
using RESQ.Application.Services;
using RESQ.Infrastructure.Services;

namespace RESQ.Tests.Infrastructure.Services;

public class MissionContextServiceTests
{
    [Fact]
    public void ExtractNeededSupplies_InfersTransportationAndRescueEquipmentForFloodIsolationEvacuation()
    {
        var method = typeof(MissionContextService).GetMethod(
            "ExtractNeededSupplies",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var sosRequests = new List<SosRequestSummary>
        {
            new()
            {
                Id = 1,
                RawMessage = "Khu vuc ngap sau, bi co lap, can ca no de so tan nguoi mac ket.",
                StructuredData = """{"group_needs":{"water":{"duration":"2 ngay"}}}"""
            }
        };

        var needed = (HashSet<string>)method!.Invoke(null, [sosRequests])!;

        Assert.Contains("WATER", needed);
        Assert.Contains("TRANSPORTATION", needed);
        Assert.Contains("RESCUE_EQUIPMENT", needed);
    }
}
