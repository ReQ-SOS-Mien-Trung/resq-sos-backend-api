namespace RESQ.Application.UseCases.Logistics.Queries.GetAllDepots.Depot
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
        
        // Changed from DepotManagerId (Guid) to Manager object
        public ManagerDto? Manager { get; set; }
        
        public DateTime? LastUpdatedAt { get; set; }
    }
}
