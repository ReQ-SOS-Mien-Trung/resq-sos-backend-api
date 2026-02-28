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

            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Tên là bắt buộc")
                .MaximumLength(100).WithMessage("Tên không được vượt quá 100 ký tự");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Họ là bắt buộc")
                .MaximumLength(100).WithMessage("Họ không được vượt quá 100 ký tự");

            RuleFor(x => x.Phone)
                .Matches(@"^(0|\+84)[3-9]\d{8}$")
                .WithMessage("Số điện thoại phải là số điện thoại Việt Nam hợp lệ (VD: 0912345678 hoặc +84912345678)")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.Address)
                .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự");

            RuleFor(x => x.Ward)
                .MaximumLength(100).WithMessage("Phường/Xã không được vượt quá 100 ký tự");

            RuleFor(x => x.Province)
                .MaximumLength(100).WithMessage("Tỉnh/Thành phố không được vượt quá 100 ký tự");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
                .WithMessage("Vĩ độ phải nằm trong khoảng -90 đến 90");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
                .WithMessage("Kinh độ phải nằm trong khoảng -180 đến 180");

            RuleFor(x => x.Note)
                .MaximumLength(2000).WithMessage("Ghi chú không được vượt quá 2000 ký tự");
        }
    }
}
