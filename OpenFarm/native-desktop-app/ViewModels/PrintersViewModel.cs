// Purpose
//   ViewModel that lists printers and provides a command to delete a specific
//   printer after a user confirmation dialog. Data is loaded from
//   DatabaseAccessHelper and exposed via an ObservableCollection for binding.
// -----------------------------------------------------------------------------

using System;
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

namespace native_desktop_app.ViewModels;

/// <summary>
///     ViewModel for the printers management page.
///     <para>
///         Responsible for:
///         • Loading printers and their associated models from the database.
///         • Joining those entities into lightweight <see cref="PrinterRow" /> objects for UI display.
///         • Handling printer deletion via user confirmation dialog.
///     </para>
/// </summary>
public class PrintersViewModel : ViewModelBase
{
    /// <summary>
    ///     Backing field for the collection of printer display rows.
    /// </summary>
    private ObservableCollection<PrinterRow> _rows = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="PrintersViewModel" /> class.
    ///     Reads the database connection from environment, constructs the data helper,
    ///     wires commands, and starts the initial asynchronous load of printers.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the <c>DATABASE_CONNECTION_STRING</c> environment variable is not set.
    /// </exception>
    public PrintersViewModel(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper) : base(
        databaseAccessHelper, rmqHelper)
    {
        // RelayCommand<T> binds a Printer parameter from the UI to the async handler.
        MarkPrinterDeleted =
            new RelayCommand<Printer>(async printer => await ConfirmAndMarkPrinterDeletedAsync(printer));

        // Fire-and-forget to keep constructor synchronous.
        _ = LoadPrintersAsync();
    }

    /// <summary>
    ///     Collection of printer rows currently displayed in the UI.
    /// </summary>
    /// <remarks>
    ///     Each <see cref="PrinterRow" /> combines a <see cref="Printer" /> entity
    ///     with a pre-resolved <see cref="PrinterRow.ModelName" /> value.
    ///     The view binds directly to this collection via <c>ItemsSource</c>.
    /// </remarks>
    public ObservableCollection<PrinterRow> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>
    ///     Command to delete a specific printer after prompting the user
    ///     with a confirmation dialog.
    /// </summary>
    public ICommand MarkPrinterDeleted { get; }

    /// <summary>
    ///     Loads printers and their corresponding printer models from the database,
    ///     combines them into <see cref="PrinterRow" /> objects, and updates the UI.
    /// </summary>
    /// <remarks>
    ///     This method performs two asynchronous queries:
    ///     <list type="bullet">
    ///         <item>
    ///             <description>Retrieves all <see cref="Printer" /> entities.</description>
    ///         </item>
    ///         <item>
    ///             <description>Retrieves all <see cref="PrinterModel" /> entities.</description>
    ///         </item>
    ///     </list>
    ///     Then it performs an in-memory join (dictionary lookup) on
    ///     <see cref="Printer.PrinterModelId" /> to resolve <see cref="PrinterRow.ModelName" />.
    /// </remarks>
    private async Task LoadPrintersAsync()
    {
        // Fetch printers and models independently
        var printers = await _databaseAccessHelper.Printers.GetPrintersAsync();
        var models = await _databaseAccessHelper.PrinterModels.GetPrinterModelsAsync();

        // Map model ID -> name for fast lookup
        var nameById = models.ToDictionary(m => m.Id, m => m.Model);

        // Build row models for the view
        var newRows = printers.Select(p => new PrinterRow
        {
            Printer = p,
            ModelName = nameById.TryGetValue(p.PrinterModelId, out var name)
                ? name
                : "(unknown)"
        });

        // Marshal updates to the UI thread since ObservableCollection must be updated there
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Rows.Clear();
            foreach (var r in newRows)
                Rows.Add(r);
        });
    }

    /// <summary>
    ///     Shows a confirmation dialog and, if accepted, deletes the selected printer
    ///     using a cascading delete. No-ops if <paramref name="printer" /> is <c>null</c>.
    /// </summary>
    /// <param name="printer">The printer to delete.</param>
    private async Task ConfirmAndMarkPrinterDeletedAsync(Printer printer)
    {
        if (printer == null)
            return;

        // Construct a minimal confirmation dialog at runtime.
        var confirmDialog = new Window
        {
            Title = "Confirm Deletion",
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
            Text = $"Delete {printer.Id} {printer.Name}?",
            TextAlignment = TextAlignment.Center,
            FontSize = 14
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var yesButton = new Button
        {
            Content = "Yes",
            Width = 80,
            Height = 30
        };

        var noButton = new Button
        {
            Content = "No",
            Width = 80,
            Height = 30
        };

        var result = false;

        yesButton.Click += (sender, e) =>
        {
            result = true;
            confirmDialog.Close();
        };

        noButton.Click += (sender, e) => { confirmDialog.Close(); };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);

        panel.Children.Add(messageText);
        panel.Children.Add(buttonPanel);

        confirmDialog.Content = panel;

        // Determine the owner window (if running in a classic desktop lifetime).
        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        // Show the dialog and await user input.
        await confirmDialog.ShowDialog(parentWindow);

        if (result) await _databaseAccessHelper.Printers.DeletePrinterCascadingAsync(printer.Id);
        //await _rmqHelper.QueueMessage(ExchangeNames.JobPaid, new Message {JobId = job.Id});
    }
}