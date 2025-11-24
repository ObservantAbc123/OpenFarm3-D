using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using DatabaseAccess.Models;

namespace native_desktop_app.ViewModels;

/// <summary>
/// Functions as a DTO and wrapper for the
/// detail-expansion functionality available
/// to the user from each row.
/// </summary>
public partial class MaintenanceRowViewModel : ObservableObject
{
    [ObservableProperty] private string _associatedPrinterName;

    [ObservableProperty] private IBrush _detailsColoring;

    [ObservableProperty]
    private string _timeSinceLastService;

    [ObservableProperty]
    private string _timeTillNextService;

    [ObservableProperty]
    private string _lastServicedDate;

    [ObservableProperty] private string _uptime;

    [ObservableProperty] private string _tempC;

    [ObservableProperty] private string _tempF;

    [ObservableProperty] private string _extrusion;

    [ObservableProperty] private string _travel;

    [ObservableProperty] private string _countPrintSuccesses;

    [ObservableProperty] private string _countPrintFailures;

    [ObservableProperty] private string _errorCount;

    // for custom widget
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isExpanded;

    public MaintenanceRowViewModel(string associatedPrinterName, string timeSinceLastService, string timeTillNextService, string lastServicedDate,
        string uptime, string tempC, string tempF, string countPrintSuccesses, string countPrintFailures,
        string extrusion, string travel, string errorCount, IBrush detailsColoring)
    {
        _associatedPrinterName = associatedPrinterName;
        _timeSinceLastService = timeSinceLastService;
        _timeTillNextService = timeTillNextService;
        _lastServicedDate = lastServicedDate;
        _uptime = uptime;
        _tempC = tempC;
        _tempF = tempF;
        _countPrintSuccesses = countPrintSuccesses;
        _countPrintFailures = countPrintFailures;
        _extrusion = extrusion;
        _travel = travel;
        _errorCount = errorCount;
        _detailsColoring = detailsColoring;
    }

    /// <summary>
    /// Updates the properties of this existing row instance.
    /// This triggers PropertyChanged events, which Avalonia bindings listen to.
    /// </summary>
    public void Update(string timeSinceLastService, string timeTillNextService, string lastServicedDate,
        string uptime, string tempC, string tempF, string countPrintSuccesses, string countPrintFailures,
        string extrusion, string travel, string errorCount, IBrush detailsColoring)
    {
        TimeSinceLastService = timeSinceLastService;
        TimeTillNextService = timeTillNextService;
        LastServicedDate = lastServicedDate;
        Uptime = uptime;
        TempC = tempC;
        TempF = tempF;
        CountPrintSuccesses = countPrintSuccesses;
        CountPrintFailures = countPrintFailures;
        Extrusion = extrusion;
        Travel = travel;
        ErrorCount = errorCount;
        DetailsColoring = detailsColoring;
    }

    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }
}
