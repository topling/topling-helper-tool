namespace ToplingHelperModels.Models;

public class Instance
{
    public string? PrivateIp { get; set; }

    public string? InstanceEcsId { get; set; }

    public string? VpcId { get; set; }

    public string? ToplingVpcId { get; set; }

    public string? RouterId { get; set; }
    public string? PeerId { get; set; }

}