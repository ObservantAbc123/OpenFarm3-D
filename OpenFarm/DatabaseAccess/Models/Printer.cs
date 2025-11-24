using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("printers")]
[Index("Name", Name = "printers_printer_key", IsUnique = true)]
public partial class Printer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("printer")]
    [StringLength(255)]
    public string Name { get; set; } = null!;

    [Column("printer_model_id")]
    public int PrinterModelId { get; set; }

    [Column("ip_address")]
    [StringLength(255)]
    public string? IpAddress { get; set; }

    [Column("api_key")]
    [StringLength(255)]
    public string? ApiKey { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; }

    [Column("autostart")]
    public bool? Autostart { get; set; }

    [Column("currently_printing")]
    public bool CurrentlyPrinting { get; set; }

    [InverseProperty("MaintenanceReport")]
    public virtual Maintenance? Maintenance { get; set; }

    [ForeignKey("PrinterModelId")]
    [InverseProperty("Printers")]
    public virtual PrinterModel PrinterModel { get; set; } = null!;

    [InverseProperty("Printer")]
    public virtual ICollection<PrintersLoadedMaterial> PrintersLoadedMaterials { get; set; } = new List<PrintersLoadedMaterial>();

    [InverseProperty("Printer")]
    public virtual ICollection<Print> Prints { get; set; } = new List<Print>();
}
