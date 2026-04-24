using RESQ.Application.UseCases.Logistics.Queries.GetInventoryActionTypes;
using RESQ.Application.UseCases.Logistics.Queries.GetInventorySourceTypes;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Tests.Application.UseCases.Logistics.Queries;

public class InventoryLogMetadataQueryHandlerTests
{
    [Fact]
    public async Task GetInventoryActionTypes_Returns_All_Action_Enums_With_Vietnamese_Display_Names()
    {
        var handler = new GetInventoryActionTypesQueryHandler();

        var result = await handler.Handle(new GetInventoryActionTypesQuery(), CancellationToken.None);

        Assert.Equal(Enum.GetValues<InventoryActionType>().Length, result.Count);
        Assert.Equal(
            Enum.GetValues<InventoryActionType>().Select(x => x.ToString()).ToArray(),
            result.Select(x => x.Key).ToArray());
        Assert.DoesNotContain(result, x => string.IsNullOrWhiteSpace(x.Value));
        Assert.Contains(result, x => x.Key == InventoryActionType.Return.ToString() && x.Value == "Hoàn trả");
        Assert.Contains(result, x => x.Key == InventoryActionType.Reserve.ToString() && x.Value == "Đặt trữ");
        Assert.Contains(result, x => x.Key == InventoryActionType.MissionPickup.ToString() && x.Value == "Xuất cho hoạt động nhiệm vụ");
        Assert.Contains(result, x => x.Key == InventoryActionType.DepotClosureExternalDisposal.ToString() && x.Value == "Xuất xử lý bên ngoài khi đóng kho");
    }

    [Fact]
    public async Task GetInventorySourceTypes_Returns_All_Source_Enums_With_Vietnamese_Display_Names()
    {
        var handler = new GetInventorySourceTypesQueryHandler();

        var result = await handler.Handle(new GetInventorySourceTypesQuery(), CancellationToken.None);

        Assert.Equal(Enum.GetValues<InventorySourceType>().Length, result.Count);
        Assert.Equal(
            Enum.GetValues<InventorySourceType>().Select(x => x.ToString()).ToArray(),
            result.Select(x => x.Key).ToArray());
        Assert.DoesNotContain(result, x => string.IsNullOrWhiteSpace(x.Value));
        Assert.Contains(result, x => x.Key == InventorySourceType.Expired.ToString() && x.Value == "Hết hạn");
        Assert.Contains(result, x => x.Key == InventorySourceType.Damaged.ToString() && x.Value == "Hư hỏng");
        Assert.Contains(result, x => x.Key == InventorySourceType.Disposed.ToString() && x.Value == "Thanh lý");
        Assert.Contains(result, x => x.Key == InventorySourceType.MissionActivity.ToString() && x.Value == "Hoạt động nhiệm vụ");
        Assert.Contains(result, x => x.Key == InventorySourceType.DepotClosure.ToString() && x.Value == "Đóng kho");
    }
}
