namespace ToplingHelperModels.Models.CloudService
{
    internal class UserVpc
    {
        public string OwnerId { get; set; } = default!;

        public string VpcId { get; init; } = default!;

        public string SubNetCidr { get; set; } = default!;

        public string RouteId { get; init; } = default!;
    }
}
