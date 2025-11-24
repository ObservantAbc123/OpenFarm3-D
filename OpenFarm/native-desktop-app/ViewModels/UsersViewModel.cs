// Purpose
//   ViewModel for managing and displaying a list of users. Loads user data from
//   the database and exposes it via an ObservableCollection for data binding.
// -----------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using DatabaseAccess;
using RabbitMQHelper;

namespace native_desktop_app.ViewModels;

/// <summary>
///     ViewModel for displaying and managing all users in the system.
///     <para>
///         • Fetches user data from the database using <see cref="DatabaseAccessHelper.Users" /> and
///         <see cref="DatabaseAccessHelper.Emails" />.
///         • Projects them into <see cref="UserRow" /> objects so the view can bind to display-friendly fields.
///     </para>
/// </summary>
public class UsersViewModel : ViewModelBase
{
    /// <summary>
    ///     Backing field for the collection of user rows.
    /// </summary>
    private ObservableCollection<UserRow> _allUsers = new();

    public UsersViewModel(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper)
        : base(databaseAccessHelper, rmqHelper)
    {
        // fire-and-forget initial load
        _ = LoadUsersAsync();

        ToggleBlacklistCommand = new AsyncRelayCommand<UserRow>(ToggleBlacklistAsync);
        DeleteUserCommand = new AsyncRelayCommand<UserRow>(DeleteUserAsync);
    }

    public ICommand ToggleBlacklistCommand { get; }
    public ICommand DeleteUserCommand { get; }

    /// <summary>
    ///     Collection of all users, pre-resolved to display fields.
    ///     Bind your ItemsControl / DataGrid to this.
    /// </summary>
    public ObservableCollection<UserRow> AllUsers
    {
        get => _allUsers;
        private set => SetProperty(ref _allUsers, value);
    }

    /// <summary>
    ///     Loads users from DB, fetches their primary email and created-at, and
    ///     materializes them into UserRow objects.
    /// </summary>
    private async Task LoadUsersAsync()
    {
        // fetch all users first
        var users = await _databaseAccessHelper.Users.GetUsersAsync();

        // temp collection to build before we slam the UI collection
        var rows = new ObservableCollection<UserRow>();

        foreach (var user in users)
        {
            // get their primary email (may be null)
            var email = await _databaseAccessHelper.Emails
                .GetUserPrimaryEmailAddressAsync(user.Id);

            // get their created-at (might be null depending on your schema)
            var createdAtUtc = await _databaseAccessHelper.Users
                .GetUserCreatedAtAsync(user.Id);

            // convert to local (your app runs in local time)
            var createdAtLocal = createdAtUtc?.ToLocalTime();

            rows.Add(new UserRow
            {
                User = user,
                PrimaryEmail = string.IsNullOrWhiteSpace(email) ? "—" : email,
                CreatedAtLocal = createdAtLocal
            });
        }

        // now swap the collection once
        AllUsers = rows;
    }


    private async Task ToggleBlacklistAsync(UserRow? row)
    {
        if (row?.User == null) return;

        var newStatus = !row.User.Suspended;
        var result = await _databaseAccessHelper.Users.SetUserSuspensionStatusAsync(row.User.Id, newStatus);

        if (result == DatabaseAccess.TransactionResult.Succeeded)
        {
            row.User.Suspended = newStatus;
            // Force UI update if needed, though ObservableCollection + INotifyPropertyChanged on UserRow would be better
            // For now, we reload to be safe and simple
            await LoadUsersAsync();
        }
    }

    private async Task DeleteUserAsync(UserRow? row)
    {
        if (row?.User == null) return;

        // Confirm deletion
        var confirmed = await ShowConfirmDialogAsync($"Delete User '{row.User.Name}'?", "Are you sure you want to delete this user? This action cannot be undone.");
        if (!confirmed) return;

        var result = await _databaseAccessHelper.Users.DeleteUserAsync(row.User.Id);

        if (result == DatabaseAccess.TransactionResult.Succeeded)
        {
            AllUsers.Remove(row);
        }
        else
        {
            // Show error
            // Assuming we have a ShowSimpleDialogAsync helper in ViewModelBase or similar
            // If not, we can skip or implement one. Based on ConfigViewModel, it seems likely.
             await ShowSimpleDialogAsync("Error", "Failed to delete user.");
        }
    }

    // Helper for confirmation dialog (copied/adapted from ConfigViewModel patterns)
    private async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
        var yesBtn = new Button { Content = "Yes", Width = 70 };
        var noBtn = new Button { Content = "No", Width = 70 };

        bool result = false;
        yesBtn.Click += (_, _) => { result = true; dialog.Close(); };
        noBtn.Click += (_, _) => { dialog.Close(); };

        btnPanel.Children.Add(yesBtn);
        btnPanel.Children.Add(noBtn);
        panel.Children.Add(btnPanel);
        dialog.Content = panel;

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }

        return result;
    }

    private async Task ShowSimpleDialogAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        
        var okBtn = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Width = 70 };
        okBtn.Click += (_, _) => dialog.Close();
        
        panel.Children.Add(okBtn);
        dialog.Content = panel;

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }
    }
}