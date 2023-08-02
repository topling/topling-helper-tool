namespace ToplingHelperModels.Models;

public class Instance
{
    public string? PrivateIp { get; set; }

    public string? InstanceEcsId { get; set; }

    public string? VpcId { get; set; }

    public string? PeerId { get; set; }

    public string? RouteId { get; set; }

    public string? ToplingVpcId { get; set; }

    public string InstanceType { get; set; } = string.Empty;
}