using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ToplingHelperModels.Models;

namespace ToplingHelper
{
    /// <summary>
    /// MyToplingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MyToplingWindow : Window
    {

        private readonly ResultDataContext _resultDataContext;

        public MyToplingWindow(Instance instance, ToplingConstants constants)
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


        private void Engine_OnClick(object sender, RoutedEventArgs e)
        {
            var ecsUrl = $"http://{_resultDataContext.EcsId}.aliyun.db.{BaseDomain}:8000";
            try
            {
                Process.Start("Explorer", ecsUrl);
            }
            catch (Exception)
            {
                var window = OpenUrlFail.New(ecsUrl, this);
                window.Show();
            }

        }


        private void Grafana_OnClick(object sender, RoutedEventArgs e)
        {
            var ecsUrl = $"http://{_resultDataContext.EcsId}.aliyun.db.{BaseDomain}:3000";
            try
            {
                Process.Start("Explorer", ecsUrl);
            }
            catch (Exception)
            {
                var window = OpenUrlFail.New(ecsUrl, this);
                window.Show();
            }
        }

        private void RouteTable_click(object sender, RoutedEventArgs e)
        {

            try
            {
                Process.Start("Explorer", _resultDataContext.RouteUrl);
            }
            catch (Exception)
            {
                var window = OpenUrlFail.New(_resultDataContext.RouteUrl, this);
                window.Show();
            }

        }
    }
}
