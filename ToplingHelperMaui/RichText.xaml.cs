using ToplingHelperModels.Models;

namespace ToplingHelperMaui;

public partial class RichText : ContentPage
{
    private readonly ResultDataContext _context;
    public RichText()
	{
		InitializeComponent();
	}

    public RichText(Instance instance, ToplingConstants constants)
    {
        InitializeComponent();
        _context = new ResultDataContext
        {
            Constants = constants,
            EcsId = instance.InstanceEcsId,
            UserVpcId = instance.VpcId,
            PeerId = instance.PeerId,
            InstancePrivateIp = instance.PrivateIp,
            RouteId = instance.RouteId
        };
        TestPerformance.Text = PreTestText;
        TestCommands.Text = GetTestText(_context.InstancePrivateIp);
        Resources["ContextKey"] = _context;

        GrafanaLink.Url = $"http://{_context.EcsId}.aliyun.db.topling.cn:3000";
        EngineLink.Url = $"http://{_context.EcsId}.aliyun.db.topling.cn:8000";
        RouteLink.Url = _context.RouteUrl;
    }

    private const string PreTestText = @"# 请首先确保测试实例为CentOS/AliOS
# 下载自动脚本
wget https://topling.cn/downloads/mount-test.sh && chmod +x ./mount-test.sh

# 挂载测试程序及源数据到 /mnt
sudo bash mount-test.sh /mnt
";
    private static string GetTestText(string privateIp) => $@"# 直接执行查看帮助
/mnt/InsertKeys

# 插入顺序数据(源文件过大，已使用 zstd 压缩)
zstd -d -c -q /mnt/wikipedia-flat-seq.zst | /mnt/InsertKeys -h {privateIp} -t 8 --multi-set 8 -f /dev/stdin

# 插入乱序数据(源文件过大，已使用 zstd 压缩)
zstd -d -c -q /mnt/wikipedia-flat-rand.zst | /mnt/InsertKeys -h {privateIp} -t 8 --multi-set 8 -f /dev/stdin

# 读取数据(顺序)
/mnt/GetKeysQps -t 32 -n 8 -f /mnt/wikipedia-flat-key-seq.txt -h {privateIp}

# 读取数据(乱序)
/mnt/GetKeysQps -t 32 -n 8 -f /mnt/wikipedia-flat-key-rand.txt -h {privateIp}

# 测试 hash 写性能
zstd -d -c -q /mnt/weibo.zst  |  /mnt/InsertWeiboData -h {privateIp} -t 32 -f /dev/stdin  --disabled_seekg --ignore_logs
";
}