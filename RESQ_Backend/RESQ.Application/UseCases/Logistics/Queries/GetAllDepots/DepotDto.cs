namespace RESQ.Application.UseCases.Logistics.Queries.Depot
{
    public class DepotDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? Capacity { get; set; }
        public int? CurrentUtilization { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? DepotManagerId { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }
}
