using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
namespace RESQ.Application.UseCases.Logistics.Queries.ExportClosureTemplate;

public class ExportClosureTemplateQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotRepository depotRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IExcelExportService excelExportService,
    ILogger<ExportClosureTemplateQueryHandler> logger)
    : IRequestHandler<ExportClosureTemplateQuery, ExportClosureTemplateResponse>
{
    public async Task<ExportClosureTemplateResponse> Handle(
        ExportClosureTemplateQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("T�i kho?n kh�ng qu?n l� kho n�o dang ho?t d?ng.");

        var depot = await depotRepository.GetByIdAsync(depotId, cancellationToken)
            ?? throw new NotFoundException("Kh�ng t�m th?y kho c?u tr?.");

        var items = await depotRepository.GetLotDetailedInventoryForClosureAsync(depotId, cancellationToken);
        if (items.Count == 0)
            throw new ConflictException("Kho kh�ng c�n h�ng t?n, kh�ng c?n xu?t m?u x? l�.");

        var fileContent = excelExportService.GenerateClosureExternalTemplate(depot.Name, items);
        var safeDepotName = depot.Name.Replace(" ", "_");

        logger.LogInformation(
            "ExportClosureTemplate | DepotId={DepotId} UserId={UserId} Items={Count}",
            depotId,
            request.UserId,
            items.Count);

        return new ExportClosureTemplateResponse
        {
            FileContent = fileContent,
            FileName = $"Mau_Xu_Ly_Hang_Ton_Dong_Kho_{safeDepotName}_{DateTime.UtcNow.AddHours(7):yyyyMMdd}.xlsx"
        };
    }
}
