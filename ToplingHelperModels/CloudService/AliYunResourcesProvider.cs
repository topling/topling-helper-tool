using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Exceptions;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Ecs.Model.V20140526;
using Newtonsoft.Json.Linq;
using ToplingHelperModels.Models.CloudService;
using ToplingHelperModels.Models.WebApi;
using CreateRouteEntryRequest = Aliyun.Acs.Vpc.Model.V20160428.CreateRouteEntryRequest;
using CreateVpcRequest = Aliyun.Acs.Ecs.Model.V20140526.CreateVpcRequest;
using DescribeVpcsRequest = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsRequest;
using DescribeVpcsResponse = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsResponse;

namespace ToplingHelperModels.CloudService
{
    internal class AliYunResourcesProvider : CloudServiceResources
    {

        private readonly DefaultAcsClient _client;

        private string? _ownerId = null;





        public AliYunResourcesProvider(ToplingConstants constants, ToplingUserData userData, Action<string> logger)
        : base(constants, userData, logger)
        {

            UserData = userData;

            _client = new DefaultAcsClient(DefaultProfile.GetProfile(constants.ToplingTestRegion, userData.AccessId,
                userData.AccessSecret));
        }

        
        protected override string DefaultZoneId(string regionId)
        {
            throw new NotImplementedException();
        }

        public override UserVpc? GetVpcForTopling(string region)
        {

            for (var pageNumber = 1; ; ++pageNumber)
            {
                DescribeVpcsResponse vpcResponse;
                try
                {
                    vpcResponse = _client.GetAcsResponse(new DescribeVpcsRequest
                    {
                        RegionId = ToplingConstants.ToplingTestRegion,
                        PageNumber = pageNumber,
                        PageSize = 50
                    });
                }
                catch (ClientException e) when
                    (e.ErrorCode.Equals("SDK.InvalidProfile", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("请确保您提供的AccessKey Secret有效");
                }

                var vpcList = vpcResponse.Vpcs;
                if (vpcList.Any())
                {
                    _ownerId = _client.GetAcsResponse(new DescribeVpcsRequest
                    {
                        RegionId = ToplingConstants.ToplingTestRegion,
                        PageNumber = 1,
                        PageSize = 50
                    }).Vpcs.First().OwnerId!.Value.ToString();
                }
                else
                {
                    //break;
                    return null;
                }

                var vpc = vpcList
                    .Where(v => v.Tags.Any(t =>
                        t.Key.Equals(ToplingConstants.ToplingVpcTagKey, StringComparison.OrdinalIgnoreCase)))
                    .Select(i => new UserVpc
                    {
                        OwnerId = i.OwnerId!.ToString()!,
                        VpcId = i.VpcId,
                        SubNetCidr = i.Tags.First(tag => tag.Key.Equals(ToplingConstants.ToplingVpcTagKey, StringComparison.OrdinalIgnoreCase))._Value
                    }).FirstOrDefault();
                if (vpc != null)
                {
                    return vpc;
                }
            }

        }


        public override UserVpc? GetUserVpcForTopling(string region)
        {
            throw new NotImplementedException();
        }

        public override void AddVpcTag(string vpcId, string cidr)
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

        public override string GetUserCloudId()
        {
            throw new NotImplementedException();
        }

        internal override void CreateIdempotentVSwitch(string vpcId, int secondCidr)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 创建一个VPC用于并网(暂不创建交换机)
        /// </summary>
        /// <returns>vpc-id</returns>
        public override UserVpc CreateDefaultVpc(string cidr)
        {

            Log("创建阿里云VPC");
            var res = _client.GetAcsResponse(new CreateVpcRequest
            {
                RegionId = ToplingConstants.ToplingTestRegion,
                CidrBlock = cidr
            });
            Log($"创建VPC成功: {res.VpcId}");
            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            AddVpcTag(res.VpcId, cidr);

            _ownerId ??= _client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = ToplingConstants.ToplingTestRegion,
                PageNumber = 1,
                PageSize = 50
            }).Vpcs.First().OwnerId!.Value.ToString();

            CreateDefaultSecurityGroupIfNotExists(res.VpcId);
            return new UserVpc
            {
                OwnerId = _ownerId,
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
            request.AddQueryParameters("AcceptingRegionId", ToplingConstants.ToplingTestRegion);
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
                RegionId = ToplingConstants.ToplingTestRegion
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


        public override string AddRoute(string cidr, string vpcId, string pccId)
        {

            var routeTableId = _client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = ToplingConstants.ToplingTestRegion,
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
                    return routeTableId;
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
                    return routeTableId;
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
