using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Ecs.Model.V20140526;
using Newtonsoft.Json.Linq;
using ToplingHelperModels.Models;
using CreateVpcRequest = Aliyun.Acs.Ecs.Model.V20140526.CreateVpcRequest;
using CreateVSwitchRequest = Aliyun.Acs.Ecs.Model.V20140526.CreateVSwitchRequest;
using DescribeVpcsRequest = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsRequest;
using DescribeVpcsResponse = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsResponse;

namespace ToplingHelperModels.SubNetLogic;

public sealed class AliYunResources : IDisposable
{
    private readonly ToplingConstants _toplingConstants;
    private readonly DefaultAcsClient _client;
    private readonly ToplingUserData _userData;
    private readonly ToplingResources _toplingResources;

    private readonly Action<string> _appendLog;

    public AliYunResources(ToplingConstants constants, ToplingUserData userData, Action<string> logger)
    {
        _toplingConstants = constants;
        _client = new DefaultAcsClient();
        _userData = userData;
        _appendLog = logger;
        _toplingResources = new ToplingResources(constants, userData, logger);
    }

    public Instance CreateInstance()
    {
        // 先处理记录了VPC的情况
        var subNet = _toplingResources.GetDefaultUserSubNet();
        AvailableVpc? availableVpc = null;
        var pageNumber = 1;
        DescribeVpcsResponse.DescribeVpcs_Vpc? vpc;
        string? userId = null;
        for (; ; ++pageNumber)
        {
            var vpcResponse = _client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = _toplingConstants.ToplingTestRegion,
                PageNumber = pageNumber,
                PageSize = 50
            });

