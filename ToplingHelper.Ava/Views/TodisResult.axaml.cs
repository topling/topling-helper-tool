using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SkiaSharp;
using ToplingHelper.Ava.Models;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava.Views;

public partial class TodisResult : Window
{
    public ToplingConstants ToplingConstants { get; init; } = default!;

    public TodisResult()
    {
        InitializeComponent();

    
    }


    public string? PrivateIp => (DataContext as InstanceDataBinding)?.PrivateIp;

    public string TestText => $@"
# 请首先确保测试实例为CentOS/AliOS
# 下载自动脚本
wget https://topling.cn/downloads/mount-test.sh && chmod +x ./mount-test.sh

# 挂载测试程序及源数据到 /mnt
sudo bash mount-test.sh /mnt

# 直接执行查看帮助
/mnt/InsertKeys

# 插入顺序数据(源文件过大，已使用 zstd 压缩)
zstd -d -c -q /mnt/wikipedia-flat-seq.zst | /mnt/InsertKeys -h {PrivateIp} -t 8 --multi-set 8 -f /dev/stdin

# 插入乱序数据(源文件过大，已使用 zstd 压缩)
zstd -d -c -q /mnt/wikipedia-flat-rand.zst | /mnt/InsertKeys -h {PrivateIp} -t 8 --multi-set 8 -f /dev/stdin

# 读取数据(顺序)
/mnt/GetKeysQps -t 32 -n 8 -f /mnt/wikipedia-flat-key-seq.txt -h {PrivateIp}

# 读取数据(乱序)
/mnt/GetKeysQps -t 32 -n 8 -f /mnt/wikipedia-flat-key-rand.txt -h {PrivateIp}

# 测试 hash 写性能
zstd -d -c -q /mnt/weibo.zst  |  /mnt/InsertWeiboData -h {PrivateIp} -t 32 -f /dev/stdin  --disabled_seekg --ignore_logs
";
}
