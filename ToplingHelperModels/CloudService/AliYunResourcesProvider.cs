using System.Diagnostics;
using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Exceptions;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Ecs.Model.V20140526;
using Aliyun.Acs.Sts.Model.V20150401;
using Newtonsoft.Json.Linq;
using ToplingHelperModels.Models.CloudService;
using ToplingHelperModels.Models.WebApi;
using CreateRouteEntryRequest = Aliyun.Acs.Vpc.Model.V20160428.CreateRouteEntryRequest;
using CreateVpcRequest = Aliyun.Acs.Ecs.Model.V20140526.CreateVpcRequest;
using DescribeVpcsRequest = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsRequest;

namespace ToplingHelperModels.CloudService
{
    internal sealed class AliYunResourcesProvider : CloudServiceResources
    {

        private readonly DefaultAcsClient _client;

        private readonly string _regionId;
        public AliYunResourcesProvider(ToplingConstants constants, ToplingUserData userData, Action<string> logger)
        : base(constants, userData, logger)
        {

            UserData = userData;

            _client = new DefaultAcsClient(DefaultProfile.GetProfile(constants.ProviderToRegion[userData.Provider].RegionId, userData.AccessId,
                userData.AccessSecret));
            UserCloudId = _client.GetAcsResponse(new GetCallerIdentityRequest())
                .AccountId;
            _regionId = constants.ProviderToRegion[userData.Provider].RegionId;
        }

        public override string UserCloudId { get; protected init; }


