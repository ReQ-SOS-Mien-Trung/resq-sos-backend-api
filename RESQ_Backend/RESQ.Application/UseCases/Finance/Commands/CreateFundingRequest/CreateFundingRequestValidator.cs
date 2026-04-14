using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;

public class CreateFundingRequestValidator : AbstractValidator<CreateFundingRequestCommand>
{
    public CreateFundingRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Danh sách v?t ph?m không du?c d? tr?ng.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemName)
                .NotEmpty().WithMessage("Tên v?t ph?m không du?c d? tr?ng.");
            item.RuleFor(i => i.CategoryCode)
                .NotEmpty().WithMessage("Mã danh m?c không du?c d? tr?ng.");
            item.RuleFor(i => i.ItemType)
                .NotEmpty().WithMessage("Lo?i v?t ph?m không du?c d? tr?ng.");
            item.RuleFor(i => i.TargetGroup)
                .NotEmpty().WithMessage("Nhóm d?i tu?ng không du?c d? tr?ng.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("S? lu?ng ph?i l?n hon 0.");
            item.RuleFor(i => i.UnitPrice)
                .GreaterThan(0).WithMessage("Ðon giá ph?i l?n hon 0.");
            item.RuleFor(i => i.VolumePerUnit)
                .GreaterThanOrEqualTo(0).WithMessage("Th? tích m?i don v? không du?c âm.");
            item.RuleFor(i => i.WeightPerUnit)
                .GreaterThanOrEqualTo(0).WithMessage("Cân n?ng m?i don v? không du?c âm.");
        });

        RuleFor(x => x.RequestedBy)
            .NotEmpty().WithMessage("Ngu?i yêu c?u không h?p l?.");
    }
}

