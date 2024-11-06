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
    
    public static string FriendlyOutput(this OperationalStatus status, bool colorful = true)
        => status switch
        {
            OperationalStatus.Up => colorful ? "[green]已连接[/]" : "已连接",
            OperationalStatus.Down => colorful ? "[red]未连接[/]" : "未连接",
            OperationalStatus.Testing => colorful ? "[yellow]测试中[/]" : "测试中",
            OperationalStatus.Unknown => colorful ? $"[yellow]未知({status.ToString()})[/]" : $"未知({status.ToString()})",
            OperationalStatus.Dormant => colorful ? "[yellow]休眠[/]" : "休眠",
            OperationalStatus.NotPresent => colorful ? "[red]不存在[/]" : "不存在",
            OperationalStatus.LowerLayerDown => colorful ? "[red]下层已禁用[/]" : "下层已禁用",
            _ => colorful ? $"[yellow]未知({status.ToString()})[/]" : $"未知({status.ToString()})"
        };

    public static string FriendlyOutput(this IPStatus status, bool colorful = true)
        => status switch
        {
            IPStatus.Success => colorful ? "[green]成功[/]" : "成功",
            IPStatus.DestinationNetworkUnreachable => colorful ? "[red]目标网络不可达[/]" : "目标网络不可达",
            IPStatus.DestinationHostUnreachable => colorful ? "[red]目标主机不可达[/]" : "目标主机不可达",
            IPStatus.DestinationProhibited => colorful ? "[red]目标被禁止[/]" : "目标被禁止",
            IPStatus.DestinationPortUnreachable => colorful ? "[red]目标端口不可达[/]" : "目标端口不可达",
            IPStatus.NoResources => colorful ? "[red]无资源[/]" : "无资源",
            IPStatus.BadOption => colorful ? "[red]选项错误[/]" : "选项错误",
            IPStatus.HardwareError => colorful ? "[red]硬件错误[/]" : "硬件错误",
            IPStatus.PacketTooBig => colorful ? "[red]数据包过大[/]" : "数据包过大",
            IPStatus.TimedOut => colorful ? "[red]超时[/]" : "超时",
            IPStatus.BadRoute => colorful ? "[red]路由错误[/]" : "路由错误",
            IPStatus.TtlExpired => colorful ? "[red]TTL 过期[/]" : "TTL 过期",
            IPStatus.TtlReassemblyTimeExceeded => colorful ? "[red]TTL 重组时间过期[/]" : "TTL 重组时间过期",
            IPStatus.ParameterProblem => colorful ? "[red]参数错误[/]" : "参数错误",
            IPStatus.SourceQuench => colorful ? "[red]源阻塞[/]" : "源阻塞",
            IPStatus.BadDestination => colorful ? "[red]目标错误[/]" : "目标错误",
            IPStatus.DestinationUnreachable => colorful ? "[red]目标不可达[/]" : "目标不可达",
            IPStatus.TimeExceeded => colorful ? "[red]超时[/]" : "超时",
            _ => colorful ? $"[yellow]未知({status.ToString()})[/]" : $"未知({status.ToString()})"
        };
}