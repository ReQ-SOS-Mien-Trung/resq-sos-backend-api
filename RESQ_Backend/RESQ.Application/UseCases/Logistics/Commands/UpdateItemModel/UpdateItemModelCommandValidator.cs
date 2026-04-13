using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateItemModel;

public class UpdateItemModelCommandValidator : AbstractValidator<UpdateItemModelCommand>
{
    private static readonly string[] ValidItemTypes = Enum.GetNames<ItemType>();
    private static readonly string[] ValidTargetGroups = Enum.GetNames<TargetGroup>();

    public UpdateItemModelCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id vật phẩm không hợp lệ.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("CategoryId phải lớn hơn 0.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên vật phẩm không được để trống.")
            .MaximumLength(255).WithMessage("Tên vật phẩm không được vượt quá 255 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Mô tả vật phẩm không được vượt quá 1000 ký tự.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Đơn vị tính không được để trống.")
            .MaximumLength(50).WithMessage("Đơn vị tính không được vượt quá 50 ký tự.");

        RuleFor(x => x.ItemType)
            .NotEmpty().WithMessage("Loại vật phẩm không được để trống.")
            .Must(type => ValidItemTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage(x => $"Loại vật phẩm '{x.ItemType}' không hợp lệ. Giá trị hợp lệ: {string.Join(", ", ValidItemTypes)}.");

        RuleFor(x => x.TargetGroups)
            .NotEmpty().WithMessage("Nhóm đối tượng không được để trống.")
            .Must(groups => groups.All(g => !string.IsNullOrWhiteSpace(g) && ValidTargetGroups.Contains(g.Trim(), StringComparer.OrdinalIgnoreCase)))
            .WithMessage(x => $"Một hoặc nhiều nhóm đối tượng không hợp lệ. Giá trị hợp lệ: {string.Join(", ", ValidTargetGroups)}.");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2048).WithMessage("URL ảnh không được vượt quá 2048 ký tự.")
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
            .WithMessage("URL ảnh không hợp lệ.");
    }
}
