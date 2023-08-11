using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Reflection;
using Aliyun.Acs.Ecs.Model.V20140526;
using ToplingHelperModels.CloudService;
using ToplingHelperModels.Models.WebApi;

namespace ToplingHelperModels.ToplingService
{
    public sealed class ToplingResources : IDisposable
    {
        private readonly ToplingConstants _toplingConstants;
        private readonly ToplingUserData _userData;
        private readonly Action<string>? _appendLog;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly HttpClientHandler _httpClientHandler;

        private readonly Provider _provider;

        private readonly string _regionId;

        public ToplingResources(ToplingConstants toplingConstants, ToplingUserData userData, Action<string> logger)
        {
            _toplingConstants = toplingConstants;
            _userData = userData;
            _appendLog = logger;
            try
            {
                _cookieContainer = new CookieContainer();
                _httpClientHandler = new HttpClientHandler()
                {
                    UseCookies = true,
                    CookieContainer = _cookieContainer,
                };
                _httpClient = new HttpClient(_httpClientHandler);
            }
            catch (Exception)
            {
                _httpClientHandler?.Dispose();
                _httpClient?.Dispose();
                throw;
            }
            _appendLog = logger;
            _provider = userData.Provider;
            _regionId = toplingConstants.ProviderToRegion[userData.Provider].RegionId;
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
        #region instance

        private string GetDefaultZoneId()
        {
            return _toplingConstants.ProviderToRegion[_provider].ZoneId;
        }


        internal void CreateInstance(string subnetId)
        {
            var model = new CreateInstanceModel
            {
                Provider = _userData.Provider.ToString(),
                ZoneId = GetDefaultZoneId(),
                SubNetId = subnetId,
                Regionid = _regionId,
                Name = "auto-created",
                // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
                InstanceType = _userData.CreatingInstanceType switch
                {
                    InstanceType.Todis => CreateInstanceRequestType.Todis,
                    InstanceType.MyTopling when _userData.UseLocalStorage => CreateInstanceRequestType
                        .MyToplingLocalStorage,
                    InstanceType.MyTopling when !_userData.UseLocalStorage => CreateInstanceRequestType
                        .MyTopling,
                    _ => throw new ArgumentOutOfRangeException()
                }
            };

            // check exists.
            var instance = GetFirstLivingInstance();
            if (instance != null)
            {
                if (!string.IsNullOrWhiteSpace(instance.InstanceType) &&
                    Enum.TryParse<CreateInstanceRequestType>(instance.InstanceType, out var type) &&
                    type != model.InstanceType)
                {
                    throw new Exception("现在已经存在和待创建实例类型不同的实例，如果需要自动创建新实例，请于控制台中删除现有实例后直接创建新实例");
                }

                return;
            }

            // create if not exists
            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/v2/instance";
            FlushXsrf();
            var body = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, MediaTypeNames.Application.Json);
            var response = _httpClient.PostAsync(uri, body).Result;

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return;
            }
            CheckError(response);
        }

        internal Instance? GetFirstLivingInstance()
        {
            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/v2/instance/{_provider}/{_regionId}";
            var response = _httpClient.GetAsync(uri).Result;
            CheckError(response);
            var res = response.Content.ReadFromJsonAsync<List<Instance>>().Result;
            return res?.FirstOrDefault();
        }

        #endregion



        #region subnet

        internal UserSubNet? GetDefaultUserSubNet()
        {
            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/v2/Subnet/{_provider}/{_regionId}";

            var response = _httpClient.GetAsync(uri).Result;
            try
            {
                var res = response.Content.ReadFromJsonAsync<List<UserSubNet>>().Result;
                return res?.FirstOrDefault();
            }
            catch (Exception)
            {
                var res = response.Content.ReadAsStringAsync().Result;
                Log(res);
                throw;
            }

        }

        internal ToplingVpcForSubnetModel GetToplingVpc()
        {
            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/v2/Subnet/{_provider}/{_regionId}/availableVpc";
            var response = _httpClient.GetAsync(uri).Result;
            //var res = response.Content.ReadFromJsonAsync<List<UserSubNet>>().Result;
            CheckError(response);
            return response.Content.ReadFromJsonAsync<ToplingVpcForSubnetModel>().Result!;
        }

        /// <summary>
        /// 并网
        /// </summary>
        internal void GrantPeer(UserSubNet subnet)
        {
            var model = new AttachSubnetModel
            {
                PeerId = subnet.PeerId,
                Provider = _provider.ToString(),
                Region = _regionId,
                Cidr = subnet.Cidr
            };

            var uri = $"{_toplingConstants.ToplingConsoleHost}/api/v2/Subnet";
            FlushXsrf();
            var response = _httpClient.PostAsync(uri, JsonContent.Create(model)).Result;
            CheckError(response);
        }

        #endregion

        private void FlushXsrf()
        {
            var xsrf = _cookieContainer.GetCookies(new Uri(_toplingConstants.ToplingConsoleHost)).Cast<Cookie>()
                .FirstOrDefault(i => i.Name.Equals("XSRF-TOKEN", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
            _httpClient.DefaultRequestHeaders.Remove("xsrf-token");
            _httpClient.DefaultRequestHeaders.Add("xsrf-token", xsrf);
        }
        public void Dispose()
        {
            _httpClient.Dispose();
            _httpClientHandler.Dispose();
        }

        private void CheckError(HttpResponseMessage message)
        {
            if (message.IsSuccessStatusCode)
            {
                return;
            }
            var msg = message.Content.ReadAsStringAsync().Result;
            Log(msg);
            throw new Exception(msg);
        }

        private void Log(string msg)
        {
            _appendLog?.Invoke(msg);
        }
    }
}
