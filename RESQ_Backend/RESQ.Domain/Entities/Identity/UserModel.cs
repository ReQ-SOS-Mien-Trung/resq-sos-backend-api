namespace RESQ.Domain.Entities.Identity
{
    public class UserModel
    {
        public Guid Id { get; set; }
        public int? RoleId { get; set; }
        public string? FullName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string Password { get; set; } = null!; // hashed password
        public string? RescuerType { get; set; }
        public string? Email { get; set; }
        public bool IsEmailVerified { get; set; } = false;
        public bool IsOnboarded { get; set; } = false;
        public bool IsEligibleRescuer { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }

        // Refresh token stored on user record
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

        // Location (stored as latitude/longitude)
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Address fields
        public string? Address { get; set; }        // Số nhà, tên đường
        public string? Ward { get; set; }           // Phường/Xã
        public string? City { get; set; }           // Tỉnh/Thành phố

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Approval info
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}
