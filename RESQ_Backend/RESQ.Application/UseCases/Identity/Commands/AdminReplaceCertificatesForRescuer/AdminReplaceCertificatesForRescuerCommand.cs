using MediatR;
using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

namespace RESQ.Application.UseCases.Identity.Commands.AdminReplaceCertificatesForRescuer
{
    public record AdminReplaceCertificatesForRescuerCommand(
        Guid UserId,
        List<DocumentDto> Documents
    ) : IRequest<AdminReplaceCertificatesForRescuerResponse>;
}
