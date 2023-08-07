
namespace ToplingHelperModels
{
    public class ToplingUserData
    {
        public Provider Provider { get; set; }

        public string RegionId { get; set; } = string.Empty;

        public string AccessId { get; set; } = string.Empty;
        public string AccessSecret { get; set; } = string.Empty;
        public string ToplingUserId { get; set; } = string.Empty;
        public string ToplingPassword { get; set; } = string.Empty;

        public bool UseLocalStorage { get; set; } = true;

        public bool GtidMode { get; set; }

        public uint ServerId { get; set; } = 0;


        public InstanceType CreatingInstanceType { get; set; } = InstanceType.Unknown;

        public bool UserdataCheck(out string error)
        {
            error = string.Empty;

            if (AccessId.Length > AccessSecret.Length)
            {
                error = "阿里云AccessId应短于AccessSecret，请检查是否粘贴错误";
                return false;
            }

            return true;
        }
    }

    public enum InstanceType
    {
        Unknown,
        Todis,
        MyTopling,
        Dummy
    }
}