using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.RescuerConsent
{
    public record RescuerConsentCommand(
        Guid UserId,
        bool AgreeMedicalFitness,
        bool AgreeLegalResponsibility,
        bool AgreeTraining,
        bool AgreeCodeOfConduct
    ) : IRequest<RescuerConsentResponse>;
}
