using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication
{
    public class SubmitRescuerApplicationRequestDto
    {
        /// <summary>
        /// Loại rescuer: Volunteer, Professional, Medical, etc.
        /// </summary>
        public string RescuerType { get; set; } = null!;

        /// <summary>
        /// Họ
        /// </summary>
        public string LastName { get; set; } = null!;

        /// <summary>
        /// Tên
        /// </summary>
        public string FirstName { get; set; } = null!;

        /// <summary>
        /// Số điện thoại
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Địa chỉ (số nhà, tên đường)
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Phường/Xã
        /// </summary>
        public string? Ward { get; set; }

        /// <summary>
        /// Quận/Huyện
        /// </summary>
        public string? District { get; set; }

        /// <summary>
        /// Tỉnh/Thành phố
        /// </summary>
        public string? Province { get; set; }

        /// <summary>
        /// Vĩ độ
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Kinh độ
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Ghi chú/mô tả thêm về kinh nghiệm, kỹ năng
        /// </summary>
        public string? Note { get; set; }
    }

    public class DocumentDto
    {
        /// <summary>
        /// URL của tài liệu đã upload lên cloud
        /// </summary>
        public string FileUrl { get; set; } = null!;

        /// <summary>
        /// Loại tài liệu: WATER_SAFETY_CERT, WATER_RESCUE_CERT, TECHNICAL_RESCUE_CERT,
        /// DISASTER_RESPONSE_CERT, BASIC_MEDICAL_CERT, ADVANCED_MEDICAL_LICENSE,
        /// LAND_VEHICLE_LICENSE, WATER_VEHICLE_LICENSE, OTHER
        /// </summary>
        public DocumentFileType FileType { get; set; } = DocumentFileType.OTHER;
    }
}
