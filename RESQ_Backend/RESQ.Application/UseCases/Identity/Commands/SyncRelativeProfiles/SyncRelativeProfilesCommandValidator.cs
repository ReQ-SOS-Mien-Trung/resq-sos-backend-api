using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.SyncRelativeProfiles
{
    public class SyncRelativeProfilesCommandValidator : AbstractValidator<SyncRelativeProfilesCommand>
    {
        private static readonly string[] ValidPersonTypes = { "ADULT", "CHILD", "ELDERLY" };
        private static readonly string[] ValidRelationGroups = { "gia_dinh", "nha_noi", "nha_ngoai", "hang_xom", "ban_be", "khac" };

        public SyncRelativeProfilesCommandValidator()
        {
            RuleFor(x => x.Profiles)
                .NotNull().WithMessage("profiles là bắt buộc.")
                .Must(profiles => profiles.Select(p => p.Id).Distinct().Count() == profiles.Count)
                .WithMessage("Payload sync chứa id trùng nhau.");

            RuleForEach(x => x.Profiles).ChildRules(item =>
            {
                item.RuleFor(p => p.Id)
                    .NotEqual(Guid.Empty).WithMessage("id không được là empty GUID.");

                item.RuleFor(p => p.DisplayName)
                    .NotEmpty().WithMessage("displayName là bắt buộc.")
                    .MaximumLength(150).WithMessage("displayName tối đa 150 ký tự.");

                item.When(p => !string.IsNullOrEmpty(p.PhoneNumber), () =>
                    item.RuleFor(p => p.PhoneNumber)
                        .MaximumLength(20).WithMessage("phoneNumber tối đa 20 ký tự."));

                item.RuleFor(p => p.PersonType)
                    .NotEmpty().WithMessage("personType là bắt buộc.")
                    .Must(v => ValidPersonTypes.Contains(v))
                    .WithMessage("personType phải là một trong: ADULT, CHILD, ELDERLY.");

                item.RuleFor(p => p.RelationGroup)
                    .NotEmpty().WithMessage("relationGroup là bắt buộc.")
                    .Must(v => ValidRelationGroups.Contains(v))
                    .WithMessage("relationGroup phải là một trong: gia_dinh, nha_noi, nha_ngoai, hang_xom, ban_be, khac.");

                item.When(p => p.Tags != null, () =>
                {
                    item.RuleFor(p => p.Tags)
                        .Must(tags => tags!.Count <= 20)
                        .WithMessage("tags tối đa 20 mục.");

                    item.RuleForEach(p => p.Tags)
                        .MaximumLength(50).WithMessage("Mỗi tag tối đa 50 ký tự.");
                });

                item.When(p => !string.IsNullOrEmpty(p.MedicalBaselineNote), () =>
                    item.RuleFor(p => p.MedicalBaselineNote)
                        .MaximumLength(2000).WithMessage("medicalBaselineNote tối đa 2000 ký tự."));

                item.When(p => !string.IsNullOrEmpty(p.SpecialNeedsNote), () =>
                    item.RuleFor(p => p.SpecialNeedsNote)
                        .MaximumLength(2000).WithMessage("specialNeedsNote tối đa 2000 ký tự."));

                item.When(p => !string.IsNullOrEmpty(p.SpecialDietNote), () =>
                    item.RuleFor(p => p.SpecialDietNote)
                        .MaximumLength(2000).WithMessage("specialDietNote tối đa 2000 ký tự."));
            });
        }
    }
}
