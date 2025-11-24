using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("material_price_periods")]
public partial class MaterialPricePeriod
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("ended_at")]
    public DateTime? EndedAt { get; set; }

    [Column("price")]
    [Precision(7, 2)]
    public decimal Price { get; set; }

    [Column("material_id")]
    public int MaterialId { get; set; }

    [ForeignKey("MaterialId")]
    [InverseProperty("MaterialPricePeriod")]
    public virtual Material Material { get; set; } = null!;
}
