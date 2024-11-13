using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using GpnuNetwork.Core.Extensions;

namespace ClashTunBypassTest;

public static class HttpHelper
{
    private static NetworkInterface? _usingInterface;
    private static readonly HttpClient _httpClient;
    private static readonly SocketsHttpHandler _httpHandler;

    public static NetworkInterface? UsingInterface
    {
        get => _usingInterface;
        set
        {
            _usingInterface = value;
            _httpHandler.ConnectCallback = value is null ? null : HttpConnectCallback;
        }
    }

    static HttpHelper()
    {
        _httpHandler = new SocketsHttpHandler()
        {
            UseProxy = false,
            Proxy = null
        };
        _httpClient = new HttpClient(_httpHandler);
    }

    public static async Task DownloadTo(Uri uri, Stream dest)
    {
        Logging.Debug($"[[http]] 向{uri}发送Get请求");
        var response = await _httpClient.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        Logging.Debug($"[[http]] 从{uri}接收到响应，正在写入到目标流，大小：{response.Content.Headers.ContentLength}bytes");
        await response.Content.CopyToAsync(dest, CancellationToken.None);
    }

    private static async ValueTask<Stream> HttpConnectCallback(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var remoteAddress = await DnsEx.GetHostAddressesAsync(context.DnsEndPoint.Host);
        var localAddress = UsingInterface?.GetIpv4Address().FirstOrDefault() ?? IPAddress.Any;
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(localAddress, 0));
        await socket.ConnectAsync(remoteAddress.Where(IPAddressExtension.IsV4).ToArray(), context.DnsEndPoint.Port, ct);
        return new NetworkStream(socket, ownsSocket: true);
    }
}