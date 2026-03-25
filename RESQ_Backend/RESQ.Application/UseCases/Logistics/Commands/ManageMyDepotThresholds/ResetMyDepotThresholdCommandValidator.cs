using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class ResetMyDepotThresholdCommandValidator : AbstractValidator<ResetMyDepotThresholdCommand>
{
    public ResetMyDepotThresholdCommandValidator()
    {
        RuleFor(x => x.ScopeType)
            .Must(x => x is StockThresholdScopeType.Depot or StockThresholdScopeType.DepotCategory or StockThresholdScopeType.DepotItem)
            .WithMessage("ScopeType không hợp lệ cho manager.");

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
