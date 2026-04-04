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

            RuleFor(x => x.Address)
                .NotEmpty().WithMessage("Địa chỉ là bắt buộc")
                .MaximumLength(500).WithMessage("Địa chỉ không được vượt quá 500 ký tự");

            RuleFor(x => x.Ward)
                .MaximumLength(100).WithMessage("Phường/Xã không được vượt quá 100 ký tự");

            RuleFor(x => x.Province)
                .NotEmpty().WithMessage("Tỉnh/Thành phố là bắt buộc")
                .MaximumLength(100).WithMessage("Tỉnh/Thành phố không được vượt quá 100 ký tự");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
                .WithMessage("Vĩ độ phải nằm trong khoảng -90 đến 90");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
                .WithMessage("Kinh độ phải nằm trong khoảng -180 đến 180");

            RuleFor(x => x.AvatarUrl)
                .MaximumLength(500).WithMessage("Avatar URL không được vượt quá 500 ký tự")
                .Must(url => url == null || Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("Avatar URL không hợp lệ")
                .When(x => x.AvatarUrl != null);
        }
    }
}
