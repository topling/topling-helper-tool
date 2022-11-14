using System;
using System.Collections.Generic;
using System.Linq;
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

        public string ToplingConsoleHost { get; set; } = "https://console.topling.cn";

        public string ToplingVpcTagKey { get; set; } = "topling-subnet-vpc";

        public int CidrMaxTry { get; set; } = 5;

    }

    public class ToplingUserData
    {
        public long AliYunId { get; set; }
        public string AccessId { get; set; } = string.Empty;
        public string AccessSecret { get; set; } = string.Empty;
        public string ToplingId { get; set; } = string.Empty;
        public string ToplingPassword { get; set; } = string.Empty;

        public bool GtidMode { get; set; }

        public uint ServerId { get; set; } = 0;


        public InstanceType CreatingInstanceType { get; set; }

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
            Todis,
            MyTopling
        }
    }
}
