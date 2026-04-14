namespace RESQ.Application.UseCases.Identity.Queries.GetCurrentUser
{
    public class RescuerScoreDto
    {
        public decimal ResponseTimeScore { get; set; }
        public decimal RescueEffectivenessScore { get; set; }
        public decimal DecisionHandlingScore { get; set; }
        public decimal SafetyMedicalSkillScore { get; set; }
        public decimal TeamworkCommunicationScore { get; set; }
        public decimal OverallAverageScore { get; set; }
        public int EvaluationCount { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class RescuerDocumentDto
    {
        public int Id { get; set; }
        public int? ApplicationId { get; set; }
        public string? FileUrl { get; set; }
        public int? FileTypeId { get; set; }
        public string? FileTypeCode { get; set; }
        public string? FileTypeName { get; set; }
        public DateTime? UploadedAt { get; set; }
    }

    public class GetCurrentUserResponse
    {
        public Guid Id { get; set; }
        public int? RoleId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string? RescuerType { get; set; }
        public string? Email { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsEligibleRescuer { get; set; }
        public int RescuerStep { get; set; }
        public string? AvatarUrl { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public List<RescuerDocumentDto> RescuerApplicationDocuments { get; set; } = [];
        public List<string> Permissions { get; set; } = [];
        public List<RESQ.Application.Services.ManagedDepotDto> ManagedDepots { get; set; } = [];
        public RescuerScoreDto? RescuerScore { get; set; }
    }
}
