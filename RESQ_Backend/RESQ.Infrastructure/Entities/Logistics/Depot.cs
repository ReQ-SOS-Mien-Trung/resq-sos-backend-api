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

    [Column("capacity", TypeName = "numeric(14,3)")]
    public decimal? Capacity { get; set; }

    [Column("current_utilization", TypeName = "numeric(14,3)")]
    public decimal? CurrentUtilization { get; set; }

    /// <summary>Sức chứa tối đa theo cân nặng (kg).</summary>
    [Column("weight_capacity", TypeName = "numeric(14,3)")]
    public decimal? WeightCapacity { get; set; }

    /// <summary>Cân nặng hiện tại đang sử dụng (kg).</summary>
    [Column("current_weight_utilization", TypeName = "numeric(14,3)")]
    public decimal? CurrentWeightUtilization { get; set; }

    [Column("advance_limit", TypeName = "numeric(18,2)")]
    public decimal AdvanceLimit { get; set; }

    [Column("outstanding_advance_amount", TypeName = "numeric(18,2)")]
    public decimal OutstandingAdvanceAmount { get; set; }

    [Column("last_updated_at", TypeName = "timestamp with time zone")]
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>Người cập nhật trạng thái kho gần nhất.</summary>
    [Column("last_status_changed_by")]
    public Guid? LastStatusChangedBy { get; set; }

    /// <summary>Người tạo kho.</summary>
    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    /// <summary>Người cập nhật kho gần nhất.</summary>
    [Column("last_updated_by")]
    public Guid? LastUpdatedBy { get; set; }

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
