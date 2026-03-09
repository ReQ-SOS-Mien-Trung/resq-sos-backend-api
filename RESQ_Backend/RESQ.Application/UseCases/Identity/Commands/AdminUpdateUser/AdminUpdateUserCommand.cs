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
    int? RoleId
) : IRequest<AdminUpdateUserResponse>;
