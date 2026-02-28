using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateDocumentFileType;

public class UpdateDocumentFileTypeCommandValidator : AbstractValidator<UpdateDocumentFileTypeCommand>
{
    public UpdateDocumentFileTypeCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id phải lớn hơn 0");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã loại tài liệu là bắt buộc")
            .MaximumLength(100).WithMessage("Mã loại tài liệu không được vượt quá 100 ký tự")
            .Matches(@"^[A-Z0-9_]+$").WithMessage("Mã loại tài liệu chỉ chấp nhận chữ in hoa, số và dấu gạch dưới");

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Tên loại tài liệu không được vượt quá 200 ký tự");
    }
}
