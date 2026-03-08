using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn;

public class ProcessPayosPaymentReturnValidator : AbstractValidator<ProcessPayosPaymentReturnCommand>
{
    public ProcessPayosPaymentReturnValidator()
    {
        RuleFor(x => x.WebhookData)
            .NotNull().WithMessage("Dá»¯ liá»‡u webhook khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");

        When(x => x.WebhookData != null, () =>
        {
            RuleFor(x => x.WebhookData!.Code)
                .NotEmpty().WithMessage("MÃ£ pháº£n há»“i tá»« cá»•ng thanh toÃ¡n khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");

            RuleFor(x => x.WebhookData!.Signature)
                .NotEmpty().WithMessage("Chá»¯ kÃ½ xÃ¡c thá»±c (Signature) khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");

            RuleFor(x => x.WebhookData!.Data)
                .NotNull().WithMessage("ThÃ´ng tin giao dá»‹ch chi tiáº¿t khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");

            When(x => x.WebhookData!.Data != null, () =>
            {
                RuleFor(x => x.WebhookData!.Data!.OrderCode)
                    .GreaterThan(0).WithMessage("MÃ£ Ä‘Æ¡n hÃ ng (OrderCode) khÃ´ng há»£p lá»‡.");

                RuleFor(x => x.WebhookData!.Data!.Amount)
                    .GreaterThan(0).WithMessage("Sá»‘ tiá»n thanh toÃ¡n pháº£i lá»›n hÆ¡n 0.");
                
                RuleFor(x => x.WebhookData!.Data!.PaymentLinkId)
                    .NotEmpty().WithMessage("MÃ£ liÃªn káº¿t thanh toÃ¡n (PaymentLinkId) khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");
            });
        });
    }
}
