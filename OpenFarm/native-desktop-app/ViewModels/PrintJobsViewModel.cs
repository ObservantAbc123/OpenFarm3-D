// Purpose
//   ViewModel that loads print jobs, exposes filter toggles (status/printer/paid/
//   autostart/date), and provides commands for marking a job as paid and showing
//   job details. Filtering is OR within a category and AND across categories.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DatabaseAccess;
using DatabaseAccess.Models;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;
using Color = Avalonia.Media.Color;

namespace native_desktop_app.ViewModels;

/// <summary>
/// ViewModel that loads print jobs (models), applies filters on models,
/// then projects filtered models into row-VMs (PrintJobVM) for the UI.
/// OR within a category; AND across categories / date range.
/// </summary>
public class PrintJobsViewModel : ViewModelBase
{
    // --- Backing model collections (DB entities) ---
    private ObservableCollection<PrintJob> _allModels = new();
    private ObservableCollection<PrintJob> _filteredModels = new();

    // --- Rows for the UI (stable via cache) ---
    private readonly Dictionary<long, PrintJobVM> _vmCache = new();
    private ObservableCollection<PrintJobVM> _filteredRows = new();

    // --- Filtering state ---
    private bool _suspendFiltering;

    // Status
    private bool showCancelled = true;
    private bool showFinished = true;
    private bool showPrinting = true;
    private bool showQueue = true;
    private bool showAwaitingPayment = true;
    private bool showRejected = true;

    // Printer model
    private bool showGigabot = true;
    private bool showPrusai3MK3S = true;

    // Autostart
    private bool showAutoStartEnabled = true;
    private bool showAutoStartDisabled = true;

    // Submitted date range
    private DateTimeOffset? submittedStartDate;
    private DateTimeOffset? submittedEndDate;

    // Sort option (0=PrintTime, 1=RemainingTime, 2=Submitted)
    private int selectedSortOption = 0;

    // Search text
    private string searchText = string.Empty;

    // Search field (0=All, 1=PR#, 2=User, 3=Printer Model)
    private int selectedSearchField = 0;

    public PrintJobsViewModel(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper)
        : base(databaseAccessHelper, rmqHelper)
    {
        MarkPaidCommand = new RelayCommand<PrintJobVM>(async row => await ConfirmAndMarkPaidAsync(row));
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ApplyAllFiltersCommand = new RelayCommand(ApplyAllFilters);
        ShowJobDetailsCommand = new RelayCommand<PrintJob>(async job => await ShowJobDetailsAsync(job));

        _ = LoadJobsAsync();
    }

    // ------------------ Commands ------------------
    public ICommand MarkPaidCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand ApplyAllFiltersCommand { get; }
    public ICommand ShowJobDetailsCommand { get; }

    // ------------------ Bindable collections ------------------
    /// <summary>Bind your ItemsControl to this (row-VMs).</summary>
    public ObservableCollection<PrintJobVM> FilteredJobs
    {
        get => _filteredRows;
        private set => SetProperty(ref _filteredRows, value);
    }

    /// <summary>Total visible vs. total loaded, for your header.</summary>
    public string JobsCountText => $"Showing {FilteredJobsModels.Count} of {AllJobsModels.Count} jobs";

    // If you need counts, expose model collections (not bound to the list)
    public ObservableCollection<PrintJob> AllJobsModels
    {
        get => _allModels;
        private set => SetProperty(ref _allModels, value);
    }

    public ObservableCollection<PrintJob> FilteredJobsModels
    {
        get => _filteredModels;
        private set => SetProperty(ref _filteredModels, value);
    }

    // ------------------ Filter properties ------------------
    public DateTimeOffset? SubmittedStartDate
    {
        get => submittedStartDate;
        set { if (SetProperty(ref submittedStartDate, value)) ApplyFilters(); }
    }

    public DateTimeOffset? SubmittedEndDate
    {
        get => submittedEndDate;
        set { if (SetProperty(ref submittedEndDate, value)) ApplyFilters(); }
    }

