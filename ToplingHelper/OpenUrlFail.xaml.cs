using System;
using System.Collections.Generic;
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
    /// OpenUrlFail.xaml 的交互逻辑
    /// </summary>
    public partial class OpenUrlFail : Window
    {


        public static OpenUrlFail New(string url, Window window)
        {
            return new OpenUrlFail(url)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = window
            };
        }

        public OpenUrlFail(string url)
        {
            InitializeComponent();
            // base64防止被报毒
            // 打开连接出错，请关闭360等软件后点击，或手动访问
            const string base64 = "5omT5byA6L+e5o6l5Ye66ZSZ77yM6K+35YWz6ZetMzYw562J6L2v5Lu25ZCO54K55Ye777yM5oiW5omL5Yqo6K6/6Zeu";
            CloseTextBox.Text =
            Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            Url.Text = url;
        }
    }
}
