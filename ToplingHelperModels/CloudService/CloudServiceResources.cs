using System.ComponentModel;
using ToplingHelperModels.Models.CloudService;
using ToplingHelperModels.Models.WebApi;

namespace ToplingHelperModels.CloudService
{
    internal abstract class CloudServiceResources : IDisposable
    {

        protected CloudServiceResources(ToplingConstants constants, ToplingUserData userData, Action<string>? logger = null)
        {
            ToplingConstants = constants;
            UserData = userData;
            _logger = logger;
            RegionId = ToplingConstants.ProviderToRegion[userData.Provider].RegionId;
        }

        protected readonly string RegionId;

        protected ToplingConstants ToplingConstants { get; init; }
        protected ToplingUserData UserData { get; init; }
        /// <summary>
        /// 用户在云服务商的用户id
        /// </summary>
        public abstract string UserCloudId { get; protected init; }

        public static CloudServiceResources GetResourcesProvider(ToplingConstants constants, ToplingUserData userData, Action<string> logger)
        {

            return userData.Provider switch
            {
                Provider.Aws => new AwsResourcesProvider(constants, userData, logger),
                Provider.AliYun => new AliYunResourcesProvider(constants, userData, logger),
                _ => throw new InvalidEnumArgumentException("未知的服务商")
            };
        }
        /// <summary>
        /// 创建为Topling准备的默认VPC。注意不是云服务商的默认VPC
        /// </summary>
        /// <param name="cidr">10.x.0.0/16</param>
        /// <returns></returns>
        public abstract UserVpc CreateVpcForTopling(string cidr);
        /// <summary>
        /// 创建对等连接,连接用户和Topling的VPC
        /// </summary>
        /// <param name="userVpc">用户VPC</param>
        /// <param name="vpc">Topling的VPC</param>
        /// <returns></returns>
        public abstract string CreatePeer(UserVpc userVpc, ToplingVpcForSubnetModel vpc);

        /// <summary>
        /// 给用户VPC添加路由表项，指向对等连接
        /// </summary>
        /// <param name="cidrForTag">用户VPC所属CIDR，仅用于标记</param>
        /// <param name="vpcId">用户VPCID</param>
        /// <param name="pccId">用户对等连接ID</param>
        /// <returns></returns>
        public abstract void AddRoute(string cidrForTag, string vpcId, string pccId);
        /// <summary>
        /// 获取当前VPC和Topling并网用对等连接
        /// </summary>
        /// <param name="vpcId"></param>
        /// <returns></returns>
        public abstract string? GetCurrentPeering(string vpcId);
        /// <summary>
        /// 获取用户为Topling创建的VPC
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        public abstract UserVpc? GetUserVpcForTopling(string region);

        /// <summary>
        /// 为用户的VPC创建子网,保证创建行为幂等
        /// </summary>
        /// <param name="vpcId"></param>
        /// <param name="secondCidr"></param>
        internal abstract void CreateIdempotentVSwitch(string vpcId, int secondCidr);


        private readonly Action<string>? _logger;

        /// <summary>
        /// 输出日志的回调，可以调用者传入
        /// </summary>
        /// <param name="message"></param>
        protected void Log(string message)
        {
            _logger?.Invoke(message);
        }

        public abstract void Dispose();
    }
}
