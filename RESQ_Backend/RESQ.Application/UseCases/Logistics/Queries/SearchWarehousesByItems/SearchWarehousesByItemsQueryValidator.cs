using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;

public class SearchWarehousesByItemsQueryValidator : AbstractValidator<SearchWarehousesByItemsQuery>
{
    public SearchWarehousesByItemsQueryValidator()
    {
        RuleFor(x => x.ItemModelIds)
            .NotNull().NotEmpty()
            .WithMessage("Vui lòng cung c?p ít nh?t m?t mã v?t ph?m (itemModelIds).");

        When(x => x.ItemModelIds != null && x.ItemModelIds.Count > 0, () =>
        {
            RuleFor(x => x.ItemModelIds!.Count)
                .LessThanOrEqualTo(50)
                .WithMessage("S? lu?ng mã v?t ph?m tìm ki?m không du?c vu?t quá 50.");

            RuleForEach(x => x.ItemModelIds)
                .GreaterThan(0)
                .WithMessage("Mã v?t ph?m ph?i là s? nguyên duong.");

            RuleFor(x => x.ItemQuantities)
                .Must((query, dict) =>
                    dict.Count == 0 || dict.Count == (query.ItemModelIds?.Count ?? 0))
                .WithMessage("S? lu?ng quantities ph?i b?ng s? lu?ng itemModelIds (ho?c d? tr?ng d? dùng m?c d?nh là 1).");

            RuleFor(x => x.ItemQuantities)
                .Must(dict => dict.Values.All(v => v > 0))
                .WithMessage("M?i s? lu?ng yêu c?u ph?i l?n hon 0.");
        });

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("S? trang ph?i l?n hon 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Kích thu?c trang ph?i t? 1 d?n 50.");
    }
}
