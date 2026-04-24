using ClosedXML.Excel;
using RESQ.Application.Common.Constants;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Infrastructure.Services;

public class ExcelExportService : IExcelExportService
{
    // ---------------------------------------------------------------------------
    //  Constants for the inventory movement report (existing)
    // ---------------------------------------------------------------------------

    private static readonly string[] Headers =
    [
        "STT", "TÃªn Váº­t Pháº©m", "Danh má»¥c", "Äá»‘i tÆ°á»£ng", "Loáº¡i váº­t pháº©m",
        "ÄÆ¡n vá»‹", "ÄÆ¡n giÃ¡", "Sá»‘ lÆ°á»£ng", "NgÃ y nháº­n",
        "Loáº¡i HÃ nh Ä‘á»™ng", "Nguá»“n", "TÃªn nhiá»‡m vá»¥",
        "Serial / Lot ID"
    ];

    // -- Palette --------------------------------------------------------------
    private static readonly XLColor OrangeDark     = XLColor.FromHtml("#E65100");
    private static readonly XLColor OrangeMid      = XLColor.FromHtml("#FF8F00");
    private static readonly XLColor OrangeLight    = XLColor.FromHtml("#FFF3E0");
    private static readonly XLColor OrangeSummary  = XLColor.FromHtml("#FFE0B2");
    private static readonly XLColor Black          = XLColor.FromHtml("#212121");
    private static readonly XLColor White          = XLColor.White;
    private static readonly XLColor LockedCellColor   = XLColor.FromHtml("#ECEFF1"); // light blue-gray - VLOOKUP read-only cells
    private static readonly XLColor LockedHeaderColor = XLColor.FromHtml("#546E7A"); // dark blue-gray - locked column headers

    // ---------------------------------------------------------------------------
    //  Constants for the donation import template
    // ---------------------------------------------------------------------------

    private static readonly string[] TemplateHeaders =
    [
        "STT",              // A
        "TÃªn váº­t pháº©m",    // B
        "Danh má»¥c",         // C
        "Äá»‘i tÆ°á»£ng",        // D
        "Loáº¡i váº­t pháº©m",   // E
        "ÄÆ¡n vá»‹",           // F
        "MÃ´ táº£ váº­t pháº©m",   // G
        "Sá»‘ lÆ°á»£ng",         // H
        "Thá»ƒ tÃ­ch (dmÂ³)",   // I
        "CÃ¢n náº·ng (kg)",    // J
        "NgÃ y háº¿t háº¡n",    // K
        "NgÃ y nháº­n",        // L
    ];

    private const int TemplateDataStartRow = 2;
    private const int TemplateDataEndRow   = 102; // 100 data rows
    private const int TemplateCols         = 12;   // A..L

    public byte[] GenerateInventoryMovementReport(
        IReadOnlyList<InventoryMovementRow> rows,
        string title,
        string depotName)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Biáº¿n Ä‘á»™ng kho");

        int col = Headers.Length;

        // --- Row 1: Depot name banner -----------------------------------------
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

        // --- Row 2: Report title ----------------------------------------------
        ws.Cell(2, 1).Value = $"BÃO CÃO BIáº¾N Äá»˜NG KHO â€“ {title.ToUpper()}";
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

        // --- Row 3: Export timestamp ------------------------------------------
        ws.Cell(3, 1).Value = $"NgÃ y xuáº¥t: {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}";
        var tsRange = ws.Range(3, 1, 3, col);
        tsRange.Merge();
        ws.Cell(3, 1).Style
            .Font.SetItalic(true)
            .Font.SetFontSize(10)
            .Font.SetFontColor(Black)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

        // --- Row 4: Header ----------------------------------------------------
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

        // --- Data rows --------------------------------------------------------
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
            // Col 13: Serial number (Reusable) hoáº·c Lot ID (Consumable)
            ws.Cell(r, 13).Value = row.SerialNumber ?? (row.LotId.HasValue ? $"LÃ´ #{row.LotId.Value}" : string.Empty);

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

        // --- Summary row ------------------------------------------------------
        int summaryRow = dataStartRow + rows.Count;
        ws.Cell(summaryRow, 1).Value = "Tá»•ng sá»‘ dÃ²ng:";
        ws.Cell(summaryRow, 2).Value = rows.Count;
        var summaryRange = ws.Range(summaryRow, 1, summaryRow, col);
        summaryRange.Style
            .Font.SetBold(true)
            .Font.SetFontColor(Black)
            .Fill.SetBackgroundColor(OrangeSummary);
        summaryRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        summaryRange.Style.Border.OutsideBorderColor = Black;

        // --- Auto-fit & freeze ------------------------------------------------
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

    // ---------------------------------------------------------------------------
    //  Donation Import Template - Excel file with dependent dropdowns
    // ---------------------------------------------------------------------------

    public byte[] GenerateDonationImportTemplate(
        IReadOnlyList<DonationImportCategoryInfo> categories,
        IReadOnlyList<DonationImportItemInfo> items,
        IReadOnlyList<DonationImportTargetGroupInfo> targetGroups)
    {
        using var workbook = new XLWorkbook();

        // -- 1. Build hidden reference sheets ----------------------------------
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

        // -- 2. Build main entry sheet -----------------------------------------
        var ws = workbook.Worksheets.Add("Nháº­p kho tá»« thiá»‡n");
        ws.SetTabActive();

        BuildMainSheet(ws, categories);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // --- DM_DanhMuc: Category list â†’ named range "Categories" -----------------
    private static void BuildCategorySheet(
        IXLWorksheet ws,
        IReadOnlyList<DonationImportCategoryInfo> categories,
        XLWorkbook workbook)
    {
        ws.Cell(1, 1).Value = "Danh má»¥c";
        for (int i = 0; i < categories.Count; i++)
        {
            // Format: "Thá»±c pháº©m - Food"
            ws.Cell(i + 2, 1).Value = $"{categories[i].Name} - {categories[i].Code}";
        }

        // Named range "Categories" â†’ DM_DanhMuc!$A$2:$A${n+1}
        // Use Math.Max to ensure lastRow >= 2 when categories list is empty,
        // preventing an inverted range (startRow > endRow) which ClosedXML rejects.
        int lastRow = Math.Max(categories.Count + 1, 2);
        workbook.NamedRanges.Add("Categories", ws.Range(2, 1, lastRow, 1));
    }

    // --- DM_VatPham: One column per category code â†’ named ranges Cat_Food, Cat_Water... -
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
                    // Format: "MÃ¬ tÃ´m - 1"
                    ws.Cell(i + 2, col).Value = $"{catItems[i].Name} - {catItems[i].Id}";
                }

