using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace native_desktop_app.Views;

public partial class MaintenanceView : UserControl
{
    public MaintenanceView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}