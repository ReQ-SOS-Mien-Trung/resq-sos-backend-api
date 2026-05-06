using MediatR;
using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

namespace RESQ.Application.UseCases.Identity.Commands.AdminAddCertificatesForRescuer
{
    public record AdminAddCertificatesForRescuerCommand(
        Guid UserId,
        List<DocumentDto> Documents
    ) : IRequest<AdminAddCertificatesForRescuerResponse>;
}
