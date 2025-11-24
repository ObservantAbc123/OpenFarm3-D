using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("threads")]
public partial class Thread
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("job_id")]
    public long? JobId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("thread_status")]
    [StringLength(255)]
    public string ThreadStatus { get; set; } = null!;

    [ForeignKey("JobId")]
    [InverseProperty("Threads")]
    public virtual PrintJob? Job { get; set; }

    [InverseProperty("Thread")]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    [ForeignKey("UserId")]
    [InverseProperty("Threads")]
    public virtual User User { get; set; } = null!;

    [InverseProperty("Thread")]
    public virtual ICollection<AiGeneratedResponse> AiGeneratedResponses { get; set; } = new List<AiGeneratedResponse>();
}
