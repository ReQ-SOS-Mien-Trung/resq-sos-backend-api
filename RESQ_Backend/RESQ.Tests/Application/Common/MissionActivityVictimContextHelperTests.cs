using RESQ.Application.Common;

namespace RESQ.Tests.Application.Common;

public class MissionActivityVictimContextHelperTests
{
    [Fact]
    public void BuildContext_ParsesVictimsAndCreatesSummary()
    {
        var context = MissionActivityVictimContextHelper.BuildContext(
            """
            {
              "incident": {
                "people_count": {
                  "adult": 1,
                  "child": 1,
                  "elderly": 1
                }
              },
              "victims": [
                {
                  "person_id": "victim-1",
                  "person_type": "CHILD",
                  "custom_name": "Khoa",
                  "incident_status": {
                    "is_injured": true,
                    "severity": "SEVERE",
                    "medical_issues": ["FRACTURE", "BLEEDING"]
                  }
                },
                {
                  "person_id": "victim-2",
                  "person_type": "ADULT",
                  "custom_name": "Thảo"
                },
                {
                  "person_id": "victim-3",
                  "person_type": "ELDERLY",
                  "custom_name": "Chu"
                }
              ]
            }
            """,
            sosRequestId: 4);

        Assert.Equal("Khoa (trẻ em), Thảo (người lớn), Chu (người già)", context.Summary);
        Assert.Equal(3, context.Victims.Count);

        var khoa = Assert.Single(context.Victims, victim => victim.DisplayName == "Khoa");
        Assert.True(khoa.IsInjured);
        Assert.Equal("SEVERE", khoa.Severity);
        Assert.Contains("FRACTURE", khoa.MedicalIssues);
    }

    [Fact]
    public void ApplySummaryToDescription_ReplacesExistingVictimLineIdempotently()
    {
        var description = """
            Tiếp cận mái nhà và cố định cáng.
            Đối tượng cần hỗ trợ: Cũ.
            """;

        var updated = MissionActivityVictimContextHelper.ApplySummaryToDescription(
            "RESCUE",
            description,
            "Khoa (trẻ em)");

        Assert.Equal(
            """
            Tiếp cận mái nhà và cố định cáng.
            Đối tượng cần hỗ trợ: Khoa (trẻ em).
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            updated?.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildContext_SynthesizesAnonymousVictims_FromPeopleCount()
    {
        var context = MissionActivityVictimContextHelper.BuildContext(
            """
            {
              "incident": {
                "people_count": {
                  "adult": 2,
                  "child": 1
                }
              }
            }
            """,
            sosRequestId: 9);

        Assert.Equal(3, context.Victims.Count);
        Assert.Contains(context.Victims, victim => victim.DisplayName == "Người lớn #1");
        Assert.Contains(context.Victims, victim => victim.DisplayName == "Người lớn #2");
        Assert.Contains(context.Victims, victim => victim.DisplayName == "Trẻ em #1");
        Assert.Equal("Người lớn #1 (người lớn), Người lớn #2 (người lớn), Trẻ em #1 (trẻ em)", context.Summary);
    }
}
