using RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication;

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
        public string Province { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        /// <summary>
        /// Danh sách tài liệu chứng chỉ (URLs đã upload lên cloud).
        /// Nếu gửi list này, hệ thống sẽ thay thế toàn bộ documents cũ.
        /// Nếu null hoặc không gửi, documents cũ giữ nguyên.
        /// </summary>
        public List<DocumentDto>? Documents { get; set; }
    }
}
