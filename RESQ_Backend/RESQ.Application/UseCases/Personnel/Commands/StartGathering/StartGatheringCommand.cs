using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.StartGathering;

/// <summary>
/// Chuyển trạng thái sự kiện từ Scheduled → Gathering (mở check-in).
/// </summary>
public record StartGatheringCommand(int AssemblyEventId) : IRequest;
