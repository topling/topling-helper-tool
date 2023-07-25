using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Exceptions;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Ecs.Model.V20140526;
using Newtonsoft.Json.Linq;
using ToplingHelperModels.Models;
using CreateVpcRequest = Aliyun.Acs.Ecs.Model.V20140526.CreateVpcRequest;
using CreateVSwitchRequest = Aliyun.Acs.Ecs.Model.V20140526.CreateVSwitchRequest;
using DescribeVpcsRequest = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsRequest;
using DescribeVpcsResponse = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsResponse;

namespace ToplingHelperModels.SubNetLogic;
#nullable disable
public sealed class AliYunResources : IResourceOperations
{
    private readonly DefaultAcsClient _client;

    public AliYunResources(ToplingConstants constants, ToplingUserData userData)
    {
        _client = new DefaultAcsClient(DefaultProfile.GetProfile(constants.ToplingTestRegion, userData.AccessId, userData.AccessSecret));
    }

    public IList<DescribeVpcsResponse.DescribeVpcs_Vpc> GetVpcs(DescribeVpcsRequest request)
    {
        DescribeVpcsResponse vpcResponse;
        try
        {
            vpcResponse = _client.GetAcsResponse(request);
        }
        catch (ClientException e) when
            (e.ErrorCode.Equals("SDK.InvalidProfile", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("请确保您提供的AccessKey Secret有效");
        }

        return vpcResponse.Vpcs;
    }
    
    public IList<DescribeZonesResponse.DescribeZones_Zone> GetZones(DescribeZonesRequest request)
    {
        // 获取所有的可用区
        return _client.GetAcsResponse(request).Zones;
    }

    public IList<DescribeVSwitchesResponse.DescribeVSwitches_VSwitch> GetVSwitches(DescribeVSwitchesRequest request)
    {
        return _client.GetAcsResponse(request).VSwitches;
    }
    
    public string CreateVSwitch(CreateVSwitchRequest request)
    {
        for (int i = 0; i < 10; ++i)
        {
            try
            {
                return _client.GetAcsResponse(request).VSwitchId;
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

        return string.Empty;
    }

    public void AddVpcTag(string vpcId, string key, string cidr)
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
        request.AddQueryParameters("Tag.1.Key", key);
        request.AddQueryParameters("Tag.1.Value", cidr);
        _client.GetCommonResponse(request);
    }

    public string CreatePeerConnection(string vpcId, string acceptingRegion, AvailableVpc toplingAvailable, string cidr)
    {
        // 创建到topling的对等连接并且添加路由表
        var clientToken = $"pcc_{toplingAvailable.VpcId}_{vpcId}_{cidr}";

        var request = new CommonRequest
        {
            Method = MethodType.POST,
            Domain = "vpcpeer.aliyuncs.com",
            Version = "2022-01-01",
            Action = "CreateVpcPeerConnection"
        };
        request.AddQueryParameters("ClientToken", clientToken);
        request.AddQueryParameters("VpcId", vpcId);
        request.AddQueryParameters("AcceptingAliUid", toplingAvailable.ToplingId);
        request.AddQueryParameters("AcceptingRegionId", acceptingRegion);
        request.AddQueryParameters("AcceptingVpcId", toplingAvailable.VpcId);
        request.AddQueryParameters("Name", "for-topling");
        var response = _client.GetCommonResponse(request);
        if (response.HttpStatus == 400)
        {
            throw new Exception(response.Data);
        }

        var pccId = JObject.Parse(response.Data)["InstanceId"].ToString();
        return pccId;
    }

    public string CreateRouteEntry(string cidr, string regionId, string vpcId, string destinationCidr, string pccId)
    {

        var routeTableId = GetVpcs(new DescribeVpcsRequest
        {
            RegionId = regionId,
            VpcId = vpcId,
        }).First().RouterTableIds.First();
        // add route
        var routeToken = $"route_{cidr}_{routeTableId}_{pccId}";

        for (int i = 0; i < 10; ++i)
        {
            try
            {
                _client.GetAcsResponse(new CreateRouteEntryRequest
                {
                    RouteTableId = routeTableId,
                    DestinationCidrBlock = destinationCidr,
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

    /// <summary>
    /// 创建一个VPC用于并网(暂不创建交换机)
    /// </summary>
    /// <returns>vpc-id</returns>
    public string CreateDefaultVpc(string cidr, string key, string regionId)
    {
        var res = _client.GetAcsResponse(new CreateVpcRequest
        {
            RegionId = regionId,
            CidrBlock = "10.0.0.0/8"
        });
        Task.Delay(TimeSpan.FromSeconds(5)).Wait();
        AddVpcTag(res.VpcId, key, cidr);

        return res.VpcId;
    }

    public string CreateDefaultSecurityGroupIfNoneExists(string vpcId, string region)
    {
        var sgId = _client.GetAcsResponse(new DescribeSecurityGroupsRequest
        {
            VpcId = vpcId,
            RegionId = region
        }).SecurityGroups.FirstOrDefault()?.SecurityGroupId;

        if (string.IsNullOrWhiteSpace(sgId))
        {
            sgId = _client.GetAcsResponse(new CreateSecurityGroupRequest
            {
                VpcId = vpcId
            }).SecurityGroupId;
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
        return sgId;
    }


#pragma warning disable CS8632
    public string? GetCurrentPeering(string vpcId)
#pragma warning restore CS8632
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
                var status = peer["Status"].ToString();
                return status.Equals("Creating") || status.Equals("Accepting");
            }).ToList();
        // 如果有创建后超时的对等连接或者用户自己创建而未接收的实例会出问题,
        // TODO 可以针对这种情况提示用户自己检测
        return peerList.FirstOrDefault()?["InstanceId"].ToString();
    }
}