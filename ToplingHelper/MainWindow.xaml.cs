using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Security.RightsManagement;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Aliyun.Acs.Cbn.Model.V20170912;
using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Exceptions;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Ecs.Model.V20140526;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ToplingHelperModels.Models;
using CreateVpcRequest = Aliyun.Acs.Vpc.Model.V20160428.CreateVpcRequest;
using CreateVSwitchRequest = Aliyun.Acs.Vpc.Model.V20160428.CreateVSwitchRequest;
using DescribeVpcsRequest = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsRequest;

namespace ToplingHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private const string Todis = "Pika";


        private readonly CookieContainer _container = new();

        private readonly StringBuilder _logBuilder = new();

        private ToplingUserData.InstanceType? InstanceType { get; set; }

        private ToplingConstants ToplingConstants { get; init; }
        private ToplingUserData UserData { get; set; }

        public MainWindow(string[] args)
        {

            InitializeComponent();

            if (args.Length > 0 && File.Exists(args[0]))
            {

                var content = File.ReadAllText(args[0]);
                try
                {
                    ToplingConstants = System.Text.Json.JsonSerializer.Deserialize<ToplingConstants>(content);
                }
                catch (Exception e)
                {
                    Dispatcher.Invoke(() => MessageBox.Show(e.ToString()));
                    ToplingConstants = new ToplingConstants();
                }
            }
            else
            {
                ToplingConstants = new ToplingConstants();
            }

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
                string.IsNullOrWhiteSpace(ToplingId.Text) ||
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

            var context = (MySqlDataContext)ThisGrid.DataContext;
            UserData = new ToplingUserData
            {
                AccessId = AccessId.Text,
                AccessSecret = AccessSecret.Text,
                ToplingId = ToplingId.Text,
                ToplingPassword = ToplingPassword.Password,
                GtidMode = UseGtid.IsChecked ?? false,
                ServerId = context.EditServerId ? uint.Parse(ServerId.Text) : 0,
                CreatingInstanceType = InstanceType.Value,
            };

            if (UserData.UserdataCheck(out var error))
            {
                MessageBox.Show(error);
                return;
            }

            MessageBox.Show("流程约两分钟，请不要关闭窗口!");



            Task.Run(Worker);

        }

        private void ShowResult(string userVpcId, string vpcToplingId, string ip, string cenId, string todisEcsId)
        {
            var window = new RichText(
                userVpcId,
                vpcToplingId,
                cenId,
                ip,
                todisEcsId)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            window.Show();
        }

        private void ShowFail(string vpcId, string cenId)
        {
            var window = new FailWindow(vpcId, cenId, $"{ToplingConstants.ToplingAliYunUserId}");
            window.Show();

        }
        private void Worker()
        {


            var client = new DefaultAcsClient(DefaultProfile.GetProfile(ToplingConstants.ToplingTestRegion,
                UserData.AccessId, UserData.AccessSecret));


            var handler = new HttpClientHandler()
            {
                UseCookies = true,

                CookieContainer = _container
            };
            var toplingHttpClient = new HttpClient(handler);
            toplingHttpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true
            };
            try
            {
                AppendLog("开始操作...");
                // 创建云企业网
                var cenId = GetOrCreateCen(client);
                AppendLog("创建云企业网完成...");
                // 创建VPC
                AppendLog("开始创建VPC...");
                var vpcId = GetOrCreateVpc(client);
                AppendLog($"创建 VPC 完成, VPCID:{vpcId}");


                LoginAndSetHeader(toplingHttpClient);
                AppendLog("开始创建预留网段");
                // topling-创建/获取VPC
                var toplingVpc = GetOrCreateToplingSideVpc(toplingHttpClient);
                AppendLog("创建预留网段完成...");

                AppendLog("加入用户 VPC 到云企业网...");
                // 把用户的vpc加入云企业网
                Task.Delay(TimeSpan.FromSeconds(10)).Wait(); // 等待10秒，VPC创建后不能立即并网
                AddVpcIntoCen(client, vpcId, cenId, UserData.AliYunId);
                AppendLog("自动创建的 VPC 加入云企业网完成...");

                // 授权
                AppendLog("开始授权云企业网...");
                AuthCen(toplingHttpClient, toplingVpc, cenId, UserData.AliYunId);
                AppendLog("授权云企业网完成...");
                AppendLog("开始并网...");
                if (!AddVpcIntoCen(client, toplingVpc, cenId, ToplingConstants.ToplingAliYunUserId))
                {
                    AppendLog("并网失败...");
                    // 并网失败
                    Dispatcher.Invoke(() => ShowFail(toplingVpc, cenId));
                    return;
                }
                AppendLog("并网完成...");

                AppendLog("开始创建实例...");
                // 创建实例


                #region MySQL

                if (UserData.CreatingInstanceType == ToplingUserData.InstanceType.MyTopling)
                {
                    var res = CreateMyToplingRequest(toplingHttpClient, toplingVpc);
                    if (!res.Success)
                    {
                        AppendLog("实例创建失败。");
                        if (res.Content.Contains("预留网段尚未并网前暂不允许创建实例"))
                        {
                            Dispatcher.Invoke(() => ShowFail(toplingVpc, cenId));
                        }
                        else
                        {
                            MessageBox.Show(
                                $"执行失败:{Environment.NewLine}{res.Content}",
                                "执行失败");
                        }

                        return;
                    }

                    AppendLog("创建实例完成，正在初始化...");
                    TodisInstance instance;
                    do
                    {
                        Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                        instance = WaitingForInstance(toplingHttpClient);

                    } while (string.IsNullOrWhiteSpace(instance.TodisPrivateIp));

                    MessageBox.Show(ResultText(vpcId, instance.TodisPrivateIp, cenId), "创建完成");
                }


                #endregion

                #region Todis

                if (UserData.CreatingInstanceType == ToplingUserData.InstanceType.Todis)
                {
                    var res = CreateTodisRequest(toplingHttpClient, toplingVpc);
                    if (!res.Success)
                    {
                        AppendLog("实例创建失败。");
                        if (res.Content.Contains("预留网段尚未并网前暂不允许创建实例"))
                        {
                            Dispatcher.Invoke(() => ShowFail(toplingVpc, cenId));
                        }
                        else
                        {
                            MessageBox.Show(
                                $"执行失败:{Environment.NewLine}{res.Content}",
                                "执行失败");
                        }

                        return;
                    }
                    AppendLog("创建实例完成，正在初始化...");

                    TodisInstance todis;
                    do
                    {
                        Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                        todis = WaitingForInstance(toplingHttpClient);

                    } while (string.IsNullOrWhiteSpace(todis.TodisPrivateIp));

                    Dispatcher.Invoke(() =>
                        ShowResult(vpcId, toplingVpc, todis.TodisPrivateIp, cenId, todis.TodisInstanceId));
                }

            }
            catch (ClientException exception)
            {
                var content = $"执行失败,阿里云操作失败:{Environment.NewLine}{exception.Message}{Environment.NewLine}" +
                              $"{exception.ErrorMessage}{Environment.NewLine}" +
                              "AccessKeyId和AccessKeySecret是否有效?";
                AppendLog(content);
                MessageBox.Show(content, "执行失败");
                return;
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"执行失败:{Environment.NewLine}{exception.Message}",
                    "执行失败");
                return;
            }
            finally
            {
                Dispatcher.Invoke(() => { SetInputs(true); });
            }

            #endregion
        }


        #region 阿里云

        private string GetOrCreateCen(DefaultAcsClient client)
        {

            var cenId = client.GetAcsResponse(new DescribeCensRequest()).Cens
                .FirstOrDefault(c => c.Name == ToplingConstants.ToplingCenName)?.CenId;



            return cenId ?? client.GetAcsResponse(new CreateCenRequest
            {
                Name = ToplingConstants.ToplingCenName,

            }).CenId;
        }
        private string GetOrCreateVpc(DefaultAcsClient client)
        {
            var vpcList = client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = ToplingConstants.ToplingTestRegion,
            }).Vpcs;


            var vpc = vpcList.FirstOrDefault(v => v.VpcName == ToplingConstants.ToplingVpcName);

            if (vpc != null)
            {
                Debug.Assert(vpc.OwnerId != null, "vpc.OwnerId != null");
                UserData.AliYunId = vpc.OwnerId.Value;
                // create switches if not exists
                if (!vpc.VSwitchIds.Any())
                {
                    CreateVSwitch(client, vpc.VpcId);
                }

                CreateDefaultSecurityGroupIfNotExists(client, vpc.VpcId);
                return vpc.VpcId;
            }

            // create vpc;
            var response = client.GetAcsResponse(new CreateVpcRequest
            {
                RegionId = ToplingConstants.ToplingTestRegion,
                VpcName = ToplingConstants.ToplingVpcName,
                CidrBlock = "172.16.0.0/12"
            });

            // VPC创建后不能立刻创建交换机，等待20秒;
            Task.Delay(TimeSpan.FromSeconds(20)).Wait();

            vpc = client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = ToplingConstants.ToplingTestRegion,
            }).Vpcs.FirstOrDefault(v => v.VpcName == ToplingConstants.ToplingVpcName);
            Debug.Assert(vpc != null);

            Debug.Assert(vpc.OwnerId != null, "vpc.OwnerId != null");
            UserData.AliYunId = vpc.OwnerId.Value;
            CreateVSwitch(client, response.VpcId);
            CreateDefaultSecurityGroupIfNotExists(client, vpc.VpcId);
            return vpc.VpcId;

        }

        private void CreateVSwitch(DefaultAcsClient client, string vpcId)
        {
            for (var ch = 'a'; ch <= 'f'; ++ch)
            {
                var cidrBlock = string.Format(ToplingConstants.ShenzhenCidrFormat, ch - 'a');
                client.GetAcsResponse(new CreateVSwitchRequest
                {
                    RegionId = ToplingConstants.ToplingTestRegion,
                    VpcId = vpcId,
                    ZoneId = $"{ToplingConstants.ToplingTestRegion}-{ch}",
                    CidrBlock = cidrBlock
                });
            }

        }

        private void CreateDefaultSecurityGroupIfNotExists(DefaultAcsClient client, string vpcId)
        {
            var sgId = client.GetAcsResponse(new DescribeSecurityGroupsRequest
            {
                VpcId = vpcId,
                RegionId = ToplingConstants.ToplingTestRegion
            }).SecurityGroups.FirstOrDefault()?.SecurityGroupId;

            if (string.IsNullOrWhiteSpace(sgId))
            {
                sgId = client.GetAcsResponse(new CreateSecurityGroupRequest
                {
                    VpcId = vpcId
                }).SecurityGroupId;
            }

            client.GetAcsResponse(new AuthorizeSecurityGroupRequest
            {
                SecurityGroupId = sgId,
                IpProtocol = "Tcp",
                PortRange = "22/22",
                SourceCidrIp = "0.0.0.0/0"
            });
            client.GetAcsResponse(new AuthorizeSecurityGroupRequest
            {
                SecurityGroupId = sgId,
                IpProtocol = "Icmp",
                PortRange = "-1/-1",
                SourceCidrIp = "0.0.0.0/0"
            });
        }

        private bool AddVpcIntoCen(DefaultAcsClient client, string vpcId, string cenId, long vpcOwnerId)
        {
            // 先查看，后加入
            var joined = client.GetAcsResponse(new DescribeCenAttachedChildInstancesRequest
            {
                CenId = cenId,
                ChildInstanceType = "VPC"
            }).ChildInstances.Any(i => i.ChildInstanceId == vpcId);
            if (joined)
            {
                return true;
            }
            client.GetAcsResponse(new AttachCenChildInstanceRequest
            {
                CenId = cenId,
                ChildInstanceId = vpcId,
                ChildInstanceOwnerId = vpcOwnerId,
                ChildInstanceType = "VPC",
                ChildInstanceRegionId = ToplingConstants.ToplingTestRegion
            });
            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            joined = client.GetAcsResponse(new DescribeCenAttachedChildInstancesRequest
            {
                CenId = cenId,
                ChildInstanceType = "VPC"
            }).ChildInstances.Any(i => i.ChildInstanceId == vpcId);
            // 加入后等待10s，否则可能会报错

            return joined;
        }
        #endregion

        #region Topling

        private void LoginAndSetHeader(HttpClient httpClient)
        {
            var uri = ToplingConstants.ToplingConsoleHost;

            var response = httpClient
                .PostAsync(new Uri($"{uri}/api/auth?username={UserData.ToplingId}&password={UserData.ToplingPassword}"),
                    null).Result
                .Content.ReadAsStringAsync().Result;
            if (response.Contains("用户名或密码错误"))
            {
                throw new Exception("拓扑岭用户名或密码错误");
            }

            if (response.Contains("邮件未验证"))
            {
                throw new Exception("拓扑岭账号注册后未激活邮箱");
            }

        }
        private string GetOrCreateToplingSideVpc(HttpClient client)
        {
            var uri = $"{ToplingConstants.ToplingConsoleHost}/api/vpc";

            dynamic vpc = JObject.Parse(client.GetAsync(uri).Result.Content.ReadAsStringAsync()
                .Result);

            if (vpc.data.Count > 0)
            {
                return (string)vpc.data[0].vpcId;
            }
            FlushXsrf(client);
            var body = JsonConvert.SerializeObject(new
            {
                provider = "AliYun",
                name = "",
                cidrSecond = -1,
                region = ToplingConstants.ToplingTestRegion

            });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            string vpcId = "";
            var count = 0;
            while (string.IsNullOrEmpty(vpcId) && count++ < 10)
            {
                var ret = client.PostAsync($"{uri}/aliyun", content).Result;
                // 等待5秒;
                Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                vpc = JObject.Parse(client.GetAsync(uri).Result.Content.ReadAsStringAsync().Result);
                vpcId = vpc.data.Count > 0 ? vpc.data[0].vpcId : null;
            }

            return vpcId;

        }

        private void AuthCen(HttpClient client, string vpcId, string cenId, long aliYunId)
        {
            var uri = $"{ToplingConstants.ToplingConsoleHost}/api/vpc/aliyun/{vpcId}/join";
            FlushXsrf(client);
            var body = JsonConvert.SerializeObject(new
            {
                aliYunUserId = aliYunId,
                aliYunCenId = cenId
            });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            client.PostAsync(uri, content).Wait();
            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
        }


        public class TodisInstance
        {
            public string TodisPrivateIp { get; set; }

            public string TodisInstanceId { get; set; }
        }


        public TodisInstance WaitingForInstance(HttpClient client)
        {
            var uri = $"{ToplingConstants.ToplingConsoleHost}/api/instance";

            var response = client.GetAsync(uri).Result;
            var content = response.Content.ReadAsStringAsync().Result;
            var body = JObject.Parse(content)["data"]!;

            var comparison = UserData.CreatingInstanceType == ToplingUserData.InstanceType.Todis
                ? Todis
                : UserData.CreatingInstanceType.ToString();

            dynamic instance = (JObject)body.FirstOrDefault(i =>
                string.Equals((string)i["InstanceType"], comparison, StringComparison.OrdinalIgnoreCase));

            if (instance == null)
            {
                throw new Exception("实例创建可能出错，请重新执行本程序");
            }

            return new TodisInstance
            {
                TodisInstanceId = instance.id,
                TodisPrivateIp = instance.host
            };
        }

        private class Response
        {
            public bool Success { get; set; }
            public string Content { get; set; }
        }

        private Response CreateMyToplingRequest(HttpClient client, string vpcId)
        {
            var uri = $"{ToplingConstants.ToplingConsoleHost}/api/instance";
            var response = client.GetAsync(uri).Result;
            var content = response.Content.ReadAsStringAsync().Result;
            var data = (JArray)((dynamic)JObject.Parse(content)).data;
            var comparison = UserData.CreatingInstanceType == ToplingUserData.InstanceType.Todis
                ? Todis
                : UserData.CreatingInstanceType.ToString();

            dynamic instance = (JObject)data.FirstOrDefault(i =>
                string.Equals((string)i["InstanceType"], comparison, StringComparison.OrdinalIgnoreCase));


            if (instance != null)
            {
                return new Response
                {
                    Success = true
                };
            }

            FlushXsrf(client);
            var bodyContent = JsonConvert.SerializeObject(new
            {
                zoneId = $"{ToplingConstants.ToplingTestRegion}-e",
                vpcId = vpcId,
                insatnceType = UserData.CreatingInstanceType.ToString(),
                ecsType = "ecs.r6e.large",
                port = 6379,
                name = "auto-create-mytopling",
                UserData.GtidMode,
                UserData.ServerId
            });
            var body = new StringContent(bodyContent, Encoding.UTF8, "application/json");
            var postResponse = client.PostAsync($"{uri}/aliyun/{UserData.CreatingInstanceType}", body).Result.Content
                .ReadAsStringAsync().Result;
            dynamic postResult = JObject.Parse(postResponse);

            try
            {
                return (int)postResult.code switch
                {
                    0 => new Response { Success = true },
                    _ => new Response { Success = false, Content = (string)postResult.msg }
                };
            }
            catch (Exception)
            {
                return new Response
                {
                    Success = false,
                    Content = postResponse
                };
            }
        }

        private Response CreateTodisRequest(HttpClient client, string vpcId)
        {
            var uri = $"{ToplingConstants.ToplingConsoleHost}/api/instance";

            var response = client.GetAsync(uri).Result;
            var content = response.Content.ReadAsStringAsync().Result;
            var data = (JArray)((dynamic)JObject.Parse(content)).data;
            dynamic instance = (JObject)data.FirstOrDefault(i => (string)(i as JObject)!["InstanceType"] == "Todis");
            if (instance != null)
            {
                return new Response
                {
                    Success = true
                };
            }

            FlushXsrf(client);
            var bodyContent = JsonConvert.SerializeObject(new
            {
                zoneId = $"{ToplingConstants.ToplingTestRegion}-e",
                vpcId = vpcId,
                insatnceType = Todis,
                ecsType = "ecs.r6e.large",
                port = 6379,
                name = "auto-create"
            });
            var body = new StringContent(bodyContent, Encoding.UTF8, "application/json");
            var postResponse = client.PostAsync($"{uri}/aliyun", body).Result.Content.ReadAsStringAsync().Result;
            dynamic postResult = JObject.Parse(postResponse);

            try
            {
                return (int)postResult.code switch
                {
                    0 => new Response { Success = true },
                    _ => new Response { Success = false, Content = (string)postResult.msg }
                };
            }
            catch (Exception)
            {
                return new Response
                {
                    Success = false,
                    Content = postResponse
                };
            }

        }


        #endregion


        private void FlushXsrf(HttpClient client)
        {
            var xsrf = _container.GetCookies(new Uri(ToplingConstants.ToplingConsoleHost)).Cast<Cookie>()
                .First(i => i.Name == "admin-XSRF-TOKEN").Value;
            client.DefaultRequestHeaders.Remove("xsrf-token");
            client.DefaultRequestHeaders.Add("xsrf-token", xsrf);
        }

        private void SetInputs(bool status)
        {
            Btn.IsEnabled = status;
            ToplingId.IsEnabled = status;
            ToplingPassword.IsEnabled = status;
            AccessId.IsEnabled = status;
            AccessSecret.IsEnabled = status;
        }



        private void AppendLog(string line)
        {
            _logBuilder.AppendLine(line);
            Dispatcher.Invoke(() => { Log.Text = _logBuilder.ToString(); });
        }

        private string ResultText(string vpcId, string ip, string cenId) =>
            string.Join($"{Environment.NewLine}{Environment.NewLine}",
                new string[]
                {
                    $"创建成功 MyTopling 实例初始化大约需要三分钟，",
                    $"可登录 {ToplingConstants.ToplingConsoleHost} 查看创建进度",
                    $"登录阿里云，在 VPC: {vpcId} 中创建新 ECS 实例连接 MyTopling 实例",
                    $"一定在 {vpcId} 下创建实例！{Environment.NewLine}" +
                    $"一定在 {vpcId} 下创建实例！{Environment.NewLine}" +
                    $"连接 {ip}:3306 (与 MySQL 使用方式相同)",
                    $"用户名: admin 密码: password",
                    $"首次登录后需修改密码使用",
                    $"如果无法连接，请先尝试 ping {ip} 查看网络是否联通(首次并网可能需要等待几分钟)",
                    $"同时可尝试重新点击主窗口上的提交(本程序执行结果幂等)"
                });


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
            ((MySqlDataContext)ThisGrid.DataContext).IsMySql = false;
        }

        private void Set_MyTopling(object sender, RoutedEventArgs e)
        {
            InstanceType = ToplingUserData.InstanceType.MyTopling;
            ((MySqlDataContext)ThisGrid.DataContext).IsMySql = true;

        }

        private static readonly Regex NumberRegex = new(@"^[1-9]\d*");

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {

            e.Handled = uint.TryParse(e.Text, out var number) && number > 0;

        }

    }
}
