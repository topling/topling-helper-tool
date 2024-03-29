﻿namespace ToplingHelperModels
{
    public class ToplingConstants
    {

        public long ToplingAliYunUserId { get; init; } = 1343819498686551;

        public string ToplingAwsId { get; set; } = "079281836905";


        public string ToplingConsoleHost { get; init; } = "https://console.topling.cn";

        public string ToplingVpcTagKey { get; init; } = "topling-subnet-vpc";


        public string ToplingCidr { get; init; } = "10.0.0.0/16";

        public Dictionary<Provider, Region> ProviderToRegion { get; set; } = new()
        {
            {Provider.AliYun,new Region {RegionId = "cn-zhenzhen",ZoneId = "cn-shenzhen-e"}},
            {Provider.Aws, new Region {RegionId = "us-west-1",ZoneId = "usw1-az1"}}
        };
    }

    public class Region
    {
        public string RegionId { get; init; } = default!;
        public string ZoneId { get; init; } = default!;
    }
}
