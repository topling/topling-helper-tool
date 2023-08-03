using Aliyun.Acs.Core;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using System;
using Amazon.Runtime.Internal;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using ToplingHelperModels.Models.CloudService;
using ToplingHelperModels.Models.WebApi;
using Tag = Amazon.EC2.Model.Tag;

namespace ToplingHelperModels.CloudService
{
    internal class AwsResourcesProvider : CloudServiceResources
    {
        private readonly AmazonEC2Client _client;
        
        public AwsResourcesProvider(ToplingConstants constants, ToplingUserData userData, Action<string>? logger = null) : base(constants, userData, logger)
        {
            _client = new AmazonEC2Client(userData.AccessId, userData.AccessSecret, RegionEndpoint.GetBySystemName(userData.RegionId));
            using var stsClient = new AmazonSecurityTokenServiceClient(userData.AccessId, userData.AccessSecret, RegionEndpoint.GetBySystemName(userData.RegionId));
            UserCloudId = stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest()).Result.Account;
        }


        public override UserVpc CreateDefaultVpc(string cidr)
        {
            Log("创建aws VPC");
            var res = _client.CreateVpcAsync(new CreateVpcRequest()
            {
                CidrBlock = cidr,
                AmazonProvidedIpv6CidrBlock = true,
                TagSpecifications = new List<TagSpecification>()
                {
                    new() { Tags = new List<Tag> { new() { Key = ToplingConstants.ToplingVpcTagKey,Value = cidr} } }
                },
            }).Result;
            Log($"创建VPC成功: {res.Vpc.VpcId}");
            Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            
            CreateDefaultSecurityGroupIfNotExists(res.Vpc.VpcId);
            var route = _client.DescribeRouteTablesAsync(new DescribeRouteTablesRequest()
            {
                Filters = new List<Filter>()
                {
                    new Filter("vpc-id", new List<string>() { res.Vpc.VpcId}),
                    new Filter("association.main", new List<string>() { "true" })
                },
            }).Result;
            return new UserVpc
            {
                OwnerId = UserCloudId,
                VpcId = res.Vpc.VpcId,
                SubNetCidr = cidr,
                RouteId = route.RouteTables.First().RouteTableId
            };
        }

        public override string CreatePeer(UserVpc userVpc, ToplingVpcForSubnetModel vpc)
        {

            var peer = _client.DescribeVpcPeeringConnectionsAsync(new DescribeVpcPeeringConnectionsRequest
            {
                Filters = new List<Filter>()
                {
                    new Filter("requester-vpc-info.vpc-id", new List<string>() { userVpc.VpcId }),
                    new Filter("accepter-vpc-info.vpc-id", new List<string>() { vpc.VpcId }),
                },
            }).Result;
            if (peer.VpcPeeringConnections.Any())
            {
                return peer.VpcPeeringConnections.First().VpcPeeringConnectionId;
            }

            var res = _client.CreateVpcPeeringConnectionAsync(new CreateVpcPeeringConnectionRequest
            {
                TagSpecifications = new List<TagSpecification>()
                {
                    new() { Tags = new List<Tag> { new() { Key = ToplingConstants.ToplingVpcTagKey } } }
                },
                PeerRegion = UserData.RegionId,
                PeerVpcId = vpc.VpcId,
                VpcId = userVpc.VpcId
            }).Result;
            return res.VpcPeeringConnection.VpcPeeringConnectionId;
        }

        public override string AddRoute(string cidr, string vpcId, string pccId)
        {

            throw new NotImplementedException();
        }

        public override string? GetCurrentPeering(string vpcId)
        {
            var peer = _client.DescribeVpcPeeringConnectionsAsync(new DescribeVpcPeeringConnectionsRequest
            {
                Filters = new List<Filter>()
                {
                    new Filter("requester-vpc-info.vpc-id", new List<string>() { vpcId }),
                },
            }).Result;
            return peer.VpcPeeringConnections.FirstOrDefault()?.VpcPeeringConnectionId;
        }

        public override UserVpc? GetUserVpcForTopling(string region)
        {
            string nextToken;
            List<Vpc> vpcList = new();
            do
            {
                var res = _client.DescribeVpcsAsync(new DescribeVpcsRequest()).Result;
                nextToken = res.NextToken;
                vpcList.AddRange(res.Vpcs);
            } while (nextToken != null);


            var vpc = vpcList.FirstOrDefault(i => i.Tags.Any(t => t.Key == ToplingConstants.ToplingVpcTagKey));
            if (vpc == null)
            {
                return null;
            }

            var route = _client.DescribeRouteTablesAsync(new DescribeRouteTablesRequest()
            {
                Filters = new List<Filter>()
                {
                    new Filter("vpc-id", new List<string>() { vpc.VpcId }),
                    new Filter("association.main", new List<string>() { "true" })
                },
            }).Result;
            return new UserVpc
            {
                OwnerId = vpc.OwnerId,
                VpcId = vpc.VpcId,
                SubNetCidr = vpc.Tags.First(t => t.Key == ToplingConstants.ToplingVpcTagKey).Value,
                RouteId = route.RouteTables.First().RouteTableId
            };
            throw new NotImplementedException();
        }


        internal override void CreateIdempotentVSwitch(string vpcId, int secondCidr)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            _client.Dispose();
            //throw new NotImplementedException();
        }

        private void CreateDefaultSecurityGroupIfNotExists(string vpcId)
        {
#if false
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
#endif
        }


    }
}