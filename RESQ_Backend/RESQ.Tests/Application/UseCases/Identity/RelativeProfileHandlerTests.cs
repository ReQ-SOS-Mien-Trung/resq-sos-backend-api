using MediatR;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Commands.CreateRelativeProfile;
using RESQ.Application.UseCases.Identity.Commands.DeleteRelativeProfile;
using RESQ.Application.UseCases.Identity.Commands.SyncRelativeProfiles;
using RESQ.Application.UseCases.Identity.Commands.UpdateRelativeProfile;
using RESQ.Application.UseCases.Identity.Queries.GetRelativeProfiles;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Tests.Application.UseCases.Identity;

/// <summary>
/// Tests for RelativeProfile CRUD + Sync + Normalizer.
/// </summary>
public sealed class RelativeProfileHandlerTests
{
    // ──────────────────────── GetRelativeProfiles ────────────────────────

    [Fact]
    public async Task GetRelativeProfiles_ReturnsDeserializedProfiles()
    {
        var userId = Guid.NewGuid();
        var profile = new UserRelativeProfileModel
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = "Mom",
            PersonType = "adult",
            RelationGroup = "family",
            TagsJson = "[\"elderly\",\"diabetic\"]",
            MedicalProfileJson = "{\"blood\":\"O+\"}",
            ProfileUpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var repo = new StubRelativeProfileRepository([profile]);
        var handler = new GetRelativeProfilesQueryHandler(repo);

        var res = await handler.Handle(new GetRelativeProfilesQuery(userId), CancellationToken.None);

        Assert.Single(res);
        Assert.Equal("Mom", res[0].DisplayName);
        Assert.Equal(["elderly", "diabetic"], res[0].Tags);
    }

