using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aliyun.Acs.Ecs.Model.V20140526;
using ToplingHelperModels;

namespace ToplingHelper.Ava.Models
{
    public class InstanceDataBinding : Instance
    {
        public InstanceDataBinding(ToplingConstants constants, Instance instance, Provider provider)
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
            ToplingTestRegion = constants.ProviderToRegion[provider].RegionId;
        }



        public string ToplingBaseHost { get; init; }

        public string ToplingTestRegion { get; init; }

        public Provider Provider { get; init; }

        public string GrafanaUrl => $"http://{InstanceEcsId}.{Provider}.db.{ToplingBaseHost}:8000";

        public string EngineUrl => $"http://{InstanceEcsId}.{Provider}.db.{ToplingBaseHost}:3000";
        public string RouteUrl => $"https://vpcnext.console.aliyun.com/vpc/{ToplingTestRegion}/route-tables/{RouteId}";
    }
}
