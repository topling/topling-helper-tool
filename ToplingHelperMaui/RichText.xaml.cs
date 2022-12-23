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
    }

    private const string PreTestText = @"# ������ȷ������ʵ��ΪCentOS/AliOS
# �����Զ��ű�
wget https://topling.cn/downloads/mount-test.sh && chmod +x ./mount-test.sh

# ���ز��Գ���Դ���ݵ� /mnt
sudo bash mount-test.sh /mnt
";
    private static string GetTestText(string privateIp) => $@"# ֱ��ִ�в鿴����
/mnt/InsertKeys

# ����˳������(Դ�ļ�������ʹ�� zstd ѹ��)
zstd -d -c -q /mnt/wikipedia-flat-seq.zst | /mnt/InsertKeys -h {privateIp} -t 8 --multi-set 8 -f /dev/stdin

# ������������(Դ�ļ�������ʹ�� zstd ѹ��)
zstd -d -c -q /mnt/wikipedia-flat-rand.zst | /mnt/InsertKeys -h {privateIp} -t 8 --multi-set 8 -f /dev/stdin

# ��ȡ����(˳��)
/mnt/GetKeysQps -t 32 -n 8 -f /mnt/wikipedia-flat-key-seq.txt -h {privateIp}

# ��ȡ����(����)
/mnt/GetKeysQps -t 32 -n 8 -f /mnt/wikipedia-flat-key-rand.txt -h {privateIp}

# ���� hash д����
zstd -d -c -q /mnt/weibo.zst  |  /mnt/InsertWeiboData -h {privateIp} -t 32 -f /dev/stdin  --disabled_seekg --ignore_logs
";
}