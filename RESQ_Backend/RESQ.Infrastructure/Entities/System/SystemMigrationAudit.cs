using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.System;

[Table("system_migration_audit")]
public class SystemMigrationAudit
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("migration_name")]
    public string MigrationName { get; set; } = string.Empty;

    [Column("applied_at", TypeName = "timestamp with time zone")]
    public DateTime AppliedAt { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }
}
