using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.AdminUpdateUser;

public record AdminUpdateUserCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Username,
    string? Phone,
    string? Email,
    string? RescuerType,
    int? RoleId,
    string? AvatarUrl,
    string? Address,
    string? Ward,
    string? Province,
    double? Latitude,
    double? Longitude,
    bool? IsEmailVerified,
    bool? IsOnboarded,
    bool? IsEligibleRescuer,
    Guid? ApprovedBy,
    DateTime? ApprovedAt
) : IRequest<AdminUpdateUserResponse>;
