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

namespace ToplingHelper
{
    /// <summary>
    /// MyToplingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MyToplingWindow : Window
    {

        public ResultDataContext Context => (ResultDataContext)Viewer.DataContext;

        public MyToplingWindow(string userVpcId, string toplingVpcId, string cenId, string instancePrivateIp, string todisEcsId)
        {
            InitializeComponent();
            Context.UserVpcId = userVpcId;
            Context.ToplingVpcId = toplingVpcId;
            Context.CenId = cenId;
            Context.InstancePrivateIp = instancePrivateIp;
            Context.EcsId = todisEcsId;
        }

        private void Engine_OnClick(object sender, RoutedEventArgs e)
        {
            var ecsUrl = $"http://{Context.EcsId}.aliyun.db.topling.cn:8000";
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
            var ecsUrl = $"http://{Context.EcsId}.aliyun.db.topling.cn:3000";
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

        private void Cen_click(object sender, RoutedEventArgs e)
        {
            var cenUrl = $"https://cen.console.aliyun.com/cen/detail/{Context.CenId}/attachInstance";
            try
            {
                Process.Start("Explorer", cenUrl);
            }
            catch (Exception)
            {
                var window = OpenUrlFail.New(cenUrl, this);
                window.Show();
            }

        }
    }
}
