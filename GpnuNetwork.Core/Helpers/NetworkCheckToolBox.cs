using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mime;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using GpnuNetwork.Core.Common;
using GpnuNetwork.Core.Extensions;
using GpnuNetwork.Core.Utils;

namespace GpnuNetwork.Core.Helpers;

public static partial class NetworkCheckToolBox
{
    private static readonly SocketsHttpHandler _httpHandler;
    private static readonly HttpClient _notRedirectClient;
    private static NetworkInterface? _usingInterface = null;

    /// <summary>
    /// It should be an url that will return 204 status code if the internet is available.
    /// Can't use https is because computer may not trust AC's certificate.
    /// </summary>
    public static Uri InternetCheckUrl { get; set; } = new("http://connect.rom.miui.com/generate_204");

    public static NetworkInterface? UsingInterface
    {
        get => _usingInterface;
        set
        {
            _usingInterface = value;
            _httpHandler.ConnectCallback = value is null ? null : HttpConnectCallback;
        }
    }


    static NetworkCheckToolBox()
    {
        _httpHandler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
        };
        _notRedirectClient = new HttpClient(_httpHandler);
    }

    [GeneratedRegex("\\.href=['\"](.+?)['\"]")]
    private static partial Regex FindAuthUrlRegex();

    private static async ValueTask<Stream> HttpConnectCallback(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var remoteAddress = (await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, context.DnsEndPoint.AddressFamily, ct)).AddressList;
        var addrFamily = context.DnsEndPoint.AddressFamily is not AddressFamily.Unspecified
            ? context.DnsEndPoint.AddressFamily
            : (remoteAddress.Any(IPAddressExtension.IsV4) ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6);

        var localAddress = UsingInterface?.GetIPProperties().UnicastAddresses
            .FirstOrDefault(addr => addr.Address.AddressFamily == addrFamily)?.Address;
        localAddress ??= addrFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
        var socket = new Socket(addrFamily, SocketType.Stream, ProtocolType.Tcp);
        // socket.Bind(new IPEndPoint(localAddress, 0));
        await socket.ConnectAsync(remoteAddress.Where(ip => ip.AddressFamily == addrFamily).ToArray(), context.DnsEndPoint.Port, ct);
        return new NetworkStream(socket, ownsSocket: true);
    }

    /// <summary>
    /// 判断是否可以连通外网
    /// </summary>
    public static async Task<CheckInternetResult> CheckInternet()
    {
        try
        {
            var response = await _notRedirectClient.GetAsync(InternetCheckUrl);

            // if the status code is 204, it means the internet is available
            if (response.StatusCode == HttpStatusCode.NoContent)
                return CheckInternetResult.CreateSuccess();

            // 30x status code means the server want to redirect to auth page
            if (response.Headers.Location is { } location)
                return CheckInternetResult.CreateAuth(location);

            // some time the auth server use javascript to redirect, so we need to check the content
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();

                // if the content is not a script, we can't determine the result
                if (!content.StartsWith("<script>"))
                    return CheckInternetResult.CreateUnknown(response);

                // try to find the auth url
                var match = FindAuthUrlRegex().Match(content);
                if (!match.Success || match.Groups.Count < 2)
                    return CheckInternetResult.CreateUnknown(response);

                return CheckInternetResult.CreateAuth(new Uri(match.Groups[1].Value));
            }
            return CheckInternetResult.CreateUnknown(response);
        }
        catch (Exception ex)
        {
            return CheckInternetResult.CreateFail(ex);
        }
    }

    public readonly record struct CheckInternetResult(CheckInternetResult.ResultType Type, object? Data)
    {
        public enum ResultType
        {
            Success,
            Fail,
            Auth,
            Unknown
        }

        [MemberNotNullWhen(false, nameof(Data))]
        public bool IsSuccess => Type is ResultType.Success;

        public Exception GetFailData() => (Exception)AccessData();
        public Uri GetAuthData() => (Uri)AccessData();
        public HttpResponseMessage GetUnknownData() => (HttpResponseMessage)AccessData();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object AccessData()
        {
            if (Data is null)
                throw new InvalidOperationException("Data is null");
            return Data;
        }

        public static CheckInternetResult CreateSuccess() => new(ResultType.Success, null);
        public static CheckInternetResult CreateFail(Exception ex) => new(ResultType.Fail, ex);
        public static CheckInternetResult CreateAuth(Uri uri) => new(ResultType.Auth, uri);
        public static CheckInternetResult CreateUnknown(HttpResponseMessage response) => new(ResultType.Unknown, response);
    }


    public static Task<Defender.CatchResult<PingExReply>> PingAsync(IPAddress dest, IPAddress? source, TimeSpan timeout, CancellationToken ct)
    {
        var ping = PingEx.Create(dest, timeout, source);
        ping.Init();
        return Defender.TryAsync(ctk => ping.SendAsync(ctk), ct);
    }
}