                // Named range: Cat_Food, Cat_Water, etc.
                int lastRow = catItems.Count + 1;
                var rangeName = $"Cat_{cat.Code}";
                if (!workbook.NamedRanges.TryGetValue(rangeName, out _))
                    workbook.NamedRanges.Add(rangeName, ws.Range(2, col, lastRow, col));
            }
            else
            {
                // Empty category - still create named range pointing to a single blank cell
                var rangeName = $"Cat_{cat.Code}";
                if (!workbook.NamedRanges.TryGetValue(rangeName, out _))
                    workbook.NamedRanges.Add(rangeName, ws.Range(2, col, 2, col));
            }

            col++;
        }
    }

    // --- DM_Lookup: Flat table for VLOOKUP (display name â†’ TargetGroup, ItemType, Unit)
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
        ws.Cell(1, 6).Value = "TheTich";
        ws.Cell(1, 7).Value = "CanNang";

        for (int i = 0; i < items.Count; i++)
        {
            int r = i + 2;
            // Lookup key must match the dropdown display: "MÃ¬ tÃ´m - 1"
            ws.Cell(r, 1).Value = $"{items[i].Name} - {items[i].Id}";
            ws.Cell(r, 2).Value = items[i].TargetGroupDisplay;
            ws.Cell(r, 3).Value = items[i].ItemTypeDisplay;
            ws.Cell(r, 4).Value = items[i].Unit;
            ws.Cell(r, 5).Value = items[i].Description;
            ws.Cell(r, 6).Value = items[i].VolumePerUnit;
            ws.Cell(r, 7).Value = items[i].WeightPerUnit;
        }
    }

    // --- DM_Metadata: Dropdown sources for manual input columns (TargetGroup, ItemType) ---
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

    private static void ConfigureManualEntryGuidance(
        IXLCell cell,
        string title,
        string inputMessage)
    {
        var validation = cell.GetDataValidation();
        validation.ShowInputMessage = true;
        validation.InputTitle = title;
        validation.InputMessage = inputMessage;
        validation.ShowErrorMessage = false;
    }

    // --- Main entry sheet: headers, STT, dropdowns, VLOOKUP formulas ----------
    private static void BuildMainSheet(
        IXLWorksheet ws,
        IReadOnlyList<DonationImportCategoryInfo> categories)
    {
        // -- Header row styling ------------------------------------------------
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

        // -- Column widths -----------------------------------------------------
        ws.Column(1).Width  = 5;   // STT
        ws.Column(2).Width  = 30;  // TÃªn váº­t pháº©m
        ws.Column(3).Width  = 25;  // Danh má»¥c
        ws.Column(4).Width  = 25;  // Äá»‘i tÆ°á»£ng
        ws.Column(5).Width  = 15;  // Loáº¡i váº­t pháº©m
        ws.Column(6).Width  = 12;  // ÄÆ¡n vá»‹
        ws.Column(7).Width  = 35;  // MÃ´ táº£ váº­t pháº©m
        ws.Column(8).Width  = 12;  // Sá»‘ lÆ°á»£ng
        ws.Column(9).Width  = 16;  // Thá»ƒ tÃ­ch (dmÂ³)
        ws.Column(10).Width = 16;  // CÃ¢n náº·ng (kg)
        ws.Column(11).Width = 16;  // NgÃ y háº¿t háº¡n
        ws.Column(12).Width = 16;  // NgÃ y nháº­n

        // -- Data rows (2..102) -------------------------------------------------
        for (int r = TemplateDataStartRow; r <= TemplateDataEndRow; r++)
        {
            int rowNum = r - TemplateDataStartRow + 1;

            ws.Row(r).Height = 20;

            // Col A: STT (auto-number)
            ws.Cell(r, 1).Value = rowNum;
            ws.Cell(r, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Col C: Danh má»¥c - dropdown from named range "Categories"
            var dvCategory = ws.Cell(r, 3).GetDataValidation();
            dvCategory.List("=Categories");
            dvCategory.IgnoreBlanks = true;
            dvCategory.ShowErrorMessage = true;
            dvCategory.ErrorTitle = "Lá»—i";
            dvCategory.ErrorMessage = "Vui lÃ²ng chá»n danh má»¥c tá»« danh sÃ¡ch.";

            // Col B: TÃªn váº­t pháº©m - dependent dropdown via INDIRECT
            // Formula: =INDIRECT("Cat_" & RIGHT(C2, LEN(C2) - FIND(" - ", C2) - 2))
            // This extracts the code part after " - " in the category dropdown value
            var dvItem = ws.Cell(r, 2).GetDataValidation();
            dvItem.List($"=INDIRECT(\"Cat_\"&RIGHT(C{r},LEN(C{r})-FIND(\" - \",C{r})-2))");
            dvItem.IgnoreBlanks = true;
            dvItem.ShowInputMessage = true;
            dvItem.InputTitle = "Gá»£i Ã½";
            dvItem.InputMessage = "Chá»n váº­t pháº©m cÃ³ sáºµn hoáº·c tá»± nháº­p tÃªn má»›i.";
            dvItem.ShowErrorMessage = false; // Allow manual entry of new items

            // Col D: Äá»‘i tÆ°á»£ng - VLOOKUP auto-fill from DM_Lookup col 2 (existing item)
            //         Dropdown guidance for new items (overrides formula when user types)
            ws.Cell(r, 4).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$B,2,FALSE),\"\")";
            var dvTargetGroup = ws.Cell(r, 4).GetDataValidation();
            dvTargetGroup.List("=TargetGroupOptions");
            dvTargetGroup.IgnoreBlanks = true;
            dvTargetGroup.ShowInputMessage = true;
            dvTargetGroup.InputTitle = "Gá»£i Ã½";
            dvTargetGroup.InputMessage = "Náº¿u váº­t pháº©m má»›i, chá»n Ä‘á»‘i tÆ°á»£ng theo máº«u: tÃªn - code hoáº·c id.";
            dvTargetGroup.ShowErrorMessage = false;

            // Col E: Loáº¡i váº­t pháº©m - VLOOKUP auto-fill from DM_Lookup col 3 (existing item)
            //         Dropdown guidance for new items
            ws.Cell(r, 5).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$C,3,FALSE),\"\")";
            var dvItemType = ws.Cell(r, 5).GetDataValidation();
            dvItemType.List("=ItemTypeOptions");
            dvItemType.IgnoreBlanks = true;
            dvItemType.ShowInputMessage = true;
            dvItemType.InputTitle = "Gá»£i Ã½";
            dvItemType.InputMessage = "Náº¿u váº­t pháº©m má»›i, chá»n loáº¡i váº­t pháº©m theo máº«u: tÃªn - code hoáº·c id.";
            dvItemType.ShowErrorMessage = false;

            // Col F: ÄÆ¡n vá»‹ - VLOOKUP auto-fill (editable)
            ws.Cell(r, 6).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$D,4,FALSE),\"\")";
            ConfigureManualEntryGuidance(
                ws.Cell(r, 6),
                "ÄÆ¡n vá»‹",
                "Náº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n Ä‘Æ¡n vá»‹.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng cá»™t nÃ y.");

            // Col G: MÃ´ táº£ váº­t pháº©m - VLOOKUP auto-fill (editable)
            ws.Cell(r, 7).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$E,5,FALSE),\"\")";
            ConfigureManualEntryGuidance(
                ws.Cell(r, 7),
                "MÃ´ táº£ váº­t pháº©m",
                "Náº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n mÃ´ táº£.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng cá»™t nÃ y.");

            // Col H: Sá»‘ lÆ°á»£ng - number format
            ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0";

            // Col I: Thá»ƒ tÃ­ch (dmÂ³) - VLOOKUP auto-fill from DM_Lookup col 6 (existing item), editable for new items
            ws.Cell(r, 9).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$G,6,FALSE),\"\")";
            ws.Cell(r, 9).Style.NumberFormat.Format = "#,##0.000";
            var dvVolume = ws.Cell(r, 9).GetDataValidation();
            dvVolume.ShowInputMessage = true;
            dvVolume.InputTitle = "Thá»ƒ tÃ­ch";
            dvVolume.InputMessage = "Thá»ƒ tÃ­ch má»—i Ä‘Æ¡n vá»‹ (dmÂ³).\nNáº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng.";
            dvVolume.ShowErrorMessage = false;

            // Col J: CÃ¢n náº·ng (kg) - VLOOKUP auto-fill from DM_Lookup col 7 (existing item), editable for new items
            ws.Cell(r, 10).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$G,7,FALSE),\"\")";
            ws.Cell(r, 10).Style.NumberFormat.Format = "#,##0.000";
            var dvWeight = ws.Cell(r, 10).GetDataValidation();
            dvWeight.ShowInputMessage = true;
            dvWeight.InputTitle = "CÃ¢n náº·ng";
            dvWeight.InputMessage = "CÃ¢n náº·ng má»—i Ä‘Æ¡n vá»‹ (kg).\nNáº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng.";
            dvWeight.ShowErrorMessage = false;

            // Col K: NgÃ y háº¿t háº¡n - DateOnly (dd/MM/yyyy)
            ws.Cell(r, 11).Style.NumberFormat.Format = "dd/MM/yyyy";
            var dvExpiryDate = ws.Cell(r, 11).GetDataValidation();
            dvExpiryDate.Date.Between(new DateTime(2020, 1, 1), new DateTime(2099, 12, 31));
            dvExpiryDate.IgnoreBlanks = true;
            dvExpiryDate.ShowInputMessage = true;
            dvExpiryDate.InputTitle = "NgÃ y háº¿t háº¡n";
            dvExpiryDate.InputMessage = "Nháº­p ngÃ y (dd/MM/yyyy).\nVÃ­ dá»¥: 25/12/2026\nÄá»ƒ trá»‘ng náº¿u khÃ´ng cÃ³.";
            dvExpiryDate.ShowErrorMessage = true;
            dvExpiryDate.ErrorTitle = "Sai Ä‘á»‹nh dáº¡ng";
            dvExpiryDate.ErrorMessage = "Vui lÃ²ng nháº­p ngÃ y há»£p lá»‡ (dd/MM/yyyy).";
            dvExpiryDate.ErrorStyle = XLErrorStyle.Warning;

            // Col L: NgÃ y nháº­n - DateTime (dd/MM/yyyy HH:mm)
            ws.Cell(r, 12).Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
            var dvReceivedDate = ws.Cell(r, 12).GetDataValidation();
            dvReceivedDate.Date.Between(new DateTime(2020, 1, 1), new DateTime(2099, 12, 31));
            dvReceivedDate.IgnoreBlanks = true;
            dvReceivedDate.ShowInputMessage = true;
            dvReceivedDate.InputTitle = "NgÃ y nháº­n";
            dvReceivedDate.InputMessage = "Nháº­p ngÃ y giá» (dd/MM/yyyy HH:mm).\nVÃ­ dá»¥: 24/03/2026 14:30";
            dvReceivedDate.ShowErrorMessage = true;
            dvReceivedDate.ErrorTitle = "Sai Ä‘á»‹nh dáº¡ng";
            dvReceivedDate.ErrorMessage = "Vui lÃ²ng nháº­p ngÃ y giá» há»£p lá»‡ (dd/MM/yyyy HH:mm).";
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

        // -- Freeze header row -------------------------------------------------
        ws.SheetView.FreezeRows(1);
    }

    // ---------------------------------------------------------------------------
    //  Purchase Import Template - Excel file with item columns + unit price
    //  (VAT invoice info is handled by the frontend, not in this template)
    // ---------------------------------------------------------------------------

    private static readonly string[] PurchaseTemplateHeaders =
    [
        "STT",              // A  (1)
        "TÃªn váº­t pháº©m",    // B  (2)
        "Danh má»¥c",         // C  (3)
        "Äá»‘i tÆ°á»£ng",        // D  (4)
        "Loáº¡i váº­t pháº©m",   // E  (5)
        "ÄÆ¡n vá»‹",           // F  (6)
        "MÃ´ táº£ váº­t pháº©m",   // G  (7)
        "Sá»‘ lÆ°á»£ng (*)",     // H  (8)
        "Thá»ƒ tÃ­ch (dmÂ³)",   // I  (9)
        "CÃ¢n náº·ng (kg)",    // J  (10)
        "ÄÆ¡n giÃ¡ (VNÄ)",   // K  (11)
        "NgÃ y háº¿t háº¡n",    // L  (12)
        "NgÃ y nháº­n",        // M  (13)
    ];

    private const int PurchaseDataStartRow = 2;
    private const int PurchaseDataEndRow   = 102; // 101 data rows (2..102)
    private const int PurchaseCols         = 13;  // A..M

    public byte[] GeneratePurchaseImportTemplate(
        IReadOnlyList<DonationImportCategoryInfo> categories,
        IReadOnlyList<DonationImportItemInfo> items,
        IReadOnlyList<DonationImportTargetGroupInfo> targetGroups)
    {
        using var workbook = new XLWorkbook();

        // -- 1. Build hidden reference sheets (reuse same helpers as donation) -
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

        // -- 2. Build main entry sheet -----------------------------------------
        var ws = workbook.Worksheets.Add("Nháº­p kho mua sáº¯m");
        ws.SetTabActive();

        BuildPurchaseMainSheet(ws);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // --- Purchase main entry sheet --------------------------------------------
    private static void BuildPurchaseMainSheet(IXLWorksheet ws)
    {
        // -- Header row styling ------------------------------------------------
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

        // -- Column widths -----------------------------------------------------
        ws.Column(1).Width  = 5;   // A: STT
        ws.Column(2).Width  = 30;  // B: TÃªn váº­t pháº©m
        ws.Column(3).Width  = 25;  // C: Danh má»¥c
        ws.Column(4).Width  = 25;  // D: Äá»‘i tÆ°á»£ng
        ws.Column(5).Width  = 15;  // E: Loáº¡i váº­t pháº©m
        ws.Column(6).Width  = 12;  // F: ÄÆ¡n vá»‹
        ws.Column(7).Width  = 35;  // G: MÃ´ táº£ váº­t pháº©m
        ws.Column(8).Width  = 12;  // H: Sá»‘ lÆ°á»£ng
        ws.Column(9).Width  = 16;  // I: Thá»ƒ tÃ­ch (dmÂ³)
        ws.Column(10).Width = 16;  // J: CÃ¢n náº·ng (kg)
        ws.Column(11).Width = 16;  // K: ÄÆ¡n giÃ¡
        ws.Column(12).Width = 16;  // L: NgÃ y háº¿t háº¡n
        ws.Column(13).Width = 18;  // M: NgÃ y nháº­n

        // -- Data rows (2..102) -------------------------------------------------
        for (int r = PurchaseDataStartRow; r <= PurchaseDataEndRow; r++)
        {
            int rowNum = r - PurchaseDataStartRow + 1;

            ws.Row(r).Height = 20;

            // Col A: STT (auto-number)
            ws.Cell(r, 1).Value = rowNum;
            ws.Cell(r, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Col C: Danh má»¥c - dropdown from named range "Categories"
            var dvCategory = ws.Cell(r, 3).GetDataValidation();
            dvCategory.List("=Categories");
            dvCategory.IgnoreBlanks = true;
            dvCategory.ShowErrorMessage = true;
            dvCategory.ErrorTitle = "Lá»—i";
            dvCategory.ErrorMessage = "Vui lÃ²ng chá»n danh má»¥c tá»« danh sÃ¡ch.";

            // Col B: TÃªn váº­t pháº©m - dependent dropdown via INDIRECT on col C
            var dvItem = ws.Cell(r, 2).GetDataValidation();
            dvItem.List($"=INDIRECT(\"Cat_\"&RIGHT(C{r},LEN(C{r})-FIND(\" - \",C{r})-2))");
            dvItem.IgnoreBlanks = true;
            dvItem.ShowInputMessage = true;
            dvItem.InputTitle = "Gá»£i Ã½";
            dvItem.InputMessage = "Chá»n váº­t pháº©m cÃ³ sáºµn hoáº·c tá»± nháº­p tÃªn má»›i.";
            dvItem.ShowErrorMessage = false; // Allow manual entry of new items

            // Col D: Äá»‘i tÆ°á»£ng - VLOOKUP auto-fill from DM_Lookup col 2 (existing item)
            //         Dropdown guidance for new items
            ws.Cell(r, 4).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$B,2,FALSE),\"\")";
            var dvTargetGroup = ws.Cell(r, 4).GetDataValidation();
            dvTargetGroup.List("=TargetGroupOptions");
            dvTargetGroup.IgnoreBlanks = true;
            dvTargetGroup.ShowInputMessage = true;
            dvTargetGroup.InputTitle = "Gá»£i Ã½";
            dvTargetGroup.InputMessage = "Náº¿u váº­t pháº©m má»›i, chá»n Ä‘á»‘i tÆ°á»£ng theo máº«u: tÃªn - code hoáº·c id.";
            dvTargetGroup.ShowErrorMessage = false;

            // Col E: Loáº¡i váº­t pháº©m - VLOOKUP auto-fill from DM_Lookup col 3 (existing item)
            //         Dropdown guidance for new items
            ws.Cell(r, 5).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$C,3,FALSE),\"\")";
            var dvItemType = ws.Cell(r, 5).GetDataValidation();
            dvItemType.List("=ItemTypeOptions");
            dvItemType.IgnoreBlanks = true;
            dvItemType.ShowInputMessage = true;
            dvItemType.InputTitle = "Gá»£i Ã½";
            dvItemType.InputMessage = "Náº¿u váº­t pháº©m má»›i, chá»n loáº¡i váº­t pháº©m theo máº«u: tÃªn - code hoáº·c id.";
            dvItemType.ShowErrorMessage = false;

            // Col F: ÄÆ¡n vá»‹ - VLOOKUP auto-fill
            ws.Cell(r, 6).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$D,4,FALSE),\"\")";
            ConfigureManualEntryGuidance(
                ws.Cell(r, 6),
                "ÄÆ¡n vá»‹",
                "Náº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n Ä‘Æ¡n vá»‹.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng cá»™t nÃ y.");

            // Col G: MÃ´ táº£ váº­t pháº©m - VLOOKUP auto-fill
            ws.Cell(r, 7).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$E,5,FALSE),\"\")";
            ConfigureManualEntryGuidance(
                ws.Cell(r, 7),
                "MÃ´ táº£ váº­t pháº©m",
                "Náº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n mÃ´ táº£.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng cá»™t nÃ y.");

            // Col H: Sá»‘ lÆ°á»£ng (*) - number format
            ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0";

            // Col I: Thá»ƒ tÃ­ch (dmÂ³) - VLOOKUP auto-fill from DM_Lookup col 6, editable for new items
            ws.Cell(r, 9).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$G,6,FALSE),\"\")";
            ws.Cell(r, 9).Style.NumberFormat.Format = "#,##0.000";
            var dvVolume = ws.Cell(r, 9).GetDataValidation();
            dvVolume.ShowInputMessage = true;
            dvVolume.InputTitle = "Thá»ƒ tÃ­ch";
            dvVolume.InputMessage = "Thá»ƒ tÃ­ch má»—i Ä‘Æ¡n vá»‹ (dmÂ³).\nNáº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng.";
            dvVolume.ShowErrorMessage = false;

            // Col J: CÃ¢n náº·ng (kg) - VLOOKUP auto-fill from DM_Lookup col 7, editable for new items
            ws.Cell(r, 10).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$G,7,FALSE),\"\")";
            ws.Cell(r, 10).Style.NumberFormat.Format = "#,##0.000";
            var dvWeight = ws.Cell(r, 10).GetDataValidation();
            dvWeight.ShowInputMessage = true;
            dvWeight.InputTitle = "CÃ¢n náº·ng";
            dvWeight.InputMessage = "CÃ¢n náº·ng má»—i Ä‘Æ¡n vá»‹ (kg).\nNáº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng.";
            dvWeight.ShowErrorMessage = false;

            // Col K: ÄÆ¡n giÃ¡ (VNÄ) - currency format (purchase-specific)
            ws.Cell(r, 11).Style.NumberFormat.Format = "#,##0";
            var dvUnitPrice = ws.Cell(r, 11).GetDataValidation();
            dvUnitPrice.ShowInputMessage = true;
            dvUnitPrice.InputTitle = "ÄÆ¡n giÃ¡";
            dvUnitPrice.InputMessage = "GiÃ¡ mua má»—i Ä‘Æ¡n vá»‹ (VNÄ).\nÄá»ƒ trá»‘ng náº¿u khÃ´ng cÃ³.";

            // Col L: NgÃ y háº¿t háº¡n - DateOnly (dd/MM/yyyy)
            ws.Cell(r, 12).Style.NumberFormat.Format = "dd/MM/yyyy";
            var dvExpiryDate = ws.Cell(r, 12).GetDataValidation();
            dvExpiryDate.Date.Between(new DateTime(2020, 1, 1), new DateTime(2099, 12, 31));
            dvExpiryDate.IgnoreBlanks = true;
            dvExpiryDate.ShowInputMessage = true;
            dvExpiryDate.InputTitle = "NgÃ y háº¿t háº¡n";
            dvExpiryDate.InputMessage = "Nháº­p ngÃ y (dd/MM/yyyy).\nVÃ­ dá»¥: 25/12/2026\nÄá»ƒ trá»‘ng náº¿u khÃ´ng cÃ³.";
            dvExpiryDate.ShowErrorMessage = true;
            dvExpiryDate.ErrorTitle = "Sai Ä‘á»‹nh dáº¡ng";
            dvExpiryDate.ErrorMessage = "Vui lÃ²ng nháº­p ngÃ y há»£p lá»‡ (dd/MM/yyyy).";
            dvExpiryDate.ErrorStyle = XLErrorStyle.Warning;

            // Col M: NgÃ y nháº­n - DateTime (dd/MM/yyyy HH:mm)
            ws.Cell(r, 13).Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
            var dvReceivedDate = ws.Cell(r, 13).GetDataValidation();
            dvReceivedDate.Date.Between(new DateTime(2020, 1, 1), new DateTime(2099, 12, 31));
            dvReceivedDate.IgnoreBlanks = true;
            dvReceivedDate.ShowInputMessage = true;
            dvReceivedDate.InputTitle = "NgÃ y nháº­n";
            dvReceivedDate.InputMessage = "Nháº­p ngÃ y giá» (dd/MM/yyyy HH:mm).\nVÃ­ dá»¥: 24/03/2026 14:30";
            dvReceivedDate.ShowErrorMessage = true;
            dvReceivedDate.ErrorTitle = "Sai Ä‘á»‹nh dáº¡ng";
            dvReceivedDate.ErrorMessage = "Vui lÃ²ng nháº­p ngÃ y giá» há»£p lá»‡ (dd/MM/yyyy HH:mm).";
            dvReceivedDate.ErrorStyle = XLErrorStyle.Warning;

            // -- Row styling ---------------------------------------------------
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

        // -- Freeze header row -------------------------------------------------
        ws.SheetView.FreezeRows(1);
    }

    // ---------------------------------------------------------------------------
    //  Funding Request Template - like purchase but without expiry/received date
    //  Cols: STT (A), TÃªn váº­t pháº©m (B), Danh má»¥c (C), Äá»‘i tÆ°á»£ng (D),
    //        Loáº¡i váº­t pháº©m (E), ÄÆ¡n vá»‹ (F), MÃ´ táº£ váº­t pháº©m (G),
    //        Sá»‘ lÆ°á»£ng (*) (H), ÄÆ¡n giÃ¡ (VNÄ) (I),
    //        Thá»ƒ tÃ­ch (dmÂ³) (J), CÃ¢n náº·ng (kg) (K) - 11 cols total
    // ---------------------------------------------------------------------------

    private static readonly string[] FundingRequestTemplateHeaders =
    [
        "STT",              // A  (1)
        "TÃªn váº­t pháº©m",    // B  (2)
        "Danh má»¥c",         // C  (3)
        "Äá»‘i tÆ°á»£ng",        // D  (4)
        "Loáº¡i váº­t pháº©m",   // E  (5)
        "ÄÆ¡n vá»‹",           // F  (6)
        "MÃ´ táº£ váº­t pháº©m",   // G  (7)
        "Sá»‘ lÆ°á»£ng (*)",     // H  (8)
        "ÄÆ¡n giÃ¡ (VNÄ)",   // I  (9)
        "Thá»ƒ tÃ­ch (dmÂ³)",  // J  (10) - VLOOKUP auto-fill
        "CÃ¢n náº·ng (kg)",   // K  (11) - VLOOKUP auto-fill
    ];

    private const int FundingRequestDataStartRow = 2;
    private const int FundingRequestDataEndRow   = 102; // 101 data rows (2..102)
    private const int FundingRequestCols         = 11;  // A..K

    public byte[] GenerateFundingRequestTemplate(
        IReadOnlyList<DonationImportCategoryInfo> categories,
        IReadOnlyList<DonationImportItemInfo> items,
        IReadOnlyList<DonationImportTargetGroupInfo> targetGroups)
    {
        using var workbook = new XLWorkbook();

        // -- 1. Build hidden reference sheets (reuse same helpers as donation/purchase) -
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

        // -- 2. Build main entry sheet -----------------------------------------
        var ws = workbook.Worksheets.Add("YÃªu cáº§u cáº¥p tiá»n");
        ws.SetTabActive();

        BuildFundingRequestMainSheet(ws);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // --- Funding Request main entry sheet ------------------------------------
    private static void BuildFundingRequestMainSheet(IXLWorksheet ws)
    {
        // -- Header row styling ------------------------------------------------
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

        // -- Column widths -----------------------------------------------------
        ws.Column(1).Width  = 5;   // A: STT
        ws.Column(2).Width  = 30;  // B: TÃªn váº­t pháº©m
        ws.Column(3).Width  = 25;  // C: Danh má»¥c
        ws.Column(4).Width  = 25;  // D: Äá»‘i tÆ°á»£ng
        ws.Column(5).Width  = 15;  // E: Loáº¡i váº­t pháº©m
        ws.Column(6).Width  = 12;  // F: ÄÆ¡n vá»‹
        ws.Column(7).Width  = 35;  // G: MÃ´ táº£ váº­t pháº©m
        ws.Column(8).Width  = 12;  // H: Sá»‘ lÆ°á»£ng
        ws.Column(9).Width  = 16;  // I: ÄÆ¡n giÃ¡

        // -- Data rows (2..102) -------------------------------------------------
        ws.Column(10).Width = 20;  // J: The tich / don vi
        ws.Column(11).Width = 22;  // K: Can nang / don vi

        for (int r = FundingRequestDataStartRow; r <= FundingRequestDataEndRow; r++)
        {
            int rowNum = r - FundingRequestDataStartRow + 1;

            ws.Row(r).Height = 20;

            // Col A: STT (auto-number)
            ws.Cell(r, 1).Value = rowNum;
            ws.Cell(r, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Col C: Danh má»¥c - dropdown from named range "Categories"
            var dvCategory = ws.Cell(r, 3).GetDataValidation();
            dvCategory.List("=Categories");
            dvCategory.IgnoreBlanks = true;
            dvCategory.ShowErrorMessage = true;
            dvCategory.ErrorTitle = "Lá»—i";
            dvCategory.ErrorMessage = "Vui lÃ²ng chá»n danh má»¥c tá»« danh sÃ¡ch.";

            // Col B: TÃªn váº­t pháº©m - dependent dropdown via INDIRECT on col C
            var dvItem = ws.Cell(r, 2).GetDataValidation();
            dvItem.List($"=INDIRECT(\"Cat_\"&RIGHT(C{r},LEN(C{r})-FIND(\" - \",C{r})-2))");
            dvItem.IgnoreBlanks = true;
            dvItem.ShowInputMessage = true;
            dvItem.InputTitle = "Gá»£i Ã½";
            dvItem.InputMessage = "Chá»n váº­t pháº©m cÃ³ sáºµn hoáº·c tá»± nháº­p tÃªn má»›i.";
            dvItem.ShowErrorMessage = false; // Allow manual entry of new items

            // Col D: Äá»‘i tÆ°á»£ng - VLOOKUP auto-fill from DM_Lookup col 2
            ws.Cell(r, 4).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$B,2,FALSE),\"\")";
            var dvTargetGroup = ws.Cell(r, 4).GetDataValidation();
            dvTargetGroup.List("=TargetGroupOptions");
            dvTargetGroup.IgnoreBlanks = true;
            dvTargetGroup.ShowInputMessage = true;
            dvTargetGroup.InputTitle = "Gá»£i Ã½";
            dvTargetGroup.InputMessage = "Náº¿u váº­t pháº©m má»›i, chá»n Ä‘á»‘i tÆ°á»£ng theo máº«u: tÃªn - code hoáº·c id.";
            dvTargetGroup.ShowErrorMessage = false;

            // Col E: Loáº¡i váº­t pháº©m - VLOOKUP auto-fill from DM_Lookup col 3
            ws.Cell(r, 5).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$C,3,FALSE),\"\")";
            var dvItemType = ws.Cell(r, 5).GetDataValidation();
            dvItemType.List("=ItemTypeOptions");
            dvItemType.IgnoreBlanks = true;
            dvItemType.ShowInputMessage = true;
            dvItemType.InputTitle = "Gá»£i Ã½";
            dvItemType.InputMessage = "Náº¿u váº­t pháº©m má»›i, chá»n loáº¡i váº­t pháº©m theo máº«u: tÃªn - code hoáº·c id.";
            dvItemType.ShowErrorMessage = false;

            // Col F: ÄÆ¡n vá»‹ - VLOOKUP auto-fill
            ws.Cell(r, 6).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$D,4,FALSE),\"\")";
            ConfigureManualEntryGuidance(
                ws.Cell(r, 6),
                "ÄÆ¡n vá»‹",
                "Náº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n Ä‘Æ¡n vá»‹.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng cá»™t nÃ y.");

            // Col G: MÃ´ táº£ váº­t pháº©m - VLOOKUP auto-fill
            ws.Cell(r, 7).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$E,5,FALSE),\"\")";
            ConfigureManualEntryGuidance(
                ws.Cell(r, 7),
                "MÃ´ táº£ váº­t pháº©m",
                "Náº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n mÃ´ táº£.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng cá»™t nÃ y.");

            // Col H: Sá»‘ lÆ°á»£ng (*) - number format
            ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0";

            // Col I: ÄÆ¡n giÃ¡ (VNÄ) - currency format
            ws.Cell(r, 9).Style.NumberFormat.Format = "#,##0";
            var dvUnitPrice = ws.Cell(r, 9).GetDataValidation();
            dvUnitPrice.ShowInputMessage = true;
            dvUnitPrice.InputTitle = "ÄÆ¡n giÃ¡";
            dvUnitPrice.InputMessage = "GiÃ¡ dá»± kiáº¿n má»—i Ä‘Æ¡n vá»‹ (VNÄ).\nÄá»ƒ trá»‘ng náº¿u chÆ°a xÃ¡c Ä‘á»‹nh.";

            // -- Row styling ---------------------------------------------------
            if (rowNum % 2 == 0)
            {
                var rowRange = ws.Range(r, 1, r, FundingRequestCols);
                rowRange.Style.Fill.SetBackgroundColor(OrangeLight);
            }

            ws.Cell(r, 10).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$F,6,FALSE),\"\")";
            ws.Cell(r, 10).Style.NumberFormat.Format = "#,##0.###";
            var dvVolumePerUnit = ws.Cell(r, 10).GetDataValidation();
            dvVolumePerUnit.ShowInputMessage = true;
            dvVolumePerUnit.InputTitle = "Thá»ƒ tÃ­ch / Ä‘Æ¡n vá»‹";
            dvVolumePerUnit.InputMessage = "Thá»ƒ tÃ­ch má»—i Ä‘Æ¡n vá»‹ (dmÂ³).\nNáº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng.";
            dvVolumePerUnit.ShowErrorMessage = false;

            ws.Cell(r, 11).FormulaA1 = $"IFERROR(VLOOKUP(B{r},DM_Lookup!$A:$G,7,FALSE),\"\")";
            ws.Cell(r, 11).Style.NumberFormat.Format = "#,##0.###";
            var dvWeightPerUnit = ws.Cell(r, 11).GetDataValidation();
            dvWeightPerUnit.ShowInputMessage = true;
            dvWeightPerUnit.InputTitle = "CÃ¢n náº·ng / Ä‘Æ¡n vá»‹";
            dvWeightPerUnit.InputMessage = "CÃ¢n náº·ng má»—i Ä‘Æ¡n vá»‹ (kg).\nNáº¿u chá»n váº­t pháº©m cÃ³ sáºµn, há»‡ thá»‘ng tá»± Ä‘iá»n.\nNáº¿u tá»± nháº­p váº­t pháº©m má»›i, báº¡n cÃ³ thá»ƒ nháº­p thá»§ cÃ´ng.";
            dvWeightPerUnit.ShowErrorMessage = false;

            // Thin borders for all data cells
            var dataRow = ws.Range(r, 1, r, FundingRequestCols);
            dataRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRow.Style.Border.InsideBorder  = XLBorderStyleValues.Hair;
            dataRow.Style.Border.OutsideBorderColor = XLColor.FromHtml("#BDBDBD");
            dataRow.Style.Border.InsideBorderColor  = XLColor.FromHtml("#E0E0E0");
        }

        // -- Freeze header row -------------------------------------------------
        ws.SheetView.FreezeRows(1);
    }

    // ---------------------------------------------------------------------------
    //  Depot Closure - External Resolution Template
    // ---------------------------------------------------------------------------

        private static readonly string[] ClosureTemplateHeaders =
    [
        "STT",                  // A
        "Tên vật phẩm",         // B
        "Danh mục",             // C
        "Đối tượng",            // D
        "Loại vật phẩm",        // E
        "Đơn vị",               // F
        "Serial Number",        // G
        "Ngày nhập",            // H
        "Hạn sử dụng",          // I
        "Số lượng",             // J
        "Đơn giá (VNĐ)",        // K
        "Thành tiền (VNĐ)",     // L
        "Hình thức xử lý",      // M
        "Người nhận",           // N
        "Ghi chú",              // O
        "ItemModelId (ẩn)",     // P
        "LotId (ẩn)",           // Q
        "ReusableItemId (ẩn)"   // R
    ];

    private const int ClosureCols = 18; // A..R
    private const int ClosurePreFilledCols = 10; // A..J (pre-filled)
    private static readonly string[] ClosureHandlingMethodOptions = Enum
        .GetValues<ExternalDispositionType>()
        .Select(ExternalDispositionMetadata.GetDisplayValue)
        .ToArray();

    public byte[] GenerateClosureExternalTemplate(string depotName, IReadOnlyList<ClosureInventoryLotItemDto> items)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Xử lý đóng kho");

        ws.Cell(1, 1).Value = $"MẪU XỬ LÝ HÀNG TỒN KHI ĐÓNG KHO — {depotName.ToUpper()}";
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

        ws.Cell(2, 1).Value = $"Ngày xuất: {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}";
        var tsRange = ws.Range(2, 1, 2, ClosureCols);
        tsRange.Merge();
        ws.Cell(2, 1).Style
            .Font.SetItalic(true)
            .Font.SetFontSize(10)
            .Font.SetFontColor(Black)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

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
            cell.Style.Fill.SetBackgroundColor(c < ClosurePreFilledCols ? OrangeMid : LockedHeaderColor);
        }
        ws.Row(headerRow).Height = 24;

        int dataStartRow = 4;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            int r = dataStartRow + i;

            ws.Cell(r, 1).Value = i + 1;
            ws.Cell(r, 2).Value = item.ItemName;
            ws.Cell(r, 3).Value = item.CategoryName;
            ws.Cell(r, 4).Value = item.TargetGroup;
            ws.Cell(r, 5).Value = item.ItemType;
            ws.Cell(r, 6).Value = item.Unit;
            ws.Cell(r, 7).Value = item.SerialNumber;

            if (item.ReceivedDate.HasValue)
            {
                ws.Cell(r, 8).Value = item.ReceivedDate.Value.ToString("dd/MM/yyyy");
            }

            if (item.ExpiredDate.HasValue)
            {
                ws.Cell(r, 9).Value = item.ExpiredDate.Value.ToString("dd/MM/yyyy");
            }

            ws.Cell(r, 10).Value = item.Quantity;
            ws.Cell(r, 12).FormulaA1 = $"J{r}*K{r}";
            ws.Cell(r, 12).Style.NumberFormat.Format = "#,##0";

            var preFilledRange = ws.Range(r, 1, r, ClosurePreFilledCols);
            preFilledRange.Style.Fill.SetBackgroundColor(i % 2 == 0 ? OrangeLight : XLColor.White);
            preFilledRange.Style.Font.SetFontColor(Black);

            var editableRange = ws.Range(r, ClosurePreFilledCols + 1, r, ClosureCols);
            editableRange.Style.Fill.SetBackgroundColor(XLColor.White);
            editableRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Dashed);
            editableRange.Style.Border.SetOutsideBorderColor(XLColor.FromHtml("#90A4AE"));

            var dvHandling = ws.Cell(r, 13).GetDataValidation();
            dvHandling.List($"\"{string.Join(",", ClosureHandlingMethodOptions)}\"");
            dvHandling.ShowErrorMessage = true;
            dvHandling.ErrorTitle = "Giá trị không hợp lệ";
            dvHandling.ErrorMessage = "Vui lòng chọn HandlingMethod từ danh sách có sẵn.";
            dvHandling.IgnoreBlanks = true;
            dvHandling.InputTitle = "Hình thức xử lý";
            dvHandling.InputMessage = "Chọn một giá trị trong danh sách. Nếu chọn Other thì bắt buộc nhập Ghi chú.";

            var dataRow = ws.Range(r, 1, r, ClosureCols);
            dataRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRow.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            dataRow.Style.Border.OutsideBorderColor = XLColor.FromHtml("#BDBDBD");
            dataRow.Style.Border.InsideBorderColor = XLColor.FromHtml("#E0E0E0");

            ws.Cell(r, 16).Value = item.ItemModelId;
            if (item.LotId.HasValue)
            {
                ws.Cell(r, 17).Value = item.LotId.Value;
            }

            if (item.ReusableItemId.HasValue)
            {
                ws.Cell(r, 18).Value = item.ReusableItemId.Value;
            }
        }

        ws.Column(1).Width = 6;
        ws.Column(2).Width = 30;
        ws.Column(3).Width = 20;
        ws.Column(4).Width = 22;
        ws.Column(5).Width = 14;
        ws.Column(6).Width = 10;
        ws.Column(7).Width = 18;
        ws.Column(8).Width = 14;
        ws.Column(9).Width = 14;
        ws.Column(10).Width = 12;
        ws.Column(11).Width = 16;
        ws.Column(12).Width = 18;
        ws.Column(13).Width = 22;
        ws.Column(14).Width = 25;
        ws.Column(15).Width = 30;

        ws.Column(16).Hide();
        ws.Column(17).Hide();
        ws.Column(18).Hide();

        ws.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}






