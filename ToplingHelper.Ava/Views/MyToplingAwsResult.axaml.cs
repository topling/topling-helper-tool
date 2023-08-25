using Avalonia.Controls;
using ToplingHelperModels;

namespace ToplingHelper.Ava.Views;

public partial class MyToplingAwsResult : Window
{
    public ToplingConstants ToplingConstants { get; init; } = default!;
    public MyToplingAwsResult()
    {
        InitializeComponent();
    }
}