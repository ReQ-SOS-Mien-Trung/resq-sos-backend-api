using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.UploadExternalResolution;

/// <summary>
/// Xử lý tồn kho bên ngoài hệ thống.
/// Depot ID được suy ra từ token của manager (không cần truyền qua URL).
/// Frontend gửi danh sách items dạng JSON (đã convert từ Excel).
/// </summary>
public record UploadExternalResolutionCommand(
    Guid ManagerUserId,
    List<ExternalResolutionItemDto> Items, int? DepotId = null) : IRequest<UploadExternalResolutionResponse>;
