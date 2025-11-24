using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("colors")]
[Index("Name", Name = "colors_color_key", IsUnique = true)]
public partial class Color
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("color")]
    [StringLength(255)]
    public string Name { get; set; } = null!;

    [InverseProperty("MaterialColor")]
    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
}
