using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ToplingHelperModels.CloudService;
using static ToplingHelperModels.ToplingUserData;

namespace ToplingHelperModels.Models.WebApi
{
    internal class CreateInstanceModel
    {
        public string Provider { get; set; }=string.Empty;
        public string ZoneId { get; set; } = default!;

        public string Regionid { get; set; } = default!;

        public string Name { get; set; } = default!;

        public CreateInstanceRequestType InstanceType { get; set; }
        public string SubNetId { get; set; } = default!;
    }

    internal enum CreateInstanceRequestType
    {
        Todis = 0,
        MyTopling = 4,
        MyToplingLocalStorage =6
    }
}
