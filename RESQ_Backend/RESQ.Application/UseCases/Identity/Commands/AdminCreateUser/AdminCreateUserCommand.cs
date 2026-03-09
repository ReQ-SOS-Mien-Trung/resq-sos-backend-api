using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.AdminCreateUser;

public record AdminCreateUserCommand(
    string? Phone,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Username,
    string Password,
    int RoleId
) : IRequest<AdminCreateUserResponse>;
