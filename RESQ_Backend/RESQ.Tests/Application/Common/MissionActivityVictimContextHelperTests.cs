using System.Text.Json;
using RESQ.Application.Common;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

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
                  "need_rescue": true,
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
        Assert.True(khoa.NeedRescue);
        Assert.True(khoa.IsInjured);
        Assert.Equal("SEVERE", khoa.Severity);
        Assert.Contains("FRACTURE", khoa.MedicalIssues);
        Assert.NotNull(context.RescueSummary);
        Assert.StartsWith("Khoa", context.RescueSummary);
    }

    [Fact]
    public void BuildContext_RescueSummary_OnlyIncludesNeedRescueTrueVictims()
    {
        var context = MissionActivityVictimContextHelper.BuildContext(
            """
            {
              "incident": {},
              "victims": [
                {
                  "person_id": "adult_1",
                  "person_type": "ADULT",
                  "custom_name": "Anh Minh",
                  "need_rescue": true
                },
                {
                  "person_id": "adult_2",
                  "person_type": "ADULT",
                  "custom_name": "Chi Lan",
                  "need_rescue": false,
                  "incident_status": {
                    "severity": "MODERATE",
                    "medical_issues": ["DIABETES"]
                  }
                },
                {
                  "person_id": "child_1",
                  "person_type": "CHILD",
                  "custom_name": "Be Nam"
                }
              ]
            }
            """);

        Assert.Equal(3, context.Victims.Count);
        Assert.NotNull(context.RescueSummary);
        Assert.StartsWith("Anh Minh", context.RescueSummary);
        Assert.DoesNotContain("Chi Lan", context.RescueSummary);
        Assert.False(context.Victims.Single(victim => victim.PersonId == "adult_2").NeedRescue);
        Assert.Null(context.Victims.Single(victim => victim.PersonId == "child_1").NeedRescue);
    }

    [Fact]
    public void StructuredDataDtos_SerializeNeedRescue()
    {
        var requestDto = JsonSerializer.Deserialize<StructuredDataDto>(
            """
            {
              "incident": {},
              "victims": [
                {
                  "person_id": "adult_2",
                  "need_rescue": false,
                  "incident_status": {
                    "medical_issues": ["BLOOD_PRESSURE"]
                  }
                }
              ]
            }
            """);

        var victim = Assert.Single(requestDto!.Victims!);
        Assert.False(victim.NeedRescue);

        var responseDto = SosStructuredDataParser.Parse(JsonSerializer.Serialize(requestDto));
        var responseVictim = Assert.Single(responseDto!.Victims!);
        Assert.False(responseVictim.NeedRescue);

        var serialized = JsonSerializer.Serialize(responseDto);
        Assert.Contains("\"need_rescue\":false", serialized);
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
