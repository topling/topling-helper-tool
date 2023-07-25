using Aliyun.Acs.Ecs.Model.V20140526;
using DescribeVpcsRequest = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsRequest;
using DescribeVpcsResponse = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsResponse;

namespace ToplingHelperModels.SubNetLogic;

internal interface IResourceOperations
{
    public IList<DescribeVpcsResponse.DescribeVpcs_Vpc> GetVpcs(DescribeVpcsRequest r);

    public IList<DescribeZonesResponse.DescribeZones_Zone> GetZones(DescribeZonesRequest r);

    public IList<DescribeVSwitchesResponse.DescribeVSwitches_VSwitch> GetVSwitches(DescribeVSwitchesRequest request);

    public string? GetCurrentPeering(string vpcId);
    
    // 创建一个VPC用于并网(暂不创建交换机)
    public string CreateDefaultVpc(string cidr, string key, string regionId);

    public string CreateDefaultSecurityGroupIfNoneExists(string vpcId, string region);

    public string CreateVSwitch(CreateVSwitchRequest request);
    
    // 到Topling的对等连接
    public string CreatePeerConnection(string vpcId, string acceptingRegion, AvailableVpc toplingAvailable, string cidr);

    public string CreateRouteEntry(string cidr, string regionId, string vpcId, string destinationCidr, string pccId);

    public void AddVpcTag(string vpcId, string key, string cidr);
    
    public static IResourceOperations InitializeProvider()
    {
        throw new NotImplementedException();
    }
}