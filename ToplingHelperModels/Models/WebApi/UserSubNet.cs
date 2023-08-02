namespace ToplingHelperModels.Models.WebApi;

/// <summary>
/// 用户并网后的子网网段
/// </summary>
internal class UserSubNet
{
    /// <summary>
    /// 用户的云服务商ID
    /// </summary>
    public string PeerId { get; set; } = default!;
    /// <summary>
    /// 用户的网段。在此网段的IP可以被路由
    /// </summary>
    public string Cidr { get; set; } = default!;

    /// <summary>
    /// 用户的云服务商ID
    /// </summary>
    public string UserCloudId { get; set; } = default!;

}