namespace RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile
{
    public class UpdateRescuerProfileRequestDto
    {
        public string FirstName { get; set; } = null!;
        public string? LastName { get; set; }
        public string Phone { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? Ward { get; set; }
        public string? District { get; set; }
        public string City { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
