using FluentValidation.TestHelper;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;
using RESQ.Application.UseCases.Personnel.RescueTeams.DTOs;
using RESQ.Application.UseCases.Personnel.RescueTeams.Validators;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Tests.Application.UseCases.Personnel.Commands;

public class CreateRescueTeamCommandValidatorTests
{
    private readonly CreateRescueTeamCommandValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenNameIsEmpty()
    {
        var result = _validator.TestValidate(BuildCommand(name: ""));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_Fails_WhenNameExceeds255Characters()
    {
        var result = _validator.TestValidate(BuildCommand(name: new string('A', 256)));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_Fails_WhenTypeIsInvalidEnum()
    {
        var result = _validator.TestValidate(BuildCommand(type: (RescueTeamType)999));
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Validate_Fails_WhenAssemblyPointIdIsZero()
    {
        var result = _validator.TestValidate(BuildCommand(assemblyPointId: 0));
        result.ShouldHaveValidationErrorFor(x => x.AssemblyPointId);
    }

    [Fact]
    public void Validate_Fails_WhenManagedByIsEmpty()
    {
        var result = _validator.TestValidate(BuildCommand(managedBy: Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.ManagedBy);
    }

    [Fact]
    public void Validate_Fails_WhenMaxMembersIs5()
    {
        var result = _validator.TestValidate(BuildCommand(maxMembers: 5, memberCount: 5));
        result.ShouldHaveValidationErrorFor(x => x.MaxMembers);
    }

    [Fact]
    public void Validate_Fails_WhenMaxMembersIs9()
    {
        var result = _validator.TestValidate(BuildCommand(maxMembers: 9, memberCount: 9));
        result.ShouldHaveValidationErrorFor(x => x.MaxMembers);
    }

    [Fact]
    public void Validate_Fails_WhenMemberCountDoesNotMatchMaxMembers()
    {
        var result = _validator.TestValidate(BuildCommand(maxMembers: 6, memberCount: 4));
        result.ShouldHaveValidationErrorFor(x => x.Members);
    }

    [Fact]
    public void Validate_Fails_WhenAnyMemberEventIdIsInvalid()
    {
        var command = BuildCommand();
        command.Members[0].EventId = 0;

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Members);
    }

    [Fact]
    public void Validate_Fails_WhenMembersContainDuplicateRescuer()
    {
        var command = BuildCommand();
        command.Members[1].UserId = command.Members[0].UserId;

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Members);
    }

    [Fact]
    public void Validate_Fails_WhenNoLeaderInMembers()
    {
        var members = Enumerable.Range(0, 6)
            .Select(_ => new AddMemberRequestDto { UserId = Guid.NewGuid(), EventId = 100, IsLeader = false })
            .ToList();

        var command = new CreateRescueTeamCommand(
            "TestTeam", RescueTeamType.Rescue, 1, Guid.NewGuid(), 6, members);

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Members);
    }

    [Fact]
    public void Validate_Fails_WhenMultipleLeadersInMembers()
    {
        var members = Enumerable.Range(0, 6)
            .Select(i => new AddMemberRequestDto { UserId = Guid.NewGuid(), EventId = 100 + i, IsLeader = i < 2 })
            .ToList();

        var command = new CreateRescueTeamCommand(
            "TestTeam", RescueTeamType.Rescue, 1, Guid.NewGuid(), 6, members);

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Members);
    }

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        var result = _validator.TestValidate(BuildCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Validate_Passes_ForAllValidMaxMemberCounts(int maxMembers)
    {
        var result = _validator.TestValidate(BuildCommand(maxMembers: maxMembers, memberCount: maxMembers));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(RescueTeamType.Rescue)]
    [InlineData(RescueTeamType.Medical)]
    [InlineData(RescueTeamType.Transportation)]
    [InlineData(RescueTeamType.Mixed)]
    public void Validate_Passes_ForAllValidTeamTypes(RescueTeamType type)
    {
        var result = _validator.TestValidate(BuildCommand(type: type));
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateRescueTeamCommand BuildCommand(
        string name = "Đội cứu hộ Alpha",
        RescueTeamType type = RescueTeamType.Rescue,
        int assemblyPointId = 1,
        Guid? managedBy = null,
        int maxMembers = 6,
        int? memberCount = null)
    {
        var count = memberCount ?? maxMembers;
        var members = Enumerable.Range(0, count)
            .Select(i => new AddMemberRequestDto
            {
                UserId = Guid.NewGuid(),
                EventId = 100 + i,
                IsLeader = i == 0
            })
            .ToList();

        return new CreateRescueTeamCommand(
            name, type, assemblyPointId, managedBy ?? Guid.NewGuid(), maxMembers, members);
    }
}
