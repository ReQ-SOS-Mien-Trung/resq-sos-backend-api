namespace RESQ.Application.UseCases.Identity.Commands.RescuerConsent
{
    public class RescuerConsentResponse
    {
        public Guid UserId { get; set; }
        public bool IsEligibleRescuer { get; set; }
        public DateTime AcceptedAt { get; set; }
        public string Message { get; set; } = null!;
    }
}
