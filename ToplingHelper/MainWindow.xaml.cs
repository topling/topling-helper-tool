using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.RightsManagement;
using System.Text;
using System.Text.RegularExpressions;
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
using Aliyun.Acs.Cbn.Model.V20170912;
using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Exceptions;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Ecs.Model.V20140526;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        private const string ToplingCenName = "for-toping";
        private const string ToplingVpcName = "for-toping-shenzhen";
        private const string ToplingTestRegion = "cn-shenzhen";

        private const string ShenzhenCidrFormat = "172.17.{0}.0/24";

        private const long ToplingAliYunUserId = 1343819498686551;

        private const string Todis = "Pika";

        private readonly CookieContainer _container = new CookieContainer();

        private readonly StringBuilder LogBuilder = new StringBuilder();

        private long _aliyunId;
        private string _accessId;
        private string _accessSecret;
        private string _toplingId;
        private string _toplingPassword;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {

            LogBuilder.Clear();
            Log.Text = "";

            SetInputs(false);
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

            _accessId = AccessId.Text;
            _accessSecret = AccessSecret.Text;
            _toplingId = ToplingId.Text;
            _toplingPassword = ToplingPassword.Password;
            if (AccessId.Text.Length > AccessSecret.Text.Length)
            {
                MessageBox.Show("阿里云AccessId应短于AccessSecret，请检查是否粘贴错误");
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


        private void Worker()
        {


            var client = new DefaultAcsClient(DefaultProfile.GetProfile(ToplingTestRegion, _accessId, _accessSecret));


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
                AddVpcIntoCen(client, vpcId, cenId, _aliyunId);
                AppendLog("自动创建的 VPC 加入云企业网完成...");

                // 授权
                AppendLog("开始授权云企业网...");
                AuthCen(toplingHttpClient, toplingVpc, cenId, _aliyunId);
                AppendLog("授权云企业网完成...");
                AppendLog("开始并网...");
                AddVpcIntoCen(client, toplingVpc, cenId, ToplingAliYunUserId);
                AppendLog("并网完成...");

                AppendLog("开始创建实例...");
                // 创建实例
                CreateTodisRequest(toplingHttpClient, toplingVpc);
                AppendLog("创建实例完成，正在初始化...");

                TodisInstance todis;
                do
                {
                    Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                    todis = WaitingForInstance(toplingHttpClient);

                } while (string.IsNullOrWhiteSpace(todis.TodisPrivateIp));

                Dispatcher.Invoke(() => ShowResult(vpcId, toplingVpc, todis.TodisPrivateIp, cenId, todis.TodisInstanceId));

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
                Dispatcher.Invoke(() =>
                {
                    SetInputs(true);
                });
            }

        }


        #region 阿里云

        private string GetOrCreateCen(DefaultAcsClient client)
        {

            var cenId = client.GetAcsResponse(new DescribeCensRequest()).Cens
                .FirstOrDefault(c => c.Name == ToplingCenName)?.CenId;



            return cenId ?? client.GetAcsResponse(new CreateCenRequest
            {
                Name = ToplingCenName,

            }).CenId;
        }
        private string GetOrCreateVpc(DefaultAcsClient client)
        {
            var vpcList = client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = ToplingTestRegion,
            }).Vpcs;


            var vpc = vpcList.FirstOrDefault(v => v.VpcName == ToplingVpcName);

            if (vpc != null)
            {
                Debug.Assert(vpc.OwnerId != null, "vpc.OwnerId != null");
                _aliyunId = vpc.OwnerId.Value;
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
                RegionId = ToplingTestRegion,
                VpcName = ToplingVpcName,
                CidrBlock = "172.16.0.0/12"
            });

            // VPC创建后不能立刻创建交换机，等待20秒;
            Task.Delay(TimeSpan.FromSeconds(20)).Wait();

            vpc = client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = ToplingTestRegion,
            }).Vpcs.FirstOrDefault(v => v.VpcName == ToplingVpcName);
            Debug.Assert(vpc != null);

            Debug.Assert(vpc.OwnerId != null, "vpc.OwnerId != null");
            _aliyunId = vpc.OwnerId.Value;
            CreateVSwitch(client, response.VpcId);
            CreateDefaultSecurityGroupIfNotExists(client, vpc.VpcId);
            return vpc.VpcId;

        }

        private void CreateVSwitch(DefaultAcsClient client, string vpcId)
        {
            for (var ch = 'a'; ch <= 'f'; ++ch)
            {
                var cidrBlock = string.Format(ShenzhenCidrFormat, ch - 'a');
                client.GetAcsResponse(new CreateVSwitchRequest
                {
                    RegionId = ToplingTestRegion,
                    VpcId = vpcId,
                    ZoneId = $"{ToplingTestRegion}-{ch}",
                    CidrBlock = cidrBlock
                });
            }

        }

        private void CreateDefaultSecurityGroupIfNotExists(DefaultAcsClient client, string vpcId)
        {
            var sgId = client.GetAcsResponse(new DescribeSecurityGroupsRequest
            {
                VpcId = vpcId,
                RegionId = ToplingTestRegion
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

        private void AddVpcIntoCen(DefaultAcsClient client, string vpcId, string cenId, long vpcOwnerId)
        {
            // 先查看，后加入
            var joined = client.GetAcsResponse(new DescribeCenAttachedChildInstancesRequest
            {
                CenId = cenId,
                ChildInstanceType = "VPC"
            }).ChildInstances.Any(i => i.ChildInstanceId == vpcId);
            if (joined)
            {
                return;
            }

            client.GetAcsResponse(new AttachCenChildInstanceRequest
            {
                CenId = cenId,
                ChildInstanceId = vpcId,
                ChildInstanceOwnerId = vpcOwnerId,
                ChildInstanceType = "VPC",
                ChildInstanceRegionId = ToplingTestRegion
            });
            // 授权后等待10s，否则可能会报错
            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
        }
        #endregion

        #region Topling

        private void LoginAndSetHeader(HttpClient httpClient)
        {
            var uri = "https://console.topling.cn";

            var response = httpClient
                .PostAsync(new Uri($"{uri}/api/auth?username={_toplingId}&password={_toplingPassword}"), null).Result
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
            var uri = "https://console.topling.cn/api/vpc";

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
                region = ToplingTestRegion

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
            var uri = $"https://console.topling.cn/api/vpc/aliyun/{vpcId}/join";
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
            var uri = $"https://console.topling.cn/api/instance";

            var response = client.GetAsync(uri).Result;
            var content = response.Content.ReadAsStringAsync().Result;
            dynamic instance = JObject.Parse(content);
            if (instance.data.Count == 0)
            {
                throw new Exception("实例创建可能出错，请重新执行本程序");
            }

            return new TodisInstance
            {
                TodisInstanceId = instance.data[0].id,
                TodisPrivateIp = instance.data[0].host
            };
        }

        private string CreateTodisRequest(HttpClient client, string vpcId)
        {
            var uri = $"https://console.topling.cn/api/instance";

            var response = client.GetAsync(uri).Result;
            var content = response.Content.ReadAsStringAsync().Result;
            dynamic instance = JObject.Parse(content);
            string instanceId;
            if (instance.data.Count > 0)
            {
                instanceId = (string)instance.data[0].id;
            }
            else
            {
                FlushXsrf(client);
                var bodyContent = JsonConvert.SerializeObject(new
                {
                    zoneId = $"{ToplingTestRegion}-a",
                    vpcId = vpcId,
                    insatnceType = Todis,
                    ecsType = "ecs.r6e.large",
                    port = 6379,
                    name = "auto-create"
                });
                var body = new StringContent(bodyContent, Encoding.UTF8, "application/json");
                var postResponse = client.PostAsync($"{uri}/aliyun", body).Result;
                instance = JObject.Parse(client.GetAsync(uri).Result.Content.ReadAsStringAsync()
                    .Result);
                instanceId = (string)instance.data[0].id;
            }

            return instanceId;
        }


        #endregion


        private void FlushXsrf(HttpClient client)
        {
            var xsrf = _container.GetCookies(new Uri("https://console.topling.cn")).Cast<Cookie>()
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
            LogBuilder.AppendLine(line);
            Dispatcher.Invoke(() =>
            {
                Log.Text = LogBuilder.ToString();
            });
        }

        private string ResultText(string vpcId, string ip, string cenId) =>
            string.Join($"{Environment.NewLine}{Environment.NewLine}",
                new string[]
                {
                    $"创建成功" ,
                    $"请登录阿里云，在 VPC: {vpcId} 中创建新 ECS 实例(操作系统请选择 CentOS)" ,
                    $"一定在 {vpcId} 下创建实例！(操作系统请选择 CentOS){Environment.NewLine}" +
                    $"一定在 {vpcId} 下创建实例！(操作系统请选择 CentOS){Environment.NewLine}" +
                    $"一定在 {vpcId} 下创建实例！(操作系统请选择 CentOS){Environment.NewLine}" ,
                    $"连接 {ip}:6379 (与 Redis 使用方式相同)" ,
                    $"如果无法连接，请先尝试 ping {ip} 查看网络是否联通(首次并网可能需要等待几分钟)" ,
                    $"同时可尝试重新点击主窗口上的提交(本程序执行结果幂等)" ,
                    $"我们提供了测试工具和数据集，详情可访问 https://topling.cn/downloads 查看"
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
    }
}
