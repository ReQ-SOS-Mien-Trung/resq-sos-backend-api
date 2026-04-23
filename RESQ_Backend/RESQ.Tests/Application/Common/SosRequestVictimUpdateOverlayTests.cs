using RESQ.Application.Common;
using RESQ.Domain.Entities.Emergency;
using RESQ.Domain.Entities.Logistics.ValueObjects;

namespace RESQ.Tests.Application.Common;

public class SosRequestVictimUpdateOverlayTests
{
    [Fact]
    public void Apply_PreservesCoreContent_WhenVictimUpdateOmitsIt()
    {
        var sosRequest = SosRequestModel.Create(
            Guid.NewGuid(),
            new GeoLocation(10.762622, 106.660172),
            "Need rescue now",
            sosType: "Medical",
            structuredData: """{"victims":[{"name":"A"}]}""");
        sosRequest.Id = 7;

        var victimUpdate = new SosRequestVictimUpdateModel
        {
            SosRequestId = 7,
            RawMessage = string.Empty,
            StructuredData = null,
            SosType = null,
            UpdatedAt = DateTime.UtcNow
        };

        var effective = SosRequestVictimUpdateOverlay.Apply(sosRequest, victimUpdate);

        Assert.Equal(sosRequest.RawMessage, effective.RawMessage);
        Assert.Equal(sosRequest.StructuredData, effective.StructuredData);
        Assert.Equal(sosRequest.SosType, effective.SosType);
    }
}
