namespace RESQ.Application.UseCases.Identity.Commands.ReviewRescuerApplication
{
    public class ReviewRescuerApplicationRequestDto
    {
        /// <summary>
        /// ID của đơn đăng ký
        /// </summary>
        public int ApplicationId { get; set; }

        /// <summary>
        /// true = Duyệt, false = Từ chối
        /// </summary>
        public bool IsApproved { get; set; }

        /// <summary>
        /// Ghi chú của admin (lý do từ chối, etc.)
        /// </summary>
        public string? AdminNote { get; set; }
    }
}
