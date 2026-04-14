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
            .GreaterThan(0).WithMessage("Id v?t ph?m không h?p l?.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("CategoryId ph?i l?n hon 0.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên v?t ph?m không du?c d? tr?ng.")
            .MaximumLength(255).WithMessage("Tên v?t ph?m không du?c vu?t quá 255 ký t?.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Mô t? v?t ph?m không du?c vu?t quá 1000 ký t?.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Ðon v? tính không du?c d? tr?ng.")
            .MaximumLength(50).WithMessage("Ðon v? tính không du?c vu?t quá 50 ký t?.");

        RuleFor(x => x.ItemType)
            .NotEmpty().WithMessage("Lo?i v?t ph?m không du?c d? tr?ng.")
            .Must(type => ValidItemTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage(x => $"Lo?i v?t ph?m '{x.ItemType}' không h?p l?. Giá tr? h?p l?: {string.Join(", ", ValidItemTypes)}.");

        RuleFor(x => x.TargetGroups)
            .NotEmpty().WithMessage("Nhóm d?i tu?ng không du?c d? tr?ng.")
            .Must(groups => groups.All(g => !string.IsNullOrWhiteSpace(g) && ValidTargetGroups.Contains(g.Trim(), StringComparer.OrdinalIgnoreCase)))
            .WithMessage(x => $"M?t ho?c nhi?u nhóm d?i tu?ng không h?p l?. Giá tr? h?p l?: {string.Join(", ", ValidTargetGroups)}.");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2048).WithMessage("URL ?nh không du?c vu?t quá 2048 ký t?.")
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
            .WithMessage("URL ?nh không h?p l?.");
    }
}
