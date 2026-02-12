namespace RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication
{
    public class SubmitRescuerApplicationRequestDto
    {
        /// <summary>
        /// Loại rescuer: Volunteer, Professional, Medical, etc.
        /// </summary>
        public string RescuerType { get; set; } = null!;

        /// <summary>
        /// Họ và tên đầy đủ
        /// </summary>
        public string FullName { get; set; } = null!;

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
        /// Tỉnh/Thành phố
        /// </summary>
        public string? City { get; set; }

        /// <summary>
        /// Ghi chú/mô tả thêm về kinh nghiệm, kỹ năng
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// Danh sách URL tài liệu chứng minh (CMND, bằng cấp, chứng chỉ...) - đã upload lên cloud
        /// </summary>
        public List<DocumentDto>? Documents { get; set; }
    }

    public class DocumentDto
    {
        /// <summary>
        /// URL của tài liệu đã upload lên cloud
        /// </summary>
        public string FileUrl { get; set; } = null!;

        /// <summary>
        /// Loại file: PDF, JPEG, PNG, DOC, etc.
        /// </summary>
        public string? FileType { get; set; }
    }
}
