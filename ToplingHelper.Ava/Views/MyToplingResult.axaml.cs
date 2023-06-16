using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ToplingHelper.Ava.Views;

public partial class MyToplingResult : Window
{
    public MyToplingResult()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}