using ToplingHelperModels.Models;

namespace ToplingHelperMaui;

public partial class MyToplingPage : ContentPage
{
    private readonly ResultDataContext _resultDataContext;
    public MyToplingPage()
	{
		InitializeComponent();
	}
    public MyToplingPage(Instance instance, ToplingConstants constants)
    {
        InitializeComponent();
        _resultDataContext = new ResultDataContext
        {
            Constants = constants,
            EcsId = instance.InstanceEcsId,
            UserVpcId = instance.VpcId,
            PeerId = instance.PeerId,
            RouteId = instance.RouteId,
            InstancePrivateIp = instance.PrivateIp
        };
        this.Resources["ContextKey"] = _resultDataContext;

        GrafanaLink.Url = $"http://{_resultDataContext.EcsId}.aliyun.db.{BaseDomain}:3000";
        EngineLink.Url = $"http://{_resultDataContext.EcsId}.aliyun.db.{BaseDomain}:8000";
        RouteLink.Url = _resultDataContext.RouteUrl;
    }

    private string BaseDomain
    {
        get
        {
            var host = (_resultDataContext.Constants.ToplingConsoleHost.StartsWith("http")
                ? (new Uri(_resultDataContext.Constants.ToplingConsoleHost)).Host
                : _resultDataContext.Constants.ToplingConsoleHost);

            return string.Join(".", host.Split('.').TakeLast(2));

        }
    }
}