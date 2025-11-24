using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("printer_model_price_periods")]
public partial class PrinterModelPricePeriod
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

    [Column("printer_model_id")]
    public int? PrinterModelId { get; set; }

    [ForeignKey("PrinterModelId")]
    [InverseProperty("PrinterModelPricePeriod")]
    public virtual PrinterModel? PrinterModel { get; set; }
}
