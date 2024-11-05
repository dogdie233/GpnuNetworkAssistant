using System.Net.NetworkInformation;

using GpnuNetwork.Assistant;
using GpnuNetwork.Core.Helpers;

using Spectre.Console;

#region Ask For Interface

var interfaces = NetworkManagement.GetInterfaces();
if (NetworkManagement.GetRecommendedInterface(interfaces) is {} recommended)
{
    var idx = Array.IndexOf(interfaces, recommended);
    (interfaces[idx], interfaces[0]) = (interfaces[0], interfaces[idx]);
}

var selectedInterface = AnsiConsole.Prompt(
    new SelectionPrompt<InterfaceChoice>()
        .Title("请选择一个[yellow]网络适配器[/]")
        .MoreChoicesText("[grey](按下方向键查看更多选项)[/]")
        .AddChoices(interfaces.Select(i => new InterfaceChoice(i)))).Interface;

AnsiConsole.MarkupLine($"已选择[green]{selectedInterface.Name}[/]适配器");

#endregion

#region Print Interface Info

{
    var table = new Table()
        .AddColumn("属性")
        .AddColumn("值");

    table.AddRow("状态", selectedInterface.OperationalStatus.FriendlyOutput(colorful: true));
    table.AddRow("名称", selectedInterface.Name);
    table.AddRow("描述", selectedInterface.Description);
    table.AddRow("类型", selectedInterface.NetworkInterfaceType.FriendlyOutput());
    table.AddRow("速度", selectedInterface.Speed == -1 ? "未知" : $"{selectedInterface.Speed / 1000 / 1000} Mbps");
    table.AddRow("MAC 地址", selectedInterface.GetPhysicalAddress().ToString(':'));

    table.AddRow("DHCP", selectedInterface.IsDhcpEnabled() ? "[green]是[/]" : "[yellow]否[/]");

    if (selectedInterface.GetIpv4Address() is { Length: > 0 } v4Address)
        table.AddRow("Ipv4 地址", string.Join('\n', v4Address.Select(addr => addr.ToString())));
    else
        table.AddRow("Ipv4 地址", "[red]无[/]");

    if (selectedInterface.GetIpv6Address() is { Length: > 0 } v6Address)
        table.AddRow("Ipv6 地址", string.Join('\n', v6Address.Select(addr => addr.ToString())));
    else
        table.AddRow("Ipv6 地址", "[red]无[/]");

    if (selectedInterface.GetGateways() is { Length: > 0 } gateways)
        table.AddRow("网关", string.Join('\n', gateways.Select(addr => addr.ToString())));
    else
        table.AddRow("网关", "[red]无[/]");

    if (selectedInterface.GetDnsServers().Length != 0)
        table.AddRow("DNS 服务器", string.Join('\n', selectedInterface.GetDnsServers().Select(addr => addr.ToString())));
    else
        table.AddRow("DNS 服务器", "[red]无[/]");

    AnsiConsole.WriteLine("网络适配器信息：");
    AnsiConsole.Write(table);
}

#endregion

#region Check Network

await AnsiConsole.Status()
    .StartAsync("正在检查网络连接...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Circle);
        ctx.Status("正在检测能否正常打开网页...");

        LogDebug($"向 {NetworkCheckToolBox.InternetCheckUrl} 发起http请求");
        var internetCheckResult = await NetworkCheckToolBox.CheckInternet();
        AnsiConsole.MarkupLine(internetCheckResult.Type switch
        {
            NetworkCheckToolBox.CheckInternetResult.ResultType.Success => "[green]测试连接正常[/]",
            NetworkCheckToolBox.CheckInternetResult.ResultType.Fail => $"[red]测试连接失败[/] {internetCheckResult.GetFailData().GetType().Name}: {internetCheckResult.GetFailData().Message}",
            NetworkCheckToolBox.CheckInternetResult.ResultType.Auth => $"[yellow]需要认证，认证网址：[/]{internetCheckResult.GetAuthData()}",
            NetworkCheckToolBox.CheckInternetResult.ResultType.Unknown => $"[yellow]未知错误[/] {internetCheckResult.GetUnknownData().StatusCode}",
            _ => $"[yellow]未知测试结果 {internetCheckResult.Type}[/]"
        });
    });

#endregion

static void LogDebug(string message) => AnsiConsole.MarkupLine($"[grey][[debug]] {message}[/]");


internal readonly record struct InterfaceChoice(NetworkInterface Interface)
{
    public override string ToString() => $"[yellow]{Interface.NetworkInterfaceType.FriendlyOutput()}[/] 适配器 [green]{Interface.Name}[/]";
}