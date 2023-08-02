using System.ComponentModel;
using ToplingHelperModels.Models.CloudService;
using ToplingHelperModels.Models.WebApi;

namespace ToplingHelperModels.CloudService
{
    internal abstract class CloudServiceResources : IDisposable
    {

        protected abstract string DefaultZoneId(string regionId);

        protected const string ToplingCidr = "10.0.0.0/16";
        protected CloudServiceResources(ToplingConstants constants, ToplingUserData userData, Action<string>? logger = null)
        {
            ToplingConstants = constants;
            UserData = userData;
            _logger = logger;
        }


        protected ToplingConstants ToplingConstants { get; init; }
        protected ToplingUserData UserData { get; init; }

        public static CloudServiceResources GetResourcesProvider(ToplingConstants constants, ToplingUserData userData, Action<string> logger)
        {

            return userData.Provider switch
            {
                Provider.Aws => new AwsResourcesProvider(constants, userData, logger),
                Provider.AliYun => new AliYunResourcesProvider(constants, userData, logger),
                _ => throw new InvalidEnumArgumentException("未知的服务商")
            };
        }

        public abstract UserVpc? GetVpcForTopling(string region);
        public abstract UserVpc CreateDefaultVpc(string cidr);
        public abstract string CreatePeer(UserVpc userVpc, ToplingVpcForSubnetModel vpc);
        /// <summary>
        /// 给用户VPC添加路由表项，指向对等连接
        /// </summary>
        /// <param name="cidr">用户VPC所属CIDR，仅用于标记</param>
        /// <param name="vpcId">用户VPCID</param>
        /// <param name="pccId">用户对等连接ID</param>
        /// <returns></returns>
        public abstract string AddRoute(string cidr, string vpcId, string pccId);
        public abstract string? GetCurrentPeering(string vpcId);
        public abstract UserVpc? GetUserVpcForTopling(string region);
        public abstract void AddVpcTag(string vpcId, string cidr);

        public abstract string GetUserCloudId();


        internal abstract void CreateIdempotentVSwitch(string vpcId, int secondCidr);

        private readonly Action<string>? _logger;


        protected void Log(string message)
        {
            _logger?.Invoke(message);
        }

        public abstract void Dispose();
    }
}
