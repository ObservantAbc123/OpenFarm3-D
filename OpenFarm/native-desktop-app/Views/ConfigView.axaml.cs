using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using native_desktop_app.ViewModels;

namespace native_desktop_app.Views;

public partial class ConfigView : UserControl
{
    public ConfigView()
    {
        InitializeComponent();
    }

    private async void OnRefreshMaterialPricingClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConfigViewModel vm)
        {
            await vm.LoadMaterialPricingAsync();
        }
    }

    private async void OnChangeMaterialPriceClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConfigViewModel vm) return;
        var row = vm.SelectedMaterialPricingRow;
        if (row is null) return;

        // TODO: Replace this with a proper dialog.
        // For now, you might have a simple input dialog for the new price.
        var input = await ShowInputDialogAsync("Change Material Price",
            $"Enter new price for {row.Material.MaterialType}:");

        if (decimal.TryParse(input, out var newPrice))
        {
            await vm.ChangeMaterialPriceAsync(row, newPrice);
        }
        else
        {
            // TODO: Show validation message to user.
        }
    }

    private async void OnViewMaterialPriceHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConfigViewModel vm) return;
        var row = vm.SelectedMaterialPricingRow;
        if (row is null) return;

        var history = await vm.GetMaterialPriceHistoryAsync(row);

        // TODO: Show history in a dialog or side panel.
        // For now, you could just log it or show a simple dialog.
        // Example: open a custom window with an ItemsControl bound to `history`.
    }

    // Dummy placeholder; replace with your actual dialog service
    private Task<string?> ShowInputDialogAsync(string title, string message)
    {
        // Implement your own dialog (Window) and return the entered string.
        // Leaving as a stub so this file compiles once you add your own dialog logic.
        return Task.FromResult<string?>(null);
    }
}