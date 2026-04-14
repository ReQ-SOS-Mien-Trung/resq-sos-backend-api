using ClosedXML.Excel;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using System.Globalization;

namespace RESQ.Infrastructure.Services.Finance;

/// <summary>
/// Parse file Excel vật phẩm từ FundingRequest (dùng khi cần import hàng loạt).
/// Format Excel mong đợi:
/// Row 1: Header (STT | Tên vật phẩm | Mã danh mục | Đơn vị | Số lượng | Đơn giá | Thành tiền | Loại | Nhóm đối tượng | Ghi chú)
/// Row 2+: Data
/// </summary>
public class FundingRequestExcelParser : IFundingRequestExcelParser
{
    private static readonly Dictionary<string, string> TargetGroupVietnameseToRaw = BuildNormalizedLookup(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Trẻ em"] = "Children",
            ["Người già"] = "Elderly",
            ["Phụ nữ mang thai"] = "Pregnant",
            ["Người lớn"] = "Adult",
            ["Lực lượng cứu hộ"] = "Rescuer"
        });

    private static readonly Dictionary<string, string> ItemTypeVietnameseToRaw = BuildNormalizedLookup(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tiêu thụ"] = "Consumable",
            ["Tái sử dụng"] = "Reusable"
        });

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
            var targetGroupRaw = row.Cell(4).GetString()?.Trim() ?? string.Empty;
            var itemTypeRaw  = row.Cell(5).GetString()?.Trim() ?? string.Empty;
            var unit         = row.Cell(6).GetString()?.Trim();
            var notes        = row.Cell(7).GetString()?.Trim();
            var quantity     = GetIntOrDefault(row.Cell(8));
            var unitPrice    = GetDecimalOrDefault(row.Cell(9));
            var volumePerUnit = GetDecimalOrDefault(row.Cell(10));
            var weightPerUnit = GetDecimalOrDefault(row.Cell(11));
            var targetGroups = targetGroupRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeTargetGroup)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            var itemType = NormalizeItemType(itemTypeRaw);

            items.Add(new FundingRequestItemModel
            {
                Row          = i + 1,
                ItemName     = itemName,
                CategoryCode = categoryCode,
                Unit         = unit,
                Quantity     = quantity,
                UnitPrice    = unitPrice,
                TotalPrice   = unitPrice * quantity,
                ItemType     = itemType,
                TargetGroups = targetGroups,
                Notes        = notes,
                VolumePerUnit = volumePerUnit,
                WeightPerUnit = weightPerUnit
            });
        }

        return items;
    }

    public decimal CalculateTotal(List<FundingRequestItemModel> items)
    {
        return items.Sum(i => i.TotalPrice);
    }

    private static int GetIntOrDefault(IXLCell cell)
    {
        if (cell.TryGetValue<int>(out var value))
        {
            return value;
        }

        return (int)Math.Round(GetDecimalOrDefault(cell), MidpointRounding.AwayFromZero);
    }

    private static decimal GetDecimalOrDefault(IXLCell cell)
    {
        if (cell.TryGetValue<decimal>(out var value))
        {
            return value;
        }

        var text = cell.GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
        {
            return value;
        }

        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("vi-VN"), out value))
        {
            return value;
        }

        return 0m;
    }

    private static string NormalizeTargetGroup(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var display = normalized.Split(" - ", 2, StringSplitOptions.TrimEntries)[0];
        var asciiDisplay = RemoveDiacritics(display);

        return TargetGroupVietnameseToRaw.TryGetValue(asciiDisplay, out var raw)
            ? raw
            : display;
    }

    private static string NormalizeItemType(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var display = normalized.Split(" - ", 2, StringSplitOptions.TrimEntries)[0];
        var asciiDisplay = RemoveDiacritics(display);

        return ItemTypeVietnameseToRaw.TryGetValue(asciiDisplay, out var raw)
            ? raw
            : display;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'D');
    }

    private static Dictionary<string, string> BuildNormalizedLookup(Dictionary<string, string> source)
        => source.ToDictionary(
            pair => RemoveDiacritics(pair.Key),
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
}
