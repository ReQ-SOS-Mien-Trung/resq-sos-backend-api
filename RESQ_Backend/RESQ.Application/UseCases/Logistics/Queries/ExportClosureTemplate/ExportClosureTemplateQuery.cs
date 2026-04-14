using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.ExportClosureTemplate;

/// <summary>
/// T?i file Excel template d? depot manager ghi nh?n cách x? lý t?n kho bên ngoài.
/// </summary>
public record ExportClosureTemplateQuery(Guid UserId) : IRequest<ExportClosureTemplateResponse>;
