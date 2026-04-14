using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Services;

public interface IFundingRequestExcelParser
{
    /// <summary>
    /// Parse file Excel v?t ph?m t? FundingRequest.
    /// Tr? v? danh sách items và t?ng ti?n.
    /// </summary>
    /// <param name="fileStream">Stream c?a file Excel (.xlsx).</param>
    /// <returns>Danh sách items du?c parse t? file.</returns>
    List<FundingRequestItemModel> ParseSupplyItems(Stream fileStream);

    /// <summary>
    /// Tính t?ng ti?n t? danh sách items.
    /// </summary>
    decimal CalculateTotal(List<FundingRequestItemModel> items);
}
