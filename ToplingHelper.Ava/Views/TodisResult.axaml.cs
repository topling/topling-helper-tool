using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SkiaSharp;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava.Views;

public partial class TodisResult : Window
{
    public ToplingConstants ToplingConstants { get; init; } = default!;

    public TodisResult()
    {
        InitializeComponent();
    }

    public string GrafanaUrl
    {
        get
        {
            var instance = DataContext as Instance;
            var ecsId = instance?.InstanceEcsId;
            if (ecsId == null)
            {
                return "";
            }
            return $"http://{ecsId}.aliyun.db.{ToplingBaseHost}:8000";
        } 
    }

    public string EngineUrl
    {
        get
        {
            var instance = DataContext as Instance;
            var ecsId = instance?.InstanceEcsId;
            if (ecsId == null)
            {
                return "";
            }
            return $"http://{ecsId}.aliyun.db.{ToplingBaseHost}:3000";
        }
    }



    private string ToplingBaseHost =>
        string.Join(".", ToplingConstants.ToplingConsoleHost
            .Split(".")
            .TakeLast(2));
}