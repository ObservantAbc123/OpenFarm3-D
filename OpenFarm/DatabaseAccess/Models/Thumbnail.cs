using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("thumbnails")]
public partial class Thumbnail
{
    [Key]
    [Column("print_job_id")]
    public long PrintJobId { get; set; }

    [Column("thumb_string")]
    public string ThumbString { get; set; } = null!;

    [ForeignKey("PrintJobId")]
    [InverseProperty("Thumbnail")]
    public virtual PrintJob PrintJob { get; set; } = null!;
}