    public bool ShowAwaitingPayment
    {
        get => showAwaitingPayment;
        set { if (SetProperty(ref showAwaitingPayment, value)) ApplyFilters(); }
    }

    public bool ShowRejected
    {
        get => showRejected;
        set { if (SetProperty(ref showRejected, value)) ApplyFilters(); }
    }

    public bool ShowCancelled
    {
        get => showCancelled;
        set { if (SetProperty(ref showCancelled, value)) ApplyFilters(); }
    }

    public bool ShowQueue
    {
        get => showQueue;
        set { if (SetProperty(ref showQueue, value)) ApplyFilters(); }
    }

    public bool ShowFinished
    {
        get => showFinished;
        set { if (SetProperty(ref showFinished, value)) ApplyFilters(); }
    }

    public bool ShowPrinting
    {
        get => showPrinting;
        set { if (SetProperty(ref showPrinting, value)) ApplyFilters(); }
    }

    public bool ShowGigabot
    {
        get => showGigabot;
        set { if (SetProperty(ref showGigabot, value)) ApplyFilters(); }
    }

    public bool ShowPrusai3MK3S
    {
        get => showPrusai3MK3S;
        set { if (SetProperty(ref showPrusai3MK3S, value)) ApplyFilters(); }
    }

    public bool ShowAutoStartEnabled
    {
        get => showAutoStartEnabled;
        set { if (SetProperty(ref showAutoStartEnabled, value)) ApplyFilters(); }
    }

    public bool ShowAutoStartDisabled
    {
        get => showAutoStartDisabled;
        set { if (SetProperty(ref showAutoStartDisabled, value)) ApplyFilters(); }
    }

    public int SelectedSortOption
    {
        get => selectedSortOption;
        set { if (SetProperty(ref selectedSortOption, value)) ApplyFilters(); }
    }

    public string SearchText
    {
        get => searchText;
        set { if (SetProperty(ref searchText, value)) ApplyFilters(); }
    }

    public int SelectedSearchField
    {
        get => selectedSearchField;
        set { if (SetProperty(ref selectedSearchField, value)) ApplyFilters(); }
    }

