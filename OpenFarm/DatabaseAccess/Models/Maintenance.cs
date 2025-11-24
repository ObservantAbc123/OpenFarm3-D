using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Models;

[Table("maintenance")]
public partial class Maintenance
{
    /// <summary>
    /// Primary key for table.
    /// </summary>
    [Key]
    [Column("maintenance_report_id")]
    public int MaintenanceReportId { get; set; }

    /// <summary>
    /// When the printer last got serviced.
    /// </summary>
    [Column("date_of_last_service")]
    public DateTime? DateOfLastService { get; set; }

    /// <summary>
    /// When the printer should next be serviced.
    /// </summary>
    [Column("date_of_next_service")]
    public DateTime? DateOfNextService { get; set; }

    /// <summary>
    /// Time in seconds that the Printer has been in active operation
    /// since 'DateOfLastService'.
    /// </summary>
    [Column("session_uptime")]
    public int SessionUptime { get; set; }

    /// <summary>
    /// Current temperature of Printer extrusion-nozzle, above
    /// ambient air temperature in Fahrenheit.
    /// </summary>
    [Column("thermal_load_f")]
    [Precision(8, 3)]
    public decimal ThermalLoadF { get; set; }

    /// <summary>
    /// Current temperature of Printer extrusion-nozzle, above
    /// ambient air temperature in Centigrade.
    /// </summary>
    [Column("thermal_load_c")]
    [Precision(8, 3)]
    public decimal ThermalLoadC { get; set; }

    /// <summary>
    /// Cubic meters extruded by the Printer since
    /// 'DateOfLastService'.
    /// </summary>
    [Column("session_extrusion_volume_m3")]
    [Precision(8, 3)]
    public decimal SessionExtrusionVolumeM3 { get; set; }

    /// <summary>
    /// Linear meters traveled by the Printer nozzle
    /// since 'DateOfLastService'.
    /// </summary>
    [Column("session_extruder_traveled_m")]
    [Precision(8, 3)]
    public decimal SessionExtruderTraveledM { get; set; }

    /// <summary>
    /// Count of errors system has sustained since
    /// 'DateOfLastService'.
    /// </summary>
    [Column("session_error_count")]
    public int SessionErrorCount { get; set; }

    /// <summary>
    /// Count of Prints system has successfully completed
    /// since 'DateOfLastService'.
    /// </summary>
    [Column("session_prints_completed")]
    public int SessionPrintsCompleted { get; set; }

    /// <summary>
    /// Count of Prints system has failed to complete
    /// since 'DateOfLastService'.
    /// </summary>
    [Column("session_prints_failed")]
    public int SessionPrintsFailed { get; set; }

    /// <summary>
    /// Ties report to a Printer
    /// </summary>
    [ForeignKey("MaintenanceReportId")]
    [InverseProperty("Maintenance")]
    public virtual Printer MaintenanceReport { get; set; } = null!;
}
