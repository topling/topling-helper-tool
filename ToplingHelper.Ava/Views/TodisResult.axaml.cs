using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava.Views;

public partial class TodisResult : Window
{
    public ToplingConstants ToplingConstants { get; init; } = default!;

    public TodisResult()
    {
        InitializeComponent();

#if DEBUG
        this.AttachDevTools();
#endif

    }
}