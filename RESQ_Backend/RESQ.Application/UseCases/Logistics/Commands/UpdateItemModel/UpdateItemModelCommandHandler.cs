using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateItemModel;

public class UpdateItemModelCommandHandler(IItemModelMetadataRepository itemModelMetadataRepository) : IRequestHandler<UpdateItemModelCommand, Unit>
{
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;

    public async Task<Unit> Handle(UpdateItemModelCommand request, CancellationToken cancellationToken)
    {
        var existing = await _itemModelMetadataRepository.GetByIdsAsync([request.Id], cancellationToken);
        if (!existing.ContainsKey(request.Id))
            throw new NotFoundException($"Không tìm th?y item model v?i ID = {request.Id}");

        var existingItem = existing[request.Id];

        var categoryExists = await _itemModelMetadataRepository.CategoryExistsAsync(request.CategoryId, cancellationToken);

        if (!categoryExists)
            throw new NotFoundException($"Không tìm th?y category v?i ID = {request.CategoryId}");

        if (existingItem.CategoryId != request.CategoryId)
        {
            var hasTransactions = await _itemModelMetadataRepository.HasInventoryTransactionsAsync(request.Id, cancellationToken);
            if (hasTransactions)
                throw new ConflictException("Không th? d?i categoryId vì v?t ph?m dã phát sinh giao d?ch kho. Ði?u này có th? làm l?ch d? li?u l?ch s?.");
        }

        var normalizedItemType = Enum.Parse<ItemType>(request.ItemType.Trim(), ignoreCase: true).ToString();

        var normalizedTargetGroups = request.TargetGroups
            .Select(x => Enum.Parse<TargetGroup>(x.Trim(), ignoreCase: true).ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var updated = await _itemModelMetadataRepository.UpdateItemModelAsync(
            new ItemModelRecord
            {
                Id = request.Id,
                CategoryId = request.CategoryId,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Unit = request.Unit.Trim(),
                ItemType = normalizedItemType,
                VolumePerUnit = request.VolumePerUnit,
                WeightPerUnit = request.WeightPerUnit,
                TargetGroups = normalizedTargetGroups,
                ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
                UpdatedAt = DateTime.UtcNow
            },
            cancellationToken);

        if (!updated)
            throw new NotFoundException($"Không tìm th?y item model v?i ID = {request.Id}");

        return Unit.Value;
    }
}
