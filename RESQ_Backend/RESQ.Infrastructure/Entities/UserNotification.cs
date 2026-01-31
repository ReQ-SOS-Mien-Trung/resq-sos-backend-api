using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Entities;

[Table("user_notifications")]
public partial class UserNotification
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("notification_id")]
    public int? NotificationId { get; set; }

    [Column("is_read")]
    public bool? IsRead { get; set; }

    [Column("read_at", TypeName = "timestamp with time zone")]
    public DateTime? ReadAt { get; set; }

    [Column("delivered_at", TypeName = "timestamp with time zone")]
    public DateTime? DeliveredAt { get; set; }

    [ForeignKey("NotificationId")]
    [InverseProperty("UserNotifications")]
    public virtual Notification? Notification { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserNotifications")]
    public virtual User? User { get; set; }
}