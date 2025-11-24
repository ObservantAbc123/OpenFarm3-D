// Purpose
//   ViewModel that aggregates and summarizes print job and printer statistics.
//   Includes lifetime totals, breakdowns by model and status, and basic printer
//   availability counts. Provides refresh and export commands for interaction.
// -----------------------------------------------------------------------------

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DatabaseAccess;
using DatabaseAccess.Models;
using RabbitMQHelper;

namespace native_desktop_app.ViewModels;

/// <summary>
///     ViewModel responsible for computing and exposing high-level statistics
///     about print jobs and printers.
///     <para>
///         • Calculates success/failure counts and success rate.
///         • Groups jobs by printer model and status for quick overviews.
///         • Tracks recent failures and longest-running jobs.
///         • Gathers a printer availability snapshot.
///     </para>
/// </summary>
public class StatsViewModel : ViewModelBase
{
    private int _cancelledCount;

    private int _completedCount;

    private int _failedCount;

    private int _paidCount;

    private int _printersDisabled;

    private int _printersEnabled;

    private int _printersIdle;

    private int _printersPrinting;

    // --- Printer snapshot section ---

    private int _printersTotal;

    private string _successRateText = "0%";

    // --- Summary section ---

    private string _summaryLine = "Lifetime stats across all print jobs.";

    private int _totalJobs;

    /// <summary>
    ///     Creates a new <see cref="StatsViewModel" />, initializing database connection,
    ///     wiring commands, and triggering an initial refresh.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <c>DATABASE_CONNECTION_STRING</c> is not set.
    /// </exception>
    public StatsViewModel(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper) : base(databaseAccessHelper,
        rmqHelper)
    {
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);

