namespace RESQ.Application.UseCases.Logistics.Queries.ExportClosureTemplate;

public class ExportClosureTemplateResponse
{
    public byte[] FileContent { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
