using ToplingHelperModels.Models.CloudService;
using ToplingHelperModels.Models.WebApi;
using ToplingHelperModels.ToplingService;

namespace ToplingHelperModels.CloudService
{
    internal class AwsResourcesProvider : CloudServiceResources
    {
        public AwsResourcesProvider(ToplingConstants constants, ToplingUserData userData, Action<string>? logger = null) : base(constants, userData, logger)
        {
        }


        protected override string DefaultZoneId(string regionId)
        {
            throw new NotImplementedException();
        }

        public override UserVpc? GetVpcForTopling(string region)
        {
            throw new NotImplementedException();
        }

        public override UserVpc CreateDefaultVpc(string cidr)
        {
            throw new NotImplementedException();
        }

        public override string CreatePeer(UserVpc userVpc, ToplingVpcForSubnetModel vpc)
        {
            throw new NotImplementedException();
        }

        public override string AddRoute(string cidr, string vpcId, string pccId)
        {
            throw new NotImplementedException();
        }

        public override string? GetCurrentPeering(string vpcId)
        {
            throw new NotImplementedException();
        }

        public override UserVpc? GetUserVpcForTopling(string region)
        {
            throw new NotImplementedException();
        }

        public override void AddVpcTag(string vpcId, string cidr)
        {
            throw new NotImplementedException();
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
    }
}