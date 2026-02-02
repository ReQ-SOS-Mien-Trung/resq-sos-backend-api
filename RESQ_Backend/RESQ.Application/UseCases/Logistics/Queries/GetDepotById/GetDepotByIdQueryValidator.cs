using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotById;

public class GetDepotByIdQueryValidator : AbstractValidator<GetDepotByIdQuery>
{
    public GetDepotByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id kho phải lớn hơn 0.");
    }
}