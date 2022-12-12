using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Aliyun.Acs.Core.Exceptions;
using ToplingHelperModels.Models;
using ToplingHelperModels.SubNetLogic;

namespace ToplingHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private readonly StringBuilder _logBuilder = new();

        private ToplingUserData.InstanceType? InstanceType { get; set; }

        private ToplingConstants ToplingConstants { get; init; }
        private ToplingUserData UserData { get; set; }

        public MainWindow(ToplingConstants toplingConstants, ToplingUserData toplingUserData)
        {

            InitializeComponent();
            UserData = toplingUserData;
            ToplingConstants = toplingConstants;
            AccessSecret.Text = UserData.AccessSecret;
            AccessId.Text = UserData.AccessId;
            ToplingUserId.Text = UserData.ToplingUserId;
            ToplingPassword.Password = UserData.ToplingPassword;

        }
        private void Submit_Click(object sender, RoutedEventArgs e)
        {

            _logBuilder.Clear();
            Log.Text = "";
            SetInputs(false);

            if (InstanceType == null)
            {
                MessageBox.Show("请选择创建 Todis 服务或 MyTopling 服务");
                SetInputs(true);
                return;
            }
            if (
                string.IsNullOrWhiteSpace(AccessSecret.Text) ||
                string.IsNullOrWhiteSpace(AccessId.Text) ||
                string.IsNullOrWhiteSpace(ToplingUserId.Text) ||
                string.IsNullOrWhiteSpace(ToplingPassword.Password)
            )
            {
                MessageBox.Show("请检查是否全部输入");
                SetInputs(true);
                return;
            }

            if (InstanceType == null)
            {
                MessageBox.Show("请选服务类型");
                SetInputs(true);
                return;
            }

            if (!uint.TryParse(ServerId.Text, out var serverId) || serverId == 0)
            {
                MessageBox.Show("自定义 server-id 输入不合法");
                SetInputs(true);
                return;
            }

            var context = (MySqlDataContext)MainWindowGrid.DataContext;
            UserData = new ToplingUserData
            {
                AccessId = AccessId.Text,
                AccessSecret = AccessSecret.Text,
                ToplingUserId = ToplingUserId.Text,
                ToplingPassword = ToplingPassword.Password,
                GtidMode = UseGtid.IsChecked ?? false,
                ServerId = context.EditServerId ? uint.Parse(ServerId.Text) : 0,
                CreatingInstanceType = InstanceType.Value,
            };

            if (!UserData.UserdataCheck(out var error))
            {
                MessageBox.Show(error);
                SetInputs(true);
                return;
            }
            Task.Run(Worker);

        }


        private void Worker()
        {

            try
            {
                var handler = new AliYunResources(ToplingConstants, UserData, AppendLog);
                // 上面构造的过程中会尝试登录topling服务器，判定用户名密码。
                Dispatcher.BeginInvoke(() => MessageBox.Show("流程约三分钟，请不要关闭窗口!"));
                var instance = handler.CreateInstance();
                Action action = UserData.CreatingInstanceType switch
                {
                    ToplingUserData.InstanceType.Todis => () =>
                    {
                        var window = new RichText(instance, ToplingConstants)
                        {
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this
                        };
                        window.Show();
                    }
                    ,
                    ToplingUserData.InstanceType.MyTopling => () =>
                    {
                        var window = new MyToplingWindow(instance, ToplingConstants)
                        {
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this
                        };
                        window.Show();
                    }
                    ,
                    _ => throw new ArgumentOutOfRangeException()
                };

                Dispatcher.Invoke(action);
            }
            catch (ClientException e)
            {
                // 后面测试这里是否能够捕获
                if (e.ErrorCode.Equals("InvalidStatus.RouteEntry"))
                {
                    ShowError($"云服务商路由表状态错误，请重试(不需要关闭自动化工具)。{Environment.NewLine}" +
                              "如果十分钟后此错误仍然出现，请联系客服");
                }
                else
                {
                    ShowError(e.Message);
                }

            }
            catch (Exception e)
            {
                if (e.Message.Contains("OperationFailed.CdtNotOpened"))
                {
                    Dispatcher.Invoke(() =>
                    {
                        var window = new CdtNotOpened()
                        {
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this
                        };
                        window.Show();
                    });
                }
                else
                {
                    ShowError(e.Message);
                }

            }
            finally
            {
                Dispatcher.Invoke(() => { SetInputs(true); });
            }
        }

        private static void ShowError(string message, string caption = "执行失败")
        {
            // 保证报错在前台
            MessageBox.Show(
                $"执行失败:{Environment.NewLine}{message}",
                caption,
                MessageBoxButton.OK,
                MessageBoxImage.Exclamation,
                MessageBoxResult.OK,
                MessageBoxOptions.DefaultDesktopOnly);
        }

        private void SetInputs(bool status)
        {
            Btn.IsEnabled = status;
            ToplingUserId.IsEnabled = status;
            ToplingPassword.IsEnabled = status;
            AccessId.IsEnabled = status;
            AccessSecret.IsEnabled = status;
        }



        private void AppendLog(string line)
        {
            _logBuilder.AppendLine(line);
            Dispatcher.Invoke(() => { Log.Text = _logBuilder.ToString(); });
        }


        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            const string url = "https://ram.console.aliyun.com/manage";
            try
            {
                Process.Start("Explorer", url);

            }
            catch (Exception)
            {
                var window = OpenUrlFail.New(url, this);
                window.Show();
            }
        }


        private void Set_Todis(object sender, RoutedEventArgs e)
        {
            InstanceType = ToplingUserData.InstanceType.Todis;
            ((MySqlDataContext)MainWindowGrid.DataContext).IsMySql = false;
        }

        private void Set_MyTopling(object sender, RoutedEventArgs e)
        {
            InstanceType = ToplingUserData.InstanceType.MyTopling;
            ((MySqlDataContext)MainWindowGrid.DataContext).IsMySql = true;

        }


    }
}
