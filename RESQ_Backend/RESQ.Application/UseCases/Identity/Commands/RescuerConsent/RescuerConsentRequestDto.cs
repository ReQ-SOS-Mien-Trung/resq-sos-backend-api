namespace RESQ.Application.UseCases.Identity.Commands.RescuerConsent
{
    public class RescuerConsentRequestDto
    {
        public bool AgreeMedicalFitness { get; set; }
        public bool AgreeLegalResponsibility { get; set; }
        public bool AgreeTraining { get; set; }
        public bool AgreeCodeOfConduct { get; set; }
    }
}
