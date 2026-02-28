using MediatR;
using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

namespace RESQ.Application.UseCases.Identity.Commands.ReplaceRescuerDocuments
{
    public record ReplaceRescuerDocumentsCommand(
        Guid UserId,
        List<DocumentDto> Documents
    ) : IRequest<ReplaceRescuerDocumentsResponse>;
}
