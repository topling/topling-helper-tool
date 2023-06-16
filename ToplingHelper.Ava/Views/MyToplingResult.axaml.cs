using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava.Views;

public partial class MyToplingResult : Window
{
    public Instance Instance { get; init; } = default!;
    public ToplingConstants ToplingConstants { get; init; } = default!;
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