        _ = RefreshAsync(); // Start loading data immediately.
    }

    /// <summary>
    ///     Command to refresh statistics by reloading data from the database.
    /// </summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>
    ///     Command to export data as CSV (currently placeholder implementation).
    /// </summary>
    public IAsyncRelayCommand ExportCsvCommand { get; }

    /// <summary> Summary string describing the dataset timeframe.</summary>
    public string SummaryLine
    {
        get => _summaryLine;
        set => SetProperty(ref _summaryLine, value);
    }

    /// <summary>Total number of jobs in the database.</summary>
    public int TotalJobs
    {
        get => _totalJobs;
        set => SetProperty(ref _totalJobs, value);
    }

    /// <summary>Count of successfully completed jobs.</summary>
    public int CompletedCount
    {
        get => _completedCount;
        set => SetProperty(ref _completedCount, value);
    }

    /// <summary>Count of failed jobs.</summary>
    public int FailedCount
    {
        get => _failedCount;
        set => SetProperty(ref _failedCount, value);
    }

    /// <summary>Count of cancelled jobs.</summary>
    public int CancelledCount
    {
        get => _cancelledCount;
        set => SetProperty(ref _cancelledCount, value);
    }

    /// <summary>Count of jobs marked as paid.</summary>
    public int PaidCount
    {
        get => _paidCount;
        set => SetProperty(ref _paidCount, value);
    }

    /// <summary>Calculated success rate formatted as a percentage string.</summary>
    public string SuccessRateText
    {
        get => _successRateText;
        set => SetProperty(ref _successRateText, value);
    }

    /// <summary>Total number of printers tracked in the system.</summary>
    public int PrintersTotal
    {
        get => _printersTotal;
        set => SetProperty(ref _printersTotal, value);
    }

    /// <summary>Number of printers currently enabled (available).</summary>
    public int PrintersEnabled
    {
        get => _printersEnabled;
        set => SetProperty(ref _printersEnabled, value);
    }

    /// <summary>Number of printers currently disabled.</summary>
    public int PrintersDisabled
    {
        get => _printersDisabled;
        set => SetProperty(ref _printersDisabled, value);
    }

    /// <summary>Number of printers currently in printing state.</summary>
    public int PrintersPrinting
    {
        get => _printersPrinting;
        set => SetProperty(ref _printersPrinting, value);
    }

    /// <summary>Number of printers currently idle.</summary>
    public int PrintersIdle
    {
        get => _printersIdle;
        set => SetProperty(ref _printersIdle, value);
    }

    // --- Collections for sub-stats and lists ---

    /// <summary>
    ///     Breakdown of job counts by printer model.
    /// </summary>
    public ObservableCollection<StatItem> JobsByModel { get; } = new();

    /// <summary>
    ///     Breakdown of job counts by job status.
    /// </summary>
    public ObservableCollection<StatItem> JobsByStatus { get; } = new();

    /// <summary>
    ///     List of most recent failed jobs (up to 5).
    /// </summary>
    public ObservableCollection<PrintJob> RecentFailures { get; } = new();

    /// <summary>
    ///     List of longest-duration print jobs (up to 5).
    /// </summary>
    public ObservableCollection<PrintJob> LongestJobs { get; } = new();

    /// <summary>
    ///     Reloads all statistics from the database, updating summary counts, grouped job data,
    ///     and printer status snapshots.
    /// </summary>
    private async Task RefreshAsync()
    {
        // --- Job counts (lifetime) ---
        var all = await _databaseAccessHelper.PrintJobs.GetPrintJobsAsync();
        var paid = await _databaseAccessHelper.PrintJobs.GetPaidPrintJobsAsync();
        var failed = await _databaseAccessHelper.PrintJobs.GetPrintJobsByStatusAsync("failed");
        var cancelled = await _databaseAccessHelper.PrintJobs.GetPrintJobsByStatusAsync("cancelled");

        TotalJobs = all.Count;
        PaidCount = paid.Count;
        FailedCount = failed.Count;
        CancelledCount = cancelled.Count;
        CompletedCount = TotalJobs - FailedCount;

        var rate = TotalJobs == 0 ? 0 : (double)CompletedCount / TotalJobs;
        SuccessRateText = $"{Math.Round(rate * 100)}%";

        if (all.Count > 0)
        {
            var min = all.Min(j => j.CreatedAt);
            var max = all.Max(j => j.CreatedAt);
            SummaryLine = $"Lifetime stats from {min:d} to {max:d}";
        }
        else
        {
            SummaryLine = "No jobs yet.";
        }

        // --- Jobs by model ---
        JobsByModel.Clear();
        foreach (var g in all.GroupBy(j => j.PrinterModel?.Model ?? $"Model #{j.PrinterModelId}")
                     .OrderByDescending(g => g.Count()))
            JobsByModel.Add(new StatItem(g.Key, g.Count()));

        // --- Jobs by status ---
        JobsByStatus.Clear();
        foreach (var g in all.GroupBy(j => j.JobStatus ?? "unknown")
                     .OrderByDescending(g => g.Count()))
            JobsByStatus.Add(new StatItem(g.Key, g.Count()));

        // --- Recent failures (latest 5) ---
        RecentFailures.Clear();
        foreach (var j in failed.OrderByDescending(j => j.CreatedAt).Take(5))
            RecentFailures.Add(j);

        // --- Longest jobs (top 5 by print time) ---
        LongestJobs.Clear();
        var withDuration = all.Where(j => j.PrintTime != null)
            .OrderByDescending(j => j.PrintTime)
            .Take(5)
            .ToList();
        foreach (var j in withDuration) LongestJobs.Add(j);

        // --- Printer snapshot ---
        var printersAll = await _databaseAccessHelper.Printers.GetPrintersAsync();
        var printersEnabled = await _databaseAccessHelper.Printers.GetEnabledPrintersAsync();
        var printersDisabled = await _databaseAccessHelper.Printers.GetDisabledPrintersAsync();
        var printersPrinting = await _databaseAccessHelper.Printers.GetCurrentlyPrintingPrintersAsync();
        var printersIdle = await _databaseAccessHelper.Printers.GetIdlePrintersAsync();

        PrintersTotal = printersAll.Count;
        PrintersEnabled = printersEnabled.Count;
        PrintersDisabled = printersDisabled.Count;
        PrintersPrinting = printersPrinting.Count;
        PrintersIdle = printersIdle.Count;
    }

    /// <summary>
    ///     Placeholder for CSV export functionality (not yet implemented).
    /// </summary>
    private async Task ExportCsvAsync()
    {
        await Task.CompletedTask;
    }
}

/// <summary>
///     Simple record representing a labeled numeric statistic, e.g., count by model or status.
/// </summary>
public record StatItem(string Label, int Value);