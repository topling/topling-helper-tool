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

        public ResultDataContext Context => (ResultDataContext)Viewer.DataContext;

        public MyToplingWindow(ResultDataContext context)
        {
            InitializeComponent();
            Viewer.DataContext = context;
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

        private void RouteTable_click(object sender, RoutedEventArgs e)
        {

            try
            {
                Process.Start("Explorer", Context.RouteUrl);
            }
            catch (Exception)
            {
                var window = OpenUrlFail.New(Context.RouteUrl, this);
                window.Show();
            }

        }
    }
}
