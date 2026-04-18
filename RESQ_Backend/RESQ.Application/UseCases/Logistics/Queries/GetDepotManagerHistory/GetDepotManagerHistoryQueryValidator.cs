using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotManagerHistory;

public class GetDepotManagerHistoryQueryValidator : AbstractValidator<GetDepotManagerHistoryQuery>
{
    public GetDepotManagerHistoryQueryValidator()
    {
        RuleFor(x => x.DepotId)
            .GreaterThan(0).WithMessage("Id kho phải lớn hơn 0.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Số trang phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage("Kích thước trang phải lớn hơn hoặc bằng 1.");
    }
}
