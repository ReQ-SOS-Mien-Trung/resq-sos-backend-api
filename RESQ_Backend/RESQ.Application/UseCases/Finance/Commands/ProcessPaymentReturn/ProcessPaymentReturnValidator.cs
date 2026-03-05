using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessPaymentReturn;

public class ProcessPaymentReturnValidator : AbstractValidator<ProcessPaymentReturnCommand>
{
    public ProcessPaymentReturnValidator()
    {
        RuleFor(x => x.WebhookData)
            .NotNull().WithMessage("Dữ liệu webhook không được để trống.");

        When(x => x.WebhookData != null, () =>
        {
            RuleFor(x => x.WebhookData!.Code)
                .NotEmpty().WithMessage("Mã phản hồi từ cổng thanh toán không được để trống.");

            RuleFor(x => x.WebhookData!.Signature)
                .NotEmpty().WithMessage("Chữ ký xác thực (Signature) không được để trống.");

            RuleFor(x => x.WebhookData!.Data)
                .NotNull().WithMessage("Thông tin giao dịch chi tiết không được để trống.");

            When(x => x.WebhookData!.Data != null, () =>
            {
                RuleFor(x => x.WebhookData!.Data!.OrderCode)
                    .GreaterThan(0).WithMessage("Mã đơn hàng (OrderCode) không hợp lệ.");

                RuleFor(x => x.WebhookData!.Data!.Amount)
                    .GreaterThan(0).WithMessage("Số tiền thanh toán phải lớn hơn 0.");
                
                RuleFor(x => x.WebhookData!.Data!.PaymentLinkId)
                    .NotEmpty().WithMessage("Mã liên kết thanh toán (PaymentLinkId) không được để trống.");
            });
        });
    }
}