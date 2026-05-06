using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

namespace RESQ.Application.UseCases.Identity.Commands.AdminReplaceCertificatesForRescuer
{
    public class AdminReplaceCertificatesForRescuerRequestDto
    {
        public List<DocumentDto> Documents { get; set; } = new();
    }
}
