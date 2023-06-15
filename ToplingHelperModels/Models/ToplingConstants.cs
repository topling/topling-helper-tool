using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace ToplingHelperModels.Models
{
    public class ToplingConstants
    {
        public string ToplingCenName { get; init; } = "for-toping";
        public string ToplingVpcName { get; init; } = "for-toping-shenzhen";
        public string ToplingTestRegion { get; init; } = "cn-shenzhen";

        public long ToplingAliYunUserId { get; init; } = 1343819498686551;

        public string ShenzhenCidrFormat { get; init; } = "172.17.{0}.0/24";

        public string ToplingConsoleHost { get; init; } = "https://console.topling.cn";

        public string ToplingVpcTagKey { get; init; } = "topling-subnet-vpc";

        public int CidrMaxTry { get; init; } = 5;

        public string DefaultTodisEcsType { get; init; } = "ecs.r6e.large";

        public string DefaultMyToplingEcsType { get; init; } = "ecs.g7.2xlarge";


        public string ToplingCidr { get; init; } = "10.0.0.0/16";
    }

    public class ToplingUserData
    {

        public string AccessId { get; set; } = string.Empty;
        public string AccessSecret { get; set; } = string.Empty;
        public string ToplingUserId { get; set; } = string.Empty;
        public string ToplingPassword { get; set; } = string.Empty;

        public bool GtidMode { get; set; }

        public uint ServerId { get; set; } = 0;


        public InstanceType CreatingInstanceType { get; set; } = InstanceType.Unknown;

        public bool UserdataCheck(out string error)
        {
            error = string.Empty;

            if (AccessId.Length > AccessSecret.Length)
            {
                error = "阿里云AccessId应短于AccessSecret，请检查是否粘贴错误";
                return false;
            }

            return true;
        }

        public enum InstanceType
        {
            Unknown,
            Todis,
            MyTopling
        }
    }
}
