using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("printers_loaded_materials")]
[Index("PrinterId", "MaterialId", Name = "printers_loaded_materials_printer_id_material_id_key", IsUnique = true)]
public partial class PrintersLoadedMaterial
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("printer_id")]
    public int PrinterId { get; set; }

    [Column("material_id")]
    public int MaterialId { get; set; }

    [ForeignKey("MaterialId")]
    [InverseProperty("PrintersLoadedMaterials")]
    public virtual Material Material { get; set; } = null!;

    [ForeignKey("PrinterId")]
    [InverseProperty("PrintersLoadedMaterials")]
    public virtual Printer Printer { get; set; } = null!;
}
