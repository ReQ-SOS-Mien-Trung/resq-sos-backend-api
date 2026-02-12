using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.SubmitRescuerApplication
{
    public class SubmitRescuerApplicationCommandValidator : AbstractValidator<SubmitRescuerApplicationCommand>
    {
        public SubmitRescuerApplicationCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId là bắt buộc");

            RuleFor(x => x.RescuerType)
                .NotEmpty().WithMessage("Loại rescuer là bắt buộc")
                .MaximumLength(50).WithMessage("Loại rescuer không được vượt quá 50 ký tự");

            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Họ và tên là bắt buộc")
                .MaximumLength(255).WithMessage("Họ và tên không được vượt quá 255 ký tự");

            RuleFor(x => x.Phone)
                .MaximumLength(20).WithMessage("Số điện thoại không được vượt quá 20 ký tự")
                .Matches(@"^[0-9+\-\s()]*$").WithMessage("Số điện thoại không hợp lệ")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.Address)
                .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự");

            RuleFor(x => x.Ward)
                .MaximumLength(100).WithMessage("Phường/Xã không được vượt quá 100 ký tự");

            RuleFor(x => x.City)
                .MaximumLength(100).WithMessage("Tỉnh/Thành phố không được vượt quá 100 ký tự");

            RuleFor(x => x.Note)
                .MaximumLength(2000).WithMessage("Ghi chú không được vượt quá 2000 ký tự");

            RuleFor(x => x.Documents)
                .Must(docs => docs == null || docs.Count <= 10)
                .WithMessage("Số lượng tài liệu không được vượt quá 10");

            RuleForEach(x => x.Documents)
                .ChildRules(doc =>
                {
                    doc.RuleFor(d => d.FileUrl)
                        .NotEmpty().WithMessage("URL tài liệu là bắt buộc")
                        .MaximumLength(2000).WithMessage("URL không được vượt quá 2000 ký tự")
                        .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                        .WithMessage("URL không hợp lệ");

                    doc.RuleFor(d => d.FileType)
                        .MaximumLength(50).WithMessage("Loại file không được vượt quá 50 ký tự");
                })
                .When(x => x.Documents is not null && x.Documents.Count > 0);
        }
    }
}
