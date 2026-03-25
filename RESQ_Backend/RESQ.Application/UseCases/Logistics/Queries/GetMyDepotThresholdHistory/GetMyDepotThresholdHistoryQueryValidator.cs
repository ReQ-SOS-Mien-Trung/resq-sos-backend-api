using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyDepotThresholdHistory;

public class GetMyDepotThresholdHistoryQueryValidator : AbstractValidator<GetMyDepotThresholdHistoryQuery>
{
    public GetMyDepotThresholdHistoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("pageNumber phải >= 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("pageSize phải trong khoảng 1-100.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).When(x => x.CategoryId.HasValue)
            .WithMessage("categoryId phải > 0.");

        RuleFor(x => x.ItemModelId)
            .GreaterThan(0).When(x => x.ItemModelId.HasValue)
            .WithMessage("itemModelId phải > 0.");
    }
}
