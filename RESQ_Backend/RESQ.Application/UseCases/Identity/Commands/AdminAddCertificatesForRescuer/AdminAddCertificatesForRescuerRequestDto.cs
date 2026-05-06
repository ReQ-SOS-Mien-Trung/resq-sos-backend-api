using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

namespace RESQ.Application.UseCases.Identity.Commands.AdminAddCertificatesForRescuer
{
    public class AdminAddCertificatesForRescuerRequestDto
    {
        public List<DocumentDto> Documents { get; set; } = new();
    }
}
