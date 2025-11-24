using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("print_jobs")]
public partial class PrintJob
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long? UserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("printer_model_id")]
    public int? PrinterModelId { get; set; }

    [Column("material_id")]
    public int? MaterialId { get; set; }

    [Column("response_id")]
    [StringLength(255)]
    public string? ResponseId { get; set; }

    [Column("num_copies")]
    public int NumCopies { get; set; }

    [Column("print_cost")]
    [Precision(7, 2)]
    public decimal? PrintCost { get; set; }

    [Column("print_weight")]
    public double? PrintWeight { get; set; }

    [Column("print_time")]
    public double? PrintTime { get; set; }

    [Column("paid")]
    public bool Paid { get; set; }

    [Column("finished_byte_pos")]
    public long? FinishedBytePos { get; set; }

    [Column("job_status")]
    [StringLength(255)]
    public string JobStatus { get; set; } = null!;

    [InverseProperty("Job")]
    public virtual ICollection<Thread> Threads { get; set; } = new List<Thread>();

    [ForeignKey("MaterialId")]
    [InverseProperty("PrintJobs")]
    public virtual Material? Material { get; set; }

    [ForeignKey("PrinterModelId")]
    [InverseProperty("PrintJobs")]
    public virtual PrinterModel? PrinterModel { get; set; }

    [InverseProperty("PrintJob")]
    public virtual ICollection<Print> Prints { get; set; } = new List<Print>();

    [InverseProperty("PrintJob")]
    public virtual Thumbnail? Thumbnail { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("PrintJobs")]
    public virtual User? User { get; set; }
}