    // ------------------ Load & map ------------------
    private async Task LoadJobsAsync()
    {
        var jobs = await _databaseAccessHelper.PrintJobs.GetPrintJobsAsync();

        // Exclude systemApproved
        var visibleJobs = jobs
            .Where(j => !string.Equals(j.JobStatus, "systemApproved", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Hydrate printer model if present
        foreach (var j in visibleJobs)
        {
            if (j.PrinterModelId.HasValue)
                j.PrinterModel = await _databaseAccessHelper.PrinterModels.GetPrinterModelAsync(j.PrinterModelId.Value);
        }

        AllJobsModels = new ObservableCollection<PrintJob>(visibleJobs);
        FilteredJobsModels = new ObservableCollection<PrintJob>(visibleJobs);
        FilteredJobs = ToRows(FilteredJobsModels);

        OnPropertyChanged(nameof(JobsCountText));
        ApplyFilters();
    }

    /// <summary>
    /// Project a set of models into stable row-VMs using the cache.
    /// </summary>
    private ObservableCollection<PrintJobVM> ToRows(IEnumerable<PrintJob> models)
    {
        var rows = models.Select(m =>
        {
            if (!_vmCache.TryGetValue(m.Id, out var vm))
            {
                vm = new PrintJobVM(this, _databaseAccessHelper, _rmqHelper, m);
                _vmCache[m.Id] = vm;
            }
            else
            {
                // If data was reloaded, sync key fields & notify
                if (!ReferenceEquals(vm.Model, m))
                {
                    vm.Model.JobStatus = m.JobStatus;
                    vm.Model.Paid = m.Paid;
                    vm.Model.NumCopies = m.NumCopies;
                    vm.Model.PrinterModel = m.PrinterModel;
                    vm.Model.PrintTime = m.PrintTime;
                    vm.Model.SubmittedAt = m.SubmittedAt;
                    vm.RefreshBindings();
                }
            }
            return vm;
        });

        return new ObservableCollection<PrintJobVM>(rows);
    }

    // ------------------ Filtering ------------------
    private void ApplyFilters()
    {
        if (_suspendFiltering) return;

        var totalSelected =
            (ShowCancelled ? 1 : 0) + (ShowQueue ? 1 : 0) + (ShowFinished ? 1 : 0) + (ShowPrinting ? 1 : 0) +
            (ShowAwaitingPayment ? 1 : 0) + (ShowRejected ? 1 : 0) +
            (ShowGigabot ? 1 : 0) + (ShowPrusai3MK3S ? 1 : 0) +
            (ShowAutoStartEnabled ? 1 : 0) + (ShowAutoStartDisabled ? 1 : 0);

        if (totalSelected == 0 && !SubmittedStartDate.HasValue && !SubmittedEndDate.HasValue)
        {
            FilteredJobsModels = new ObservableCollection<PrintJob>();
            FilteredJobs = new ObservableCollection<PrintJobVM>();
            OnPropertyChanged(nameof(JobsCountText));
            return;
        }

        var anyStatus = ShowCancelled || ShowFinished || ShowPrinting || ShowQueue || ShowAwaitingPayment || ShowRejected;
        var anyPrinter = ShowGigabot || ShowPrusai3MK3S;
        var anyAuto = ShowAutoStartEnabled || ShowAutoStartDisabled;

        var filteredModels = AllJobsModels.Where(job =>
        {
            var status = job.JobStatus ?? string.Empty;

            var isCancelled = status.Equals("cancelled", StringComparison.OrdinalIgnoreCase);
            var isCompleted = status.Equals("completed", StringComparison.OrdinalIgnoreCase);
            var isPrinting = status.Equals("printing", StringComparison.OrdinalIgnoreCase);
            var isOpApproved = status.Equals("operatorApproved", StringComparison.OrdinalIgnoreCase);
            var isRejected = status.Equals("rejected", StringComparison.OrdinalIgnoreCase);

            var isQueue = isOpApproved && job.Paid;
            var isAwaitingPayment = isOpApproved && !job.Paid;

            var statusMatches = !anyStatus
                                || (ShowCancelled && isCancelled)
                                || (ShowFinished && isCompleted)
                                || (ShowPrinting && isPrinting)
                                || (ShowQueue && isQueue)
                                || (ShowAwaitingPayment && isAwaitingPayment)
                                || (ShowRejected && isRejected);

            bool printerMatches;
            if (!anyPrinter) printerMatches = true;
            else if (job.PrinterModel == null || string.IsNullOrWhiteSpace(job.PrinterModel.Model)) printerMatches = true;
            else
            {
                var model = job.PrinterModel.Model;
                printerMatches =
                    (ShowGigabot && model.Equals("Gigabot", StringComparison.OrdinalIgnoreCase)) ||
                    (ShowPrusai3MK3S && model.Equals("MK4S", StringComparison.OrdinalIgnoreCase));
            }

            bool autoMatches;
            if (!anyAuto) autoMatches = true;
            else if (job.PrinterModel == null) autoMatches = true;
            else
                autoMatches =
                    (ShowAutoStartEnabled && job.PrinterModel.Autostart) ||
                    (ShowAutoStartDisabled && !job.PrinterModel.Autostart);

            var submittedMatches = true;
            if (SubmittedStartDate.HasValue) submittedMatches &= job.SubmittedAt >= SubmittedStartDate.Value;
            if (SubmittedEndDate.HasValue) submittedMatches &= job.SubmittedAt <= SubmittedEndDate.Value;

            return statusMatches && printerMatches && autoMatches && submittedMatches;
        }).ToList();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filteredModels = filteredModels.Where(job =>
            {
                // Search based on selected field
                return SelectedSearchField switch
                {
                    0 => // All fields
                        ($"PR-{job.Id}".ToLowerInvariant().Contains(searchLower) || job.Id.ToString().Contains(searchLower)) ||
                        (job.User?.Name?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                        (job.PrinterModel?.Model?.ToLowerInvariant().Contains(searchLower) ?? false),
                    1 => // PR# only
                        $"PR-{job.Id}".ToLowerInvariant().Contains(searchLower) || job.Id.ToString().Contains(searchLower),
                    2 => // User only
                        job.User?.Name?.ToLowerInvariant().Contains(searchLower) ?? false,
                    3 => // Printer Model only
                        job.PrinterModel?.Model?.ToLowerInvariant().Contains(searchLower) ?? false,
                    _ => false
                };
            }).ToList();
        }

        // Apply sorting
        IEnumerable<PrintJob> sortedModels = SelectedSortOption switch
        {
            0 => filteredModels.OrderBy(j => j.PrintTime ?? double.MaxValue), // Print Time
            1 => filteredModels.OrderBy(j => (j.PrintTime ?? 0) - CalculateElapsed(j)), // Remaining Time
            2 => filteredModels.OrderBy(j => j.SubmittedAt), // Submitted
            _ => filteredModels
        };

        FilteredJobsModels = new ObservableCollection<PrintJob>(sortedModels);
        FilteredJobs = ToRows(FilteredJobsModels);

        OnPropertyChanged(nameof(JobsCountText));
    }

    private static double CalculateElapsed(PrintJob job)
    {
        // For jobs that are printing, calculate elapsed time
        // For completed jobs, use the full print time
        if (string.Equals(job.JobStatus, "printing", StringComparison.OrdinalIgnoreCase))
        {
            // Simplified: assume some elapsed time based on submission
            var elapsed = (DateTime.UtcNow - job.SubmittedAt).TotalMinutes;
            return Math.Min(elapsed, job.PrintTime ?? 0);
        }
        return 0;
    }

    // ------------------ Commands ------------------
    private async Task ConfirmAndMarkPaidAsync(PrintJobVM row)
    {
        if (row is null) return;
        var job = row.Model;
        if (job.Paid) return;

        var confirmDialog = new Window
        {
            Title = "Confirm Payment",
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var messageText = new TextBlock
        {
            Text = $"Mark PrintJob {job.Id} as Paid?",
            TextAlignment = TextAlignment.Center,
            FontSize = 14
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var yesButton = new Button { Content = "Yes", Width = 80, Height = 30 };
        var noButton = new Button { Content = "No", Width = 80, Height = 30 };

        var result = false;
        yesButton.Click += (_, __) => { result = true; confirmDialog.Close(); };
        noButton.Click += (_, __) => { confirmDialog.Close(); };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);
        panel.Children.Add(messageText);
        panel.Children.Add(buttonPanel);
        confirmDialog.Content = panel;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await confirmDialog.ShowDialog(parentWindow);
        if (!result) return;

        var txn = await _databaseAccessHelper.PrintJobs.MarkPrintJobAsPaidAsync(job.Id);
        if (txn != TransactionResult.Succeeded) return;

        job.Paid = true;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Reflect in row + re-filter
            row.RefreshBindings();

            // Nudge in collections to refresh row visuals without rebuilding everything
            void Bump<T>(ObservableCollection<T> col, T item)
            {
                var idx = col.IndexOf(item);
                if (idx >= 0) { col.RemoveAt(idx); col.Insert(idx, item); }
            }

            // Bump VM in row collection
            Bump(FilteredJobs, row);

            // Re-apply filters because Paid status may move this row between groups
            ApplyFilters();

            OnPropertyChanged(nameof(JobsCountText));
        });

        // Notify the rest of the system
        await _rmqHelper.QueueMessage(ExchangeNames.JobPaid, new RabbitMQHelper.MessageTypes.Message { JobId = job.Id });
    }

    public async Task ShowJobDetailsAsync(PrintJob job)
    {
        if (job == null) return;

        var window = new Window
        {
            Title = "Job Details",
            Width = 450,
            Height = 500,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.Parse("#282828")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#32302f")),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        var rowIndex = 0;

        void AddSectionHeader(string text)
        {
            var header = new TextBlock
            {
                Text = text,
                FontWeight = FontWeight.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 10, 0, 5),
                Foreground = new SolidColorBrush(Color.Parse("#fabd2f"))
            };
            Grid.SetRow(header, rowIndex++);
            Grid.SetColumnSpan(header, 2);
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Children.Add(header);
        }

        void AddRow(string label, object? value)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var labelBlock = new TextBlock
            {
                Text = label + ":",
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 2, 10, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#b57614"))
            };

            var displayValue = value switch
            {
                null => "N/A",
                bool b => b ? "✅ Yes" : "❌ No",
                DateTimeOffset dto => dto.ToString("g"),
                DateTime dt => dt.ToString("g"),
                double d => $"{d:F2}",
                decimal dec => $"{dec:C}",
                _ => value.ToString() ?? "N/A"
            };

            var valueBlock = new TextBlock
            {
                Text = displayValue,
                Margin = new Thickness(0, 2, 0, 2),
                Foreground = new SolidColorBrush(Color.Parse("#ebdbb2"))
            };

            Grid.SetRow(labelBlock, rowIndex);
            Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(valueBlock, rowIndex);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
            rowIndex++;
        }

        // Sections
        AddSectionHeader("Job Info");
        AddRow("PR#", job.Id);
        AddRow("Status", job.JobStatus);
        AddRow("User Id", job.UserId);
        AddRow("User", job.User);
        AddRow("Submitted", job.SubmittedAt);
        AddRow("Estimated Print Time", job.PrintTime);
        AddRow("Created At", job.CreatedAt);

        AddSectionHeader("Printer Info");
        AddRow("Printer Model Id", job.PrinterModelId);
        AddRow("Printer Model", job.PrinterModel?.Model);
        AddRow("Autostart", job.PrinterModel?.Autostart);

        AddSectionHeader("Payment & Material");
        AddRow("Paid", job.Paid);
        AddRow("Material", job.Material);
        AddRow("Weight", job.PrintWeight);
        AddRow("Cost", job.PrintCost);

        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0),
            Width = 80,
            Background = new SolidColorBrush(Color.Parse("#fabd2f")),
            Foreground = Brushes.Black
        };
        closeButton.Click += (_, __) => window.Close();

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(closeButton, rowIndex);
        Grid.SetColumnSpan(closeButton, 2);
        grid.Children.Add(closeButton);

        border.Child = grid;
        window.Content = new ScrollViewer { Content = border };

        var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
        await window.ShowDialog(lifetime.MainWindow);
    }

    // ------------------ Bulk filter helpers ------------------
    private void ClearFilters()
    {
        _suspendFiltering = true;
        try
        {
            ShowCancelled = false;
            ShowFinished = false;
            ShowPrinting = false;
            ShowQueue = false;
            ShowAwaitingPayment = false;
            ShowRejected = false;

            ShowGigabot = false;
            ShowPrusai3MK3S = false;

            ShowAutoStartEnabled = false;
            ShowAutoStartDisabled = false;

            SubmittedStartDate = null;
            SubmittedEndDate = null;
        }
        finally
        {
            _suspendFiltering = false;
            ApplyFilters();
        }
    }

    private void ApplyAllFilters()
    {
        ShowCancelled = true;
        ShowFinished = true;
        ShowPrinting = true;
        ShowQueue = true;
        ShowAwaitingPayment = true;
        ShowRejected = true;

        ShowGigabot = true;
        ShowPrusai3MK3S = true;

        ShowAutoStartEnabled = true;
        ShowAutoStartDisabled = true;
    }
}