        public override UserVpc? GetUserVpcForTopling(string region)
        {
            var vpcList = _client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = UserData.RegionId
            }).Vpcs;
            return vpcList
                .Where(v => v.Tags.Any(i => i.Key == ToplingConstants.ToplingVpcTagKey))
                .Select(v => new UserVpc
                {
                    OwnerId = UserCloudId,
                    VpcId = v.VpcId,
                    SubNetCidr = v.Tags.First(i => i.Key == ToplingConstants.ToplingVpcTagKey)._Value,
                    RouteId = v.VRouterId
                }).FirstOrDefault();
        }

        private void AddVpcTag(string vpcId, string cidr)
        {
            var request = new CommonRequest
            {
                Method = MethodType.POST,
                Domain = "vpc.aliyuncs.com",
                Version = "2016-04-28",
                Action = "TagResources"
            };
            request.AddQueryParameters("ResourceType", "VPC");
            request.AddQueryParameters("ResourceId.1", vpcId);
            request.AddQueryParameters("Tag.1.Key", ToplingConstants.ToplingVpcTagKey);
            request.AddQueryParameters("Tag.1.Value", cidr);
            _client.GetCommonResponse(request);
        }

        internal override void CreateIdempotentVSwitch(string vpcId, int secondCidr)
        {
            // 获取所有的可用区
            var zoneList = _client.GetAcsResponse(new DescribeZonesRequest
            {
                RegionId = UserData.RegionId
            });
            var switchCidrList = _client.GetAcsResponse(new DescribeVSwitchesRequest
            {
                VpcId = vpcId,
            }).VSwitches.Select(s => s.CidrBlock).ToList();
            foreach (var (index, zoneId) in zoneList.Zones.Select((zone, index) => (index, zone.ZoneId)))
            {
                var block = $"10.{secondCidr}.{index}.0/24";
                // 阿里云幂等性不做长时间保证,这里手动判定
                if (switchCidrList.Contains(block))
                {
                    continue;
                }
                for (int i = 0; i < 10; ++i)
                {
                    try
                    {
                        _client.GetAcsResponse(new CreateVSwitchRequest
                        {
                            VpcId = vpcId,
                            ZoneId = zoneId,
                            CidrBlock = block,
                            ClientToken = $"{vpcId}_{zoneId}_{block}"
                        });
                        break;
                    }
                    catch (ClientException e) when (e.ErrorCode.Equals("InvalidStatus.RouteEntry",
                                                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (i < 9)
                        {
                            Task.Delay(TimeSpan.FromSeconds(3 * (i + 1))).Wait();
                            continue;
                        }

                        throw;
                    }
                }

            }

        }

        public override void Dispose()
        {

        }

        /// <summary>
        /// 创建一个VPC用于并网(暂不创建交换机)
        /// </summary>
        /// <returns>vpc-id</returns>
        public override UserVpc CreateVpcForTopling(string cidr)
        {

            Log("创建阿里云VPC");
            var res = _client.GetAcsResponse(new CreateVpcRequest
            {
                RegionId = _regionId,
                CidrBlock = cidr
            });
            Log($"创建VPC成功: {res.VpcId}");
            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            AddVpcTag(res.VpcId, cidr);

            CreateDefaultSecurityGroupIfNotExists(res.VpcId);
            return new UserVpc
            {
                OwnerId = UserCloudId,
                VpcId = res.VpcId
            };
        }

        public override string CreatePeer(UserVpc userVpc, ToplingVpcForSubnetModel toplingAvailableVpc)
        {
            Log("对VPC创建对等连接并设置路由");
            // 创建到topling的对等连接并且添加路由表
            var clientToken = $"pcc_{toplingAvailableVpc.VpcId}_{userVpc}_{userVpc.SubNetCidr}";

            var request = new CommonRequest
            {
                Method = MethodType.POST,
                Domain = "vpcpeer.aliyuncs.com",
                Version = "2022-01-01",
                Action = "CreateVpcPeerConnection"
            };
            request.AddQueryParameters("ClientToken", clientToken);
            request.AddQueryParameters("VpcId", userVpc.VpcId);
            request.AddQueryParameters("AcceptingAliUid", toplingAvailableVpc.ToplingId);
            request.AddQueryParameters("AcceptingRegionId", _regionId);
            request.AddQueryParameters("AcceptingVpcId", toplingAvailableVpc.VpcId);
            request.AddQueryParameters("Name", "for-topling");
            var response = _client.GetCommonResponse(request);
            if (response.HttpStatus == 400)
            {
                throw new Exception(response.Data);
            }

            var pccId = JObject.Parse(response.Data)["InstanceId"]!.ToString();
            return pccId;
        }


        #region helpers

        private void CreateDefaultSecurityGroupIfNotExists(string vpcId)
        {
            var sgId = _client.GetAcsResponse(new DescribeSecurityGroupsRequest
            {
                VpcId = vpcId,
                RegionId = _regionId
            }).SecurityGroups.FirstOrDefault()?.SecurityGroupId;

            if (string.IsNullOrWhiteSpace(sgId))
            {
                Log("创建VPC安全组");
                sgId = _client.GetAcsResponse(new CreateSecurityGroupRequest
                {
                    VpcId = vpcId
                }).SecurityGroupId;
            }
            else
            {
                Log("VPC安全组已存在");
            }

            _client.GetAcsResponse(new AuthorizeSecurityGroupRequest
            {
                SecurityGroupId = sgId,
                IpProtocol = "Tcp",
                PortRange = "22/22",
                SourceCidrIp = "0.0.0.0/0"
            });
            _client.GetAcsResponse(new AuthorizeSecurityGroupRequest
            {
                SecurityGroupId = sgId,
                IpProtocol = "Icmp",
                PortRange = "-1/-1",
                SourceCidrIp = "0.0.0.0/0"
            });
        }

        #endregion


        public override void AddRoute(string cidr, string vpcId, string pccId)
        {

            var routeTableId = _client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = _regionId,
                VpcId = vpcId,
            }).Vpcs.First().RouterTableIds.First();
            // add route
            var routeToken = $"route_{cidr}_{routeTableId}_{pccId}";

            for (int i = 0; i < 10; ++i)
            {
                try
                {
                    _client.GetAcsResponse(new CreateRouteEntryRequest
                    {
                        RouteTableId = routeTableId,
                        DestinationCidrBlock = ToplingConstants.ToplingCidr,
                        NextHopId = pccId,
                        NextHopType = "VpcPeer",
                        ClientToken = routeToken
                    });
                    return;
                }
                catch (ClientException e) when (e.ErrorCode.Equals("InvalidStatus.RouteEntry",
                                                    StringComparison.OrdinalIgnoreCase))
                {
                    if (i < 9)
                    {
                        Task.Delay(TimeSpan.FromSeconds(3) * (i + 1)).Wait();
                        continue;
                    }

                    throw;
                }
                catch (ClientException e) when (e.ErrorMessage.Equals("Specified CIDR block is already exists.",
                                                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            throw new Exception(("添加路由失败，请重新操作"));

        }


        public override string? GetCurrentPeering(string vpcId)
        {
            var request = new CommonRequest
            {
                Method = MethodType.POST,
                Domain = "vpcpeer.aliyuncs.com",
                Version = "2022-01-01",
                Action = "ListVpcPeerConnections",
            };
            request.AddQueryParameters("MaxResults", "100");
            // request.Protocol = ProtocolType.HTTP;
            request.AddQueryParameters("VpcId.1", vpcId);
            var response = _client.GetCommonResponse(request);
            var peerList = ((JObject.Parse(response.Data)?["Data"]?["VpcPeerConnects"] as JArray) ?? new JArray())
                .Where(peer =>
                {
                    var status = peer["Status"]!.ToString();
                    return status.Equals("Creating") || status.Equals("Accepting");
                }).ToList();
            // 如果有创建后超时的对等连接或者用户自己创建而未接收的实例会出问题,
            // TODO 可以针对这种情况提示用户自己检测
            return peerList.FirstOrDefault()?["InstanceId"]?.ToString();
        }
    }
}
