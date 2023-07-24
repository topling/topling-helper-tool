using System.Diagnostics;
using System.Text.RegularExpressions;
using Aliyun.Acs.Ecs.Model.V20140526;
using ToplingHelperModels.Models;
using DescribeVpcsRequest = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsRequest;
using DescribeVpcsResponse = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsResponse;

namespace ToplingHelperModels.SubNetLogic;

public class SubnetLogic : IDisposable
{
    private readonly IResourceOperations _operations;
    private readonly ToplingConstants _toplingConstants;
    private readonly ToplingResources _toplingResources;
    private readonly Action<string> _appendLog;

    public string ExistingVpcId { get; private set; } = string.Empty;

    public SubnetLogic(ToplingConstants constants, ToplingUserData userData, Action<string> logger)
    {
        _toplingConstants = constants;
        // TODO _operations初始化
        _appendLog = logger;
        _toplingResources = new ToplingResources(constants, userData, logger);
    }

    public Task<Instance> CreateInstanceAsync()
    {
        return Task.Run(CreateInstanceSync);
    }

    private Instance CreateInstanceSync()
    {
        // 先处理记录了VPC的情况
        var subNet = _toplingResources.GetDefaultUserSubNet();
        AvailableVpc? availableVpc = null;
        DescribeVpcsResponse.DescribeVpcs_Vpc? vpc;
        string? userId = null;
        for (var pageNumber = 1; ; ++pageNumber)
        {
            var vpcList = _operations.GetVpcs(new DescribeVpcsRequest
                {
                    RegionId = _toplingConstants.ToplingTestRegion,
                    PageNumber = pageNumber,
                    PageSize = 50
                });
            if (userId == null && vpcList.Any())
            {
                userId = vpcList.First(v => v.OwnerId != null).OwnerId.ToString();
            }
            vpc = vpcList
                .FirstOrDefault(v =>
                    v.Tags.Any(t => t.Key.Equals(_toplingConstants.ToplingVpcTagKey, StringComparison.OrdinalIgnoreCase)));
            if (vpc != null)
            {
                ExistingVpcId = vpc.VpcId;
            }
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
            var vpcId = _operations.CreateDefaultVpc(cidr);
            _appendLog("为VPC创建默认安全组");
            _operations.CreateDefaultSecurityGroupIfNoneExists(vpcId);
            // 创建对等连接&发送并网请求
            _appendLog("对VPC创建对等连接并设置路由");
            var peerId = _operations.CreatePeerConnection(vpcId, availableVpc, cidr);
            _appendLog("使用对等连接并网");
            _toplingResources.GrantPeer(peerId, second, vpcId);

            Task.Delay(TimeSpan.FromSeconds(10)).Wait();
            // 添加路由
            _appendLog("添加路由表项");
            var routeId = _operations.CreateRouteEntry(cidr, vpcId, peerId);
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
            var peerId = _operations.GetCurrentPeering(vpc.VpcId);
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
                _operations.AddVpcTag(vpc.VpcId, cidr);
                _operations.CreateDefaultSecurityGroupIfNoneExists(vpc.VpcId);

                Debug.Assert(availableVpc != null);
                peerId = _operations.CreatePeerConnection(vpc.VpcId, availableVpc, cidr);
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
            _operations.CreateRouteEntry(cidr, vpc.VpcId, peerId);
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
            _operations.CreateRouteEntry(subNet.Cidr, vpc.VpcId, subNet.PeerId);
            _operations.CreateDefaultSecurityGroupIfNoneExists(vpc.VpcId);
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

    private void CreateIdempotentVSwitch(string vpcId, int secondCidr)
    {
        var zoneList = _operations.GetZones(new DescribeZonesRequest()
        {
            RegionId = _toplingConstants.ToplingTestRegion,
        });
        var switchCidrList = _operations.GetVSwitches(new DescribeVSwitchesRequest()
        {
            VpcId = vpcId,
        }).Select(s => s.CidrBlock).ToList();
        foreach (var (index, zoneId) in zoneList.Select((zone, index) => (index, zone.ZoneId)))
        {
            var block = $"10.{secondCidr}.{index}.0/24";
            // 阿里云幂等性不做长时间保证,这里手动判定
            if (switchCidrList.Contains(block))
            {
                continue;
            }
            _operations.CreateVSwitch(new CreateVSwitchRequest
            {
                VpcId = vpcId,
                ZoneId = zoneId,
                CidrBlock = block,
                ClientToken = $"{vpcId}_{zoneId}_{block}"
            });
        }
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

    public void Dispose()
    {
        _toplingResources.Dispose();
    }
}