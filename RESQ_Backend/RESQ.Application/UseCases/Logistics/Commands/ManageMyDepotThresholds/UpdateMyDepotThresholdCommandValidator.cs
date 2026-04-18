using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class UpdateMyDepotThresholdCommandValidator : AbstractValidator<UpdateMyDepotThresholdCommand>
{
    public UpdateMyDepotThresholdCommandValidator()
    {
        // Caller có quyền toàn cục: chỉ được cấu hình scope Global
        RuleFor(x => x.ScopeType)
            .Must(x => x == StockThresholdScopeType.Global)
            .When(x => x.CanManageGlobalThresholds)
            .WithMessage("Caller có quyền toàn cục chỉ được cấu hình scope Global.");

        // Caller quản lý kho: chỉ được cấu hình Depot/DepotCategory/DepotItem
        RuleFor(x => x.ScopeType)
            .Must(x => x is StockThresholdScopeType.Depot or StockThresholdScopeType.DepotCategory or StockThresholdScopeType.DepotItem)
            .When(x => !x.CanManageGlobalThresholds)
            .WithMessage("Caller quản lý kho chỉ được cấu hình scope Depot, DepotCategory hoặc DepotItem.");

        // MinimumThreshold: null = xóa config, otherwise must be > 0
        RuleFor(x => x.MinimumThreshold)
            .GreaterThan(0)
            .When(x => x.MinimumThreshold.HasValue)
            .WithMessage("minimumThreshold phải > 0.");

        // Scope Global không nhận categoryId/itemModelId
        RuleFor(x => x)
            .Must(x => x.CategoryId == null && x.ItemModelId == null)
            .When(x => x.CanManageGlobalThresholds)
            .WithMessage("Scope Global không nhận categoryId hoặc itemModelId.");

        RuleFor(x => x.CategoryId)
            .NotNull().When(x => x.ScopeType == StockThresholdScopeType.DepotCategory)
            .WithMessage("categoryId là bắt buộc khi scopeType=DepotCategory.");

        RuleFor(x => x.ItemModelId)
            .NotNull().When(x => x.ScopeType == StockThresholdScopeType.DepotItem)
            .WithMessage("itemModelId là bắt buộc khi scopeType=DepotItem.");

        RuleFor(x => x)
            .Must(x => x.ScopeType != StockThresholdScopeType.Depot || (x.CategoryId == null && x.ItemModelId == null))
            .WithMessage("Scope Depot không được truyền categoryId/itemModelId.");

        RuleFor(x => x)
            .Must(x => x.ScopeType != StockThresholdScopeType.DepotCategory || (x.ItemModelId == null && x.CategoryId > 0))
            .WithMessage("Scope DepotCategory yêu cầu categoryId hợp lệ và không nhận itemModelId.");

        RuleFor(x => x)
            .Must(x => x.ScopeType != StockThresholdScopeType.DepotItem || (x.CategoryId == null && x.ItemModelId > 0))
            .WithMessage("Scope DepotItem yêu cầu itemModelId hợp lệ và không nhận categoryId.");
    }
}
