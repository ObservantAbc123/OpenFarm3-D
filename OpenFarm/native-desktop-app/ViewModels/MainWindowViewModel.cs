// Purpose
//   ViewModel controlling the main window tab navigation and active sub-view model.
//   Keeps track of the selected tab index and updates CurrentViewModel accordingly.
// -----------------------------------------------------------------------------

using native_desktop_app.ViewModels;
using System;
using System.Collections.Generic;
using native_desktop_app.Views;
using DatabaseAccess;
using RabbitMQHelper;

namespace native_desktop_app.ViewModels;

/// <summary>
///     The main window view model that manages active tab navigation.
///     <para>
///         Handles switching between various page ViewModels based on the selected tab index.
///     </para>
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    // Backing field for the currently active ViewModel
    private ViewModelBase _currentViewModel;

    // Backing field for SelectedTabIndex
    private int _selectedTabIndex;

    /// <summary>
    /// Initializes a new instance of <see cref="MainWindowViewModel"/>,
    /// setting the default tab to Home and instantiating its corresponding ViewModel.
    /// </summary>
    public MainWindowViewModel() : this(CreateDbHelper(), new RmqHelper())
    {
    }

    public MainWindowViewModel(DatabaseAccessHelper db, IRmqHelper rmq) : base(db, rmq)
    {
        // Default to the Home tab
        SelectedTabIndex = 0;
        UpdateCurrentViewModel();
    }

    private static DatabaseAccessHelper CreateDbHelper()
    {
        var conn = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(conn)) throw new InvalidOperationException("Database connection string not set.");
        return new DatabaseAccessHelper(conn.Trim('"'));
    }

    /// <summary>
    /// The index of the currently selected tab.
    /// Changing this property triggers a ViewModel switch.
    /// </summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                UpdateCurrentViewModel();
            }
        }
    }

    /// <summary>
    /// The active <see cref="ViewModelBase"/> instance corresponding to the selected tab.
    /// </summary>
    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    /// <summary>
    /// Updates the <see cref="CurrentViewModel"/> to match the selected tab.
    /// Each tab index corresponds to a specific ViewModel.
    /// </summary>
    private void UpdateCurrentViewModel()
    {
        switch (SelectedTabIndex)
        {
            case 0:
                CurrentViewModel = new HomeViewModel(_databaseAccessHelper, _rmqHelper);
                break;
            case 1:
                CurrentViewModel = new PrintJobsViewModel(_databaseAccessHelper, _rmqHelper);
                break;
            case 2:
                CurrentViewModel = new StaffReviewViewModel(_databaseAccessHelper, _rmqHelper);
                break;
            case 3:
                CurrentViewModel = new PrintersViewModel(_databaseAccessHelper, _rmqHelper);
                break;
            case 4:
                CurrentViewModel = new MessagesViewModel(_databaseAccessHelper, _rmqHelper);
                break;
            case 5:
                CurrentViewModel = new UsersViewModel(_databaseAccessHelper, _rmqHelper);
                break;
            case 6:
                CurrentViewModel = new StatsViewModel(_databaseAccessHelper, _rmqHelper);
                break;
            case 7:
                CurrentViewModel = new ConfigViewModel(_databaseAccessHelper, _rmqHelper);
                break;
            case 8:
                CurrentViewModel = new MaintenanceViewModel(_databaseAccessHelper, _rmqHelper);
                break;
            default:
                CurrentViewModel = new HomeViewModel(_databaseAccessHelper, _rmqHelper);
                break;
        }
    }
}
