using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("messages")]
public partial class Message
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("thread_id")]
    public long ThreadId { get; set; }

    [Column("message_content")]
    public string MessageContent { get; set; } = null!;

    [Column("message_subject")]
    public string? MessageSubject { get; set; }

    [Column("message_type")]
    [StringLength(255)]
    public string MessageType { get; set; } = null!;

    [Column("sender_type")]
    [StringLength(255)]
    public string SenderType { get; set; } = null!;

    [Column("from_email_address")]
    [StringLength(255)]
    public string? FromEmailAddress { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("message_status")]
    [StringLength(255)]
    public string MessageStatus { get; set; } = null!;

    [ForeignKey("ThreadId")]
    [InverseProperty("Messages")]
    public virtual Thread Thread { get; set; } = null!;

    [InverseProperty("Message")]
    public virtual ICollection<AiGeneratedResponse> AiGeneratedResponses { get; set; } = new List<AiGeneratedResponse>();
}
