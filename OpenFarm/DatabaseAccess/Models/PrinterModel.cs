using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("printer_models")]
[Index("Model", Name = "printer_models_model_key", IsUnique = true)]
public partial class PrinterModel
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("model")]
    [StringLength(255)]
    public string Model { get; set; } = null!;

    [Column("autostart")]
    public bool Autostart { get; set; }

    [Column("bed_x_min")]
    public int? BedXMin { get; set; }

    [Column("bed_x_max")]
    public int? BedXMax { get; set; }

    [Column("bed_y_min")]
    public int? BedYMin { get; set; }

    [Column("bed_y_max")]
    public int? BedYMax { get; set; }

    [Column("bed_z_min")]
    public int? BedZMin { get; set; }

    [Column("bed_z_max")]
    public int? BedZMax { get; set; }

    [InverseProperty("PrinterModel")]
    public virtual ICollection<PrintJob> PrintJobs { get; set; } = new List<PrintJob>();

    [InverseProperty("PrinterModel")]
    public virtual PrinterModelPricePeriod? PrinterModelPricePeriod { get; set; }

    [InverseProperty("PrinterModel")]
    public virtual ICollection<Printer> Printers { get; set; } = new List<Printer>();
}
