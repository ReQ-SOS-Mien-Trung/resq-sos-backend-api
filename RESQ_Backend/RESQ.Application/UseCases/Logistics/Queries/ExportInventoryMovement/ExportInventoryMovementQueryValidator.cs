using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.ExportInventoryMovement;

public class ExportInventoryMovementQueryValidator : AbstractValidator<ExportInventoryMovementQuery>
{
    private static readonly int CurrentYear = DateTime.UtcNow.AddHours(7).Year;
    private static readonly int CurrentMonth = DateTime.UtcNow.AddHours(7).Month;

    public ExportInventoryMovementQueryValidator()
    {
        // ─── ByMonth ──────────────────────────────────────────────────────────
        When(x => x.PeriodType == ExportPeriodType.ByMonth, () =>
        {
            RuleFor(x => x.Month)
                .NotNull().WithMessage("Tháng không được để trống khi xuất theo tháng.")
                .InclusiveBetween(1, 12).WithMessage("Tháng phải nằm trong khoảng từ 1 đến 12.");

            RuleFor(x => x.Year)
                .NotNull().WithMessage("Năm không được để trống khi xuất theo tháng.")
                .InclusiveBetween(2000, CurrentYear).WithMessage($"Năm phải nằm trong khoảng từ 2000 đến {CurrentYear}.");

            RuleFor(x => x)
                .Must(x => !IsFutureMonth(x.Year!.Value, x.Month!.Value))
                .When(x => x.Year.HasValue && x.Month.HasValue)
                .WithMessage("Không thể xuất báo cáo cho tháng trong tương lai.");
        });

        // ─── ByYear ───────────────────────────────────────────────────────────
        When(x => x.PeriodType == ExportPeriodType.ByYear, () =>
        {
            RuleFor(x => x.Year)
                .NotNull().WithMessage("Năm không được để trống khi xuất theo năm.")
                .InclusiveBetween(2000, CurrentYear).WithMessage($"Năm phải nằm trong khoảng từ 2000 đến {CurrentYear}.");
        });

        // ─── ByMonthRange ─────────────────────────────────────────────────────
        When(x => x.PeriodType == ExportPeriodType.ByMonthRange, () =>
        {
            RuleFor(x => x.FromMonth)
                .NotNull().WithMessage("Tháng bắt đầu không được để trống khi xuất theo khoảng tháng.")
                .InclusiveBetween(1, 12).WithMessage("Tháng bắt đầu phải nằm trong khoảng từ 1 đến 12.");

            RuleFor(x => x.FromYear)
                .NotNull().WithMessage("Năm bắt đầu không được để trống khi xuất theo khoảng tháng.")
                .InclusiveBetween(2000, CurrentYear).WithMessage($"Năm bắt đầu phải nằm trong khoảng từ 2000 đến {CurrentYear}.");

            RuleFor(x => x.ToMonth)
                .NotNull().WithMessage("Tháng kết thúc không được để trống khi xuất theo khoảng tháng.")
                .InclusiveBetween(1, 12).WithMessage("Tháng kết thúc phải nằm trong khoảng từ 1 đến 12.");

            RuleFor(x => x.ToYear)
                .NotNull().WithMessage("Năm kết thúc không được để trống khi xuất theo khoảng tháng.")
                .InclusiveBetween(2000, CurrentYear).WithMessage($"Năm kết thúc phải nằm trong khoảng từ 2000 đến {CurrentYear}.");

            RuleFor(x => x)
                .Must(x => !IsFutureMonth(x.ToYear!.Value, x.ToMonth!.Value))
                .When(x => x.ToYear.HasValue && x.ToMonth.HasValue)
                .WithMessage("Tháng kết thúc không được là tháng trong tương lai.");

            RuleFor(x => x)
                .Must(x => IsValidRange(x.FromYear!.Value, x.FromMonth!.Value, x.ToYear!.Value, x.ToMonth!.Value))
                .When(x => x.FromYear.HasValue && x.FromMonth.HasValue && x.ToYear.HasValue && x.ToMonth.HasValue)
                .WithMessage("Tháng kết thúc phải sau hoặc bằng tháng bắt đầu.");
        });
    }

    private static bool IsFutureMonth(int year, int month)
        => year > CurrentYear || (year == CurrentYear && month > CurrentMonth);

    private static bool IsValidRange(int fromYear, int fromMonth, int toYear, int toMonth)
        => toYear > fromYear || (toYear == fromYear && toMonth >= fromMonth);
}
