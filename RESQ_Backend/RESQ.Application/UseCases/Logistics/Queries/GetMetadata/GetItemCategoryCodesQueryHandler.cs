using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetItemCategoryCodesQueryHandler : IRequestHandler<GetItemCategoryCodesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetItemCategoryCodesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<ItemCategoryCode>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    ItemCategoryCode.Food            => "Thực phẩm",
                    ItemCategoryCode.Water           => "Nước uống",
                    ItemCategoryCode.Medical         => "Y tế",
                    ItemCategoryCode.Hygiene         => "Vệ sinh cá nhân",
                    ItemCategoryCode.Clothing        => "Quần áo",
                    ItemCategoryCode.Shelter         => "Nơi trú ẩn",
                    ItemCategoryCode.RepairTools     => "Công cụ sửa chữa",
                    ItemCategoryCode.RescueEquipment => "Thiết bị cứu hộ",
                    ItemCategoryCode.Heating         => "Sưởi ấm",
                    ItemCategoryCode.Others          => "Khác",
                    _                                => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
