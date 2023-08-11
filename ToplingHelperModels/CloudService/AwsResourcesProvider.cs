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
    internal sealed class AwsResourcesProvider : CloudServiceResources
    {
        private readonly AmazonEC2Client _client;


        public AwsResourcesProvider(ToplingConstants constants, ToplingUserData userData, Action<string>? logger = null) : base(constants, userData, logger)
        {
            using var stsClient = new AmazonSecurityTokenServiceClient(userData.AccessId, userData.AccessSecret,
                RegionEndpoint.GetBySystemName(RegionId));
            try
            {

                UserCloudId = stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest()).Result.Account;
            }
            catch (AggregateException e) when (e.InnerExceptions.First() is AmazonSecurityTokenServiceException)
            {
                throw new Exception("请检查是否AccessKeyId和AccessKeySecret有效");
            }

            _client = new AmazonEC2Client(userData.AccessId, userData.AccessSecret, RegionEndpoint.GetBySystemName(RegionId));

        }


        public override string UserCloudId { get; protected init; }

        public override UserVpc CreateVpcForTopling(string cidr)
        {
            Log("创建aws VPC");
            CreateVpcResponse res = null;
            try
            {
                res = _client.CreateVpcAsync(new CreateVpcRequest()
                {
                    CidrBlock = cidr,
                    AmazonProvidedIpv6CidrBlock = true,
                    TagSpecifications = new List<TagSpecification>()
                    {
                        new()
                        {
                            ResourceType = ResourceType.Vpc,
                            Tags = new List<Tag> { new() { Key = ToplingConstants.ToplingVpcTagKey, Value = cidr } }
                        }
                    },
                }).Result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Log($"创建VPC成功: {res!.Vpc.VpcId}");
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
                    new() { ResourceType = ResourceType.VpcPeeringConnection,Tags = new List<Tag> { new() { Key = ToplingConstants.ToplingVpcTagKey,Value=string.Empty } } }
                },
                PeerRegion = RegionId,
                PeerOwnerId = ToplingConstants.ToplingAwsId,
                PeerVpcId = vpc.VpcId,
                VpcId = userVpc.VpcId
            }).Result;
            return res.VpcPeeringConnection.VpcPeeringConnectionId;
        }

        public override void AddRoute(string cidr, string vpcId, string pccId)
        {

            var routeTable = _client.DescribeRouteTablesAsync(new DescribeRouteTablesRequest()
            {
                Filters = new List<Filter>()
                {
                    new Filter() { Name = "vpc-id", Values = new List<string> { vpcId } }
                }
            }).Result.RouteTables.First()!;

            #region igw
            var igw = _client.DescribeInternetGatewaysAsync(new DescribeInternetGatewaysRequest()
            {
                Filters = new List<Filter>()
                {
                    new Filter("attachment.vpc-id", new List<string>() { vpcId })
                }
            }).Result;
            string igwId;
            if (!igw.InternetGateways.Any())
            {
                var igw2 = _client.CreateInternetGatewayAsync(new CreateInternetGatewayRequest
                {
                    TagSpecifications = new List<TagSpecification>()
                    {
                        new() { ResourceType = ResourceType.InternetGateway,Tags = new List<Tag> { new() { Key = ToplingConstants.ToplingVpcTagKey,Value = string.Empty} } }
                    },
                }).Result;
                _client.AttachInternetGatewayAsync(new AttachInternetGatewayRequest
                {
                    InternetGatewayId = igw2.InternetGateway.InternetGatewayId,
                    VpcId = vpcId
                }).Wait();
                igwId = igw2.InternetGateway.InternetGatewayId;
            }
            else
            {
                igwId = igw.InternetGateways.First().InternetGatewayId;
            }


            #endregion

            //  添加路由表：
            // 网关
            if (routeTable.Routes.All(i => i.DestinationCidrBlock != "0.0.0.0/0"))
            {
                _client.CreateRouteAsync(new CreateRouteRequest()
                {
                    RouteTableId = routeTable.RouteTableId,
                    DestinationCidrBlock = "0.0.0.0/0",
                    GatewayId = igwId,
                }).Wait();
            }
            if (routeTable.Routes.All(i => i.DestinationIpv6CidrBlock != "::/0"))
            {
                _client.CreateRouteAsync(new CreateRouteRequest()
                {
                    RouteTableId = routeTable.RouteTableId,
                    DestinationIpv6CidrBlock = "::/0",
                    GatewayId = igwId,
                }).Wait();
            }

            if (routeTable.Routes.All(i => i.DestinationCidrBlock != cidr))
            {
                _client.CreateRouteAsync(new CreateRouteRequest()
                {
                    DestinationCidrBlock = cidr,
                    RouteTableId = routeTable.RouteTableId,
                    VpcPeeringConnectionId = pccId
                });
            }
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
                SubNetCidr = vpc.CidrBlock,
                RouteId = route.RouteTables.First().RouteTableId
            };

        }


        internal override void CreateIdempotentVSwitch(string vpcId, int secondCidr)
        {
            var zones = _client.DescribeAvailabilityZonesAsync(new DescribeAvailabilityZonesRequest()
            {
                AllAvailabilityZones = true,
            }).Result.AvailabilityZones.Select(i => new
            {
                i.ZoneId,
                i.ZoneName
            }).OrderBy(i => i.ZoneName).ToList();

            var subnets = _client.DescribeSubnetsAsync(new DescribeSubnetsRequest()
            {
                Filters = new List<Filter>()
                {
                    new Filter("vpc-id", new List<string>() { vpcId })
                },
            }).Result.Subnets!;
            foreach (var (index, zone) in zones.Select((zone, index) => (index, zone)))
            {
                var block = $"10.{secondCidr}.{index}.0/24";
                if (subnets.Any(s => s.CidrBlock == block))
                {
                    continue;
                }

                _client.CreateSubnetAsync(new CreateSubnetRequest
                {
                    AvailabilityZoneId = zone.ZoneId,
                    CidrBlock = block,
                    VpcId = vpcId
                }).Wait();
            }
        }

        public override void Dispose()
        {
            _client.Dispose();
        }

        private void CreateDefaultSecurityGroupIfNotExists(string vpcId)
        {
            // 可能不需要
        }


    }
}