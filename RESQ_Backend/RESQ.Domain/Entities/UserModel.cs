using System;

namespace RESQ.Domain.Models
{
    public class UserModel
    {
        public Guid Id { get; set; }
        public int? RoleId { get; set; }
        public string? FullName { get; set; }
        public string? Username { get; set; }
        public string? Phone { get; set; }
        public string Password { get; set; } = null!; // hashed password
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Refresh token stored on user record
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }
    }
}
