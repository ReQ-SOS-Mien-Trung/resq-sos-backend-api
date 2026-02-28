using FluentValidation;

namespace RESQ.Application.UseCases.Identity.Commands.ReplaceRescuerDocuments
{
    public class ReplaceRescuerDocumentsCommandValidator : AbstractValidator<ReplaceRescuerDocumentsCommand>
    {
        public ReplaceRescuerDocumentsCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId là bắt buộc");

            RuleFor(x => x.Documents)
                .NotNull().WithMessage("Danh sách tài liệu là bắt buộc")
                .Must(docs => docs != null && docs.Count > 0)
                .WithMessage("Phải có ít nhất 1 tài liệu")
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
                        .IsInEnum().WithMessage("Loại tài liệu không hợp lệ. Giá trị hợp lệ: WATER_SAFETY_CERT, WATER_RESCUE_CERT, TECHNICAL_RESCUE_CERT, DISASTER_RESPONSE_CERT, BASIC_MEDICAL_CERT, ADVANCED_MEDICAL_LICENSE, LAND_VEHICLE_LICENSE, WATER_VEHICLE_LICENSE, OTHER");
                })
                .When(x => x.Documents is not null && x.Documents.Count > 0);
        }
    }
}
