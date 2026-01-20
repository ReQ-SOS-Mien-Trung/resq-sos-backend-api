using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Domain.Entities;

[Table("rescue_units")]
public partial class RescueUnit
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("current_latitude")]
    public double? CurrentLatitude { get; set; }

    [Column("current_longitude")]
    public double? CurrentLongitude { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [Column("distance_available")]
    public int? DistanceAvailable { get; set; }

    [Column("max_capacity_people")]
    public int? MaxCapacityPeople { get; set; }

    [Column("formed_at")]
    public DateOnly? FormedAt { get; set; }

    [Column("disbanded_at")]
    public DateOnly? DisbandedAt { get; set; }

    [InverseProperty("FromUnit")]
    public virtual ICollection<ActivityHandoverLog> ActivityHandoverLogFromUnits { get; set; } = new List<ActivityHandoverLog>();

    [InverseProperty("ToUnit")]
    public virtual ICollection<ActivityHandoverLog> ActivityHandoverLogToUnits { get; set; } = new List<ActivityHandoverLog>();

    [InverseProperty("AssignedUnit")]
    public virtual ICollection<MissionActivity> MissionActivities { get; set; } = new List<MissionActivity>();

    [InverseProperty("PrimaryUnit")]
    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();

    [InverseProperty("SuggestedRescueUnit")]
    public virtual ICollection<RescueUnitAiSuggestion> RescueUnitAiSuggestions { get; set; } = new List<RescueUnitAiSuggestion>();

    [InverseProperty("RescueUnit")]
    public virtual ICollection<UnitMember> UnitMembers { get; set; } = new List<UnitMember>();
}
