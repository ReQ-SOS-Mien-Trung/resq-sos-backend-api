using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.FirebasePhoneLogin;

public record FirebasePhoneLoginCommand(string IdToken) : IRequest<FirebasePhoneLoginResponse>;
