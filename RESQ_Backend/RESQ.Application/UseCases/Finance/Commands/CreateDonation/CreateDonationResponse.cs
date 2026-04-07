using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation
{
    public class CreateDonationResponse
    {
        public int DonationId { get; set; }
        public string CheckoutUrl { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        /// <summary>
        /// Mã đơn hàng được lưu trong DB sau khi gateway xử lý.
        /// PayOS: số Unix ms; ZaloPay: định dạng yyMMdd_xxxxxx.
        /// Dùng làm tham số cho <c>payos-verify?orderId=</c> hoặc <c>zalopay-verify?apptransid=</c>.
        /// </summary>
        public string OrderId { get; set; } = string.Empty;
    }
}