// Used by the expanded "Copies" panel (PrinterName shown here)
public class PrintCopyVM
{
    public int CopyIndex { get; init; }
    public string Status { get; init; } = "";
    public string PrinterName { get; init; } = "Unassigned";
    public string? Elapsed { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>
///     Row view-model used by the ItemsControl (DataType: PrintJobsViewModel+PrintJobVM).
///     - Keeps the row showing the Printer Model (via Model.PrinterModel?.Model).
///     - Loads actual assigned Printer *Name* per-copy only when expanded.
/// </summary>
public class PrintJobVM : ViewModelBase
{
    private readonly DatabaseAccessHelper _db;
    private readonly PrintJobsViewModel _owner; // parent, useful if you want to call its commands

    private ObservableCollection<PrintCopyVM> _copies = new();

    // === Expansion + Copies (expanded panel) ===
    private bool _isExpanded;

    public PrintJobVM(PrintJobsViewModel owner, DatabaseAccessHelper db, IRmqHelper rmq, PrintJob model)
        : base(db, rmq)
    {
        _owner = owner;
        _db = db;
        Model = model ?? throw new ArgumentNullException(nameof(model));

        // You can keep this command if you want to use it elsewhere;
        // the ToggleButton is using IsChecked binding, not this command.
        ToggleExpandCommand = new RelayCommand(async () =>
        {
            IsExpanded = !IsExpanded;
            if (IsExpanded && Copies.Count == 0 && CanExpand)
                await LoadCopiesAsync();
        });
    }

    // The underlying DB entity for the row
    public PrintJob Model { get; }

    // === Helper flags for expansion rules ===
    public bool IsCancelled =>
        string.Equals(JobStatus, "cancelled", StringComparison.OrdinalIgnoreCase);

    public bool IsRejected =>
        string.Equals(JobStatus, "rejected", StringComparison.OrdinalIgnoreCase);

    public bool IsAwaitingPayment =>
        string.Equals(JobStatus, "operatorApproved", StringComparison.OrdinalIgnoreCase) && !Paid;

    /// <summary>
    /// Only allow expansion for rows that can show meaningful per-print info.
    /// (e.g. printing, completed, queue). You can tweak this logic if needed.
    /// </summary>
    public bool CanExpand => !(IsCancelled || IsRejected || IsAwaitingPayment);

    /// <summary>
    /// Convenience for XAML: expanded panel should only show when we *can*
    /// expand and the row is actually expanded.
    /// </summary>
    public bool ShowExpandedPanel => CanExpand && IsExpanded;

    // === Properties bound in your XAML row ===
    public long Id => Model.Id;

    public string? JobStatus
    {
        get => Model.JobStatus;
        set
        {
            if (Model.JobStatus != value)
            {
                Model.JobStatus = value;
                OnPropertyChanged();

                // These depend on status:
                OnPropertyChanged(nameof(IsCancelled));
                OnPropertyChanged(nameof(IsRejected));
                OnPropertyChanged(nameof(IsAwaitingPayment));
                OnPropertyChanged(nameof(CanExpand));
                OnPropertyChanged(nameof(ShowExpandedPanel));

                // If it became non-expandable, collapse it
                if (!CanExpand && IsExpanded)
                    IsExpanded = false;
            }
        }
    }

    public bool Paid
    {
        get => Model.Paid;
        set
        {
            if (Model.Paid != value)
            {
                Model.Paid = value;
                OnPropertyChanged();

                // These depend on Paid:
                OnPropertyChanged(nameof(IsAwaitingPayment));
                OnPropertyChanged(nameof(CanExpand));
                OnPropertyChanged(nameof(ShowExpandedPanel));

                if (!CanExpand && IsExpanded)
                    IsExpanded = false;
            }
        }
    }

    // Shown in the row (col 3)
    public int NumCopies
    {
        get
        {
            // Prefer the model’s count if present; otherwise fall back to loaded copies
            if (Model.NumCopies > 0) return Model.NumCopies;
            return Copies.Count;
        }
        set
        {
            if (Model.NumCopies != value)
            {
                Model.NumCopies = value;
                OnPropertyChanged();
            }
        }
    }

    // Row shows PrinterModel.Model and PrinterModel.Autostart
    public PrinterModel? PrinterModel
    {
        get => Model.PrinterModel;
        set
        {
            if (!ReferenceEquals(Model.PrinterModel, value))
            {
                Model.PrinterModel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrinterModelName));
                OnPropertyChanged(nameof(PrinterModelAutostart));
            }
        }
    }

