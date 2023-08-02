namespace ToplingHelperModels
{
    public class ToplingConstants
    {

        public string ToplingTestRegion { get; init; } = "cn-shenzhen";

        public long ToplingAliYunUserId { get; init; } = 1343819498686551;


        public string ToplingConsoleHost { get; init; } = "https://console.topling.cn";

        public string ToplingVpcTagKey { get; init; } = "topling-subnet-vpc";

        public int CidrMaxTry { get; init; } = 5;

        public string DefaultTodisEcsType { get; init; } = "ecs.r6e.large";

        public string DefaultMyToplingEcsType { get; init; } = "ecs.g6.2xlarge";


        public string ToplingCidr { get; init; } = "10.0.0.0/16";
    }
}
