namespace RESQ.Domain.Entities.Personnel.ValueObjects;

public sealed record RescuerProfile(
    string? FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    string? AvatarUrl,
    string? RescuerType
);