    [Fact]
    public async Task GetRelativeProfiles_EmptyList_ReturnsEmpty()
    {
        var repo = new StubRelativeProfileRepository([]);
        var handler = new GetRelativeProfilesQueryHandler(repo);

        var res = await handler.Handle(new GetRelativeProfilesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(res);
    }

    // ──────────────────────── CreateRelativeProfile ────────────────────────

    [Fact]
    public async Task CreateRelativeProfile_Success_NormalizesAndReturnsProfile()
    {
        var repo = new StubRelativeProfileRepository([]);
        var handler = new CreateRelativeProfileCommandHandler(repo);

        var cmd = new CreateRelativeProfileCommand(
            UserId: Guid.NewGuid(),
            ClientId: null,
            DisplayName: "  Dad  ",
            PhoneNumber: "  0901234567  ",
            PersonType: " adult ",
            RelationGroup: " family ",
            Tags: ["elderly", "  elderly  ", "ELDERLY", "diabetic"],
            MedicalBaselineNote: "  note  ",
            SpecialNeedsNote: "   ",       // empty → null
            SpecialDietNote: null,
            Gender: " male ",
            MedicalProfileJson: "{\"blood\":\"A+\"}",
            UpdatedAt: null);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("Dad", res.DisplayName);          // trimmed
        Assert.Equal("0901234567", res.PhoneNumber);   // trimmed
        Assert.Equal("adult", res.PersonType);          // trimmed
        Assert.Equal("family", res.RelationGroup);      // trimmed
        Assert.Equal("note", res.MedicalBaselineNote);  // trimmed
        Assert.Null(res.SpecialNeedsNote);              // whitespace → null
        Assert.Null(res.SpecialDietNote);               // null stays null
        Assert.Equal("male", res.Gender);               // trimmed

        // Tags: deduped (case-insensitive), sorted ordinally
        Assert.Equal(["diabetic", "elderly"], res.Tags);
    }

    [Fact]
    public async Task CreateRelativeProfile_ClientIdProvided_UsesClientId()
    {
        var clientId = Guid.NewGuid();
        var repo = new StubRelativeProfileRepository([]);
        var handler = new CreateRelativeProfileCommandHandler(repo);

        var cmd = new CreateRelativeProfileCommand(
            UserId: Guid.NewGuid(),
            ClientId: clientId,
            DisplayName: "Sister",
            PhoneNumber: null,
            PersonType: "adult",
            RelationGroup: "family",
            Tags: null,
            MedicalBaselineNote: null,
            SpecialNeedsNote: null,
            SpecialDietNote: null,
            Gender: null,
            MedicalProfileJson: null,
            UpdatedAt: null);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(clientId, res.Id);
    }

    // ──────────────────────── UpdateRelativeProfile ────────────────────────

    [Fact]
    public async Task UpdateRelativeProfile_Success_UpdatesAndNormalizes()
    {
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var existing = new UserRelativeProfileModel
        {
            Id = profileId,
            UserId = userId,
            DisplayName = "Old",
            PersonType = "child",
            RelationGroup = "family",
            TagsJson = "[]",
            MedicalProfileJson = "{}",
            ProfileUpdatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var repo = new StubRelativeProfileRepository([existing]);
        var handler = new UpdateRelativeProfileCommandHandler(repo);

        var cmd = new UpdateRelativeProfileCommand(
            UserId: userId,
            ProfileId: profileId,
            DisplayName: "  Updated  ",
            PhoneNumber: null,
            PersonType: "adult",
            RelationGroup: "friend",
            Tags: ["tag1"],
            MedicalBaselineNote: null,
            SpecialNeedsNote: null,
            SpecialDietNote: null,
            Gender: null,
            MedicalProfileJson: null,
            UpdatedAt: null);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("Updated", res.DisplayName);
        Assert.Equal("adult", res.PersonType);
        Assert.Equal(["tag1"], res.Tags);
    }

    [Fact]
    public async Task UpdateRelativeProfile_NotFound_ThrowsNotFoundException()
    {
        var repo = new StubRelativeProfileRepository([]);
        var handler = new UpdateRelativeProfileCommandHandler(repo);

        var cmd = new UpdateRelativeProfileCommand(
            UserId: Guid.NewGuid(),
            ProfileId: Guid.NewGuid(),
            DisplayName: "X",
            PhoneNumber: null,
            PersonType: "adult",
            RelationGroup: "family",
            Tags: null,
            MedicalBaselineNote: null,
            SpecialNeedsNote: null,
            SpecialDietNote: null,
            Gender: null,
            MedicalProfileJson: null,
            UpdatedAt: null);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── DeleteRelativeProfile ────────────────────────

    [Fact]
    public async Task DeleteRelativeProfile_Success_Deletes()
    {
        var userId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var existing = new UserRelativeProfileModel
        {
            Id = profileId,
            UserId = userId,
            DisplayName = "ToDelete",
            PersonType = "adult",
            RelationGroup = "family",
            TagsJson = "[]",
            MedicalProfileJson = "{}",
            ProfileUpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var repo = new StubRelativeProfileRepository([existing]);
        var handler = new DeleteRelativeProfileCommandHandler(repo);

        var cmd = new DeleteRelativeProfileCommand(UserId: userId, ProfileId: profileId);
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(Unit.Value, res);
        Assert.True(repo.DeletedIds.Contains((profileId, userId)));
    }

    [Fact]
    public async Task DeleteRelativeProfile_NotFound_ThrowsNotFoundException()
    {
        var repo = new StubRelativeProfileRepository([]);
        var handler = new DeleteRelativeProfileCommandHandler(repo);

        var cmd = new DeleteRelativeProfileCommand(UserId: Guid.NewGuid(), ProfileId: Guid.NewGuid());
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── SyncRelativeProfiles ────────────────────────

    [Fact]
    public async Task SyncRelativeProfiles_Success_ReturnsCounts()
    {
        var userId = Guid.NewGuid();
        var repo = new StubRelativeProfileRepository([])
        {
            ReplaceResult = (2, 1, 0)
        };
        var handler = new SyncRelativeProfilesCommandHandler(repo);

        var cmd = new SyncRelativeProfilesCommand(
            UserId: userId,
            Profiles:
            [
                new SyncProfileItemDto
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Profile1",
                    PersonType = "adult",
                    RelationGroup = "family",
                    UpdatedAt = DateTime.UtcNow
                },
                new SyncProfileItemDto
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Profile2",
                    PersonType = "child",
                    RelationGroup = "family",
                    UpdatedAt = DateTime.UtcNow
                }
            ]);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(2, res.CreatedCount);
        Assert.Equal(1, res.UpdatedCount);
        Assert.Equal(0, res.DeletedCount);
    }

    [Fact]
    public async Task SyncRelativeProfiles_DuplicateIds_ThrowsBadRequestException()
    {
        var repo = new StubRelativeProfileRepository([]);
        var handler = new SyncRelativeProfilesCommandHandler(repo);

        var duplicateId = Guid.NewGuid();
        var cmd = new SyncRelativeProfilesCommand(
            UserId: Guid.NewGuid(),
            Profiles:
            [
                new SyncProfileItemDto { Id = duplicateId, DisplayName = "A", PersonType = "adult", RelationGroup = "family", UpdatedAt = DateTime.UtcNow },
                new SyncProfileItemDto { Id = duplicateId, DisplayName = "B", PersonType = "child", RelationGroup = "friend", UpdatedAt = DateTime.UtcNow }
            ]);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── RelativeProfileNormalizer ────────────────────────

    [Fact]
    public void NormalizeTags_DeduplicatesCaseInsensitiveAndSortsOrdinally()
    {
        var result = RelativeProfileNormalizer.NormalizeTags(["ZZZ", "aaa", "AAA", "zzz", "bbb"]);
        // Dedup case-insensitively: keeps first occurrence per lower group, then sort ordinally
        var deserialized = RelativeProfileNormalizer.DeserializeTags(result);
        Assert.Equal(["ZZZ", "aaa", "bbb"], deserialized);
    }

    [Fact]
    public void NormalizeTags_NullInput_ReturnsEmptyArray()
    {
        var result = RelativeProfileNormalizer.NormalizeTags(null);
        Assert.Equal("[]", result);
    }

    [Fact]
    public void NormalizeTags_TrimsAndRemovesBlanks()
    {
        var result = RelativeProfileNormalizer.NormalizeTags(["  tag1  ", "  ", "", "tag2"]);
        var deserialized = RelativeProfileNormalizer.DeserializeTags(result);
        Assert.Equal(["tag1", "tag2"], deserialized);
    }

    [Fact]
    public void Normalize_TrimsStringsAndConvertsEmptyToNull()
    {
        var (displayName, phoneNumber, personType, relationGroup, _, medNote, specialNeeds, diet, gender, medJson) =
            RelativeProfileNormalizer.Normalize(
                "  Name  ",
                "   ",          // empty → null
                " type ",
                " group ",
                null,
                "  note  ",
                "",             // empty → null
                null,
                " male ",
                null);          // null → "{}"

        Assert.Equal("Name", displayName);
        Assert.Null(phoneNumber);
        Assert.Equal("type", personType);
        Assert.Equal("group", relationGroup);
        Assert.Equal("note", medNote);
        Assert.Null(specialNeeds);
        Assert.Null(diet);
        Assert.Equal("male", gender);
        Assert.Equal("{}", medJson);
    }

    [Fact]
    public void DeserializeTags_InvalidJson_ReturnsEmptyList()
    {
        var result = RelativeProfileNormalizer.DeserializeTags("not-json");
        Assert.Empty(result);
    }

    [Fact]
    public void DeserializeMedicalProfile_EmptyBraces_ReturnsNull()
    {
        var result = RelativeProfileNormalizer.DeserializeMedicalProfile("{}");
        Assert.Null(result);
    }

    [Fact]
    public void DeserializeMedicalProfile_ValidJson_ReturnsObject()
    {
        var result = RelativeProfileNormalizer.DeserializeMedicalProfile("{\"blood\":\"A+\"}");
        Assert.NotNull(result);
    }

    // ──────────────────────── stubs ────────────────────────

    private sealed class StubRelativeProfileRepository : IRelativeProfileRepository
    {
        private readonly List<UserRelativeProfileModel> _profiles;
        public List<(Guid ProfileId, Guid UserId)> DeletedIds { get; } = [];
        public (int Created, int Updated, int Deleted) ReplaceResult { get; set; } = (0, 0, 0);

        public StubRelativeProfileRepository(List<UserRelativeProfileModel> profiles)
        {
            _profiles = new List<UserRelativeProfileModel>(profiles);
        }

        public Task<List<UserRelativeProfileModel>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(_profiles.Where(p => p.UserId == userId).ToList());

        public Task<UserRelativeProfileModel?> GetByIdAsync(Guid profileId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(_profiles.FirstOrDefault(p => p.Id == profileId && p.UserId == userId));

        public Task<UserRelativeProfileModel> CreateAsync(UserRelativeProfileModel model, CancellationToken ct = default)
        {
            _profiles.Add(model);
            return Task.FromResult(model);
        }

        public Task<UserRelativeProfileModel> UpdateAsync(UserRelativeProfileModel model, CancellationToken ct = default)
        {
            var idx = _profiles.FindIndex(p => p.Id == model.Id);
            if (idx >= 0) _profiles[idx] = model;
            return Task.FromResult(model);
        }

        public Task DeleteAsync(Guid profileId, Guid userId, CancellationToken ct = default)
        {
            DeletedIds.Add((profileId, userId));
            _profiles.RemoveAll(p => p.Id == profileId && p.UserId == userId);
            return Task.CompletedTask;
        }

        public Task<(int Created, int Updated, int Deleted)> ReplaceAllForUserAsync(Guid userId, IList<UserRelativeProfileModel> profiles, CancellationToken ct = default)
        {
            _profiles.RemoveAll(p => p.UserId == userId);
            _profiles.AddRange(profiles);
            return Task.FromResult(ReplaceResult);
        }
    }
}
