using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Services;

public interface IFundingRequestExcelParser
{
    /// <summary>
    /// Parse file Excel vật phẩm từ FundingRequest.
    /// Trả về danh sách items và tổng tiền.
    /// </summary>
    /// <param name="fileStream">Stream của file Excel (.xlsx).</param>
    /// <returns>Danh sách items được parse từ file.</returns>
    List<FundingRequestItemModel> ParseSupplyItems(Stream fileStream);

    /// <summary>
    /// Tính tổng tiền từ danh sách items.
    /// </summary>
    decimal CalculateTotal(List<FundingRequestItemModel> items);
}
