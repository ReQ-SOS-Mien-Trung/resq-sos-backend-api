using System.Linq;
using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.CreateRelativeProfile
{
    public class CreateRelativeProfileCommandValidator : AbstractValidator<CreateRelativeProfileCommand>
    {
        private static readonly string[] ValidPersonTypes = { "ADULT", "CHILD", "ELDERLY" };
        private static readonly string[] ValidRelationGroups = { "gia_dinh", "nha_noi", "nha_ngoai", "hang_xom", "ban_be", "khac" };

        public CreateRelativeProfileCommandValidator()
        {
            RuleFor(x => x.DisplayName)
                .NotEmpty().WithMessage("displayName là bắt buộc.")
                .MaximumLength(150).WithMessage("displayName tối đa 150 ký tự.");

            When(x => !string.IsNullOrEmpty(x.PhoneNumber), () =>
                RuleFor(x => x.PhoneNumber)
                    .MaximumLength(20).WithMessage("phoneNumber tối đa 20 ký tự."));

            RuleFor(x => x.PersonType)
                .NotEmpty().WithMessage("personType là bắt buộc.")
                .Must(v => ValidPersonTypes.Contains(v))
                .WithMessage("personType phải là một trong: ADULT, CHILD, ELDERLY.");

            RuleFor(x => x.RelationGroup)
                .NotEmpty().WithMessage("relationGroup là bắt buộc.")
                .Must(v => ValidRelationGroups.Contains(v))
                .WithMessage("relationGroup phải là một trong: gia_dinh, nha_noi, nha_ngoai, hang_xom, ban_be, khac.");

            When(x => x.Tags != null, () =>
            {
                RuleFor(x => x.Tags)
                    .Must(tags => tags!.Count <= 20)
                    .WithMessage("tags tối đa 20 mục.");

                RuleForEach(x => x.Tags)
                    .MaximumLength(50).WithMessage("Mỗi tag tối đa 50 ký tự.");
            });

            When(x => !string.IsNullOrEmpty(x.MedicalBaselineNote), () =>
                RuleFor(x => x.MedicalBaselineNote)
                    .MaximumLength(2000).WithMessage("medicalBaselineNote tối đa 2000 ký tự."));

            When(x => !string.IsNullOrEmpty(x.SpecialNeedsNote), () =>
                RuleFor(x => x.SpecialNeedsNote)
                    .MaximumLength(2000).WithMessage("specialNeedsNote tối đa 2000 ký tự."));

            When(x => !string.IsNullOrEmpty(x.SpecialDietNote), () =>
                RuleFor(x => x.SpecialDietNote)
                    .MaximumLength(2000).WithMessage("specialDietNote tối đa 2000 ký tự."));

            When(x => !string.IsNullOrEmpty(x.Gender), () =>
                RuleFor(x => x.Gender)
                    .Must(v => v == "MALE" || v == "FEMALE")
                    .WithMessage("gender phải là MALE hoặc FEMALE."));

            When(x => !string.IsNullOrEmpty(x.MedicalProfileJson), () =>
                RuleFor(x => x.MedicalProfileJson)
                    .MaximumLength(10000).WithMessage("medicalProfile tối đa 10000 ký tự."));
        }
    }
}
