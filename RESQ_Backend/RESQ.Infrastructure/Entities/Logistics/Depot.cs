using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Entities.Operations;


namespace RESQ.Infrastructure.Entities.Logistics;

[Table("depots")]
public partial class Depot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;

    [Column("capacity")]
    public int? Capacity { get; set; }

    [Column("current_utilization")]
    public int? CurrentUtilization { get; set; }

    [Column("last_updated_at", TypeName = "timestamp with time zone")]
    public DateTime? LastUpdatedAt { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [InverseProperty("Depot")]
    public virtual ICollection<DepotManager> DepotManagers { get; set; } = new List<DepotManager>();

    [InverseProperty("Depot")]
    public virtual ICollection<SupplyInventory> SupplyInventories { get; set; } = new List<SupplyInventory>();

    [InverseProperty("SourceDepot")]
    public virtual ICollection<MissionItem> MissionItems { get; set; } = new List<MissionItem>();

    [InverseProperty("Depot")]
    public virtual ICollection<ReusableItem> ReusableItems { get; set; } = new List<ReusableItem>();

    [InverseProperty("RequestingDepot")]
    public virtual ICollection<DepotSupplyRequest> SupplyRequestsAsRequester { get; set; } = new List<DepotSupplyRequest>();

    [InverseProperty("SourceDepot")]
    public virtual ICollection<DepotSupplyRequest> SupplyRequestsAsSource { get; set; } = new List<DepotSupplyRequest>();

    [InverseProperty("Depot")]
    public virtual ICollection<CampaignDisbursement> CampaignDisbursements { get; set; } = new List<CampaignDisbursement>();

    [InverseProperty("Depot")]
    public virtual ICollection<FundingRequest> FundingRequests { get; set; } = new List<FundingRequest>();

    [InverseProperty("Depot")]
    public virtual ICollection<DepotFund> DepotFunds { get; set; } = new List<DepotFund>();
}
