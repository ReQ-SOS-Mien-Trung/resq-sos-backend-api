using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.BanUser;

public record BanUserCommand(
    Guid TargetUserId,
    Guid AdminId,
    string? Reason
) : IRequest<BanUserResponse>;
