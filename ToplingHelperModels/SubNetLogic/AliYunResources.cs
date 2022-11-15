﻿using System.Diagnostics;
using System.Net;
using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Ecs.Model.V20140526;
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

    private string? _cidr = null;

    public AliYunResources(ToplingConstants constants, DefaultAcsClient client, ToplingUserData userData, ToplingResources toplingResources)
    {
        _toplingConstants = constants;
        _client = client;
        _userData = userData;
        _toplingResources = toplingResources;

    }

    public string GetOrCreateVpcAndSubnet()
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
            var vpcId = CreateDefaultVpc($"10.{second}.0.0/16");

            // 创建对等连接&发送并网请求
            var peerId = CreatePeerAndAddRoute(vpcId, availableVpc);
            _toplingResources.GrantPeer(peerId, second, vpcId);
            // 创建交换机(幂等)
            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            CreateIdempotentVSwitch(vpcId);
            // 创建实例
            _toplingResources.CreateDefaultInstance(_userData);


        }
        // 本地创建了但是却没有并网
        if (subNet == null && vpc != null)
        {
            // 查看现在是否存在请求中的对等连接，尝试并网，
            // 如果失败，则删除对等连接并且在这个上面重新尝试新的交换机并且尝试并网
        }
        // 已经并网了查看是否正确工作
        if (subNet != null && vpc != null)
        {
            // 检测对等连接是否是这个账号上的，如果不是，提示核对用户的accessKey
            if (!subNet.UserCloudId.Equals(vpc.OwnerId.ToString()))
            {
                //提示核对用户的accessKey,两边账号对不上
            }
            // 已经联网完成，添加路由表和交换机
        }

        // 这种情况属于病态
        // subnet!=null && vpc == null 核对用户的accessKey是否正确
        if (subNet != null && vpc == null)
        {
            // 这是用户并网了之后又把自己的vpc删掉了(或删除了key，或者手动并网的)的情况

            if (userId != null && userId == subNet.UserCloudId)
            {
                // 提示自己到控制台下手工删除这个子网，然后使用自动化工具重新并网
            }
            // 这是账号输错了的情况
            if (userId != null && userId != subNet.UserCloudId)
            {
                // 提示用户检查自己当前的accessID所属账号是否是{subNet.UserCloudId}的，
            }
            // 当前用户在阿里云上不存在任何vpc但是有subnet的记录
            if (userId == null)
            {
                // 提示用户检查自己当前的accessID所属账号是否是{subNet.UserCloudId}的，
                // 如果是，那么自己到控制台下把现有的子网删掉
            }
        }




        if (vpc == null)
        {
            // 从数据库中获取对应的实例,以处理手动创建的情况
            if (userId == null)
            {
                //if ()
            }

            //int secondCidr = 0;
            //var max
            // 重复n次尝试获取一个可用的子网网段
            for (var i = 0; i < _toplingConstants.CidrMaxTry; ++i)
            {
                // 获取可用的cidr
            }
        }

        // 已经存在，则尝试获取 tag中的value(没有则创建)
        if (vpc != null)
        {

            Debug.Assert(vpc.OwnerId != null, "vpc.OwnerId != null");
            _userData.AliYunId = vpc.OwnerId.Value;
            // create switches if not exists
            if (!vpc.VSwitchIds.Any())
            {
                //CreateVSwitch(vpc.VpcId);
            }

            CreateDefaultSecurityGroupIfNotExists(vpc.VpcId);
            return vpc.VpcId;
        }

        // create vpc;
        var response = _client.GetAcsResponse(new CreateVpcRequest
        {
            RegionId = _toplingConstants.ToplingTestRegion,
            VpcName = _toplingConstants.ToplingVpcName,
            CidrBlock = "172.16.0.0/12"
        });

        // VPC创建后不能立刻创建交换机，等待20秒;
        Task.Delay(TimeSpan.FromSeconds(20)).Wait();

        vpc = _client.GetAcsResponse(new DescribeVpcsRequest
        {
            RegionId = _toplingConstants.ToplingTestRegion,
        }).Vpcs.FirstOrDefault(v => v.VpcName == _toplingConstants.ToplingVpcName);
        Debug.Assert(vpc != null);

        Debug.Assert(vpc.OwnerId != null, "vpc.OwnerId != null");
        _userData.AliYunId = vpc.OwnerId.Value;
        //CreateVSwitch(response.VpcId);
        CreateDefaultSecurityGroupIfNotExists(vpc.VpcId);
        return vpc.VpcId;

    }

    public void CreateVSwitch(string vpcId)
    {
        for (var ch = 'a'; ch <= 'f'; ++ch)
        {
            var cidrBlock = string.Format(_toplingConstants.ShenzhenCidrFormat, ch - 'a');
            _client.GetAcsResponse(new CreateVSwitchRequest
            {
                RegionId = _toplingConstants.ToplingTestRegion,
                VpcId = vpcId,
                ZoneId = $"{_toplingConstants.ToplingTestRegion}-{ch}",
                CidrBlock = cidrBlock
            });
        }

    }



    private void CreateIdempotentVSwitch(string vpcId)
    {
        // 获取所有的可用区
        // 列出现有的交换机
        // 查看网段冲突
        // 对缺少的地址创建实例
    }

    private string CreatePeerAndAddRoute(string vpcId, AvailableVpc toplingAvailable)
    {
        // 创建到topling的对等连接并且添加路由表
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
        var request = new CommonRequest
        {
            Method = MethodType.POST,
            Domain = "vpc.cn-guangzhou.aliyuncs.com",
            Version = "2016-04-28",
            Action = "TagResources"
        };
        request.AddQueryParameters("ResourceType", "VPC");
        request.AddQueryParameters("ResourceId.1", res.VpcId);
        request.AddQueryParameters("Tag.1.Key", _toplingConstants.ToplingVpcTagKey);
        request.AddQueryParameters("Tag.1.Value", cidr);
        _client.GetCommonResponse(request);
        return res.VpcId;
    }

    public void CreateDefaultSecurityGroupIfNotExists(string vpcId)
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

    // 子网逻辑

    public void CreatePeering(string vpcId, string toplingVpcId)
    {

    }

    private void TestCidrAvailable(string cidr)
    {
        // 测试在topling上是否存在/是否冲突
        //

    }
    // 缓存 peering 
    public string Cidr
    {
        get
        {
            if (_cidr == null)
            {
                throw new NullReferenceException();
            }
            return _cidr;
        }
        private set => _cidr = value;
    }

    public void Dispose()
    {
        _toplingResources.Dispose();
    }


}