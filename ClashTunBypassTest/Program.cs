using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;

using ClashTunBypassTest;

using GpnuNetwork.Core.Extensions;
using GpnuNetwork.Core.Helpers;

using Spectre.Console;

var interfaces = NetworkManagement.GetInterfaces()
    .Where(ic => ic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Unknown))
    .ToArray();
var niceInterface = NetworkManagement.GetRecommendedInterface(interfaces) ?? interfaces.FirstOrDefault();
AnsiConsole.Write(new Rule("[fuchsia]Network Adapter[/]"));
if (niceInterface is null)
{
    niceInterface = AnsiConsole.Prompt(
        new SelectionPrompt<InterfaceChoice>()
            .Title("请选择你正在使用的[yellow]网络适配器[/]")
            .MoreChoicesText("[grey](按下方向键查看更多选项)[/]")
            .AddChoices(interfaces.Select(i => new InterfaceChoice(i)))).Interface;
}

AnsiConsole.MarkupLine($"将使用 [yellow]{niceInterface.Name}[/] 进行测试");
DnsEx.UsingInterface = niceInterface;
HttpHelper.UsingInterface = niceInterface;

var adapterInfoTable = new Table()
    .AddColumn("属性")
    .AddColumn("值");

adapterInfoTable.AddRow("状态", niceInterface.OperationalStatus.FriendlyOutput(colorful: true));
if (niceInterface.GetIpv4Address() is { Length: > 0 } v4Address)
    adapterInfoTable.AddRow("Ipv4 地址", string.Join('\n', v4Address.Select(addr => addr.ToString())));
else
    adapterInfoTable.AddRow("Ipv4 地址", "[red]无[/]");
if (niceInterface.GetIPProperties().GatewayAddresses.Select(addr => addr.Address).Where(IPAddressExtension.IsV4).ToArray() is { Length: > 0 } gateways)
    adapterInfoTable.AddRow("网关", string.Join('\n', gateways.Select(addr => addr.ToString())));
else
    adapterInfoTable.AddRow("网关", "[red]无[/]");
if (niceInterface.GetIPProperties().DnsAddresses.Where(IPAddressExtension.IsV4).ToArray() is { Length: > 0 } dns)
    adapterInfoTable.AddRow("DNS", string.Join('\n', dns.Select(addr => addr.ToString())));
else
    adapterInfoTable.AddRow("DNS", "[red]无[/]");

AnsiConsole.Write(adapterInfoTable);

AnsiConsole.Write(new Rule("[fuchsia]Download Url[/]"));
var url = AnsiConsole.Prompt(
    new TextPrompt<string>("请输入你要下载的内容的网址：")
        .Validate(s => Uri.TryCreate(s.StartsWith("http://") || s.StartsWith("https://") ? s : "https://" + s, UriKind.Absolute, out _)
            ? ValidationResult.Success()
            : ValidationResult.Error("请输入一个有效的网址")));
if (!Uri.TryCreate(url.StartsWith("http://") || url.StartsWith("https://") ? url : "https://" + url, UriKind.Absolute, out var uri))
    Exit();

AnsiConsole.MarkupLine($"正在测试解析 [aqua]{uri.Host}[/]");
var ipAddresses = Array.Empty<IPAddress>();
try
{
    ipAddresses = await DnsEx.GetHostAddressesAsync(uri.Host);
}
catch (Exception e)
{
    Logging.Exception(e);
    Exit();
}

var filterIp = ipAddresses.Where(IPAddressExtension.IsV4).ToArray();
if (filterIp.Length == 0)
{
    AnsiConsole.MarkupLine("[red]无法解析到 IPv4 地址[/]");
    Exit();
}
AnsiConsole.MarkupLine($"测试解析得到[green]{filterIp.Length}[/]条地址：{string.Join('，', filterIp.Select(ip => $"[aqua]{ip}[/]"))}");
var fileName = uri.LocalPath.LastIndexOf('/') != -1 ? uri.LocalPath[(uri.LocalPath.LastIndexOf('/') + 1)..] : "download";
fileName = string.IsNullOrWhiteSpace(fileName) ? "download" : fileName;
var savePath = Path.Combine(AppContext.BaseDirectory, fileName);
var accept = AnsiConsole.Prompt(
    new TextPrompt<char>($"是否保存到{savePath}？[[Y/n]]")
        .DefaultValue('Y')
        .Validate(s => s is 'Y' or 'y' or 'N' or 'n'
            ? ValidationResult.Success()
            : ValidationResult.Error("请输入 Y 或 N"))) is 'Y' or 'y';

if (!accept)
    Exit();

AnsiConsole.MarkupLine("开始下载...懒得做进度条了...");
if (File.Exists(savePath))
    File.Delete(savePath);

await using var fs = File.Create(savePath);
try
{
    await HttpHelper.DownloadTo(uri, fs);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine("[red]下载文件时发生异常[/]");
    AnsiConsole.WriteException(ex);
    Exit();
}
await fs.FlushAsync();
var size = fs.Length;
fs.Close();

AnsiConsole.MarkupLine($"[green]下载完了，下了[yellow]{size}[/]bytes[/]");
Exit();
return;


[DoesNotReturn]
static void Exit()
{
    AnsiConsole.MarkupLine("[lime]程序运行结束，按任意键退出[/]");
    Console.ReadKey();
    Environment.Exit(0);
}

internal readonly record struct InterfaceChoice(NetworkInterface Interface)
{
    public override string ToString()
        => $"[blue]{Interface.NetworkInterfaceType.FriendlyOutput()}[/] 适配器 [yellow]{Interface.Name}[/]";
}
