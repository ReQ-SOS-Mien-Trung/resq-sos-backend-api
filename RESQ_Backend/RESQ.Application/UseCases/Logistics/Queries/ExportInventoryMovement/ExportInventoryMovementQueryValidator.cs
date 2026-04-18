using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.ExportInventoryMovement;

public class ExportInventoryMovementQueryValidator : AbstractValidator<ExportInventoryMovementQuery>
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private static readonly int CurrentYear = DateTime.UtcNow.AddHours(7).Year;

    public ExportInventoryMovementQueryValidator()
    {
        // --- ByDateRange ------------------------------------------------------
        When(x => x.PeriodType == ExportPeriodType.ByDateRange, () =>
        {
            RuleFor(x => x.FromDate)
                .NotNull().WithMessage("Ngày bắt đầu không được để trống khi xuất theo khoảng ngày.");

            RuleFor(x => x.ToDate)
                .NotNull().WithMessage("Ngày kết thúc không được để trống khi xuất theo khoảng ngày.");

            RuleFor(x => x.ToDate)
                .Must(d => d!.Value <= Today)
                .When(x => x.ToDate.HasValue)
                .WithMessage("Ngày kết thúc không được là ngày trong tương lai.");

            RuleFor(x => x)
                .Must(x => x.FromDate!.Value <= x.ToDate!.Value)
                .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
                .WithMessage("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
        });

        // --- ByMonth ----------------------------------------------------------
        When(x => x.PeriodType == ExportPeriodType.ByMonth, () =>
        {
            RuleFor(x => x.Month)
                .NotNull().WithMessage("Tháng không được để trống khi xuất theo tháng.")
                .InclusiveBetween(1, 12).WithMessage("Tháng phải nằm trong khoảng từ 1 đến 12.");

            RuleFor(x => x.Year)
                .NotNull().WithMessage("Năm không được để trống khi xuất theo tháng.")
                .InclusiveBetween(2000, CurrentYear + 1).WithMessage($"Năm phải nằm trong khoảng từ 2000 đến {CurrentYear + 1}.");

            RuleFor(x => x)
                .Must(x => DateOnly.FromDateTime(new DateTime(x.Year!.Value, x.Month!.Value, 1)) <= Today)
                .When(x => x.Year.HasValue && x.Month.HasValue)
                .WithMessage("Không thể xuất báo cáo cho tháng trong tương lai.");
        });
    }
}
