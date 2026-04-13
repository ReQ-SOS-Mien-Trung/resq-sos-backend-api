using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.CheckOutAtAssemblyPoint
{
    public class CheckOutAtAssemblyPointCommand : IRequest
    {
        public int EventId { get; set; }
        public Guid RescuerId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public CheckOutAtAssemblyPointCommand(int eventId, Guid rescuerId, double latitude, double longitude)
        {
            EventId = eventId;
            RescuerId = rescuerId;
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
