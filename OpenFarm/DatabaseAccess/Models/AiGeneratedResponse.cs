using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseAccess.Models;

[Table("ai_generated_responses")]
public partial class AiGeneratedResponse
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("thread_id")]
    public long ThreadId { get; set; }

    [Column("message_id")]
    public long MessageId { get; set; }

    [Column("generated_content")]
    public string GeneratedContent { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

    [ForeignKey("ThreadId")]
    [InverseProperty("AiGeneratedResponses")]
    public virtual Thread Thread { get; set; } = null!;

    [ForeignKey("MessageId")]
    [InverseProperty("AiGeneratedResponses")]
    public virtual Message Message { get; set; } = null!;
}
