using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities.System;

[Table("prompts")]
public partial class Prompt
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? Name { get; set; }

    [Column("purpose")]
    public string? Purpose { get; set; }

    [Column("system_prompt")]
    public string? SystemPrompt { get; set; }

    [Column("user_prompt_template")]
    public string? UserPromptTemplate { get; set; }

    [Column("model")]
    [StringLength(100)]
    public string? Model { get; set; }

    [Column("temperature")]
    public double? Temperature { get; set; }

    [Column("max_tokens")]
    public int? MaxTokens { get; set; }

    [Column("version")]
    [StringLength(20)]
    public string? Version { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }
}
