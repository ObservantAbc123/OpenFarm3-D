using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace native_desktop_app.Views;

public partial class PrintJobsView : UserControl
{
    public PrintJobsView()
    {
        InitializeComponent();
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}