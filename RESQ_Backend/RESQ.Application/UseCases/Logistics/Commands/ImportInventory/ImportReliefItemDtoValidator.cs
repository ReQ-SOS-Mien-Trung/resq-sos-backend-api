using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemDtoValidator : AbstractValidator<ImportReliefItemDto>
{
    private static readonly string[] ValidCategoryCodes = Enum.GetNames<ItemCategoryCode>();
    private static readonly string[] ValidItemTypes = Enum.GetNames<ItemType>();
    private static readonly string[] ValidTargetGroups = Enum.GetNames<TargetGroup>();

    public ImportReliefItemDtoValidator()
    {
        RuleFor(x => x.Row)
            .GreaterThan(0).WithMessage("Số dòng phải lớn hơn 0.");

        // ── Priority rule: if ItemModelId exists, import uses ID-path and ignores lookup metadata.
        // Otherwise, ItemName/metadata path is required to create a new item model. ──
        RuleFor(x => x)
            .Must(x => x.ItemModelId.HasValue || !string.IsNullOrWhiteSpace(x.ItemName))
            .WithMessage("Phải cung cấp ItemModelId hoặc ItemName.");

        // ── Path A: Existing item by ID ──
        RuleFor(x => x.ItemModelId)
            .GreaterThan(0).WithMessage("ItemModelId phải lớn hơn 0.")
            .When(x => x.ItemModelId.HasValue);

        // ── Path B: New item by metadata (only when ItemModelId is not provided) ──
        When(x => !x.ItemModelId.HasValue, () =>
        {
            RuleFor(x => x.ItemName)
                .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Tên vật phẩm không được để trống.")
                .MaximumLength(255).WithMessage("Tên vật phẩm không được vượt quá 255 ký tự.");

            RuleFor(x => x.CategoryCode)
                .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Mã danh mục không được để trống.")
                .Must(code => !string.IsNullOrWhiteSpace(code) && ValidCategoryCodes.Contains(code.Trim(), StringComparer.OrdinalIgnoreCase))
                .WithMessage(x => $"Mã danh mục '{x.CategoryCode}' không hợp lệ. Giá trị hợp lệ: {string.Join(", ", ValidCategoryCodes)}.");

            RuleFor(x => x.Unit)
                .Must(unit => !string.IsNullOrWhiteSpace(unit)).WithMessage("Đơn vị tính không được để trống.")
                .MaximumLength(50).WithMessage("Đơn vị tính không được vượt quá 50 ký tự.");

            RuleFor(x => x.ItemType)
                .Must(type => !string.IsNullOrWhiteSpace(type)).WithMessage("Loại vật phẩm không được để trống.")
                .Must(type => !string.IsNullOrWhiteSpace(type) && ValidItemTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase))
                .WithMessage(x => $"Loại vật phẩm '{x.ItemType}' không hợp lệ. Giá trị hợp lệ: {string.Join(", ", ValidItemTypes)}.");

            RuleFor(x => x.TargetGroups)
                .NotEmpty().WithMessage("Nhóm đối tượng không được để trống.")
                .Must(groups => groups != null && groups.All(g => !string.IsNullOrWhiteSpace(g) && ValidTargetGroups.Contains(g.Trim(), StringComparer.OrdinalIgnoreCase)))
                .WithMessage(x => $"Một hoặc nhiều nhóm đối tượng không hợp lệ. Giá trị hợp lệ: {string.Join(", ", ValidTargetGroups)}.");
        });

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng nhập phải lớn hơn 0.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Mô tả vật phẩm không được vượt quá 1000 ký tự.");

        // Rule: ReceivedDate is required
        RuleFor(x => x.ReceivedDate)
            .NotNull().WithMessage("Ngày nhận không được để trống.");

        // Rule: ReceivedDate <= Now (can be past, cannot be future)
        RuleFor(x => x.ReceivedDate)
            .Must(date => date!.Value <= DateTime.UtcNow.AddHours(7))
            .When(x => x.ReceivedDate.HasValue)
            .WithMessage("Ngày nhận không được là ngày trong tương lai.");

        // Rule: ExpiredDate > ReceivedDate (DateOnly vs DateTime — compare by date part)
        RuleFor(x => x.ExpiredDate)
            .Must((dto, expiredDate) => expiredDate!.Value > DateOnly.FromDateTime(dto.ReceivedDate!.Value))
            .When(x => x.ExpiredDate.HasValue && x.ReceivedDate.HasValue)
            .WithMessage("Ngày hết hạn phải sau ngày nhận.");

    }
}