            var vpcList = vpcResponse.Vpcs;
            if (userId == null && vpcList.Any())
            {
                userId = vpcList.First(v => v.OwnerId != null).OwnerId.ToString();
            }
            vpc = vpcList
                .FirstOrDefault(v =>
                    v.Tags.Any(t => t.Key.Equals(_toplingConstants.ToplingVpcTagKey, StringComparison.OrdinalIgnoreCase)));
            if (!vpcList.Any() || vpc != null)
            {
                break;
            }
        }

        var errorMessage = string.Empty;
        // 默认初始状态
        if (subNet == null && vpc == null)
        {
            _appendLog("尝试获取可用网段创建子网.");
            // 获取一段合法的cidr并创建vpc和对等连接准备并网
            var rand = new Random();
            var second = rand.Next(1, 255);

            for (var i = 0; i < _toplingConstants.CidrMaxTry; ++i)
            {
                availableVpc = _toplingResources.GetAvailableVpc(second, out errorMessage);
                if (availableVpc != null)
                {
                    break;
                }
            }
            // 重复后依旧获取不到可用的VPC来并网
            if (availableVpc == null)
            {
                throw new Exception($"未能获取可用的VPC，请重试或联系管理员: {errorMessage}");
            }
            // 已经找到可用的VPC与网段，尝试本地创建
            _appendLog("创建阿里云VPC");
            var cidr = $"10.{second}.0.0/16";
            var vpcId = CreateDefaultVpc(cidr);
            _appendLog("为VPC创建默认安全组");
            CreateDefaultSecurityGroupIfNotExists(vpcId);
            // 创建对等连接&发送并网请求
            _appendLog("对VPC创建对等连接并设置路由");
            var peerId = CreatePeerAndAddRoute(vpcId, availableVpc, cidr);
            _appendLog("使用对等连接并网");
            _toplingResources.GrantPeer(peerId, second, vpcId);
            // 创建交换机(幂等)
            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            CreateIdempotentVSwitch(vpcId, second);
            // 创建实例
            return _toplingResources.CreateDefaultInstance(peerId);


        }
        // 本地创建了但是却没有并网
        if (subNet == null && vpc != null)
        {
            var rand = new Random();
            var second = rand.Next(1, 255);
            // 查看现在是否存在请求中的对等连接，尝试并网，(注意确认对等连接一端是否是这个VPC)
            var peerId = GetCurrentPeering(vpc.VpcId);
            if (peerId == null)
            {
                for (var i = 0; i < _toplingConstants.CidrMaxTry; ++i)
                {
                    availableVpc = _toplingResources.GetAvailableVpc(second, out errorMessage);
                    if (availableVpc != null)
                    {
                        break;
                    }
                }
                // 重复后依旧获取不到可用的VPC来并网
                if (availableVpc == null)
                {
                    throw new Exception($"未能获取可用的VPC，请重试或联系管理员: {errorMessage}");
                }
                CreateDefaultSecurityGroupIfNotExists(vpc.VpcId);
                var cidr = $"10.{second}.0.0/16";
                AddVpcTag(vpc.VpcId, cidr);
                peerId = CreatePeerAndAddRoute(vpc.VpcId, availableVpc, cidr);
            }
            // 如果失败，则删除对等连接并且在这个上面重新尝试新的交换机并且尝试并网


            _appendLog("使用对等连接并网");
            // 获取peerId和second
            _toplingResources.GrantPeer(peerId, second, vpc.VpcId);
            // 创建交换机(幂等)
            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            CreateIdempotentVSwitch(vpc.VpcId, second);
            // 创建实例

            return _toplingResources.CreateDefaultInstance(peerId);
        }
        // 已经并网了查看是否正确工作
        if (subNet != null && vpc != null)
        {

            // 检测对等连接是否是这个账号上的，如果不是，提示核对用户的accessKey
            if (!subNet.UserCloudId.Equals(vpc.OwnerId.ToString()))
            {
                //提示核对用户的accessKey,两边账号对不上
                throw new Exception($"已注册注册子网账号{subNet.UserCloudId}和提交AccessKey账号{vpc.OwnerId}不同，请检查");
            }
            var regex = new Regex(@"10\.(?<second>\d+)\.0\.0/16");
            var second = int.Parse(regex.Match(subNet.Cidr).Groups["second"].Value);
            // 已经联网完成，添加路由表和交换机
            CreateDefaultSecurityGroupIfNotExists(vpc.VpcId);
            CreateIdempotentVSwitch(vpc.VpcId, second);

            return _toplingResources.CreateDefaultInstance(subNet.PeerId);
        }

        // 这种情况属于病态
        // subnet!=null && vpc == null 核对用户的accessKey是否正确
        if (subNet != null && vpc == null)
        {
            // 这是用户并网了之后又把自己的vpc删掉了(或删除了key，或者手动并网的)的情况

            if (userId != null && userId == subNet.UserCloudId)
            {
                // 提示自己到控制台下手工删除这个子网，然后使用自动化工具重新并网
                throw new Exception($"已注册子网的VPC被删除，请在topling控制台中手动删除子网后重新运行本程序");
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


    private void CreateIdempotentVSwitch(string vpcId, int secondCidr)
    {
        // 获取所有的可用区
        var zoneList = _client.GetAcsResponse(new DescribeZonesRequest
        {
            RegionId = _toplingConstants.ToplingTestRegion,
        });
        foreach (var (index, zoneId) in zoneList.Zones.Select((zone, index) => (index, zone.ZoneId)))
        {
            var block = $"10.{secondCidr}.{index}.0/24";
            _client.GetAcsResponse(new CreateVSwitchRequest
            {
                VpcId = vpcId,
                ZoneId = zoneId,
                CidrBlock = block,
                ClientToken = $"{vpcId}_{zoneId}_{block}"
            });
        }
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
        request.AddQueryParameters("Tag.1.Key", _toplingConstants.ToplingVpcTagKey);
        request.AddQueryParameters("Tag.1.Value", cidr);
        _client.GetCommonResponse(request);
    }

    private string CreatePeerAndAddRoute(string vpcId, AvailableVpc toplingAvailable, string cidr)
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
        request.AddQueryParameters("AcceptingRegionId", _toplingConstants.ToplingTestRegion);
        request.AddQueryParameters("AcceptingVpcId", toplingAvailable.VpcId);
        request.AddQueryParameters("Name", "for-topling");
        var response = _client.GetCommonResponse(request);
        if (response.HttpStatus == 400)
        {
            throw new Exception(response.Data);
        }


        var routeTableId = _client.GetAcsResponse(new DescribeVpcsRequest
        {
            RegionId = _toplingConstants.ToplingTestRegion,
            VpcId = vpcId,
        }).Vpcs.First().RouterTableIds.First();
        var pccId = JObject.Parse(response.Data)["InstanceId"].ToString();

        // add route
        var routeToken = $"route_{cidr}_{routeTableId}_{pccId}";
        _client.GetAcsResponse(new CreateRouteEntryRequest
        {
            RouteTableId = routeTableId,
            DestinationCidrBlock = _toplingConstants.ToplingCidr,
            NextHopId = pccId,
            NextHopType = "VpcPeer",
            ClientToken = routeToken
        });
        return pccId;
    }

    /// <summary>
    /// 创建一个VPC用于并网(暂不创建交换机)
    /// </summary>
    /// <returns>vpc-id</returns>
    private string CreateDefaultVpc(string cidr)
    {
        var res = _client.GetAcsResponse(new CreateVpcRequest
        {
            RegionId = _toplingConstants.ToplingTestRegion,
            CidrBlock = "10.0.0.0/8"
        });
        Task.Delay(TimeSpan.FromSeconds(5)).Wait();
        AddVpcTag(res.VpcId, cidr);

        return res.VpcId;
    }

    private void CreateDefaultSecurityGroupIfNotExists(string vpcId)
    {
        var sgId = _client.GetAcsResponse(new DescribeSecurityGroupsRequest
        {
            VpcId = vpcId,
            RegionId = _toplingConstants.ToplingTestRegion
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
    }


    private string? GetCurrentPeering(string vpcId)
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
        var peerList = ((JObject.Parse(response.Data)["Data"]["VpcPeerConnects"] as JArray)!)
            .Where(peer =>
            {
                var status = peer["Status"].ToString();
                return status.Equals("Creating") || status.Equals("Accepting");
            }).ToList();
        // 如果有创建后超时的对等连接或者用户自己创建而未接收的实例会出问题,
        // TODO 可以针对这种情况提示用户自己检测
        return peerList.FirstOrDefault()?["InstanceId"].ToString();
    }



    public void Dispose()
    {
        _toplingResources.Dispose();
    }


}