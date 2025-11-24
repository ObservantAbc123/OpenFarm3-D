using System.Linq.Expressions;
using DatabaseAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAccess.Helpers;

public class MaintenanceHelper(OpenFarmContext context) : BaseHelper(context)
{
    private IQueryable<Maintenance> Reports => _context.Maintenances.AsNoTracking();

    /// <summary>
    /// Return all maintenance reports.
    /// </summary>
    public async Task<List<Maintenance>> GetReportsAsync() =>
        await Reports.ToListAsync();

    /// <summary>
    /// Get a single report by its maintenance_report_id (printer id).
    /// </summary>
    public async Task<Maintenance?> GetReportAsync(int maintenanceReportId) =>
        await Reports.SingleOrDefaultAsync(m => m.MaintenanceReportId == maintenanceReportId);

    /// <summary>
    /// Update printer error count for current session (since last service date).
    /// </summary>
    public async Task UpdatePrinterErrorCountAsync(int maintenanceReportId, int delta = 1)
    {
        var entity = await _context.Maintenances
            .SingleOrDefaultAsync(m => m.MaintenanceReportId == maintenanceReportId);

        if (entity is null) return;

        entity.SessionErrorCount += delta;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Increment completed/failed print counts for a report.
    /// </summary>
    public async Task UpdatePrintCountsAsync(
        int maintenanceReportId,
        int completedDelta = 0,
        int failedDelta = 0)
    {
        if (completedDelta == 0 && failedDelta == 0) return;

        var entity = await _context.Maintenances
            .SingleOrDefaultAsync(m => m.MaintenanceReportId == maintenanceReportId);

        if (entity is null) return;

        entity.SessionPrintsCompleted += completedDelta;
        entity.SessionPrintsFailed += failedDelta;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Update count of seconds that the printer has been
    /// operational and activated.
    /// </summary>
    /// <param name="maintenanceReportId">FK to Printer ID</param>
    /// <param name="delta"></param>
    public async Task UpdatePrinterUptime(int maintenanceReportId, int delta = 10)
    {
        var entity = await _context.Maintenances
            .SingleOrDefaultAsync(m => m.MaintenanceReportId == maintenanceReportId);

        if (entity is null) return;

        entity.SessionUptime += delta;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Update current temp. offset/thermal load in Centigrade
    /// for the Printer tied to <param name="maintenanceReportId"></param>.
    /// </summary>
    /// <param name="maintenanceReportId">FK to Printer ID</param>
    /// <param name="delta">Increase for current extrusion-nozzle temp in C</param>
    public async Task UpdatePrinterTempC(int maintenanceReportId, decimal delta)
    {
        var entity = await _context.Maintenances
            .SingleOrDefaultAsync(m => m.MaintenanceReportId == maintenanceReportId);

        if (entity is null) return;

        entity.ThermalLoadC = delta;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Update current temp. offset/thermal load in Fahrenheit
    /// for the Printer tied to <param name="maintenanceReportId"></param>.
    /// </summary>
    /// <param name="maintenanceReportId">FK to Printer ID</param>
    /// <param name="delta">Increase for current extrusion-nozzle temp in F</param>
    public async Task UpdatePrinterTempF(int maintenanceReportId, decimal delta)
    {
        var entity = await _context.Maintenances
            .SingleOrDefaultAsync(m => m.MaintenanceReportId == maintenanceReportId);

        if (entity is null) return;

        entity.ThermalLoadF = delta;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Update record of cubic meters extruded since
    /// last service date.
    /// </summary>
    /// <param name="maintenanceReportId">FK to Printer ID</param>
    /// <param name="delta">Increase in cubic meters of material extruded</param>
    public async Task UpdateExtrusionVolume(int maintenanceReportId, decimal delta)
    {
        var entity = await _context.Maintenances
            .SingleOrDefaultAsync(m => m.MaintenanceReportId == maintenanceReportId);

        if (entity is null) return;

        entity.SessionExtrusionVolumeM3 += delta;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Update record of meters linearly traveled by extruder
    /// head since last service date.
    /// </summary>
    /// <param name="maintenanceReportId">FK to Printer ID</param>
    /// <param name="delta">Increase in meters linearly traveled</param>
    public async Task UpdateExtruderTravel(int maintenanceReportId, decimal delta)
    {
        var entity = await _context.Maintenances
            .SingleOrDefaultAsync(m => m.MaintenanceReportId == maintenanceReportId);

        if (entity is null) return;

        entity.SessionExtruderTraveledM += delta;
        await _context.SaveChangesAsync();
    }
}
