using ClosedXML.Excel;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics.Models;

namespace RESQ.Infrastructure.Services;

public class ExcelExportService : IExcelExportService
{
    private static readonly string[] Headers =
    [
        "STT", "Tên Vật Phẩm", "Danh mục", "Đối tượng", "Loại vật phẩm",
        "Đơn vị", "Đơn giá", "Số lượng", "Ngày nhận",
        "Loại Hành động", "Nguồn", "Tên nhiệm vụ"
    ];

    // ── Palette ──────────────────────────────────────────────────────────────
    private static readonly XLColor OrangeDark     = XLColor.FromHtml("#E65100"); // title bg
    private static readonly XLColor OrangeMid      = XLColor.FromHtml("#FF8F00"); // header bg
    private static readonly XLColor OrangeLight    = XLColor.FromHtml("#FFF3E0"); // alt-row bg
    private static readonly XLColor OrangeSummary  = XLColor.FromHtml("#FFE0B2"); // summary bg
    private static readonly XLColor Black          = XLColor.FromHtml("#212121"); // text on light bg
    private static readonly XLColor White          = XLColor.White;               // text on dark bg

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
}
