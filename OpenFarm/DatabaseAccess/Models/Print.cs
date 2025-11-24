using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("prints")]
public partial class Print
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("print_job_id")]
    public long? PrintJobId { get; set; }

    [Column("printer_id")]
    public int? PrinterId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    [Column("print_status")]
    [StringLength(255)]
    public string PrintStatus { get; set; } = null!;

    [ForeignKey("PrintJobId")]
    [InverseProperty("Prints")]
    public virtual PrintJob? PrintJob { get; set; }

    [ForeignKey("PrinterId")]
    [InverseProperty("Prints")]
    public virtual Printer? Printer { get; set; }
}
