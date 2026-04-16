using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RESQ.Infrastructure.Entities.System;

[Table("ai_configs")]
public class AiConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("provider")]
    [StringLength(50)]
    public string Provider { get; set; } = "Gemini";

    [Column("model")]
    [StringLength(100)]
    public string? Model { get; set; }

    [Column("temperature")]
    public double Temperature { get; set; }

    [Column("max_tokens")]
    public int MaxTokens { get; set; }

    [Column("api_url")]
    [StringLength(500)]
    public string? ApiUrl { get; set; }

    [Column("api_key")]
    public string? ApiKey { get; set; }

    [Column("version")]
    [StringLength(20)]
    public string? Version { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at", TypeName = "timestamp with time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp with time zone")]
    public DateTime? UpdatedAt { get; set; }
}
