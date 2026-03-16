using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;

public class SearchWarehousesByItemsQueryValidator : AbstractValidator<SearchWarehousesByItemsQuery>
{
    public SearchWarehousesByItemsQueryValidator()
    {
        RuleFor(x => x.ReliefItemIds)
            .NotNull().NotEmpty()
            .WithMessage("Vui lòng cung cấp ít nhất một mã vật tư (reliefItemIds).");

        When(x => x.ReliefItemIds != null && x.ReliefItemIds.Count > 0, () =>
        {
            RuleFor(x => x.ReliefItemIds!.Count)
                .LessThanOrEqualTo(50)
                .WithMessage("Số lượng mã vật tư tìm kiếm không được vượt quá 50.");

            RuleForEach(x => x.ReliefItemIds)
                .GreaterThan(0)
                .WithMessage("Mã vật tư phải là số nguyên dương.");

            RuleFor(x => x.ItemQuantities)
                .Must((query, dict) =>
                    dict.Count == 0 || dict.Count == (query.ReliefItemIds?.Count ?? 0))
                .WithMessage("Số lượng quantities phải bằng số lượng reliefItemIds (hoặc để trống để dùng mặc định là 1).");

            RuleFor(x => x.ItemQuantities)
                .Must(dict => dict.Values.All(v => v > 0))
                .WithMessage("Mỗi số lượng yêu cầu phải lớn hơn 0.");
        });

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Số trang phải lớn hơn 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Kích thước trang phải từ 1 đến 50.");
    }
}
