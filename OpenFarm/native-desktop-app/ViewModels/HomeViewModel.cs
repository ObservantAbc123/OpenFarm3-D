// Purpose
//   ViewModel for the desktop home/dashboard. Exposes printer tiles, a queue counter,
//   a live list of *active prints* (one row per currently-running copy),
//   and summary counts of print jobs by status. Fetches data from DatabaseAccessHelper.
// -----------------------------------------------------------------------------

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using DatabaseAccess;
using DatabaseAccess.Models;
using ImageExample.Helpers;
using RabbitMQHelper;

namespace native_desktop_app.ViewModels;

/// <summary>
///     Home dashboard view model.
///     <para>
///         • Provides a live list of *active prints* for the Printing panel (one row per running copy).
///         • Computes queue and status counts for summary tiles from print jobs.
///         • Exposes up to eight printer tiles with basic connection/printing indicators.
///         • Loads data asynchronously from the database on construction.
///     </para>
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private int _cancelledCount;

    private int _failedCount;

    // Backing store for the queued-count property.

    private int _rejectedCount;

    private int _totalJobs;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HomeViewModel" /> class.
    ///     Validates the database connection string and triggers the initial asynchronous data load.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the <c>DATABASE_CONNECTION_STRING</c> environment variable is not set.
    /// </exception>
    public HomeViewModel(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper) : base(databaseAccessHelper,
        rmqHelper)
    {
        // Fire-and-forget the initial data load; no awaited call in constructor.
        _ = LoadDashboardAsync();
    }

    /// <summary>
    ///     Live list of *active* prints (one row per running copy) for the Printing panel.
    /// </summary>
    /// <remarks>
    ///     This list is built from the <c>prints</c> table, filtered to <c>print_status == "printing"</c>
    ///     and <c>FinishedAt == null</c>. Each row includes a PR-#### label (PrintJobId),
    ///     a resolved printer name (or “(unassigned)”), and the StartedAt time.
    /// </remarks>
    public ObservableCollection<PrintingPrintsHelper> ActivePrints { get; } = new();

    /// <summary>
    ///     Number of jobs in the queue. (Derived from jobs with status <c>operatorApproved</c>.)
    /// </summary>
    public int QueueCounter { get; private set; }

    /// <summary>Total number of jobs with status <c>cancelled</c>.</summary>
    public int CancelledCount
    {
        get => _cancelledCount;
        set => SetProperty(ref _cancelledCount, value);
    }

    /// <summary>Total number of jobs with status <c>failed</c>.</summary>
    public int FailedCount
    {
        get => _failedCount;
        set => SetProperty(ref _failedCount, value);
    }

    /// <summary>Total number of jobs with status <c>rejected</c>.</summary>
    public int RejectedCount
    {
        get => _rejectedCount;
        set => SetProperty(ref _rejectedCount, value);
    }

    /// <summary>Total number of jobs across all statuses.</summary>
    public int TotalJobs
    {
        get => _totalJobs;
        set => SetProperty(ref _totalJobs, value);
    }

    /// <summary>
    ///     All printers mapped to lightweight <see cref="PrinterViewModel" /> tiles.
    /// </summary>
    public ObservableCollection<PrinterViewModel> AllPrinters { get; } = new();

    /// <summary>Convenience accessor for the first printer tile (or <c>null</c> if absent).</summary>
    public PrinterViewModel? Printer1 => AllPrinters.Count > 0 ? AllPrinters[0] : null;

    /// <summary>Convenience accessor for the second printer tile.</summary>
    public PrinterViewModel? Printer2 => AllPrinters.Count > 1 ? AllPrinters[1] : null;

    /// <summary>Convenience accessor for the third printer tile.</summary>
    public PrinterViewModel? Printer3 => AllPrinters.Count > 2 ? AllPrinters[2] : null;

    /// <summary>Convenience accessor for the fourth printer tile.</summary>
    public PrinterViewModel? Printer4 => AllPrinters.Count > 3 ? AllPrinters[3] : null;

    /// <summary>Convenience accessor for the fifth printer tile.</summary>
    public PrinterViewModel? Printer5 => AllPrinters.Count > 4 ? AllPrinters[4] : null;

    /// <summary>Convenience accessor for the sixth printer tile.</summary>
    public PrinterViewModel? Printer6 => AllPrinters.Count > 5 ? AllPrinters[5] : null;

    /// <summary>Convenience accessor for the seventh printer tile.</summary>
    public PrinterViewModel? Printer7 => AllPrinters.Count > 6 ? AllPrinters[6] : null;

    /// <summary>Convenience accessor for the eighth printer tile.</summary>
    public PrinterViewModel? Printer8 => AllPrinters.Count > 7 ? AllPrinters[7] : null;

    /// <summary>
    ///     Loads active prints (for the Printing panel), printer tiles, and summary job counts.
    /// </summary>
    /// <remarks>
    ///     The Printing panel is driven by <c>Prints</c> at <em>copy granularity</em>:
    ///     each running copy is one row. This avoids ambiguity when a single job has multiple
    ///     copies printing concurrently.
    /// </remarks>
    private async Task LoadDashboardAsync()
    {
        // 1) Summary counts & queue size from jobs
        var allJobs = await _databaseAccessHelper.PrintJobs.GetPrintJobsAsync();
        QueueCounter = allJobs.Count(job => job.JobStatus == "operatorApproved");
        var cancelled = await _databaseAccessHelper.PrintJobs.GetPrintJobsByStatusAsync("cancelled");
        var failed = await _databaseAccessHelper.PrintJobs.GetPrintJobsByStatusAsync("failed");
        var rejected = await _databaseAccessHelper.PrintJobs.GetPrintJobsByStatusAsync("rejected");
        FailedCount = failed.Count;
        CancelledCount = cancelled.Count;
        TotalJobs = allJobs.Count;
        RejectedCount = rejected.Count;
        OnPropertyChanged(nameof(QueueCounter)); // reflect queued count

        // 2) Printer tiles (middle column)
        var printers = await _databaseAccessHelper.Printers.GetPrintersAsync();
        var printerNameById = printers.ToDictionary(p => p.Id, p => p.Name);
        AllPrinters.Clear();
        foreach (var p in printers)
            AllPrinters.Add(new PrinterViewModel(p, _databaseAccessHelper, _rmqHelper));

        // Notify bindings that the printer convenience properties may have changed.
        OnPropertyChanged(nameof(Printer1));
        OnPropertyChanged(nameof(Printer2));
        OnPropertyChanged(nameof(Printer3));
        OnPropertyChanged(nameof(Printer4));
        OnPropertyChanged(nameof(Printer5));
        OnPropertyChanged(nameof(Printer6));
        OnPropertyChanged(nameof(Printer7));
        OnPropertyChanged(nameof(Printer8));

        // 3) Active prints (Printing panel): only rows truly running now
        var printing = await _databaseAccessHelper.Prints.GetPrintsByStatusAsync("printing");
        var active = printing.Where(p => p.FinishedAt == null).ToList();

        var rows = active.Select(p => new PrintingPrintsHelper
        {
            Print = p,
            PrintJobId = p.PrintJobId ?? 0,
            PrinterName = p.PrinterId is int pid && printerNameById.TryGetValue(pid, out var name)
                ? name
                : "(unassigned)"
        });

        ActivePrints.Clear();
        foreach (var r in rows)
            ActivePrints.Add(r);
    }

    /// <summary>
    ///     Lightweight view model that adapts a <see cref="Printer" /> for tile display,
    ///     including computed status icons for connectivity and printing state.
    /// </summary>
    public class PrinterViewModel : ViewModelBase
    {
        // Underlying domain model instance.
        private readonly Printer printer;

        /// <summary>
        ///     Creates a new <see cref="PrinterViewModel" /> for the specified <see cref="Printer" />.
        /// </summary>
        /// <param name="printer">The backing <see cref="Printer" /> model.</param>
        public PrinterViewModel(Printer printer, DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper) : base(
            databaseAccessHelper, rmqHelper)
        {
            this.printer = printer;
        }

        // Computed flags from model state; kept private as internal implementation details.
        private bool IsPrinting => printer.CurrentlyPrinting;
        private bool IsConnected => printer.Enabled;

        /// <summary>Display name of the printer.</summary>
        public string Name => printer.Name;

        /// <summary>
        ///     Wi-Fi/connection indicator as a bitmap. Green when connected, red otherwise.
        /// </summary>
        public Bitmap? WiFiStatusImage => IsConnected
            ? ImageHelper.LoadFromResource(new Uri("avares://native-desktop-app/Assets/greenwifi.png"))
            : ImageHelper.LoadFromResource(new Uri("avares://native-desktop-app/Assets/redwifi.png"));

        /// <summary>
        ///     Printing status indicator as a bitmap. Shows a "printing" icon when actively printing;
        ///     otherwise returns <c>null</c> (no overlay).
        /// </summary>
        public Bitmap? StatusImage => IsPrinting
            ? ImageHelper.LoadFromResource(new Uri("avares://native-desktop-app/Assets/printing.png"))
            : null;
    }
}