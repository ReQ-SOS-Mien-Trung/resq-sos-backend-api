using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.ReviewRescuerApplication
{
    public class ReviewRescuerApplicationCommandValidator : AbstractValidator<ReviewRescuerApplicationCommand>
    {
        public ReviewRescuerApplicationCommandValidator()
        {
            RuleFor(x => x.ApplicationId)
                .GreaterThan(0).WithMessage("ApplicationId phải lớn hơn 0");

            RuleFor(x => x.ReviewedBy)
                .NotEmpty().WithMessage("ReviewedBy là bắt buộc");

            RuleFor(x => x.AdminNote)
                .MaximumLength(2000).WithMessage("Ghi chú không được vượt quá 2000 ký tự");
        }
    }
}
