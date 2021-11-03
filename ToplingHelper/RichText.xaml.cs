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
    /// RichText.xaml 的交互逻辑
    /// </summary>
    public partial class RichText : Window
    {
        public string UserVpcId
        {
            get => (string) GetValue(UserVpcProp);
            set => SetValue(UserVpcProp, value);
        }

        public string ToplingVpcId
        {
            get => (string)GetValue(ToplingVpcIdProp);
            set => SetValue(ToplingVpcIdProp, value);
        }

        

        public string CenId
        {
            get => (string)GetValue(CenIdProp);
            set => SetValue(CenIdProp, value);
        }

        public string TodisPrivateIp
        {
            get => (string)GetValue(TodisPrivateIpProp);
            set => SetValue(TodisPrivateIpProp, value);
        }

        public string TodisGrafana
        {
            get => (string)GetValue(TodisGrafanaProp);
            set => SetValue(TodisGrafanaProp, value);
        }

        public string TodisEcsId
        {
            get => (string)GetValue(TodisEcsIdProp);
            set => SetValue(TodisEcsIdProp, value);
        }

        public RichText()
        {
            InitializeComponent();
            UserVpcId = "vpc-user";
            ToplingVpcId = "vpc-topling";
            CenId = "cen-id";
            TodisPrivateIp = "10.1.2.3";
            TodisEcsId = "i-instance";
        }

        public static readonly DependencyProperty UserVpcProp =
            DependencyProperty.Register("UserVpcId", typeof(string), typeof(RichText), new PropertyMetadata(null));

        public static readonly DependencyProperty ToplingVpcIdProp =
            DependencyProperty.Register("ToplingVpcId", typeof(string), typeof(RichText), new PropertyMetadata(null));

        public static readonly DependencyProperty CenIdProp =
            DependencyProperty.Register("CenId", typeof(string), typeof(RichText), new PropertyMetadata(null));

        public static readonly DependencyProperty TodisEcsIdProp =
            DependencyProperty.Register("TodisEcsId", typeof(string), typeof(RichText), new PropertyMetadata(null));

        public static readonly DependencyProperty TodisPrivateIpProp =
            DependencyProperty.Register("TodisPrivateIp", typeof(string), typeof(RichText), new PropertyMetadata(null));

        public static readonly DependencyProperty TodisGrafanaProp =
            DependencyProperty.Register("TodisGrafana", typeof(string), typeof(RichText), new PropertyMetadata(null));

        public static readonly DependencyProperty TodisEngineProp =
            DependencyProperty.Register("TodisEngine", typeof(string), typeof(RichText), new PropertyMetadata(null));

        private void Cen_click(object sender, RoutedEventArgs e)
        {
            Process.Start("Explorer", $"https://cen.console.aliyun.com/cen/detail/{CenId}/attachInstance");
        }
    }
}
