using MediatR;

namespace RESQ.Application.UseCases.Personnel.Queries.GetMyUpcomingAssemblyEvents;

/// <summary>Lấy danh sách sự kiện tập trung sắp tới (Scheduled/Gathering) của rescuer đang đăng nhập.</summary>
public class GetMyUpcomingAssemblyEventsQuery : IRequest<List<UpcomingAssemblyEventDto>>
{
    public Guid RescuerId { get; }

    public GetMyUpcomingAssemblyEventsQuery(Guid rescuerId)
    {
        RescuerId = rescuerId;
    }
}
