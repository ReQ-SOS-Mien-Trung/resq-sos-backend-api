using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class UpdateMyDepotThresholdCommandValidator : AbstractValidator<UpdateMyDepotThresholdCommand>
{
    public UpdateMyDepotThresholdCommandValidator()
    {
        // Admin (role=1): chỉ được cấu hình scope Global
        RuleFor(x => x.ScopeType)
            .Must(x => x == StockThresholdScopeType.Global)
            .When(x => x.RoleId == 1)
            .WithMessage("Admin chỉ được cấu hình scope Global.");

        // Manager (role=4): chỉ được cấu hình Depot/DepotCategory/DepotItem
        RuleFor(x => x.ScopeType)
            .Must(x => x is StockThresholdScopeType.Depot or StockThresholdScopeType.DepotCategory or StockThresholdScopeType.DepotItem)
            .When(x => x.RoleId == 4)
            .WithMessage("Manager chỉ được cấu hình scope Depot, DepotCategory hoặc DepotItem.");

        RuleFor(x => x.DangerPercent)
            .GreaterThan(0).WithMessage("dangerPercent phải > 0.")
            .GreaterThanOrEqualTo(1).WithMessage("dangerPercent phải >= 1.");

        RuleFor(x => x.WarningPercent)
            .GreaterThan(0).WithMessage("warningPercent phải > 0.")
            .GreaterThanOrEqualTo(5).WithMessage("warningPercent phải >= 5.")
            .LessThanOrEqualTo(100).WithMessage("warningPercent phải <= 100.");

        RuleFor(x => x)
            .Must(x => x.DangerPercent < x.WarningPercent)
            .WithMessage("dangerPercent phải nhỏ hơn warningPercent.");

        // Admin + Global không nhận categoryId/itemModelId
        RuleFor(x => x)
            .Must(x => x.CategoryId == null && x.ItemModelId == null)
            .When(x => x.RoleId == 1)
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
