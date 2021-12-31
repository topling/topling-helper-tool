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

        public string CenId
        {
            get => (string)GetValue(CenIdProp);
            set => SetValue(CenIdProp, value);
        }


        //}

        private string TodisEcsId => (string)GetValue(TodisEcsIdProp);





        public RichText(string userVpcId, string toplingVpcId, string cenId, string todisPrivateIp, string todisEcsId)
        {
            InitializeComponent();


            SetValue(ToplingVpcIdProp, toplingVpcId);
            SetValue(UserVpcProp, userVpcId);
            SetValue(CenIdProp, cenId);
            SetValue(TodisPrivateIpProp, $"{todisPrivateIp}:6379");
            SetValue(TodisEcsIdProp, todisEcsId);
            SetValue(UserCenUrlProp, $"https://cen.console.aliyun.com/cen/detail/{cenId}/attachInstance");


            SetValue(PreTestProp, PreTestText);
            SetValue(TestTextProp, GetTestText(todisPrivateIp));

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

        public static readonly DependencyProperty UserCenUrlProp =
            DependencyProperty.Register("UserCenUrl", typeof(string), typeof(RichText), new PropertyMetadata(null));

        public static readonly DependencyProperty PreTestProp =
            DependencyProperty.Register("PreTest", typeof(string), typeof(RichText), new PropertyMetadata(null));

        public static readonly DependencyProperty TestTextProp =
            DependencyProperty.Register("TestText", typeof(string), typeof(RichText), new PropertyMetadata(null));
        private void Cen_click(object sender, RoutedEventArgs e)
        {
            var cenUrl = $"https://cen.console.aliyun.com/cen/detail/{CenId}/attachInstance";
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

        private const string PreTestText = @"# 请首先确保测试实例为CentOS/AliOS
# 下载自动脚本
wget https://topling.cn/downloads/mount-test.sh && chmod +x ./mount-test.sh

# 挂载测试程序及源数据到 /mnt
sudo bash mount-test.sh /mnt
";

        private static string GetTestText(string privateIp) => $@"# 直接执行查看帮助
/mnt/InsertKeys

# 插入顺序数据(源文件过大，已使用 zstd 压缩)
zstd -d -c -q /mnt/wikipedia-flat-seq.zst | /mnt/InsertKeys -h {privateIp} -t 8 --multi-set 8 -f /dev/stdin

# 插入乱序数据(源文件过大，已使用 zstd 压缩)
zstd -d -c -q /mnt/wikipedia-flat-rand.zst | /mnt/InsertKeys -h {privateIp} -t 8 --multi-set 8 -f /dev/stdin

# 读取数据(顺序)
/mnt/GetKeysQps -t 32 -n 8 -f /mnt/wikipedia-flat-key-seq.txt -h {privateIp}

# 读取数据(乱序)
/mnt/GetKeysQps -t 32 -n 8 -f /mnt/wikipedia-flat-key-rand.txt -h {privateIp}

# 测试 hash 写性能
zstd -d -c -q /mnt/weibo.zst  |  /mnt/InsertWeiboData -h {privateIp} -t 32 -f /dev/stdin  --disabled_seekg --ignore_logs
";

        private void Engine_OnClick(object sender, RoutedEventArgs e)
        {
            var ecsUrl = $"http://{TodisEcsId}.aliyun.db.topling.cn:8000";
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
            var ecsUrl = $"http://{TodisEcsId}.aliyun.db.topling.cn:3000";
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
    }

}
