using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllActivities;

public class GetAllActivitiesQueryValidator : AbstractValidator<GetAllActivitiesQuery>
{
    private static readonly HashSet<string> ValidActivityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "COLLECT_SUPPLIES",
        "RETURN_SUPPLIES"
    };

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Planned",
        "OnGoing",
        "on_going",
        "Succeed",
        "PendingConfirmation",
        "pending_confirmation",
        "Failed",
        "Cancelled"
    };

    public GetAllActivitiesQueryValidator()
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

        RuleFor(x => x.ActivityType)
            .Must(type => type == null || ValidActivityTypes.Contains(type))
            .WithMessage("ActivityType phải là COLLECT_SUPPLIES hoặc RETURN_SUPPLIES.");

        RuleForEach(x => x.Statuses)
            .Must(status => ValidStatuses.Contains(status))
            .WithMessage("Status không hợp lệ. Các giá trị hợp lệ: Planned, OnGoing, Succeed, PendingConfirmation, Failed, Cancelled.");
    }
}
