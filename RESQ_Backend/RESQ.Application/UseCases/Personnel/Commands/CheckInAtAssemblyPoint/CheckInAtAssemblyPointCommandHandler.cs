using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.CheckInAtAssemblyPoint;

public class CheckInAtAssemblyPointCommandHandler(
    IAssemblyEventRepository assemblyEventRepository,
    IAssemblyPointRepository assemblyPointRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CheckInAtAssemblyPointCommand>
{
    /// <summary>Sai số cho phép khi validate GPS (200m).</summary>
    private const double MaxDistanceMeters = 200;

    /// <summary>Số giờ trước EventDateTime cho phép check-in.</summary>
    private const int CheckInOpenHoursBefore = 24;

    /// <summary>Số giờ sau EventDateTime vẫn cho phép check-in.</summary>
    private const int CheckInCloseHoursAfter = 6;

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

        // 3. Validate cửa sổ thời gian check-in
        var checkInOpensAt = evt.AssemblyDate.AddHours(-CheckInOpenHoursBefore);
        var checkInClosesAt = evt.AssemblyDate.AddHours(CheckInCloseHoursAfter);
        var now = DateTime.UtcNow;

        if (now < checkInOpensAt)
            throw new BadRequestException(
                $"Chưa đến thời gian check-in. Check-in mở từ {checkInOpensAt:dd/MM/yyyy HH:mm} UTC " +
                $"(trước giờ triệu tập {CheckInOpenHoursBefore} tiếng).");

        if (now > checkInClosesAt)
            throw new BadRequestException(
                $"Đã quá thời gian check-in. Check-in đóng lúc {checkInClosesAt:dd/MM/yyyy HH:mm} UTC " +
                $"(sau giờ triệu tập {CheckInCloseHoursAfter} tiếng).");

        // 4. Validate vị trí GPS — rescuer phải nằm trong phạm vi điểm tập kết
        var assemblyPoint = await assemblyPointRepository.GetByIdAsync(evt.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết id = {evt.AssemblyPointId}");

        if (assemblyPoint.Location == null)
            throw new BadRequestException("Điểm tập kết chưa có tọa độ GPS. Vui lòng liên hệ quản trị viên.");

        var distanceMeters = HaversineMeters(
            request.Latitude, request.Longitude,
            assemblyPoint.Location.Latitude, assemblyPoint.Location.Longitude);

        if (distanceMeters > MaxDistanceMeters)
            throw new BadRequestException(
                $"Bạn hiện cách điểm tập kết \"{assemblyPoint.Name}\" khoảng {distanceMeters:F0}m. " +
                $"Vui lòng di chuyển đến trong phạm vi {MaxDistanceMeters}m để thực hiện check-in.");

        // 5. Check-in (validate participant tồn tại + idempotent)
        var success = await assemblyEventRepository.CheckInAsync(
            request.AssemblyEventId, request.UserId, cancellationToken);

        if (!success)
            throw new BadRequestException("Bạn không nằm trong danh sách tham gia sự kiện tập trung này.");

        await unitOfWork.SaveAsync();
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
