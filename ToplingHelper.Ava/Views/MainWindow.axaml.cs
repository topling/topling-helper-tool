using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Aliyun.Acs.Core.Exceptions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Models;
using ToplingHelper.Ava.Models;
using ToplingHelperModels.Models;
using ToplingHelperModels.SubNetLogic;
using static ToplingHelperModels.Models.ToplingUserData;

namespace ToplingHelper.Ava.Views
{
    public partial class MainWindow : Window
    {
        private readonly StringBuilder _logBuilder = new();


        private ToplingUserDataBinding Context => (DataContext as ToplingUserDataBinding)!;
        public ToplingConstants ToplingConstants { get; init; } = default!;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ChangeServiceInstanceType(object? sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && DataContext is not null)
            {
                var context = DataContext as ToplingUserDataBinding;
                context!.CreatingInstanceType = btn.Name switch
                {
                    "MySqlRadio" => ToplingUserData.InstanceType.MyTopling,
                    "TodisRadio" => ToplingUserData.InstanceType.Todis,
                    _ => context!.CreatingInstanceType
                };
            }

        }
        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            
            _logBuilder.Clear();
            Log.Text = "";
            SetInputs(false);

            if (Context.CreatingInstanceType == InstanceType.Unknown)
            {
                ShowMessageBox("请选择创建 Todis 服务或 MyTopling 服务");
                SetInputs(true);
                return;
            }
            if (
                string.IsNullOrWhiteSpace(Context.AccessSecret) ||
                string.IsNullOrWhiteSpace(Context.AccessId) ||
                string.IsNullOrWhiteSpace(Context.ToplingUserId) ||
                string.IsNullOrWhiteSpace(Context.ToplingPassword)
            )
            {
                ShowMessageBox("请检查是否全部输入");
                SetInputs(true);
                return;
            }

            if (Context.EditServerId &&
                (!uint.TryParse(ServerId.Text, out var serverId) || serverId == 0))
            {
                ShowMessageBox("自定义 server-id 输入不合法");
                SetInputs(true);
                return;
            }


            if (!Context.UserdataCheck(out var error))
            {
                ShowMessageBox(error);
                SetInputs(true);
                return;
            }
            SetInputs(true);
            _ = Dispatcher.UIThread.InvokeAsync(Worker, DispatcherPriority.Background);

        }

        private void AppendLog(string line)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _logBuilder.AppendLine(line);
                Log.Text = _logBuilder.ToString();
            });
        }
        private void SetInputs(bool status)
        {
            // 这里如果使用每个控件的 IsEnabledChanged 让人感觉很蠢
            Btn.IsEnabled = status;
            ToplingUserId.IsEnabled = status;
            ToplingPassword.IsEnabled = status;
            AccessId.IsEnabled = status;
            AccessSecret.IsEnabled = status;
            MySqlRadio.IsEnabled = status;
            TodisRadio.IsEnabled = status;
            UseGtid.IsEnabled = status;
            EditServerId.IsEnabled = status;
            ServerId.IsEnabled = status;
        }

        private async Task Worker()
        {
            var userData = (DataContext as ToplingUserData)!;

            try
            {
                var handler = new AliYunResources(ToplingConstants, userData, AppendLog);
                // 上面构造的过程中会尝试登录topling服务器，判定用户名密码。

                ShowMessageBox("流程约三分钟，请不要关闭窗口!", caption: "正在执行");
                var instance = await handler.CreateInstance();

                Action action = userData.CreatingInstanceType switch
                {
                    ToplingUserData.InstanceType.Todis => () =>
                    {
                        var window = new TodisResult
                        {
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ToplingConstants = ToplingConstants,
                        };
                        window.Show();
                    }
                    ,
                    ToplingUserData.InstanceType.MyTopling => () =>
                    {
                        var window = new MyToplingResult()
                        {
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ToplingConstants = ToplingConstants,
                            DataContext = new InstanceDataBinding(ToplingConstants, instance)
                        };
                        window.Show();
                    }
                    ,
                    _ => throw new ArgumentOutOfRangeException()
                };
                Dispatcher.UIThread.Post(action);

            }
            catch (ClientException e)
            {
                // 后面测试这里是否能够捕获
                if (e.ErrorCode.Equals("InvalidStatus.RouteEntry"))
                {
                    ShowMessageBox($"云服务商路由表状态错误，请重试(不需要关闭自动化工具)。{Environment.NewLine}" +
                              "如果十分钟后此错误仍然出现，请联系客服");
                }
                else
                {
                    ShowMessageBox(e.Message);
                }

            }
            catch (Exception e)
            {
                if (e.Message.Contains("OperationFailed.CdtNotOpened"))
                {
                    _ = _ = Dispatcher.UIThread.InvokeAsync(() =>
                     {
                         var window = new CdtNotOpened()
                         {
                             WindowStartupLocation = WindowStartupLocation.CenterOwner,
                         };
                         window.Show();
                     });
                }
                else
                {
                    ShowMessageBox(e.Message);
                }

            }
            finally
            {
                _ = Dispatcher.UIThread.InvokeAsync(() => { SetInputs(true); });
            }
        }


        private void ShowMessageBox(string text, string caption = "执行失败")
        {
            Dispatcher.UIThread.Post(() =>
            {

                var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager
                    .GetMessageBoxCustomWindow(new MessageBoxCustomParams
                    {
                        ContentTitle = caption,
                        ContentMessage = text,
                        FontFamily = "Microsoft YaHei,苹方-简",
                        ButtonDefinitions = new[]
                            { new ButtonDefinition { Name = "确定", IsDefault = true }, },
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Width = 200
                    });
                messageBoxStandardWindow.ShowDialog(this);
            });
        }


    }
}