using FluentValidation;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyUpcomingReturnActivities;

public class GetMyUpcomingReturnActivitiesQueryValidator : AbstractValidator<GetMyUpcomingReturnActivitiesQuery>
{
    public GetMyUpcomingReturnActivitiesQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is MissionActivityStatus.OnGoing or MissionActivityStatus.PendingConfirmation)
            .WithMessage("Status phải là OnGoing hoặc PendingConfirmation.");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Số trang phải lớn hơn 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Kích thước trang phải từ 1 đến 50.");
    }
}