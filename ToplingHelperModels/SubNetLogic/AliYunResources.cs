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

public sealed class AliYunResources : IDisposable
{
    private readonly ToplingConstants _toplingConstants;
    private readonly DefaultAcsClient _client;
    private readonly ToplingResources _toplingResources;

    private readonly Action<string> _appendLog;

    public AliYunResources(ToplingConstants constants, ToplingUserData userData, Action<string> logger)
    {
        _toplingConstants = constants;
        _client = new DefaultAcsClient(DefaultProfile.GetProfile(constants.ToplingTestRegion, userData.AccessId, userData.AccessSecret));
        _appendLog = logger;
        _toplingResources = new ToplingResources(constants, userData);
    }

    public Instance CreateInstance()
    {
        // 先处理记录了VPC的情况
        var subNet = _toplingResources.GetDefaultUserSubNet();
        AvailableVpc? availableVpc = null;
        DescribeVpcsResponse.DescribeVpcs_Vpc? vpc;
        string? userId = null;
        for (var pageNumber = 1; ; ++pageNumber)
        {
            DescribeVpcsResponse vpcResponse;
            try
            {
                vpcResponse = _client.GetAcsResponse(new DescribeVpcsRequest
                {
                    RegionId = _toplingConstants.ToplingTestRegion,
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

                second = rand.Next(1, 255);
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
            var peerId = CreatePeer(vpcId, availableVpc, cidr);
            _appendLog("使用对等连接并网");
            _toplingResources.GrantPeer(peerId, second, vpcId);

            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            // 添加路由
            _appendLog("添加路由表项");
            var routeId = AddRoute(cidr, vpcId, peerId);
            // 创建交换机(幂等)
            _appendLog("创建交换机");
            CreateIdempotentVSwitch(vpcId, second);
            // 创建实例
            _appendLog("开始创建实例");
            var instance = _toplingResources.CreateDefaultInstance(peerId, vpcId);
            instance.RouteId = routeId;
            return instance;


        }
        // 本地创建了但是却没有并网
        if (subNet == null && vpc != null)
        {
            var cidr = vpc.Tags.FirstOrDefault(v => v.Key.Equals(_toplingConstants.ToplingVpcTagKey))?._Value;
            var second = GetSecondFromCidr(cidr);
            // 查看现在是否存在请求中的对等连接，尝试并网，(注意确认对等连接一端是否是这个VPC)
            var peerId = GetCurrentPeering(vpc.VpcId);
            if (peerId == null)
            {

                var rand = new Random();
                // 如果分配过，则先尝试现有的网段(否则每次都会创建一组交换机)
                second ??= rand.Next(1, 255);

                for (var i = 0; i < _toplingConstants.CidrMaxTry; ++i)
                {
                    availableVpc = _toplingResources.GetAvailableVpc(second.Value, out errorMessage);
                    if (availableVpc != null)
                    {
                        break;
                    }
                    second = rand.Next(1, 255);
                }
                // 重复后依旧获取不到可用的VPC来并网
                if (availableVpc == null)
                {
                    throw new Exception($"未能获取可用的VPC，请重试或联系管理员: {errorMessage}");
                }
                cidr = $"10.{second}.0.0/16";

                Debug.Assert(cidr != null);
                // 如果有变化则更新cidr
                AddVpcTag(vpc.VpcId, cidr);
                CreateDefaultSecurityGroupIfNotExists(vpc.VpcId);

                Debug.Assert(availableVpc != null);
                peerId = CreatePeer(vpc.VpcId, availableVpc, cidr);
            }
            // 如果失败，则删除对等连接并且在这个上面重新尝试新的交换机并且尝试并网
            _appendLog("使用对等连接并网");
            // 获取peerId和second
            _toplingResources.GrantPeer(peerId, second!.Value, vpc.VpcId);
            // 创建交换机(幂等)

            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            // 添加路由
            Debug.Assert(cidr != null);
            _appendLog("添加路由表项");
            AddRoute(cidr, vpc.VpcId, peerId);
            _appendLog("创建交换机");
            CreateIdempotentVSwitch(vpc.VpcId, second!.Value);
            // 创建实例
            _appendLog("开始创建实例");
            var result = _toplingResources.CreateDefaultInstance(peerId, vpc.VpcId);
            result.RouteId = vpc.RouterTableIds.First();
            return result;
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

            var second = GetSecondFromCidr(subNet.Cidr)!.Value;
            // 已经联网完成，添加路由表和交换机
            AddRoute(subNet.Cidr, vpc.VpcId, subNet.PeerId);
            CreateDefaultSecurityGroupIfNotExists(vpc.VpcId);
            CreateIdempotentVSwitch(vpc.VpcId, second);
            _appendLog("开始创建实例");
            var res = _toplingResources.CreateDefaultInstance(subNet.PeerId, vpc.VpcId);
            res.RouteId = vpc.RouterTableIds.First();
            return res;
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

    private int? GetSecondFromCidr(string? cidr)
    {
        if (cidr == null)
        {
            return null;
        }
        var regex = new Regex(@"10\.(?<second>\d+)\.0\.0/16");

        if (int.TryParse(regex.Match(cidr).Groups["second"].Value, out var res))
        {
            return res;
        }

        return null;
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
            for (int i = 0; i < 5; ++i)
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
                    if (i < 4)
                    {
                        Task.Delay(TimeSpan.FromSeconds(3 * (i + 1)));
                        continue;
                    }

                    throw;
                }
            }

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

    private string CreatePeer(string vpcId, AvailableVpc toplingAvailable, string cidr)
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

        var pccId = JObject.Parse(response.Data)["InstanceId"].ToString();
        return pccId;
    }

    private string AddRoute(string cidr, string vpcId, string pccId)
    {

        var routeTableId = _client.GetAcsResponse(new DescribeVpcsRequest
        {
            RegionId = _toplingConstants.ToplingTestRegion,
            VpcId = vpcId,
        }).Vpcs.First().RouterTableIds.First();
        // add route
        var routeToken = $"route_{cidr}_{routeTableId}_{pccId}";

        for (int i = 0; i < 5; ++i)
        {
            try
            {
                _client.GetAcsResponse(new CreateRouteEntryRequest
                {
                    RouteTableId = routeTableId,
                    DestinationCidrBlock = _toplingConstants.ToplingCidr,
                    NextHopId = pccId,
                    NextHopType = "VpcPeer",
                    ClientToken = routeToken
                });
                return routeTableId;
            }
            catch (ClientException e) when (e.ErrorCode.Equals("InvalidStatus.RouteEntry", StringComparison.OrdinalIgnoreCase))
            {
                if (i < 4)
                {
                    Task.Delay(TimeSpan.FromSeconds(3)).Wait();
                    continue;
                }
                throw;
            }
        }

        throw new Exception(("添加路由失败，请重新操作"));

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



    public void Dispose()
    {
        _toplingResources.Dispose();
    }


}