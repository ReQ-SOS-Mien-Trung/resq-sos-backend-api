using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile
{
    public class UpdateRescuerProfileCommandValidator : AbstractValidator<UpdateRescuerProfileCommand>
    {
        public UpdateRescuerProfileCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId là bắt buộc");

            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Tên là bắt buộc")
                .MaximumLength(100).WithMessage("Tên không được vượt quá 100 ký tự");

            RuleFor(x => x.LastName)
                .MaximumLength(100).WithMessage("Họ không được vượt quá 100 ký tự");

            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("Số điện thoại là bắt buộc")
                .Matches(@"^(0|\+84)[3-9]\d{8}$")
                .WithMessage("Số điện thoại phải là số điện thoại Việt Nam hợp lệ (VD: 0912345678 hoặc +84912345678)");

            RuleFor(x => x.Address)
                .NotEmpty().WithMessage("Địa chỉ là bắt buộc")
                .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự");

            RuleFor(x => x.Ward)
                .MaximumLength(100).WithMessage("Phường/Xã không được vượt quá 100 ký tự");

            RuleFor(x => x.District)
                .MaximumLength(100).WithMessage("Quận/Huyện không được vượt quá 100 ký tự");

            RuleFor(x => x.Province)
                .NotEmpty().WithMessage("Tỉnh/Thành phố là bắt buộc")
                .MaximumLength(100).WithMessage("Tỉnh/Thành phố không được vượt quá 100 ký tự");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
                .WithMessage("Vĩ độ phải nằm trong khoảng -90 đến 90");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
                .WithMessage("Kinh độ phải nằm trong khoảng -180 đến 180");

            RuleForEach(x => x.Documents)
                .ChildRules(doc =>
                {
                    doc.RuleFor(d => d.FileUrl)
                        .NotEmpty().WithMessage("URL tài liệu là bắt buộc");

                    doc.RuleFor(d => d.FileType)
                        .MaximumLength(50).WithMessage("Loại file không được vượt quá 50 ký tự")
                        .When(d => !string.IsNullOrEmpty(d.FileType));
                })
                .When(x => x.Documents is not null && x.Documents.Count > 0);
        }
    }
}
