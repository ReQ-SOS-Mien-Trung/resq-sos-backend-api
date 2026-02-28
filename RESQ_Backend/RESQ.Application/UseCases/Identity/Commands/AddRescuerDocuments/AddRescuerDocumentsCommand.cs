using MediatR;
using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

namespace RESQ.Application.UseCases.Identity.Commands.AddRescuerDocuments
{
    public record AddRescuerDocumentsCommand(
        Guid UserId,
        List<DocumentDto> Documents
    ) : IRequest<AddRescuerDocumentsResponse>;
}
