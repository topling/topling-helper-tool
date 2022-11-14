using System.Diagnostics;
using Aliyun.Acs.Core;
using Aliyun.Acs.Ecs.Model.V20140526;
using ToplingHelperModels.Models;
using CreateVpcRequest = Aliyun.Acs.Ecs.Model.V20140526.CreateVpcRequest;
using CreateVSwitchRequest = Aliyun.Acs.Ecs.Model.V20140526.CreateVSwitchRequest;
using DescribeVpcsRequest = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsRequest;
using DescribeVpcsResponse = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsResponse;

namespace ToplingHelperModels.SubNetLogic;

public class AliYunResources
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

    public string GetOrCreateVpc()
    {
        // 先处理记录了VPC的情况
        var subNet = _toplingResources.GetDefaultUserSubNet();
#error 阿里云id检测
        if(subNet)

        var pageNumber = 1;
        DescribeVpcsResponse.DescribeVpcs_Vpc? vpc;
        for (; ; ++pageNumber)
        {
            var vpcResponse = _client.GetAcsResponse(new DescribeVpcsRequest
            {
                RegionId = _toplingConstants.ToplingTestRegion,
                PageNumber = pageNumber,
                PageSize = 50
            });

            var vpcList = vpcResponse.Vpcs;

            vpc = vpcList
                .FirstOrDefault(v =>
                    v.Tags.Any(t => t.Key.Equals(_toplingConstants.ToplingVpcTagKey, StringComparison.OrdinalIgnoreCase)));
            if (!vpcList.Any() || vpc != null)
            {
                break;
            }
        }


        if (vpc == null)
        {
            // 从数据库中获取对应的实例,以处理手动创建的情况


            int secondCidr = 0;
            var max
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
                CreateVSwitch(vpc.VpcId);
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
        CreateVSwitch(response.VpcId);
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
}