using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.CheckInAtAssemblyPoint;

public class CheckInAtAssemblyPointCommandHandler(
    IAssemblyEventRepository assemblyEventRepository,
    IAssemblyPointRepository assemblyPointRepository,
    ICheckInRadiusConfigRepository checkInRadiusConfigRepository,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CheckInAtAssemblyPointCommand>
{
    /// <summary>Bán kính check-in mặc định khi chưa có cấu hình (200m).</summary>
    private const double DefaultMaxDistanceMeters = 200;

    public async Task Handle(CheckInAtAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate sự kiện tồn tại
        var evt = await assemblyEventRepository.GetEventByIdAsync(request.AssemblyEventId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sự kiện tập trung id = {request.AssemblyEventId}");

        // 2. Validate trạng thái event phải là Scheduled hoặc Gathering
        if (evt.Status != AssemblyEventStatus.Scheduled.ToString() &&
            evt.Status != AssemblyEventStatus.Gathering.ToString())
            throw new BadRequestException(
                $"Sự kiện tập trung chưa mở hoặc đã kết thúc check-in. Trạng thái hiện tại: {evt.Status}.");

        // 3. Enforce thời hạn check-in nếu coordinator đã thiết lập
        var now = DateTime.UtcNow;
        if (evt.CheckInDeadline.HasValue && now > evt.CheckInDeadline.Value)
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var deadlineVn = TimeZoneInfo.ConvertTimeFromUtc(evt.CheckInDeadline.Value, vnTimeZone);
            throw new BadRequestException(
                $"Đã quá thời hạn check-in. Thời hạn check-in là {deadlineVn:HH:mm dd/MM/yyyy} (giờ Việt Nam).");
        }

        // 4. Validate vị trí GPS - rescuer phải nằm trong phạm vi điểm tập kết
        var assemblyPoint = await assemblyPointRepository.GetByIdAsync(evt.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết id = {evt.AssemblyPointId}");

        if (assemblyPoint.Status == AssemblyPointStatus.Unavailable || assemblyPoint.Status == AssemblyPointStatus.Closed)
            throw new BadRequestException($"Điểm tập kết {assemblyPoint.Name} đang bảo trì hoặc đã đóng ({assemblyPoint.Status}), không thể check-in lúc này.");

        if (assemblyPoint.Location == null)
            throw new BadRequestException("Điểm tập kết chưa có tọa độ GPS. Vui lòng liên hệ quản trị viên.");

        var radiusConfig = await checkInRadiusConfigRepository.GetAsync(cancellationToken);
        var maxDistanceMeters = radiusConfig?.MaxRadiusMeters ?? DefaultMaxDistanceMeters;

        var distanceMeters = HaversineMeters(
            request.Latitude, request.Longitude,
            assemblyPoint.Location.Latitude, assemblyPoint.Location.Longitude);

        if (distanceMeters > maxDistanceMeters)
        {
            var distanceDisplay = distanceMeters >= 1000
                ? $"{distanceMeters / 1000:F1}km"
                : $"{distanceMeters:F0}m";
            var radiusDisplay = maxDistanceMeters >= 1000
                ? $"{maxDistanceMeters / 1000:F1}km"
                : $"{maxDistanceMeters:F0}m";
            throw new BadRequestException(
                $"Bạn hiện cách điểm tập kết \"{assemblyPoint.Name}\" khoảng {distanceDisplay}. " +
                $"Vui lòng di chuyển đến trong phạm vi {radiusDisplay} để thực hiện check-in.");
        }

        // 5. Check-in (validate participant tồn tại + idempotent)
        var success = await assemblyEventRepository.CheckInAsync(
            request.AssemblyEventId, request.UserId, cancellationToken);

        if (!success)
        {
            // Phân biệt: đã tự rời sự kiện (CheckedOut) vs không có trong danh sách
            var hasCheckedOut = await assemblyEventRepository.HasParticipantCheckedOutAsync(
                request.AssemblyEventId, request.UserId, cancellationToken);
            if (hasCheckedOut)
                throw new BadRequestException("Bạn đã tự rời sự kiện này. Không thể check-in lại sau khi đã check-out.");

            throw new BadRequestException("Bạn không nằm trong danh sách tham gia sự kiện tập trung này.");
        }

        await unitOfWork.SaveAsync();
        await operationalHubService.PushAssemblyPointListUpdateAsync(cancellationToken);
    }

    /// <summary>
    /// Tính khoảng cách Haversine giữa 2 tọa độ GPS (mét).
    /// </summary>
    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000; // Bán kính Trái Đất (mét)

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
