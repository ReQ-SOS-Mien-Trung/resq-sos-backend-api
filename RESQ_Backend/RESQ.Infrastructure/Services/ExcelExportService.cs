using ClosedXML.Excel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Infrastructure.Services;

public class ExcelExportService : IExcelExportService
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Constants for the inventory movement report (existing)
    // ═══════════════════════════════════════════════════════════════════════════

    private static readonly string[] Headers =
    [
        "STT", "Tên Vật Phẩm", "Danh mục", "Đối tượng", "Loại vật phẩm",
        "Đơn vị", "Đơn giá", "Số lượng", "Ngày nhận",
        "Loại Hành động", "Nguồn", "Tên nhiệm vụ"
    ];

    // ── Palette ──────────────────────────────────────────────────────────────
    private static readonly XLColor OrangeDark     = XLColor.FromHtml("#E65100");
    private static readonly XLColor OrangeMid      = XLColor.FromHtml("#FF8F00");
    private static readonly XLColor OrangeLight    = XLColor.FromHtml("#FFF3E0");
    private static readonly XLColor OrangeSummary  = XLColor.FromHtml("#FFE0B2");
    private static readonly XLColor Black          = XLColor.FromHtml("#212121");
    private static readonly XLColor White          = XLColor.White;
    private static readonly XLColor LockedCellColor   = XLColor.FromHtml("#ECEFF1"); // light blue-gray — VLOOKUP read-only cells
    private static readonly XLColor LockedHeaderColor = XLColor.FromHtml("#546E7A"); // dark blue-gray — locked column headers

    // ═══════════════════════════════════════════════════════════════════════════
    //  Constants for the donation import template
    // ═══════════════════════════════════════════════════════════════════════════

    private static readonly string[] TemplateHeaders =
    [
        "STT",              // A
        "Tên vật phẩm",    // B
        "Danh mục",         // C
        "Đối tượng",        // D
        "Loại vật phẩm",   // E
        "Đơn vị",           // F
        "Mô tả vật phẩm",   // G
        "Số lượng",         // H
        "Ngày hết hạn",    // I
        "Ngày nhận",        // J
    ];

    private const int TemplateDataStartRow = 2;
    private const int TemplateDataEndRow   = 102; // 100 data rows
    private const int TemplateCols         = 10;   // A..J

    public byte[] GenerateInventoryMovementReport(
        IReadOnlyList<InventoryMovementRow> rows,
        string title,
        string depotName)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Biến động kho");

        int col = Headers.Length;

        // ─── Row 1: Depot name banner ─────────────────────────────────────────
        ws.Cell(1, 1).Value = $"KHO: {depotName.ToUpper()}";
        var depotRange = ws.Range(1, 1, 1, col);
        depotRange.Merge();
        depotRange.Style
            .Font.SetBold(true)
            .Font.SetFontSize(11)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Fill.SetBackgroundColor(OrangeDark)
            .Font.SetFontColor(White);
        ws.Row(1).Height = 22;

        // ─── Row 2: Report title ──────────────────────────────────────────────
        ws.Cell(2, 1).Value = $"BÁO CÁO BIẾN ĐỘNG KHO – {title.ToUpper()}";
        var titleRange = ws.Range(2, 1, 2, col);
        titleRange.Merge();
        titleRange.Style
            .Font.SetBold(true)
            .Font.SetFontSize(14)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Fill.SetBackgroundColor(OrangeDark)
            .Font.SetFontColor(White);
        ws.Row(2).Height = 30;

        // ─── Row 3: Export timestamp ──────────────────────────────────────────
        ws.Cell(3, 1).Value = $"Ngày xuất: {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}";
        var tsRange = ws.Range(3, 1, 3, col);
        tsRange.Merge();
        ws.Cell(3, 1).Style
            .Font.SetItalic(true)
            .Font.SetFontSize(10)
            .Font.SetFontColor(Black)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

        // ─── Row 4: Header ────────────────────────────────────────────────────
        int headerRow = 4;
        for (int i = 0; i < Headers.Length; i++)
            ws.Cell(headerRow, i + 1).Value = Headers[i];

        var headerRange = ws.Range(headerRow, 1, headerRow, col);
        headerRange.Style
            .Font.SetBold(true)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true)
            .Fill.SetBackgroundColor(OrangeMid)
            .Font.SetFontColor(White);
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
        headerRange.Style.Border.OutsideBorderColor = Black;
        headerRange.Style.Border.InsideBorderColor  = Black;
        ws.Row(headerRow).Height = 22;

        // ─── Data rows ────────────────────────────────────────────────────────
        int dataStartRow = headerRow + 1;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int r = dataStartRow + i;

            ws.Cell(r, 1).Value  = row.RowNumber;
            ws.Cell(r, 2).Value  = row.ItemName;
            ws.Cell(r, 3).Value  = row.Category;
            ws.Cell(r, 4).Value  = row.TargetGroup;
            ws.Cell(r, 5).Value  = row.ItemType;
            ws.Cell(r, 6).Value  = row.Unit;

            if (row.UnitPrice.HasValue)
            {
                ws.Cell(r, 7).Value = row.UnitPrice.Value;
                ws.Cell(r, 7).Style.NumberFormat.Format = "#,##0.00";
            }

            ws.Cell(r, 8).Value  = row.FormattedQuantity;
            ws.Cell(r, 9).Value  = row.CreatedAt.HasValue
                ? row.CreatedAt.Value.AddHours(7).ToString("dd/MM/yyyy HH:mm")
                : string.Empty;
            ws.Cell(r, 10).Value = row.ActionType;
            ws.Cell(r, 11).Value = row.SourceType;
            ws.Cell(r, 12).Value = row.MissionName ?? string.Empty;

            // Alternate row background: white / light-orange
            var rowRange = ws.Range(r, 1, r, col);
            rowRange.Style.Font.SetFontColor(Black);
            if (i % 2 != 0)
                rowRange.Style.Fill.SetBackgroundColor(OrangeLight);

            // Quantity cell: green = in, red = out (semantic colours kept)
            var qtyCell = ws.Cell(r, 8);
            qtyCell.Style.Font.SetBold(true);
            if (row.FormattedQuantity.StartsWith('+'))
                qtyCell.Style.Font.SetFontColor(XLColor.FromHtml("#2E7D32"));
            else if (row.FormattedQuantity.StartsWith('-'))
                qtyCell.Style.Font.SetFontColor(XLColor.FromHtml("#C62828"));

            rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rowRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
            rowRange.Style.Border.OutsideBorderColor = Black;
            rowRange.Style.Border.InsideBorderColor  = Black;
        }

        // ─── Summary row ──────────────────────────────────────────────────────
        int summaryRow = dataStartRow + rows.Count;
        ws.Cell(summaryRow, 1).Value = "Tổng số dòng:";
        ws.Cell(summaryRow, 2).Value = rows.Count;
        var summaryRange = ws.Range(summaryRow, 1, summaryRow, col);
        summaryRange.Style
            .Font.SetBold(true)
            .Font.SetFontColor(Black)
            .Fill.SetBackgroundColor(OrangeSummary);
        summaryRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        summaryRange.Style.Border.OutsideBorderColor = Black;

        // ─── Auto-fit & freeze ────────────────────────────────────────────────
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(headerRow);

        foreach (var column in ws.Columns())
        {
            if (column.Width > 50) column.Width = 50;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Donation Import Template — Excel file with dependent dropdowns
    // ═══════════════════════════════════════════════════════════════════════════

    public byte[] GenerateDonationImportTemplate(
        IReadOnlyList<DonationImportCategoryInfo> categories,
        IReadOnlyList<DonationImportItemInfo> items,
        IReadOnlyList<DonationImportTargetGroupInfo> targetGroups)
    {
        using var workbook = new XLWorkbook();

        // ── 1. Build hidden reference sheets ──────────────────────────────────
        var wsDanhMuc = workbook.Worksheets.Add("DM_DanhMuc");
        var wsVatPham = workbook.Worksheets.Add("DM_VatPham");
        var wsLookup  = workbook.Worksheets.Add("DM_Lookup");
        var wsMeta    = workbook.Worksheets.Add("DM_Metadata");

        BuildCategorySheet(wsDanhMuc, categories, workbook);
        BuildItemSheet(wsVatPham, categories, items, workbook);
        BuildLookupSheet(wsLookup, items);
        BuildMetadataSheet(wsMeta, items, targetGroups, workbook);

        wsDanhMuc.Visibility = XLWorksheetVisibility.VeryHidden;
        wsVatPham.Visibility = XLWorksheetVisibility.VeryHidden;
        wsLookup.Visibility  = XLWorksheetVisibility.VeryHidden;
        wsMeta.Visibility    = XLWorksheetVisibility.VeryHidden;

        // ── 2. Build main entry sheet ─────────────────────────────────────────
        var ws = workbook.Worksheets.Add("Nhập kho từ thiện");
        ws.SetTabActive();

        BuildMainSheet(ws, categories);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── DM_DanhMuc: Category list → named range "Categories" ─────────────────
    private static void BuildCategorySheet(
        IXLWorksheet ws,
        IReadOnlyList<DonationImportCategoryInfo> categories,
        XLWorkbook workbook)
    {
        ws.Cell(1, 1).Value = "Danh mục";
        for (int i = 0; i < categories.Count; i++)
        {
            // Format: "Thực phẩm - Food"
            ws.Cell(i + 2, 1).Value = $"{categories[i].Name} - {categories[i].Code}";
        }

        // Named range "Categories" → DM_DanhMuc!$A$2:$A${n+1}
        int lastRow = categories.Count + 1;
        workbook.NamedRanges.Add("Categories", ws.Range(2, 1, lastRow, 1));
    }

    // ─── DM_VatPham: One column per category code → named ranges Cat_Food, Cat_Water... ─
    private static void BuildItemSheet(
        IXLWorksheet ws,
        IReadOnlyList<DonationImportCategoryInfo> categories,
        IReadOnlyList<DonationImportItemInfo> items,
        XLWorkbook workbook)
    {
        // Group items by category code
        var itemsByCategory = items
            .GroupBy(i => i.CategoryCode)
            .ToDictionary(g => g.Key, g => g.ToList());

        int col = 1;
        foreach (var cat in categories)
        {
            // Header row
            ws.Cell(1, col).Value = cat.Code;

            if (itemsByCategory.TryGetValue(cat.Code, out var catItems))
            {
                for (int i = 0; i < catItems.Count; i++)
                {
                    // Format: "Mì tôm - 1"
                    ws.Cell(i + 2, col).Value = $"{catItems[i].Name} - {catItems[i].Id}";
                }

                // Named range: Cat_Food, Cat_Water, etc.
                int lastRow = catItems.Count + 1;
                var rangeName = $"Cat_{cat.Code}";
                workbook.NamedRanges.Add(rangeName, ws.Range(2, col, lastRow, col));
            }
            else
            {
                // Empty category — still create named range pointing to a single blank cell
                var rangeName = $"Cat_{cat.Code}";
                workbook.NamedRanges.Add(rangeName, ws.Range(2, col, 2, col));
            }

            col++;
        }
    }

    // ─── DM_Lookup: Flat table for VLOOKUP (display name → TargetGroup, ItemType, Unit)
    private static void BuildLookupSheet(
        IXLWorksheet ws,
        IReadOnlyList<DonationImportItemInfo> items)
    {
        // Header
        ws.Cell(1, 1).Value = "TenVatPham";
        ws.Cell(1, 2).Value = "DoiTuong";
        ws.Cell(1, 3).Value = "LoaiVatPham";
        ws.Cell(1, 4).Value = "DonVi";
        ws.Cell(1, 5).Value = "MoTa";

        for (int i = 0; i < items.Count; i++)
        {
            int r = i + 2;
            // Lookup key must match the dropdown display: "Mì tôm - 1"
            ws.Cell(r, 1).Value = $"{items[i].Name} - {items[i].Id}";
            ws.Cell(r, 2).Value = items[i].TargetGroupDisplay;
            ws.Cell(r, 3).Value = items[i].ItemTypeDisplay;
            ws.Cell(r, 4).Value = items[i].Unit;
            ws.Cell(r, 5).Value = items[i].Description;
        }
    }

    // ─── DM_Metadata: Dropdown sources for manual input columns (TargetGroup, ItemType) ───
    private static void BuildMetadataSheet(
        IXLWorksheet ws,
        IReadOnlyList<DonationImportItemInfo> items,
        IReadOnlyList<DonationImportTargetGroupInfo> targetGroups,
        XLWorkbook workbook)
    {
        ws.Cell(1, 1).Value = "DoiTuongOptions";
        ws.Cell(1, 2).Value = "LoaiVatPhamOptions";

        var targetGroupOptions = targetGroups
            .OrderBy(tg => tg.Id)
            .Select(tg => FormatDisplayWithCode(tg.NameDisplay, tg.Id.ToString()))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var itemTypeOrder = Enum.GetValues<ItemType>()
            .Select((value, index) => new { Key = value.ToString(), Index = index })
            .ToDictionary(x => x.Key, x => x.Index, StringComparer.OrdinalIgnoreCase);

        var itemTypeOptions = items
            .Select(i => new
            {
                Raw = i.ItemTypeRaw?.Trim() ?? string.Empty,
                Option = FormatDisplayWithCode(i.ItemTypeDisplay ?? string.Empty, i.ItemTypeRaw ?? string.Empty)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Option))
            .GroupBy(x => x.Option, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => itemTypeOrder.TryGetValue(x.Raw, out var order) ? order : int.MaxValue)
            .ThenBy(x => x.Option, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Option)
            .ToList();

        for (int i = 0; i < targetGroupOptions.Count; i++)
            ws.Cell(i + 2, 1).Value = targetGroupOptions[i];

        for (int i = 0; i < itemTypeOptions.Count; i++)
            ws.Cell(i + 2, 2).Value = itemTypeOptions[i];

        var targetGroupLastRow = Math.Max(targetGroupOptions.Count + 1, 2);
        var itemTypeLastRow = Math.Max(itemTypeOptions.Count + 1, 2);

        workbook.NamedRanges.Add("TargetGroupOptions", ws.Range(2, 1, targetGroupLastRow, 1));
        workbook.NamedRanges.Add("ItemTypeOptions", ws.Range(2, 2, itemTypeLastRow, 2));
    }

    private static string FormatDisplayWithCode(string display, string codeOrId)
    {
        var normalizedDisplay = display?.Trim() ?? string.Empty;
        var normalizedCode = codeOrId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedDisplay))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedCode)
            || string.Equals(normalizedDisplay, normalizedCode, StringComparison.OrdinalIgnoreCase))
            return normalizedDisplay;

        return $"{normalizedDisplay} - {normalizedCode}";
    }

    // ─── Main entry sheet: headers, STT, dropdowns, VLOOKUP formulas ──────────
    private static void BuildMainSheet(
        IXLWorksheet ws,
        IReadOnlyList<DonationImportCategoryInfo> categories)
    {
        // ── Header row styling ────────────────────────────────────────────────
        for (int c = 0; c < TemplateHeaders.Length; c++)
            ws.Cell(1, c + 1).Value = TemplateHeaders[c];

        var headerRange = ws.Range(1, 1, 1, TemplateCols);
        headerRange.Style
            .Font.SetBold(true)
            .Font.SetFontSize(11)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true)
            .Fill.SetBackgroundColor(OrangeMid)
            .Font.SetFontColor(White);
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
        ws.Row(1).Height = 24;

        // ── Column widths ─────────────────────────────────────────────────────
        ws.Column(1).Width  = 5;   // STT
        ws.Column(2).Width  = 30;  // Tên vật phẩm
        ws.Column(3).Width  = 25;  // Danh mục
        ws.Column(4).Width  = 25;  // Đối tượng
        ws.Column(5).Width  = 15;  // Loại vật phẩm
        ws.Column(6).Width  = 12;  // Đơn vị
        ws.Column(7).Width  = 35;  // Mô tả vật phẩm
        ws.Column(8).Width  = 12;  // Số lượng
        ws.Column(9).Width  = 16;  // Ngày hết hạn
        ws.Column(10).Width = 16;  // Ngày nhận

        // ── Data rows (2..102) ─────────────────────────────────────────────────
        for (int r = TemplateDataStartRow; r <= TemplateDataEndRow; r++)
        {
            int rowNum = r - TemplateDataStartRow + 1;

            ws.Row(r).Height = 20;

            // Col A: STT (auto-number)
            ws.Cell(r, 1).Value = rowNum;
            ws.Cell(r, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Col C: Danh mục — dropdown from named range "Categories"
            var dvCategory = ws.Cell(r, 3).GetDataValidation();
            dvCategory.List("=Categories");
            dvCategory.IgnoreBlanks = true;
            dvCategory.ShowErrorMessage = true;
            dvCategory.ErrorTitle = "Lỗi";
            dvCategory.ErrorMessage = "Vui lòng chọn danh mục từ danh sách.";

            // Col B: Tên vật phẩm — dependent dropdown via INDIRECT
            // Formula: =INDIRECT("Cat_" & RIGHT(C2, LEN(C2) - FIND(" - ", C2) - 2))
            // This extracts the code part after " - " in the category dropdown value
            var dvItem = ws.Cell(r, 2).GetDataValidation();
            dvItem.List($"=INDIRECT(\"Cat_\"&RIGHT(C{r},LEN(C{r})-FIND(\" - \",C{r})-2))");
            dvItem.IgnoreBlanks = true;
            dvItem.ShowInputMessage = true;
            dvItem.InputTitle = "Gợi ý";
            dvItem.InputMessage = "Chọn vật phẩm có sẵn hoặc tự nhập tên mới.";
            dvItem.ShowErrorMessage = false; // Allow manual entry of new items

            // Col D: Đối tượng — VLOOKUP auto-fill from DM_Lookup col 2 (existing item)
            //         Dropdown guidance for new items (overrides formula when user types)
            ws.Cell(r, 4).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$B,2,FALSE),\"\")";
            var dvTargetGroup = ws.Cell(r, 4).GetDataValidation();
            dvTargetGroup.List("=TargetGroupOptions");
            dvTargetGroup.IgnoreBlanks = true;
            dvTargetGroup.ShowInputMessage = true;
            dvTargetGroup.InputTitle = "Gợi ý";
            dvTargetGroup.InputMessage = "Nếu vật phẩm mới, chọn đối tượng theo mẫu: tên - code hoặc id.";
            dvTargetGroup.ShowErrorMessage = false;

            // Col E: Loại vật phẩm — VLOOKUP auto-fill from DM_Lookup col 3 (existing item)
            //         Dropdown guidance for new items
            ws.Cell(r, 5).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$C,3,FALSE),\"\")";
            var dvItemType = ws.Cell(r, 5).GetDataValidation();
            dvItemType.List("=ItemTypeOptions");
            dvItemType.IgnoreBlanks = true;
            dvItemType.ShowInputMessage = true;
            dvItemType.InputTitle = "Gợi ý";
            dvItemType.InputMessage = "Nếu vật phẩm mới, chọn loại vật phẩm theo mẫu: tên - code hoặc id.";
            dvItemType.ShowErrorMessage = false;

            // Col F: Đơn vị — VLOOKUP auto-fill (editable)
            ws.Cell(r, 6).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$D,4,FALSE),\"\")";

            // Col G: Mô tả vật phẩm — VLOOKUP auto-fill (editable)
            ws.Cell(r, 7).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$E,5,FALSE),\"\")";

            // Col H: Số lượng — number format
            ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0";

            // Col I: Ngày hết hạn — DateOnly (dd/MM/yyyy)
            ws.Cell(r, 9).Style.NumberFormat.Format = "dd/MM/yyyy";
            var dvExpiryDate = ws.Cell(r, 9).GetDataValidation();
            dvExpiryDate.Date.Between(new DateTime(2020, 1, 1), new DateTime(2099, 12, 31));
            dvExpiryDate.IgnoreBlanks = true;
            dvExpiryDate.ShowInputMessage = true;
            dvExpiryDate.InputTitle = "Ngày hết hạn";
            dvExpiryDate.InputMessage = "Nhập ngày (dd/MM/yyyy).\nVí dụ: 25/12/2026\nĐể trống nếu không có.";
            dvExpiryDate.ShowErrorMessage = true;
            dvExpiryDate.ErrorTitle = "Sai định dạng";
            dvExpiryDate.ErrorMessage = "Vui lòng nhập ngày hợp lệ (dd/MM/yyyy).";
            dvExpiryDate.ErrorStyle = XLErrorStyle.Warning;

            // Col J: Ngày nhận — DateTime (dd/MM/yyyy HH:mm)
            ws.Cell(r, 10).Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
            var dvReceivedDate = ws.Cell(r, 10).GetDataValidation();
            dvReceivedDate.Date.Between(new DateTime(2020, 1, 1), new DateTime(2099, 12, 31));
            dvReceivedDate.IgnoreBlanks = true;
            dvReceivedDate.ShowInputMessage = true;
            dvReceivedDate.InputTitle = "Ngày nhận";
            dvReceivedDate.InputMessage = "Nhập ngày giờ (dd/MM/yyyy HH:mm).\nVí dụ: 24/03/2026 14:30";
            dvReceivedDate.ShowErrorMessage = true;
            dvReceivedDate.ErrorTitle = "Sai định dạng";
            dvReceivedDate.ErrorMessage = "Vui lòng nhập ngày giờ hợp lệ (dd/MM/yyyy HH:mm).";
            dvReceivedDate.ErrorStyle = XLErrorStyle.Warning;

            // Alternate row background
            if (rowNum % 2 == 0)
            {
                var rowRange = ws.Range(r, 1, r, TemplateCols);
                rowRange.Style.Fill.SetBackgroundColor(OrangeLight);
            }

            // Thin borders for all data cells
            var dataRow = ws.Range(r, 1, r, TemplateCols);
            dataRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRow.Style.Border.InsideBorder  = XLBorderStyleValues.Hair;
            dataRow.Style.Border.OutsideBorderColor = XLColor.FromHtml("#BDBDBD");
            dataRow.Style.Border.InsideBorderColor  = XLColor.FromHtml("#E0E0E0");
        }

        // ── Freeze header row ─────────────────────────────────────────────────
        ws.SheetView.FreezeRows(1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Purchase Import Template — Excel file with item columns + unit price
    //  (VAT invoice info is handled by the frontend, not in this template)
    // ═══════════════════════════════════════════════════════════════════════════

    private static readonly string[] PurchaseTemplateHeaders =
    [
        "STT",              // A  (1)
        "Tên vật phẩm",    // B  (2)
        "Danh mục",         // C  (3)
        "Đối tượng",        // D  (4)
        "Loại vật phẩm",   // E  (5)
        "Đơn vị",           // F  (6)
        "Mô tả vật phẩm",   // G  (7)
        "Số lượng (*)",     // H  (8)
        "Đơn giá (VNĐ)",   // I  (9)
        "Ngày hết hạn",    // J  (10)
        "Ngày nhận",        // K  (11)
    ];

    private const int PurchaseDataStartRow = 2;
    private const int PurchaseDataEndRow   = 102; // 101 data rows (2..102)
    private const int PurchaseCols         = 11;  // A..K

    public byte[] GeneratePurchaseImportTemplate(
        IReadOnlyList<DonationImportCategoryInfo> categories,
        IReadOnlyList<DonationImportItemInfo> items,
        IReadOnlyList<DonationImportTargetGroupInfo> targetGroups)
    {
        using var workbook = new XLWorkbook();

        // ── 1. Build hidden reference sheets (reuse same helpers as donation) ─
        var wsDanhMuc = workbook.Worksheets.Add("DM_DanhMuc");
        var wsVatPham = workbook.Worksheets.Add("DM_VatPham");
        var wsLookup  = workbook.Worksheets.Add("DM_Lookup");
        var wsMeta    = workbook.Worksheets.Add("DM_Metadata");

        BuildCategorySheet(wsDanhMuc, categories, workbook);
        BuildItemSheet(wsVatPham, categories, items, workbook);
        BuildLookupSheet(wsLookup, items);
        BuildMetadataSheet(wsMeta, items, targetGroups, workbook);

        wsDanhMuc.Visibility = XLWorksheetVisibility.VeryHidden;
        wsVatPham.Visibility = XLWorksheetVisibility.VeryHidden;
        wsLookup.Visibility  = XLWorksheetVisibility.VeryHidden;
        wsMeta.Visibility    = XLWorksheetVisibility.VeryHidden;

        // ── 2. Build main entry sheet ─────────────────────────────────────────
        var ws = workbook.Worksheets.Add("Nhập kho mua sắm");
        ws.SetTabActive();

        BuildPurchaseMainSheet(ws);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── Purchase main entry sheet ────────────────────────────────────────────
    private static void BuildPurchaseMainSheet(IXLWorksheet ws)
    {
        // ── Header row styling ────────────────────────────────────────────────
        for (int c = 0; c < PurchaseTemplateHeaders.Length; c++)
            ws.Cell(1, c + 1).Value = PurchaseTemplateHeaders[c];

        var headerRange = ws.Range(1, 1, 1, PurchaseCols);
        headerRange.Style
            .Font.SetBold(true)
            .Font.SetFontSize(11)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true)
            .Fill.SetBackgroundColor(OrangeMid)
            .Font.SetFontColor(White);
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
        ws.Row(1).Height = 24;

        // ── Column widths ─────────────────────────────────────────────────────
        ws.Column(1).Width  = 5;   // A: STT
        ws.Column(2).Width  = 30;  // B: Tên vật phẩm
        ws.Column(3).Width  = 25;  // C: Danh mục
        ws.Column(4).Width  = 25;  // D: Đối tượng
        ws.Column(5).Width  = 15;  // E: Loại vật phẩm
        ws.Column(6).Width  = 12;  // F: Đơn vị
        ws.Column(7).Width  = 35;  // G: Mô tả vật phẩm
        ws.Column(8).Width  = 12;  // H: Số lượng
        ws.Column(9).Width  = 16;  // I: Đơn giá
        ws.Column(10).Width = 16;  // J: Ngày hết hạn
        ws.Column(11).Width = 18;  // K: Ngày nhận

        // ── Data rows (2..102) ─────────────────────────────────────────────────
        for (int r = PurchaseDataStartRow; r <= PurchaseDataEndRow; r++)
        {
            int rowNum = r - PurchaseDataStartRow + 1;

            ws.Row(r).Height = 20;

            // Col A: STT (auto-number)
            ws.Cell(r, 1).Value = rowNum;
            ws.Cell(r, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Col C: Danh mục — dropdown from named range "Categories"
            var dvCategory = ws.Cell(r, 3).GetDataValidation();
            dvCategory.List("=Categories");
            dvCategory.IgnoreBlanks = true;
            dvCategory.ShowErrorMessage = true;
            dvCategory.ErrorTitle = "Lỗi";
            dvCategory.ErrorMessage = "Vui lòng chọn danh mục từ danh sách.";

            // Col B: Tên vật phẩm — dependent dropdown via INDIRECT on col C
            var dvItem = ws.Cell(r, 2).GetDataValidation();
            dvItem.List($"=INDIRECT(\"Cat_\"&RIGHT(C{r},LEN(C{r})-FIND(\" - \",C{r})-2))");
            dvItem.IgnoreBlanks = true;
            dvItem.ShowInputMessage = true;
            dvItem.InputTitle = "Gợi ý";
            dvItem.InputMessage = "Chọn vật phẩm có sẵn hoặc tự nhập tên mới.";
            dvItem.ShowErrorMessage = false; // Allow manual entry of new items

            // Col D: Đối tượng — VLOOKUP auto-fill from DM_Lookup col 2 (existing item)
            //         Dropdown guidance for new items
            ws.Cell(r, 4).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$B,2,FALSE),\"\")";
            var dvTargetGroup = ws.Cell(r, 4).GetDataValidation();
            dvTargetGroup.List("=TargetGroupOptions");
            dvTargetGroup.IgnoreBlanks = true;
            dvTargetGroup.ShowInputMessage = true;
            dvTargetGroup.InputTitle = "Gợi ý";
            dvTargetGroup.InputMessage = "Nếu vật phẩm mới, chọn đối tượng theo mẫu: tên - code hoặc id.";
            dvTargetGroup.ShowErrorMessage = false;

            // Col E: Loại vật phẩm — VLOOKUP auto-fill from DM_Lookup col 3 (existing item)
            //         Dropdown guidance for new items
            ws.Cell(r, 5).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$C,3,FALSE),\"\")";
            var dvItemType = ws.Cell(r, 5).GetDataValidation();
            dvItemType.List("=ItemTypeOptions");
            dvItemType.IgnoreBlanks = true;
            dvItemType.ShowInputMessage = true;
            dvItemType.InputTitle = "Gợi ý";
            dvItemType.InputMessage = "Nếu vật phẩm mới, chọn loại vật phẩm theo mẫu: tên - code hoặc id.";
            dvItemType.ShowErrorMessage = false;

            // Col F: Đơn vị — VLOOKUP auto-fill
            ws.Cell(r, 6).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$D,4,FALSE),\"\")";

            // Col G: Mô tả vật phẩm — VLOOKUP auto-fill
            ws.Cell(r, 7).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$E,5,FALSE),\"\")";

            // Col H: Số lượng (*) — number format
            ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0";

            // Col I: Đơn giá (VNĐ) — currency format (purchase-specific)
            ws.Cell(r, 9).Style.NumberFormat.Format = "#,##0";
            var dvUnitPrice = ws.Cell(r, 9).GetDataValidation();
            dvUnitPrice.ShowInputMessage = true;
            dvUnitPrice.InputTitle = "Đơn giá";
            dvUnitPrice.InputMessage = "Giá mua mỗi đơn vị (VNĐ).\nĐể trống nếu không có.";

            // Col J: Ngày hết hạn — DateOnly (dd/MM/yyyy)
            ws.Cell(r, 10).Style.NumberFormat.Format = "dd/MM/yyyy";
            var dvExpiryDate = ws.Cell(r, 10).GetDataValidation();
            dvExpiryDate.Date.Between(new DateTime(2020, 1, 1), new DateTime(2099, 12, 31));
            dvExpiryDate.IgnoreBlanks = true;
            dvExpiryDate.ShowInputMessage = true;
            dvExpiryDate.InputTitle = "Ngày hết hạn";
            dvExpiryDate.InputMessage = "Nhập ngày (dd/MM/yyyy).\nVí dụ: 25/12/2026\nĐể trống nếu không có.";
            dvExpiryDate.ShowErrorMessage = true;
            dvExpiryDate.ErrorTitle = "Sai định dạng";
            dvExpiryDate.ErrorMessage = "Vui lòng nhập ngày hợp lệ (dd/MM/yyyy).";
            dvExpiryDate.ErrorStyle = XLErrorStyle.Warning;

            // Col K: Ngày nhận — DateTime (dd/MM/yyyy HH:mm)
            ws.Cell(r, 11).Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
            var dvReceivedDate = ws.Cell(r, 11).GetDataValidation();
            dvReceivedDate.Date.Between(new DateTime(2020, 1, 1), new DateTime(2099, 12, 31));
            dvReceivedDate.IgnoreBlanks = true;
            dvReceivedDate.ShowInputMessage = true;
            dvReceivedDate.InputTitle = "Ngày nhận";
            dvReceivedDate.InputMessage = "Nhập ngày giờ (dd/MM/yyyy HH:mm).\nVí dụ: 24/03/2026 14:30";
            dvReceivedDate.ShowErrorMessage = true;
            dvReceivedDate.ErrorTitle = "Sai định dạng";
            dvReceivedDate.ErrorMessage = "Vui lòng nhập ngày giờ hợp lệ (dd/MM/yyyy HH:mm).";
            dvReceivedDate.ErrorStyle = XLErrorStyle.Warning;

            // ── Row styling ───────────────────────────────────────────────────
            if (rowNum % 2 == 0)
            {
                var rowRange = ws.Range(r, 1, r, PurchaseCols);
                rowRange.Style.Fill.SetBackgroundColor(OrangeLight);
            }

            // Thin borders for all data cells
            var dataRow = ws.Range(r, 1, r, PurchaseCols);
            dataRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRow.Style.Border.InsideBorder  = XLBorderStyleValues.Hair;
            dataRow.Style.Border.OutsideBorderColor = XLColor.FromHtml("#BDBDBD");
            dataRow.Style.Border.InsideBorderColor  = XLColor.FromHtml("#E0E0E0");
        }

        // ── Freeze header row ─────────────────────────────────────────────────
        ws.SheetView.FreezeRows(1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Funding Request Template — like purchase but without expiry/received date
    //  Cols: STT (A), Tên vật phẩm (B), Danh mục (C), Đối tượng (D),
    //        Loại vật phẩm (E), Đơn vị (F), Mô tả vật phẩm (G),
    //        Số lượng (*) (H), Đơn giá (VNĐ) (I)  — 9 cols total
    // ═══════════════════════════════════════════════════════════════════════════

    private static readonly string[] FundingRequestTemplateHeaders =
    [
        "STT",              // A  (1)
        "Tên vật phẩm",    // B  (2)
        "Danh mục",         // C  (3)
        "Đối tượng",        // D  (4)
        "Loại vật phẩm",   // E  (5)
        "Đơn vị",           // F  (6)
        "Mô tả vật phẩm",   // G  (7)
        "Số lượng (*)",     // H  (8)
        "Đơn giá (VNĐ)",   // I  (9)
    ];

    private const int FundingRequestDataStartRow = 2;
    private const int FundingRequestDataEndRow   = 102; // 101 data rows (2..102)
    private const int FundingRequestCols         = 9;   // A..I

    public byte[] GenerateFundingRequestTemplate(
        IReadOnlyList<DonationImportCategoryInfo> categories,
        IReadOnlyList<DonationImportItemInfo> items,
        IReadOnlyList<DonationImportTargetGroupInfo> targetGroups)
    {
        using var workbook = new XLWorkbook();

        // ── 1. Build hidden reference sheets (reuse same helpers as donation/purchase) ─
        var wsDanhMuc = workbook.Worksheets.Add("DM_DanhMuc");
        var wsVatPham = workbook.Worksheets.Add("DM_VatPham");
        var wsLookup  = workbook.Worksheets.Add("DM_Lookup");
        var wsMeta    = workbook.Worksheets.Add("DM_Metadata");

        BuildCategorySheet(wsDanhMuc, categories, workbook);
        BuildItemSheet(wsVatPham, categories, items, workbook);
        BuildLookupSheet(wsLookup, items);
        BuildMetadataSheet(wsMeta, items, targetGroups, workbook);

        wsDanhMuc.Visibility = XLWorksheetVisibility.VeryHidden;
        wsVatPham.Visibility = XLWorksheetVisibility.VeryHidden;
        wsLookup.Visibility  = XLWorksheetVisibility.VeryHidden;
        wsMeta.Visibility    = XLWorksheetVisibility.VeryHidden;

        // ── 2. Build main entry sheet ─────────────────────────────────────────
        var ws = workbook.Worksheets.Add("Yêu cầu cấp tiền");
        ws.SetTabActive();

        BuildFundingRequestMainSheet(ws);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── Funding Request main entry sheet ────────────────────────────────────
    private static void BuildFundingRequestMainSheet(IXLWorksheet ws)
    {
        // ── Header row styling ────────────────────────────────────────────────
        for (int c = 0; c < FundingRequestTemplateHeaders.Length; c++)
            ws.Cell(1, c + 1).Value = FundingRequestTemplateHeaders[c];

        var headerRange = ws.Range(1, 1, 1, FundingRequestCols);
        headerRange.Style
            .Font.SetBold(true)
            .Font.SetFontSize(11)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Alignment.SetWrapText(true)
            .Fill.SetBackgroundColor(OrangeMid)
            .Font.SetFontColor(White);
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
        ws.Row(1).Height = 24;

        // ── Column widths ─────────────────────────────────────────────────────
        ws.Column(1).Width  = 5;   // A: STT
        ws.Column(2).Width  = 30;  // B: Tên vật phẩm
        ws.Column(3).Width  = 25;  // C: Danh mục
        ws.Column(4).Width  = 25;  // D: Đối tượng
        ws.Column(5).Width  = 15;  // E: Loại vật phẩm
        ws.Column(6).Width  = 12;  // F: Đơn vị
        ws.Column(7).Width  = 35;  // G: Mô tả vật phẩm
        ws.Column(8).Width  = 12;  // H: Số lượng
        ws.Column(9).Width  = 16;  // I: Đơn giá

        // ── Data rows (2..102) ─────────────────────────────────────────────────
        for (int r = FundingRequestDataStartRow; r <= FundingRequestDataEndRow; r++)
        {
            int rowNum = r - FundingRequestDataStartRow + 1;

            ws.Row(r).Height = 20;

            // Col A: STT (auto-number)
            ws.Cell(r, 1).Value = rowNum;
            ws.Cell(r, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Col C: Danh mục — dropdown from named range "Categories"
            var dvCategory = ws.Cell(r, 3).GetDataValidation();
            dvCategory.List("=Categories");
            dvCategory.IgnoreBlanks = true;
            dvCategory.ShowErrorMessage = true;
            dvCategory.ErrorTitle = "Lỗi";
            dvCategory.ErrorMessage = "Vui lòng chọn danh mục từ danh sách.";

            // Col B: Tên vật phẩm — dependent dropdown via INDIRECT on col C
            var dvItem = ws.Cell(r, 2).GetDataValidation();
            dvItem.List($"=INDIRECT(\"Cat_\"&RIGHT(C{r},LEN(C{r})-FIND(\" - \",C{r})-2))");
            dvItem.IgnoreBlanks = true;
            dvItem.ShowInputMessage = true;
            dvItem.InputTitle = "Gợi ý";
            dvItem.InputMessage = "Chọn vật phẩm có sẵn hoặc tự nhập tên mới.";
            dvItem.ShowErrorMessage = false; // Allow manual entry of new items

            // Col D: Đối tượng — VLOOKUP auto-fill from DM_Lookup col 2
            ws.Cell(r, 4).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$B,2,FALSE),\"\")";
            var dvTargetGroup = ws.Cell(r, 4).GetDataValidation();
            dvTargetGroup.List("=TargetGroupOptions");
            dvTargetGroup.IgnoreBlanks = true;
            dvTargetGroup.ShowInputMessage = true;
            dvTargetGroup.InputTitle = "Gợi ý";
            dvTargetGroup.InputMessage = "Nếu vật phẩm mới, chọn đối tượng theo mẫu: tên - code hoặc id.";
            dvTargetGroup.ShowErrorMessage = false;

            // Col E: Loại vật phẩm — VLOOKUP auto-fill from DM_Lookup col 3
            ws.Cell(r, 5).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$C,3,FALSE),\"\")";
            var dvItemType = ws.Cell(r, 5).GetDataValidation();
            dvItemType.List("=ItemTypeOptions");
            dvItemType.IgnoreBlanks = true;
            dvItemType.ShowInputMessage = true;
            dvItemType.InputTitle = "Gợi ý";
            dvItemType.InputMessage = "Nếu vật phẩm mới, chọn loại vật phẩm theo mẫu: tên - code hoặc id.";
            dvItemType.ShowErrorMessage = false;

            // Col F: Đơn vị — VLOOKUP auto-fill
            ws.Cell(r, 6).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$D,4,FALSE),\"\")";

            // Col G: Mô tả vật phẩm — VLOOKUP auto-fill
            ws.Cell(r, 7).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$E,5,FALSE),\"\")";

            // Col H: Số lượng (*) — number format
            ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0";

            // Col I: Đơn giá (VNĐ) — currency format
            ws.Cell(r, 9).Style.NumberFormat.Format = "#,##0";
            var dvUnitPrice = ws.Cell(r, 9).GetDataValidation();
            dvUnitPrice.ShowInputMessage = true;
            dvUnitPrice.InputTitle = "Đơn giá";
            dvUnitPrice.InputMessage = "Giá dự kiến mỗi đơn vị (VNĐ).\nĐể trống nếu chưa xác định.";

            // ── Row styling ───────────────────────────────────────────────────
            if (rowNum % 2 == 0)
            {
                var rowRange = ws.Range(r, 1, r, FundingRequestCols);
                rowRange.Style.Fill.SetBackgroundColor(OrangeLight);
            }

            // Thin borders for all data cells
            var dataRow = ws.Range(r, 1, r, FundingRequestCols);
            dataRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRow.Style.Border.InsideBorder  = XLBorderStyleValues.Hair;
            dataRow.Style.Border.OutsideBorderColor = XLColor.FromHtml("#BDBDBD");
            dataRow.Style.Border.InsideBorderColor  = XLColor.FromHtml("#E0E0E0");
        }

        // ── Freeze header row ─────────────────────────────────────────────────
        ws.SheetView.FreezeRows(1);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Depot Closure — External Resolution Template
    // ═══════════════════════════════════════════════════════════════════════════

    private static readonly string[] ClosureTemplateHeaders =
    [
        "STT",               // A
        "Tên vật phẩm",     // B
        "Danh mục",          // C
        "Đối tượng",         // D  ← NEW
        "Loại vật phẩm",    // E
        "Đơn vị",            // F
        "Ngày nhập",         // G
        "Hạn sử dụng",      // H
        "Số lượng",          // I
        "Đơn giá (VNĐ)",    // J  ← manager điền
        "Thành tiền (VNĐ)", // K  ← formula
        "Hình thức xử lý",  // L  ← manager điền (dropdown, cho phép nhập tay)
        "Người nhận",        // M  ← manager điền
        "Ghi chú"            // N  ← manager điền
    ];

    private const int ClosureCols = 14; // A..N
    private const int ClosurePreFilledCols = 9; // A..I (pre-filled)

    public byte[] GenerateClosureExternalTemplate(string depotName, IReadOnlyList<ClosureInventoryLotItemDto> items)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Xử lý tồn kho");

        // ─── Row 1: Title banner ─────────────────────────────────────────────
        ws.Cell(1, 1).Value = $"MẪU XỬ LÝ TỒN KHO BÊN NGOÀI — {depotName.ToUpper()}";
        var titleRange = ws.Range(1, 1, 1, ClosureCols);
        titleRange.Merge();
        titleRange.Style
            .Font.SetBold(true)
            .Font.SetFontSize(13)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Fill.SetBackgroundColor(OrangeDark)
            .Font.SetFontColor(White);
        ws.Row(1).Height = 28;

        // ─── Row 2: Export timestamp ──────────────────────────────────────────
        ws.Cell(2, 1).Value = $"Ngày xuất: {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}";
        var tsRange = ws.Range(2, 1, 2, ClosureCols);
        tsRange.Merge();
        ws.Cell(2, 1).Style
            .Font.SetItalic(true)
            .Font.SetFontSize(10)
            .Font.SetFontColor(Black)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

        // ─── Row 3: Header ────────────────────────────────────────────────────
        int headerRow = 3;
        for (int c = 0; c < ClosureTemplateHeaders.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = ClosureTemplateHeaders[c];
            cell.Style
                .Font.SetBold(true)
                .Font.SetFontSize(11)
                .Font.SetFontColor(White)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center);

            // Pre-filled columns (A-H): orange header
            // Editable columns (I-M): dark blue-gray header
            cell.Style.Fill.SetBackgroundColor(c < ClosurePreFilledCols ? OrangeMid : LockedHeaderColor);
        }
        ws.Row(headerRow).Height = 24;

        // ─── Data rows ───────────────────────────────────────────────────────
        int dataStartRow = 4;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            int r = dataStartRow + i;

            ws.Cell(r, 1).Value = i + 1;                       // A: STT
            ws.Cell(r, 2).Value = item.ItemName;                // B: Tên vật phẩm
            ws.Cell(r, 3).Value = item.CategoryName;            // C: Danh mục
            ws.Cell(r, 4).Value = item.TargetGroup;             // D: Đối tượng
            ws.Cell(r, 5).Value = item.ItemType;                // E: Loại vật phẩm
            ws.Cell(r, 6).Value = item.Unit;                    // F: Đơn vị
            // G: Ngày nhập
            if (item.ReceivedDate.HasValue)
            {
                ws.Cell(r, 7).Value = item.ReceivedDate.Value.ToString("dd/MM/yyyy");
            }
            // H: Hạn sử dụng
            if (item.ExpiredDate.HasValue)
            {
                ws.Cell(r, 8).Value = item.ExpiredDate.Value.ToString("dd/MM/yyyy");
            }
            ws.Cell(r, 9).Value = item.Quantity;                // I: Số lượng

            // J: Đơn giá (editable — manager điền)
            // K: Thành tiền = Số lượng × Đơn giá (formula)
            ws.Cell(r, 11).FormulaA1 = $"I{r}*J{r}";
            ws.Cell(r, 11).Style.NumberFormat.Format = "#,##0";

            // Pre-filled cells: read-only style (light gray background)
            var preFilledRange = ws.Range(r, 1, r, ClosurePreFilledCols);
            preFilledRange.Style.Fill.SetBackgroundColor(
                i % 2 == 0 ? OrangeLight : XLColor.White);
            preFilledRange.Style.Font.SetFontColor(Black);

            // Editable cells: white background with dashed border
            var editableRange = ws.Range(r, ClosurePreFilledCols + 1, r, ClosureCols);
            editableRange.Style.Fill.SetBackgroundColor(XLColor.White);
            editableRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Dashed);
            editableRange.Style.Border.SetOutsideBorderColor(XLColor.FromHtml("#90A4AE"));

            // Dropdown for Handling Method (column L) — cho phép nhập tay nếu có hình thức khác
            var dvHandling = ws.Cell(r, 12).GetDataValidation();
            dvHandling.List("\"Donated,Disposed,Sold\"");
            dvHandling.IgnoreBlanks = true;
            dvHandling.ShowErrorMessage = false;  // Cho phép nhập tay giá trị ngoài danh sách
            dvHandling.InputTitle = "Hình thức xử lý";
            dvHandling.InputMessage = "Chọn hoặc nhập tay cách xử lý vật phẩm này";

            // Full row thin border
            var dataRow = ws.Range(r, 1, r, ClosureCols);
            dataRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRow.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            dataRow.Style.Border.OutsideBorderColor = XLColor.FromHtml("#BDBDBD");
            dataRow.Style.Border.InsideBorderColor = XLColor.FromHtml("#E0E0E0");
        }

        // ─── Column widths ───────────────────────────────────────────────────
        ws.Column(1).Width = 6;    // STT
        ws.Column(2).Width = 30;   // Tên vật phẩm
        ws.Column(3).Width = 20;   // Danh mục
        ws.Column(4).Width = 22;   // Đối tượng
        ws.Column(5).Width = 14;   // Loại vật phẩm
        ws.Column(6).Width = 10;   // Đơn vị
        ws.Column(7).Width = 14;   // Ngày nhập
        ws.Column(8).Width = 14;   // Hạn sử dụng
        ws.Column(9).Width = 12;   // Số lượng
        ws.Column(10).Width = 16;  // Đơn giá
        ws.Column(11).Width = 18;  // Thành tiền
        ws.Column(12).Width = 22;  // Hình thức xử lý
        ws.Column(13).Width = 25;  // Người nhận
        ws.Column(14).Width = 30;  // Ghi chú

        // ─── Protect pre-filled columns ──────────────────────────────────────
        ws.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
