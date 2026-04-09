using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.ExportClosureTemplate;

/// <summary>
/// Tải file Excel template để depot manager ghi nhận cách xử lý tồn kho bên ngoài.
/// </summary>
public record ExportClosureTemplateQuery(int DepotId) : IRequest<ExportClosureTemplateResponse>;