    public string? PrinterModelName => Model.PrinterModel?.Model;
    public bool? PrinterModelAutostart => Model.PrinterModel?.Autostart;

    public double? PrintTime
    {
        get => Model.PrintTime;
        set
        {
            if (Model.PrintTime != value)
            {
                Model.PrintTime = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime SubmittedAt
    {
        get => Model.SubmittedAt;
        set
        {
            if (Model.SubmittedAt != value)
            {
                Model.SubmittedAt = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                // Keep XAML-friendly flag in sync
                OnPropertyChanged(nameof(ShowExpandedPanel));

                if (value && Copies.Count == 0 && CanExpand)
                {
                    // Fire & forget; UI will render once Copies is set
                    _ = LoadCopiesAsync();
                }
            }
        }
    }

    public ObservableCollection<PrintCopyVM> Copies
    {
        get => _copies;
        private set => SetProperty(ref _copies, value);
    }

    public ICommand ToggleExpandCommand { get; }

    /// <summary>
    ///     Loads this job's prints, eager-loading Printer so we can show Printer.Name
    ///     in the expanded "Copies" panel.
    /// </summary>
    public async Task LoadCopiesAsync()
    {
        var prints =
            await _db.Prints.GetPrintsByPrintJobIdAsync(Model.Id);

        var mapped = prints
            .OrderBy(p => p.CreatedAt)
            .Select((p, i) => new PrintCopyVM
            {
                CopyIndex = i + 1,
                Status = p.PrintStatus ?? "",
                PrinterName = p.Printer?.Name ?? "Unassigned",
                Elapsed = FormatElapsed(p.StartedAt, p.FinishedAt),
                CompletedAt = p.FinishedAt
            })
            .ToList();

        // marshal updates to UI thread
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Copies = new ObservableCollection<PrintCopyVM>(mapped);
            if (Model.NumCopies <= 0)
                OnPropertyChanged(nameof(NumCopies));
        });
    }

    private static string? FormatElapsed(DateTime? startUtc, DateTime? endUtc)
    {
        var start = startUtc ?? endUtc;
        var end = endUtc ?? DateTime.UtcNow;
        if (start is null) return null;

        var span = end - start.Value;
        return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
    }

    /// <summary>
    ///     Helper if the owner updates this model instance externally (e.g., mark paid).
    ///     Call after you mutate Model to notify the row.
    /// </summary>
    public void RefreshBindings()
    {
        OnPropertyChanged(nameof(JobStatus));
        OnPropertyChanged(nameof(Paid));
        OnPropertyChanged(nameof(NumCopies));
        OnPropertyChanged(nameof(PrinterModel));
        OnPropertyChanged(nameof(PrinterModelName));
        OnPropertyChanged(nameof(PrinterModelAutostart));
        OnPropertyChanged(nameof(PrintTime));
        OnPropertyChanged(nameof(SubmittedAt));

        // Also refresh the helper flags used for expansion
        OnPropertyChanged(nameof(IsCancelled));
        OnPropertyChanged(nameof(IsRejected));
        OnPropertyChanged(nameof(IsAwaitingPayment));
        OnPropertyChanged(nameof(CanExpand));
        OnPropertyChanged(nameof(ShowExpandedPanel));

        if (!CanExpand && IsExpanded)
            IsExpanded = false;
    }
}
