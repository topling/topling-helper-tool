using Aliyun.Acs.Core.Exceptions;
using Microsoft.Maui.Controls.Shapes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ToplingHelperModels.Models;
using ToplingHelperModels.SubNetLogic;

namespace ToplingHelperMaui
{
    public partial class MainPage : ContentPage
    {
        private readonly StringBuilder _logBuilder = new();

        private ToplingUserData.InstanceType? InstanceType { get; set; }

        public ToplingConstants ToplingConstants { get; set; }
        public ToplingUserData ToplingUserData { get; set; }

        public MainPage()
        {
            InitializeComponent();
            var args = Environment.GetCommandLineArgs();
            var userData = new ToplingUserData();
            var constants = new ToplingConstants();
            if (args.Length > 1 && File.Exists(args[1]))
            {
                var content = File.ReadAllText(args[1], Encoding.UTF8);
                try
                {
                    var json = JsonNode.Parse(content);

                    userData = json?["ToplingUserData"]?.Deserialize<ToplingUserData>() ?? userData;
                    constants = json?["ToplingConstants"]?.Deserialize<ToplingConstants>() ?? constants;
                }
                catch (Exception e1)
                {
                    DisplayAlert("错误", e1.ToString(), "OK");
                    // 可疑的alert用法
                }

            }
            ToplingUserData = userData;
            ToplingConstants = constants;
            AccessSecret.Text = ToplingUserData.AccessSecret;
            AccessId.Text = ToplingUserData.AccessId;
            ToplingId.Text = ToplingUserData.ToplingUserId;
            ToplingPassword.Text = ToplingUserData.ToplingPassword;

        }

        private void Set_InstanceType(object sender, CheckedChangedEventArgs e)
        {
            RadioButton button = sender as RadioButton;
            if (button.IsChecked)
            {
                InstanceType = button.Value.ToString() switch { 
                    "MyTopling" => ToplingUserData.InstanceType.MyTopling,
                    "Todis" => ToplingUserData.InstanceType.Todis,
                    _ => null
                };
                ((MySqlDataContext)InputGrid.BindingContext).IsMySql = (
                    InstanceType == ToplingUserData.InstanceType.MyTopling
                );
            }
            
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            _logBuilder.Clear();
            Log.Text = "";
            SetInputs(false);

            if (InstanceType == null)
            {
                await DisplayAlert("错误", "请选择创建 Todis 服务或 MyTopling 服务", "OK");
                SetInputs(true);
                return;
            }
            if (
                string.IsNullOrWhiteSpace(AccessSecret.Text) ||
                string.IsNullOrWhiteSpace(AccessId.Text) ||
                string.IsNullOrWhiteSpace(ToplingId.Text) ||
                string.IsNullOrWhiteSpace(ToplingPassword.Text)
            )
            {
                await DisplayAlert("错误", "请检查是否全部输入", "OK");
                SetInputs(true);
                return;
            }

            if (InstanceType == null)
            {
                await DisplayAlert("错误", "请选服务类型", "OK");
                SetInputs(true);
                return;
            }

            if (
                ((MySqlDataContext)InputGrid.BindingContext).EditServerId &&
                (!uint.TryParse(ServerId.Text, out var serverId) || serverId == 0)
            ) {
                await DisplayAlert("错误", "自定义 server-id 输入不合法", "OK");
                SetInputs(true);
                return;
            }

            var context = (MySqlDataContext)InputGrid.BindingContext;
            ToplingUserData = new ToplingUserData
            {
                AccessId = AccessId.Text,
                AccessSecret = AccessSecret.Text,
                ToplingUserId = ToplingId.Text,
                ToplingPassword = ToplingPassword.Text,
                GtidMode = UseGtid.IsChecked,   // 可疑的 bool? 类型
                ServerId = context.EditServerId ? uint.Parse(ServerId.Text) : 0,
                CreatingInstanceType = InstanceType.Value,
            };

            if (!ToplingUserData.UserdataCheck(out var error))
            {
                await DisplayAlert("错误", error, "OK");
                SetInputs(true);
                return;
            }

            await Task.Run(Worker);
        }

        private async Task Worker()
        {
            try
            {
                var handler = new AliYunResources(ToplingConstants, ToplingUserData, AppendLog);
                // 上面构造的过程中会尝试登录topling服务器，判定用户名密码。
                AppendLog("提示: 流程约三分钟，请不要关闭窗口!");
                //Dispatcher.Dispatch(() => DisplayAlert("提示", "流程约三分钟，请不要关闭窗口!", "OK"));
                var instance = await handler.CreateInstance();
                Action action = ToplingUserData.CreatingInstanceType switch
                {
                    ToplingUserData.InstanceType.Todis => () =>
                    {
                        var window = new Window(new RichText(instance, ToplingConstants)) { };
                        Application.Current.OpenWindow(window);
                    }
                    ,
                    ToplingUserData.InstanceType.MyTopling => () =>
                    {
                        var window = new Window(new MyToplingPage(instance, ToplingConstants)) { };
                        Application.Current.OpenWindow(window);
                    }
                    ,
                    _ => throw new ArgumentOutOfRangeException()
                };
                Dispatcher.Dispatch(() => { SetInputs(true); });
                Dispatcher.Dispatch(action);
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
                    Dispatcher.Dispatch(() =>
                    {
                        var window = new Window(new CdtNotOpened())
                        {
                            //WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            //Owner = this
                        };
                        Application.Current.OpenWindow(window);
                    });
                }
                else
                {
                    ShowError(e.Message);
                }

            }
            finally
            {
                if(!Btn.IsEnabled)
                {
                    Dispatcher.Dispatch(() => { SetInputs(true); });
                }
            
            }
        }

        private void ShowError(string message, string caption = "执行失败")
        {
            Dispatcher.Dispatch(() => DisplayAlert(caption, $"执行失败:{Environment.NewLine}{message}", "OK"));
        }

        private void AppendLog(string line)
        {

            Dispatcher.Dispatch(() =>
            {
                _logBuilder.AppendLine(line);
                Log.Text = _logBuilder.ToString();
            });

        }

        private async void SetInputs(bool status)
        {
            if (status) { 
                await Task.Yield();
            }
            Btn.IsEnabled = status;
            ToplingId.IsEnabled = status;
            ToplingPassword.IsEnabled = status;
            AccessId.IsEnabled = status;
            AccessSecret.IsEnabled = status;
        }
    }
}