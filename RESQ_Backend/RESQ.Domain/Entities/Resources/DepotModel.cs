using RESQ.Domain.Entities.Resources.Exceptions;
using RESQ.Domain.Entities.Resources.ValueObjects;
using RESQ.Domain.Enum.Resources;

namespace RESQ.Domain.Entities.Resources;

public class DepotModel
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public GeoLocation? Location { get; set; }

    public int Capacity { get; set; }
    public int CurrentUtilization { get; set; }
    public DepotStatus Status { get; set; }

    public Guid? DepotManagerId { get; set; }

    public DepotModel() { }

    public static DepotModel Create (
        string name,
        string address,
        GeoLocation location,
        int capacity,
        DepotStatus status,
        Guid? depotManagerId = null)
    {
        if (capacity <= 0)
            throw new InvalidDepotCapacityException(capacity);

        return new DepotModel
        {
            Name = name,
            Address = address,
            Location = location,
            Capacity = capacity,
            CurrentUtilization = 0,
            Status = DepotStatus.Available,
            DepotManagerId = depotManagerId
        };
    }

    public void UpdateUtilization(int amount)
    {
        if (Status == DepotStatus.Closed)
            throw new DepotClosedException();

        if (amount <= 0)
            throw new InvalidDepotUtilizationAmountException(amount);

        if (CurrentUtilization + amount > Capacity)
            throw new DepotCapacityExceededException();

        CurrentUtilization += amount;
    }
}
