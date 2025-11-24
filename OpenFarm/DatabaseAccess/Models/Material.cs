using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("materials")]
[Index("MaterialTypeId", "MaterialColorId", Name = "materials_material_type_id_material_color_id_key", IsUnique = true)]
public partial class Material
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("material_type_id")]
    public int MaterialTypeId { get; set; }

    [Column("material_color_id")]
    public int MaterialColorId { get; set; }

    [Column("in_stock")]
    public bool InStock { get; set; }

    [ForeignKey("MaterialColorId")]
    [InverseProperty("Materials")]
    public virtual Color MaterialColor { get; set; } = null!;

    [InverseProperty("Material")]
    public virtual MaterialPricePeriod? MaterialPricePeriod { get; set; }

    [ForeignKey("MaterialTypeId")]
    [InverseProperty("Materials")]
    public virtual MaterialType MaterialType { get; set; } = null!;

    [InverseProperty("Material")]
    public virtual ICollection<PrintJob> PrintJobs { get; set; } = new List<PrintJob>();

    [InverseProperty("Material")]
    public virtual ICollection<PrintersLoadedMaterial> PrintersLoadedMaterials { get; set; } = new List<PrintersLoadedMaterial>();
}
