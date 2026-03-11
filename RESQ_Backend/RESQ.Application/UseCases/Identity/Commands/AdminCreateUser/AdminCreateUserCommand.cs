using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.AdminCreateUser;

public record AdminCreateUserCommand(
    string? Phone,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Username,
    string Password,
    int RoleId,
    string? RescuerType,
    string? AvatarUrl,
    string? Address,
    string? Ward,
    string? Province,
    double? Latitude,
    double? Longitude,
    bool IsEmailVerified,
    bool IsOnboarded,
    bool IsEligibleRescuer,
    Guid? ApprovedBy,
    DateTime? ApprovedAt
) : IRequest<AdminCreateUserResponse>;
