using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.RejectSupplyRequest;

public class RejectSupplyRequestCommandValidator : AbstractValidator<RejectSupplyRequestCommand>
{
    public RejectSupplyRequestCommandValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng cung cấp lý do từ chối.");
    }
}
