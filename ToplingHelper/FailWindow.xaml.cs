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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ToplingHelper
{
    /// <summary>
    /// Fail.xaml 的交互逻辑
    /// </summary>
    public partial class FailWindow : Window
    {
        public FailWindow(string vpcId,string cenId,string toplingId)
        {
            InitializeComponent();

            AccountId.Text = toplingId;
            VpcId.Text = vpcId;
            _cenId = cenId;
        }

        private string _cenId;


        private void Cen_OnClick(object sender, RoutedEventArgs e)
        {
            var cenUrl = $"https://cen.console.aliyun.com/cen/detail/{_cenId}/attachInstance";
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
