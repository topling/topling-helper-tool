using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToplingHelperModels.Models.WebApi
{
    internal class ToplingVpcForSubnetModel
    {
        public string VpcId { get; init; } = default!;
        public string ToplingId { get; init; } = default!;
        public string Cidr { get; init; } = default!;

    }
}
