using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyPickupHistoryActivities;

public class GetMyPickupHistoryActivitiesQueryValidator : AbstractValidator<GetMyPickupHistoryActivitiesQuery>
{
    public GetMyPickupHistoryActivitiesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Số trang phải lớn hơn 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Kích thước trang phải từ 1 đến 50.");

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value <= x.ToDate.Value)
            .WithMessage("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
    }
}
