using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using DatabaseAccess;
using DatabaseAccess.Models;
using RabbitMQHelper;

namespace native_desktop_app.ViewModels;

public class MaintenanceViewModel : ViewModelBase
{
    private readonly DispatcherTimer _refreshTimer;

    private ObservableCollection<MaintenanceRowViewModel> _rows = new();

    public ObservableCollection<MaintenanceRowViewModel> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    private static string ToHumanReadable(TimeSpan span)
    {
        span = span.Duration();
        var parts = new List<string>();
        if (span.Days > 0)
            parts.Add($"{span.Days} day{(span.Days == 1 ? "" : "s")}");
        if (span.Hours > 0)
            parts.Add($"{span.Hours} hour{(span.Hours == 1 ? "" : "s")}");
        if (span.Minutes > 0)
            parts.Add($"{span.Minutes} minute{(span.Minutes == 1 ? "" : "s")}");
        if (parts.Count == 0)
            return "0 minutes";
        return string.Join(", ", parts);
    }

    public MaintenanceViewModel(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper)
        : base(databaseAccessHelper, rmqHelper)
    {
        _ = LoadTopLevelMaintenanceRowsAsync();

        // setup timer for 1-second interval updates
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += (sender, e) => _ = LoadTopLevelMaintenanceRowsAsync();
        _refreshTimer.Start();
    }

    public static string FormatSeconds(int seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        if (time.TotalDays >= 1)
        {
            return string.Format("{0}d {1:D2}h {2:D2}m {3:D2}s",
                time.Days,
                time.Hours,
                time.Minutes,
                time.Seconds);
        }

        return string.Format("{0:D2}h {1:D2}m {2:D2}s",
            time.Hours,
            time.Minutes,
            time.Seconds);
    }

    private async Task LoadTopLevelMaintenanceRowsAsync()
    {
        // 1. fetch Data
        List<Maintenance> maintenanceReports = await _databaseAccessHelper.Maintenance.GetReportsAsync();

        // 2. marshal to UI Thread immediately for safe collection modification
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // track which printer names we have seen in this update cycle
            var processedPrinters = new HashSet<string>();

            foreach (Maintenance report in maintenanceReports)
            {
                Printer associatedPrinter = (await _databaseAccessHelper.Printers.GetPrinterAsync(report.MaintenanceReportId))!;

                // calculate values
                TimeSpan tslsTimespan = (report.DateOfLastService ?? DateTime.MaxValue) - DateTime.Now;
                TimeSpan ttnsTimespan = (report.DateOfNextService ?? DateTime.MinValue) - DateTime.Now;

                // prepare formatted strings
                string uptimeStr = FormatSeconds(report.SessionUptime); // Dynamic Uptime
                string tslsStr = ToHumanReadable(tslsTimespan);
                string ttnsStr = ToHumanReadable(ttnsTimespan);
                string dateStr = report.DateOfNextService?.Date.ToString("MM/dd/yyyy") ?? DateTime.MinValue.ToString("MM/dd/yyyy");
                IBrush brush = MaintenanceColorHelper.GetReportStatusBrush((DateTime)report.DateOfLastService, (DateTime)report.DateOfNextService);

                processedPrinters.Add(associatedPrinter.Name);

                // 3. find existing row
                var existingRow = Rows.FirstOrDefault(r => r.AssociatedPrinterName == associatedPrinter.Name);

                if (existingRow != null)
                {
                    // update existing row to preserve IsExpanded state
                    existingRow.Update(
                        tslsStr,
                        ttnsStr,
                        dateStr,
                        uptimeStr,
                        report.ThermalLoadC.ToString(),
                        report.ThermalLoadF.ToString(),
                        report.SessionPrintsCompleted.ToString(),
                        report.SessionPrintsFailed.ToString(),
                        report.SessionExtrusionVolumeM3.ToString(),
                        report.SessionExtruderTraveledM.ToString(),
                        report.SessionErrorCount.ToString(),
                        brush
                    );
                }
                else
                {
                    // create new row
                    var newRow = new MaintenanceRowViewModel(
                        associatedPrinter.Name,
                        tslsStr,
                        ttnsStr,
                        dateStr,
                        uptimeStr,
                        report.ThermalLoadC.ToString(),
                        report.ThermalLoadF.ToString(),
                        report.SessionPrintsCompleted.ToString(),
                        report.SessionPrintsFailed.ToString(),
                        report.SessionExtrusionVolumeM3.ToString(),
                        report.SessionExtruderTraveledM.ToString(),
                        report.SessionErrorCount.ToString(),
                        brush
                    );
                    Rows.Add(newRow);
                }
            }

            // 4. cleanup: remove rows that are no longer in the database
            // var rowsToRemove = Rows.Where(r => !processedPrinters.Contains(r.AssociatedPrinterName)).ToList();
            // foreach (var row in rowsToRemove)
            // {
            //     Rows.Remove(row);
            // }
        });
    }
}
