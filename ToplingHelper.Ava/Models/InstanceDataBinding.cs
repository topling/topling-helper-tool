using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aliyun.Acs.Ecs.Model.V20140526;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava.Models
{
    public class InstanceDataBinding : Instance
    {
        public InstanceDataBinding(ToplingConstants constants, Instance instance)
        {
            var props = typeof(Instance)
                .GetProperties().ToList();
            foreach (var prop in props)
            {
                var propInfo = typeof(InstanceDataBinding)
                    .GetProperty(prop.Name)!;
                propInfo.SetValue(this, prop.GetValue(instance));
            }
            ToplingBaseHost = string.Join(".", constants.ToplingConsoleHost
                .Split(".")
                .TakeLast(2));
            ToplingTestRegion = constants.ToplingTestRegion;
        }
#if DEBUG
        public InstanceDataBinding()
        {
            ToplingBaseHost = "topling.cn";
            ToplingTestRegion = "cn-shenzhen";
            InstanceEcsId = string.Empty;
        }
#endif



        public string ToplingBaseHost { get; init; }

        public string ToplingTestRegion { get; init; }

        public string GrafanaUrl => $"http://{InstanceEcsId}.aliyun.db.{ToplingBaseHost}:8000";

        public string EngineUrl => $"http://{InstanceEcsId}.aliyun.db.{ToplingBaseHost}:3000";
        public string RouteUrl=> $"https://vpcnext.console.aliyun.com/vpc/{ToplingTestRegion}/route-tables/{RouteId}";
    }
}
