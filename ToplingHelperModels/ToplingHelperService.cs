using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ToplingHelperModels.CloudService;
using ToplingHelperModels.Models;
using ToplingHelperModels.Models.CloudService;
using ToplingHelperModels.Models.WebApi;
using ToplingHelperModels.ToplingService;

namespace ToplingHelperModels
{
    public sealed class ToplingHelperService : IDisposable
    {
        private readonly ToplingConstants _toplingConstants;
        

        private readonly CloudServiceResources _resources;
        //private readonly DefaultAcsClient _client;
        private readonly ToplingResources _toplingResourcesHandler;

        private readonly Action<string> _appendLog;


        public ToplingHelperService(ToplingConstants constants, ToplingUserData userData, Action<string> logger)
        {
            _toplingConstants = constants;
            _resources = CloudServiceResources.GetResourcesProvider(constants, userData, logger);
            _toplingResourcesHandler = new ToplingResources(constants, userData, logger);
            _appendLog = logger;
            
        }


        public async Task<Instance> CreateInstanceAsync()
        {

            await Task.Yield();
            // 先处理记录了VPC的情况
            var subNet = _toplingResourcesHandler.GetDefaultUserSubNet();
            var userVpcForTopling = _resources.GetUserVpcForTopling(_toplingConstants.ToplingTestRegion);
            // 默认初始状态
            // 默认初始状态
            if (subNet == null && userVpcForTopling == null)
            {
                _appendLog("尝试获取可用网段创建VPC与子网.");
                var vpcFromTopling = _toplingResourcesHandler.GetToplingVpc();
                var userVpc = _resources.CreateDefaultVpc(vpcFromTopling.Cidr);
                InitUserVpcAndPeer(userVpc);

                return await CreateDbInstanceAsync(userVpc);
            }

            // 本地创建了但是却没有并网
            // subnet always not null
            if (subNet == null /* && userVpcForTopling!=null */)
            {
                _appendLog("发现现有VPC，测试是否已并网");
                Debug.Assert(userVpcForTopling != null);


                // 查看现在是否存在请求中的对等连接，尝试并网，(注意确认对等连接一端是否是这个VPC)
                var peerId = _resources.GetCurrentPeering(userVpcForTopling.VpcId);
                // 没有对等连接，准备并网并初始化交换机
                if (peerId == null)
                {
                    InitUserVpcAndPeer(userVpcForTopling);
                }
                return await CreateDbInstanceAsync(userVpcForTopling);

            }

            // 已经并网过查看是否正确工作
            if (/*subNet != null &&*/ userVpcForTopling != null)
            {
                Debug.Assert(subNet != null);
                // 检测对等连接是否是这个账号上的，如果不是，提示核对用户的accessKey
                if (!subNet.UserCloudId.Equals(userVpcForTopling.OwnerId))
                {
                    //提示核对用户的accessKey,两边账号对不上
                    throw new Exception($"已注册注册子网账号{subNet.UserCloudId}和提交AccessKey账号{userVpcForTopling.OwnerId}不同，请检查");
                }

                _appendLog("检测到已并网，检测是否正常工作");
                InitUserVpcAndPeer(userVpcForTopling, subNet);
                _appendLog("开始创建实例");
                return await CreateDbInstanceAsync(userVpcForTopling);
            }
            // 病态
            if (/*subNet != null &&*/userVpcForTopling == null)
            {
                Debug.Assert(subNet != null);
                var userId = _resources.GetUserCloudId();
                //var userId = subNet.
                if (userId != null && userId == subNet.UserCloudId)
                {
                    
                    // 提示自己到控制台下手工删除这个子网，然后使用自动化工具重新并网
                    throw new Exception($"已注册子网的用户端VPC被删除，请在topling控制台中手动删除子网后重新运行本程序");
                }
                // 这是账号输错了的情况
                if (userId != null && userId != subNet.UserCloudId)
                {
                    // 提示用户检查自己当前的accessID所属账号是否是{subNet.UserCloudId}的，
                    throw new Exception($"请检查当前accessID所属账号是否属于{subNet.UserCloudId}");
                }


                // 当前用户在阿里云上不存在任何vpc但是有subnet的记录
                // userId == null
                throw new Exception($"请检查当前accessID所属账号是否属于{subNet.UserCloudId}，若属于，" +
                                    "请在topling控制台中手动删除子网后重新运行本程序");
            }
            throw new IndexOutOfRangeException("执行错误，请联系客服");
        }

        private void InitUserVpcAndPeer(UserVpc userVpcForTopling, UserSubNet? userSubnet = null)
        {
            _appendLog("开始创建对等连接");
            var vpcFromTopling = _toplingResourcesHandler.GetToplingVpc();
            string peerId;
            if (userSubnet == null)
            {
                peerId = _resources.CreatePeer(userVpcForTopling, vpcFromTopling);
                _appendLog($"已创建对等连接:{peerId}，准备并网");
                _toplingResourcesHandler.GrantPeer(new UserSubNet
                {
                    PeerId = peerId,
                    Cidr = userVpcForTopling.SubNetCidr,
                    UserCloudId = userVpcForTopling.OwnerId
                });
                _appendLog($"并网请求已提交");
            }
            else
            {
                peerId = userSubnet.PeerId;
            }

            _appendLog("添加路由表项");
            _resources.AddRoute(userVpcForTopling.SubNetCidr, userVpcForTopling.VpcId, peerId);
            // 创建交换机(幂等)
            _appendLog("创建交换机");

            #region get second of 10.second.0.0/16

            var regex = new Regex(@"10\.(\d+)\.0\.0/16");
            var matches = regex.Match(userVpcForTopling.SubNetCidr);
            Debug.Assert(matches.Success);
            var second = int.Parse(matches.Groups[1].Value);

            #endregion

            _resources.CreateIdempotentVSwitch(vpcFromTopling.VpcId, second);
        }


        private async Task<Instance> CreateDbInstanceAsync(UserVpc userVpc)
        {
            _appendLog("开始创建实例");
            try
            {
                _toplingResourcesHandler.CreateInstance();
            }
            catch (Exception e)
            {
                _appendLog("创建实例可能失败，并网成功,可前往控制台创建实例");
                _appendLog(JsonConvert.SerializeObject(e));
            }

            // 等待实例初始化完成
            Instance? instance;
            do
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                instance = _toplingResourcesHandler.GetFirstLivingInstance();
            } while (instance == null || !string.IsNullOrWhiteSpace(instance.PrivateIp));

            instance.RouteId = userVpc.RouteId;
            //_toplingResources.CreateDefaultInstance(peerId, vpcId);
            //instance.RouteId = userVpc.RouteId;

            return instance;
        }


        public void Dispose()
        {
            _toplingResourcesHandler.Dispose();
            _resources.Dispose();
        }
    }
}
