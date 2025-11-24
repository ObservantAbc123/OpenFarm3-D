using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("material_types")]
[Index("Type", Name = "material_types_type_key", IsUnique = true)]
public partial class MaterialType
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("type")]
    [StringLength(255)]
    public string Type { get; set; } = null!;

    [Column("bed_temp_floor")]
    public int? BedTempFloor { get; set; }

    [Column("bed_temp_ceiling")]
    public int? BedTempCeiling { get; set; }

    [Column("print_temp_floor")]
    public int? PrintTempFloor { get; set; }

    [Column("print_temp_ceiling")]
    public int? PrintTempCeiling { get; set; }

    [InverseProperty("MaterialType")]
    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
}
