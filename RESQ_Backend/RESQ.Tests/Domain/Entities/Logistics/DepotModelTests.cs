using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Exceptions;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Tests.Domain.Entities.Logistics;

/// <summary>
/// FE-05 – Depot Management: DepotModel domain tests.
/// Covers: Create validation, capacity, status transitions, manager assignment, utilization.
/// </summary>
public class DepotModelTests
{
    private static readonly GeoLocation DefaultLocation = new(10.8231, 106.6297);

    // -- Create --

    [Fact]
    public void Create_ReturnsDepot_WithStatusCreated_WhenNoManager()
    {
        var depot = DepotModel.Create("Kho A", "123 Đường ABC", DefaultLocation, 1000, 500);

        Assert.Equal("Kho A", depot.Name);
        Assert.Equal(DepotStatus.Created, depot.Status);
        Assert.Null(depot.CurrentManagerId);
        Assert.Equal(0, depot.CurrentUtilization);
        Assert.Equal(0, depot.CurrentWeightUtilization);
    }

    [Fact]
    public void Create_ReturnsDepot_WithStatusPendingAssignment_WhenManagerProvided()
    {
        var managerId = Guid.NewGuid();
        var depot = DepotModel.Create("Kho B", "456 Đường XYZ", DefaultLocation, 1000, 500, managerId);

        Assert.Equal(DepotStatus.PendingAssignment, depot.Status);
        Assert.Equal(managerId, depot.CurrentManagerId);
    }

    [Fact]
    public void Create_Throws_WhenCapacityIsZero()
    {
        Assert.Throws<InvalidDepotCapacityException>(
            () => DepotModel.Create("Kho", "Addr", DefaultLocation, 0, 500));
    }

    [Fact]
    public void Create_Throws_WhenCapacityIsNegative()
    {
        Assert.Throws<InvalidDepotCapacityException>(
            () => DepotModel.Create("Kho", "Addr", DefaultLocation, -100, 500));
    }

    [Fact]
    public void Create_Throws_WhenWeightCapacityIsZero()
    {
        Assert.Throws<InvalidDepotCapacityException>(
            () => DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 0));
    }

    [Fact]
    public void Create_Throws_WhenWeightCapacityIsNegative()
    {
        Assert.Throws<InvalidDepotCapacityException>(
            () => DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, -50));
    }

    // -- AssignManager --

    [Fact]
    public void AssignManager_SetsStatusAvailable()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 500);
        var managerId = Guid.NewGuid();

        depot.AssignManager(managerId);

        Assert.Equal(DepotStatus.Available, depot.Status);
        Assert.Equal(managerId, depot.CurrentManagerId);
    }

    [Fact]
    public void AssignManager_Throws_WhenManagerIdIsEmpty()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 500);

        Assert.Throws<InvalidDepotManagerException>(
            () => depot.AssignManager(Guid.Empty));
    }

    // -- UpdateUtilization --

    [Fact]
    public void UpdateUtilization_IncreasesCurrentValues()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 500);
        depot.AssignManager(Guid.NewGuid());

        depot.UpdateUtilization(200, 100);

        Assert.Equal(200, depot.CurrentUtilization);
        Assert.Equal(100, depot.CurrentWeightUtilization);
    }

    [Fact]
    public void UpdateUtilization_Throws_WhenExceedsCapacity()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 100, 500);
        depot.AssignManager(Guid.NewGuid());

        Assert.Throws<DepotCapacityExceededException>(
            () => depot.UpdateUtilization(150, 50));
    }

    [Fact]
    public void UpdateUtilization_Throws_WhenExceedsWeightCapacity()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 100);
        depot.AssignManager(Guid.NewGuid());

        Assert.Throws<DepotCapacityExceededException>(
            () => depot.UpdateUtilization(50, 150));
    }

    // -- DecreaseUtilization --

    [Fact]
    public void DecreaseUtilization_ClampsToZero()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 500);
        depot.AssignManager(Guid.NewGuid());
        depot.UpdateUtilization(100, 50);

        depot.DecreaseUtilization(200, 200);

        Assert.Equal(0, depot.CurrentUtilization);
        Assert.Equal(0, depot.CurrentWeightUtilization);
    }

    // -- ChangeStatus --

    [Fact]
    public void ChangeStatus_Available_To_Unavailable_Succeeds()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 500);
        depot.AssignManager(Guid.NewGuid());

        depot.ChangeStatus(DepotStatus.Closing);

        Assert.Equal(DepotStatus.Closing, depot.Status);
    }

    [Fact]
    public void ChangeStatus_Throws_WhenFromCreated()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 500);

        Assert.Throws<InvalidDepotStatusTransitionException>(
            () => depot.ChangeStatus(DepotStatus.Available));
    }

    [Fact]
    public void ChangeStatus_Throws_WhenFromClosed()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 500);
        depot.AssignManager(Guid.NewGuid());
        depot.ChangeStatus(DepotStatus.Closing);
        depot.CompleteClosing();

        Assert.Throws<InvalidDepotStatusTransitionException>(
            () => depot.ChangeStatus(DepotStatus.Available));
    }

    // -- UnassignManager --

    [Fact]
    public void UnassignManager_SetsStatusPendingAssignment()
    {
        var depot = DepotModel.Create("Kho", "Addr", DefaultLocation, 1000, 500);
        depot.AssignManager(Guid.NewGuid());

        depot.UnassignManager();

        Assert.Equal(DepotStatus.PendingAssignment, depot.Status);
    }
}
