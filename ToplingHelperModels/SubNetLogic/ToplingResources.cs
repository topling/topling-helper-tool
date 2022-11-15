using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DescribeVpcsResponse = Aliyun.Acs.Vpc.Model.V20160428.DescribeVpcsResponse;
namespace ToplingHelperModels.SubNetLogic
{
    public class ToplingResources
    {
        
        public UserSubNet? GetDefaultUserSubNet()
        {
            return null;
            throw new IndexOutOfRangeException();
        }
    }

    public class UserSubNet
    {
        public string PeerId { get; init; } = default!;

        public string Cidr { get; init; } = default!;

        public string UserCloudId { get; init; } = default!;
    }
}
