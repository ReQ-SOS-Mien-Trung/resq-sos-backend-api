using ClosedXML.Excel;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Infrastructure.Services.Finance;

/// <summary>
/// Parse file Excel vật tư từ FundingRequest (dùng khi cần import hàng loạt).
/// Format Excel mong đợi:
/// Row 1: Header (STT | Tên vật tư | Mã danh mục | Đơn vị | Số lượng | Đơn giá | Thành tiền | Loại | Nhóm đối tượng | Ghi chú)
/// Row 2+: Data
/// </summary>
public class FundingRequestExcelParser : IFundingRequestExcelParser
{
    public List<FundingRequestItemModel> ParseSupplyItems(Stream fileStream)
    {
        var items = new List<FundingRequestItemModel>();

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.First();

        // Skip header row (row 1), start from row 2
        var rows = worksheet.RowsUsed().Skip(1).ToList();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var itemName = row.Cell(2).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(itemName)) continue;

            var categoryCode = row.Cell(3).GetString()?.Trim() ?? string.Empty;
            var unit         = row.Cell(4).GetString()?.Trim();
            var quantity     = (int)row.Cell(5).GetDouble();
            var unitPrice    = (decimal)row.Cell(6).GetDouble();
            var totalPrice   = (decimal)row.Cell(7).GetDouble();
            var itemType     = row.Cell(8).GetString()?.Trim() ?? string.Empty;
            var targetGroupRaw = row.Cell(9).GetString()?.Trim() ?? string.Empty;
            var targetGroups = targetGroupRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            var notes        = row.Cell(10).GetString()?.Trim();

            items.Add(new FundingRequestItemModel
            {
                Row          = i + 1,
                ItemName     = itemName,
                CategoryCode = categoryCode,
                Unit         = unit,
                Quantity     = quantity,
                UnitPrice    = unitPrice,
                TotalPrice   = totalPrice > 0 ? totalPrice : unitPrice * quantity,
                ItemType     = itemType,
                TargetGroups = targetGroups,
                Notes        = notes
            });
        }

        return items;
    }

    public decimal CalculateTotal(List<FundingRequestItemModel> items)
    {
        return items.Sum(i => i.TotalPrice);
    }
}
