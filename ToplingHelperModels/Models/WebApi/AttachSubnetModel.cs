using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ToplingHelperModels.CloudService;

namespace ToplingHelperModels.Models.WebApi
{
    /// <summary>
    /// 并网请求
    /// </summary>
    internal class AttachSubnetModel
    {
        /// <summary>
        /// 待并网对等连接ID
        /// </summary>
        public string PeerId { get; set; } = default!;
        public Provider Provider { get; set; } = default!;
        public string Region { get; set; } = default!;

        /// <summary>
        /// 10.x.0.0/16
        /// </summary>
        public string SubNetCidr { get; set; } = default!;

    }
}
