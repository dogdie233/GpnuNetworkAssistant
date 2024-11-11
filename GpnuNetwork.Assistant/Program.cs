using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using GpnuNetwork.Assistant;
using GpnuNetwork.Core.Common;
using GpnuNetwork.Core.EPortalAuth;
using GpnuNetwork.Core.Extensions;
using GpnuNetwork.Core.Helpers;
using GpnuNetwork.Core.Utils;

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
NetworkCheckToolBox.UsingInterface = selectedInterface;

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

NetworkCheckToolBox.CheckInternetResult internetCheckResult = NetworkCheckToolBox.CheckInternetResult.CreateSuccess();
await AnsiConsole.Status()
    .StartAsync("正在检查网络连接...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Circle);

        #region Check Internet

        ctx.Status("正在检测能否正常打开网页...");

        LogDebug($"向 {NetworkCheckToolBox.InternetCheckUrl} 发起http请求");
        internetCheckResult = await NetworkCheckToolBox.CheckInternet();
        AnsiConsole.MarkupLine(internetCheckResult.Type switch
        {
            NetworkCheckToolBox.CheckInternetResult.ResultType.Success => "[green]√ 外网连接正常[/]",
            NetworkCheckToolBox.CheckInternetResult.ResultType.Fail => $"[red]× 外网连接失败 [/] {FriendlyNetworkExceptionMessage(internetCheckResult.GetFailData())}",
            NetworkCheckToolBox.CheckInternetResult.ResultType.Auth => $"[yellow]? 需要认证，认证网址：[/]{internetCheckResult.GetAuthData().ToString().EscapeMarkup()}",
            NetworkCheckToolBox.CheckInternetResult.ResultType.Unknown => $"[yellow]? 未知错误[/] {internetCheckResult.GetUnknownData().StatusCode}",
            _ => $"[yellow]? 未知测试结果 {internetCheckResult.Type}[/]"
        });

        #endregion
    });

if (internetCheckResult.Type == NetworkCheckToolBox.CheckInternetResult.ResultType.Auth)
{
    var action = AnsiConsole.Prompt(
        new SelectionPrompt<EnumChoice<AuthAction>>()
            .Title("当前网络需要认证，是否进行认证")
            .AddChoices(
                new EnumChoice<AuthAction>(AuthAction.LoginByApi, "在本程序内登录"),
                new EnumChoice<AuthAction>(AuthAction.OpenInBrowser | AuthAction.ExitProgram, "打开浏览器登录并退出本程序"),
                new EnumChoice<AuthAction>(AuthAction.ExitProgram, "什么都不干退出程序")));

    if (action.Value.HasFlag(AuthAction.OpenInBrowser))
        Process.Start(internetCheckResult.GetAuthData().ToString());

    if (action.Value.HasFlag(AuthAction.LoginByApi))
    {
        if (await DoAuthLogin(internetCheckResult.GetAuthData().ToString()) == false)
            Exit();
    }

    if (action.Value.HasFlag(AuthAction.ExitProgram))
        Exit();
}


await AnsiConsole.Status()
    .StartAsync("正在检测网关连通性...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Circle);

        #region Check Gateway

        ctx.Status("正在检测网关连通性...");

        // select the first ipv4 gateway, if not exist, select the first gateway
        var gateway = selectedInterface.GetGateways().FirstOrDefault(IPAddressExtension.IsV4)
                      ?? selectedInterface.GetGateways().FirstOrDefault();

        if (gateway is not null)
        {
            var source = selectedInterface.GetIPProperties().UnicastAddresses.FirstOrDefault(ip => ip.Address.AddressFamily == gateway.AddressFamily)?.Address;
            var timeout = TimeSpan.FromSeconds(3);
            const int count = 5;
            AnsiConsole.MarkupLine($"{(source is not null ? $"从 [aqua]{source} [/]" : "" )}向 [aqua]{gateway}[/] 发起 [yellow]{count}[/] 次ping请求，请求超时时间为 [yellow]{timeout.TotalSeconds}[/]s");
            for (var i = 1; i <= count; i++)
            {
                var pingResult = await NetworkCheckToolBox.PingAsync(gateway, source, timeout, CancellationToken.None);
                var success = pingResult is { IsSuccess: true, Data.Status: IPStatus.Success };
                var reply = pingResult.Data;

                AnsiConsole.Markup($"[[{i}/{count}]] ");
                var msg = success
                    ? $"[green]成功[/] <-- [aqua]{reply.Address}[/] 载荷：[yellow]{reply.Buffer.Length}[/]B 延迟：{reply.RoundtripTime.Paint(10, 30, Color.Green, Color.Yellow, Color.Red)}ms"
                    : $"[red]失败[/]：{reply.Status.FriendlyOutput()}";
                AnsiConsole.MarkupLine(msg);
            }
        }
        else
            AnsiConsole.MarkupLine("[red]× 不存在网关[/]");

        #endregion
    });

Exit();

#endregion

static async Task<bool> DoAuthLogin(string authUrl)
{
    var username = AnsiConsole.Prompt(new TextPrompt<string>("[yellow]用户名：[/]"));
    var password = AnsiConsole.Prompt(new TextPrompt<string>("[yellow]密  码：[/]").Secret());

    var auth = AuthContext.Create(authUrl, true);
    try
    {
        var (success, message) = await auth.LoginAsync(username, password);
        AnsiConsole.MarkupLine(!success ? $"[red]× 登录失败：[/]{message}" : "[green]√ 登录成功[/]");
        return success;
    }
    catch (Exception e)
    {
        AnsiConsole.MarkupLine($"[red]× 登录失败：[/]{e.Message}");
        return false;
    }
}

static void LogDebug(string message) => AnsiConsole.MarkupLine($"[grey][[debug]] {message}[/]");

static string FriendlyNetworkExceptionMessage(Exception ex)
{
    var socketException = ex.ExpandTreeDeepFirst(e => e.InnerException is not null ? [e.InnerException] : [])
        .OfType<SocketException>()
        .FirstOrDefault();

    if (socketException is not null)
    {
        return "Socket Exception: " + socketException.SocketErrorCode switch
        {
            SocketError.HostNotFound => "找不到主机",
            SocketError.ConnectionRefused => "连接被拒绝",
            SocketError.NetworkUnreachable => "网络不可达",
            SocketError.TimedOut => "连接超时",
            _ => ex.Message
        };
    }

    return $"{ex.GetType().Name}: {ex.Message}";
}

[DoesNotReturn]
static void Exit()
{
    AnsiConsole.MarkupLine("[lime]程序运行结束，按任意键退出[/]");
    Console.ReadKey();
    Environment.Exit(0);
}

[Flags]
public enum AuthAction
{
    None = 0,
    LoginByApi = 1,
    OpenInBrowser = 2,
    ExitProgram = 4
}

internal readonly record struct InterfaceChoice(NetworkInterface Interface)
{
    public override string ToString()
        => $"[yellow]{Interface.NetworkInterfaceType.FriendlyOutput()}[/] 适配器 [green]{Interface.Name}[/]";
}

internal readonly record struct EnumChoice<T>(T Value, string Prompt) where T : Enum
{
    public override string ToString() => Prompt;
}