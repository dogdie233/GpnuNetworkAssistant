using System.Net.NetworkInformation;

namespace GpnuNetwork.Core.Helpers;

public static class Translator
{
    public static string FriendlyOutput(this NetworkInterfaceType type)
        => type switch
        {
            NetworkInterfaceType.Ethernet => "以太网",
            NetworkInterfaceType.Loopback => "环回",
            NetworkInterfaceType.Wireless80211 => "无线",
            NetworkInterfaceType.Unknown => "未知",
            _ => $"未知({type.ToString()})"
        };
    
    public static string FriendlyOutput(this OperationalStatus status, bool colorful = false)
        => status switch
        {
            OperationalStatus.Up => colorful ? "[green]已启用[/]" : "已启用",
            OperationalStatus.Down => colorful ? "[red]已禁用[/]" : "已禁用",
            OperationalStatus.Testing => colorful ? "[yellow]测试中[/]" : "测试中",
            OperationalStatus.Unknown => "未知",
            OperationalStatus.Dormant => colorful ? "[yellow]休眠[/]" : "休眠",
            OperationalStatus.NotPresent => colorful ? "[red]不存在[/]" : "不存在",
            OperationalStatus.LowerLayerDown => colorful ? "[red]下层已禁用[/]" : "下层已禁用",
            _ => $"未知({status.ToString()})"
        };
}