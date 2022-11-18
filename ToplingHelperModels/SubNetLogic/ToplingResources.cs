using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ToplingHelperModels.Models;
using Newtonsoft.Json.Linq;
using static ToplingHelperModels.Models.ToplingUserData;
using Newtonsoft.Json;

namespace ToplingHelperModels.SubNetLogic
{
    public sealed class ToplingResources : IDisposable
    {
        private readonly ToplingConstants _toplingConstants;
        private readonly ToplingUserData _userData;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly HttpClientHandler _httpClientHandler;
        public ToplingResources(ToplingConstants constants, ToplingUserData userData)
        {
            _toplingConstants = constants;
            _userData = userData;
            _cookieContainer = new CookieContainer();
            _httpClientHandler = new HttpClientHandler()
            {
                UseCookies = true,
                CookieContainer = _cookieContainer,
            };
            _httpClient = new HttpClient(_httpClientHandler);

            #region Login
            var uri = _toplingConstants.ToplingConsoleHost;

            var response = _httpClient
                .PostAsync(new Uri($"{uri}/api/auth?username={_userData.ToplingUserId}&password={_userData.ToplingPassword}"),
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
            #endregion
        }

        public UserSubNet? GetDefaultUserSubNet()
        {
            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/SubNet";
            return ((JArray)JObject.Parse(_httpClient.GetAsync(uri).Result.Content.ReadAsStringAsync().Result)["data"]!)
                .Select(i => new UserSubNet
                {
                    PeerId = i["peerId"].ToString(),
                    Cidr = i["cidr"].ToString(),
                    UserCloudId = i["userCloudId"].ToString()
                })
                .FirstOrDefault();
        }

        public AvailableVpc? GetAvailableVpc(int secondCidr, out string errorMessage)
        {
            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/SubNet/aliyun/{_toplingConstants.ToplingTestRegion}/available-from-cidr?cidr=10.{secondCidr}.0.0/16";


            var res = JObject.Parse(_httpClient.GetAsync(uri).Result.Content.ReadAsStringAsync().Result);
            if (res["code"].ToObject<int>() == 0)
            {
                errorMessage = string.Empty;
                return new AvailableVpc
                {
                    VpcId = res["availableVpcId"].ToString(),
                    ToplingId = res["toplingId"].ToString()
                };
            }

            errorMessage = res["msg"].ToString();
            return null;

        }

        public void GrantPeer(string peerId, int subNetCidr, string vpcId)
        {
            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/SubNet";
            FlushXsrf();
            var res = JObject.Parse(_httpClient.PostAsync(uri,
                JsonContent.Create(new
                {
                    peerId,
                    subNetCidrSecond = subNetCidr,
                    vpcId,
                    region = _toplingConstants.ToplingTestRegion,
                    name = "auto-create",
                    provider = "AliYun"
                })
                ).Result.Content.ReadAsStringAsync().Result);
            if (res["code"].ToObject<int>() != 0)
            {
                throw new Exception(res["msg"].ToString());
            }
        }

        public Instance CreateDefaultInstance(string subNetId)
        {

            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/SubNet";
            var res = ((JArray)JObject.Parse(_httpClient.GetStringAsync(uri).Result)["data"]!)
                    .FirstOrDefault();
            // TODO  检测实例类型
            if (res != null)
            {
                if (_userData.CreatingInstanceType.ToString().Equals(res["instanceType"].ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("现在已经存在和待创建实例类型不同的实例，请再控制台中删除实例后直接创建新实例");
                }
                return new Instance
                {
                    PrivateIp = res["host"].ToString(),
                    InstanceEcsId = res["id"].ToString()
                };
            }
            // 创建并等待

            uri = $"/api/SubNetInstance/aliyun/{_userData.CreatingInstanceType}";
            FlushXsrf();

            var bodyContent = JsonConvert.SerializeObject(new
            {
                subNetId,
                ecsType = _userData.CreatingInstanceType switch
                {
                    InstanceType.Todis => _toplingConstants.DefaultTodisEcsType,
                    InstanceType.MyTopling => _toplingConstants.DefaultMyToplingEcsType,
                    _ => throw new ArgumentOutOfRangeException()
                },
                name = "auto-created",
                _userData.GtidMode,
                _userData.ServerId,
                zoneId = $"{_toplingConstants.ToplingTestRegion}-e",

            });
            var body = new StringContent(bodyContent, Encoding.UTF8, "application/json");
            _httpClient.PostAsync(uri, body).Wait();
            Instance instance;
            do
            {
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                instance = WaitingForInstance();

            } while (instance.PrivateIp == null);

            return instance;
        }

        public Instance WaitingForInstance()
        {
            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/subnetinstance";

            var response = _httpClient.GetAsync(uri).Result;
            var content = response.Content.ReadAsStringAsync().Result;
            var body = JObject.Parse(content)["data"]!;

            var comparison = _userData.CreatingInstanceType == ToplingUserData.InstanceType.Todis
                ? "pika"
                : _userData.CreatingInstanceType.ToString();

            dynamic? instance = body.FirstOrDefault(i =>
                string.Equals((string)i["instanceType"]!, comparison, StringComparison.OrdinalIgnoreCase));



            if (instance == null)
            {
                throw new Exception("实例创建可能出错，请重新执行本程序");
            }

            return new Instance
            {
                InstanceEcsId = instance.id,
                PrivateIp = instance.host
            };

        }

        private void FlushXsrf()
        {
            var xsrf = _cookieContainer.GetCookies(new Uri(_toplingConstants.ToplingConsoleHost)).Cast<Cookie>()
                .First(i => i.Name == "admin-XSRF-TOKEN").Value;
            _httpClient.DefaultRequestHeaders.Remove("xsrf-token");
            _httpClient.DefaultRequestHeaders.Add("xsrf-token", xsrf);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _httpClientHandler.Dispose();
        }
    }

    public class UserSubNet
    {
        public string PeerId { get; init; } = default!;

        public string Cidr { get; init; } = default!;

        public string UserCloudId { get; init; } = default!;
    }

    public class AvailableVpc
    {
        public string VpcId { get; init; } = default!;

        public string ToplingId { get; set; } = default!;

    }
}
