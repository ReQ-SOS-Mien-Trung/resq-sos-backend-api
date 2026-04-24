using RESQ.Domain.Entities.Personnel.Exceptions;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Domain.Entities.Personnel;

public class AssemblyEventModel
{
    public int Id { get; set; }
    public int AssemblyPointId { get; set; }
    public DateTime AssemblyDate { get; set; }
    public AssemblyEventStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<AssemblyParticipantModel> Participants { get; set; } = [];

    public AssemblyEventModel() { }

    public static AssemblyEventModel Create(int assemblyPointId, DateTime assemblyDate, Guid createdBy)
    {
        return new AssemblyEventModel
        {
            AssemblyPointId = assemblyPointId,
            AssemblyDate = assemblyDate,
            Status = AssemblyEventStatus.Gathering,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Complete()
    {
        if (Status != AssemblyEventStatus.Gathering)
            throw new InvalidAssemblyEventStatusException(
                $"Cannot complete assembly event when status is {Status}. Required: Gathering.");

        Status = AssemblyEventStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != AssemblyEventStatus.Gathering)
            throw new InvalidAssemblyEventStatusException(
                $"Cannot cancel assembly event when status is {Status}. Required: Gathering.");

        Status = AssemblyEventStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
}
