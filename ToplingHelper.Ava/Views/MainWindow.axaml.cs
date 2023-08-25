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
using Newtonsoft.Json;
using ToplingHelper.Ava.Models;
using ToplingHelperModels;
using static ToplingHelperModels.ToplingUserData;

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
#if !DEBUG
            Debugger.IsVisible = false;
#endif
        }


        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            _logBuilder.Clear();
            Log.Text = "";
            SetInputs(false);
            if (Context.Provider == Provider.Unknown)
            {
                ShowMessageBox("请选云服务商");
                SetInputs(true);
                return;
            }
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
            MyToplingRadio.IsEnabled = status;
            TodisRadio.IsEnabled = status;
            UseGtid.IsEnabled = status;
            EditServerId.IsEnabled = status;
            ServerId.IsEnabled = status;
        }

        private async Task Worker()
        {
            var userData = (DataContext as ToplingUserData)!;
            ToplingHelperService? handler = null;
            try
            {


                try
                {
                    handler = new ToplingHelperService(ToplingConstants, userData, AppendLog);
                }
                catch (Exception e)
                {
                    Log.Text = JsonConvert.SerializeObject(e);
                    return;
                }

                // 上面构造的过程中会尝试登录topling服务器，判定用户名密码。
                var content = "流程约三分钟，请不要关闭工具主窗口!";
                if (userData.Provider == Provider.Aws)
                {

                    content += " 您正在创建aws实例，aws目前以演示为主，compact 服务集群未长期激活。如果您有测试aws实例性能的需求，请联系客服启动 compact 集群";
                }
                ShowMessageBox(content, caption: "正在执行");
                //return;
                var instance = await handler.CreateInstanceAsync();
                //if (instance == null)
                //{
                //    ShowMessageBox("并网完成，实例创建可能失败，您可以前往 topling 控制台手动创建实例(选项更加丰富)，或者手动重试");
                //    return;
                //}

                Action action = userData.CreatingInstanceType switch
                {
                    InstanceType.Todis => () =>
                    {
                        var window = new TodisResult
                        {
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ToplingConstants = ToplingConstants,
                            DataContext = new InstanceDataBinding(ToplingConstants, instance, userData.Provider)
                        };
                        window.Show();
                        AppendLog("实例创建完成");
                    }
                    ,
                    InstanceType.MyTopling when userData.Provider == Provider.AliYun => () =>
                    {
                        var window = new MyToplingResult()
                        {
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ToplingConstants = ToplingConstants,
                            DataContext = new InstanceDataBinding(ToplingConstants, instance, userData.Provider)
                        };
                        window.Show();
                        AppendLog("实例创建完成");
                    }
                    ,
                    InstanceType.MyTopling when userData.Provider == Provider.Aws => () =>
                    {
                        var window = new MyToplingAwsResult()
                        {
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            ToplingConstants = ToplingConstants,
                            DataContext = new InstanceDataBinding(ToplingConstants, instance, userData.Provider)
                        };
                        window.Show();
                        AppendLog("实例创建完成");
                    }
                    ,
                    _ => throw new ArgumentOutOfRangeException()
                };
                Dispatcher.UIThread.Post(action);

            }
            // 下沉到运营商
            //catch (ClientException e)
            //{
            //    // 后面测试这里是否能够捕获
            //    if (e.ErrorCode.Equals("InvalidStatus.RouteEntry"))
            //    {
            //        ShowMessageBox($"云服务商路由表状态错误，请重试(不需要关闭自动化工具)。{Environment.NewLine}" +
            //                       "如果十分钟后此错误仍然出现，请联系客服");
            //    }
            //    else
            //    {
            //        ShowMessageBox(e.Message);
            //    }

            //}
            //catch (Exception e) when (e.Message.Equals("CheckExisting", StringComparison.OrdinalIgnoreCase))
            //{
            //    // 删除并重新创建实例
            //    ShowMessageBox($"如果您在控制台上删除过网段，请删除 {handler.ExistingVpcId} 后重新运行本工具");
            //}
            //catch (Exception e) when (e.Message.Contains("OperationFailed.CdtNotOpened"))
            //{
            //    _ = _ = Dispatcher.UIThread.InvokeAsync(() =>
            //    {
            //        var window = new CdtNotOpened()
            //        {
            //            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            //        };
            //        window.Show();
            //    });
            //}
            catch (Exception e)
            {
                var content = new StringBuilder();
                content.AppendLine("操作失败，请重试");
                content.AppendLine(e.Message);
                content.AppendLine(e.StackTrace);
                ShowMessageBox(content.ToString(), "请重试");

                _logBuilder.AppendLine(content.ToString());
            }
            finally
            {
                _ = Dispatcher.UIThread.InvokeAsync(() => { SetInputs(true); });
                handler?.Dispose();
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

        private void Flush(object? sender, RoutedEventArgs e)
        {
#if DEBUG
            Dispatcher.UIThread.Post(() =>
            {
                Log.Text = JsonConvert.SerializeObject(Context, Formatting.Indented);
            });
#endif

        }
    }
}