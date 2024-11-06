using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using GpnuNetwork.Core.Utils;

namespace GpnuNetwork.Core.Helpers;

public static partial class NetworkCheckToolBox
{
    /// <summary>
    /// It should be an url that will return 204 status code if the internet is available.
    /// Can't use https is because computer may not trust AC's certificate.
    /// </summary>
    public static Uri InternetCheckUrl { get; set; } = new Uri("http://connect.rom.miui.com/generate_204");


    private static readonly HttpClient notRedirectClient = new(new HttpClientHandler()
    {
        AllowAutoRedirect = false,
    });

    [GeneratedRegex("\\.href=['\"](.+?)['\"]")]
    private static partial Regex FindAuthUrlRegex();

    /// <summary>
    /// 判断是否可以连通外网
    /// </summary>
    public static async Task<CheckInternetResult> CheckInternet()
    {
        try
        {
            var response = await notRedirectClient.GetAsync(InternetCheckUrl);

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

    public static Task<Defender.CatchResult<PingReply>> PingAsync(string host, TimeSpan timeout, CancellationToken ct)
    {
        var ping = new Ping();
        return Defender.TryAsync(ctk => ping.SendPingAsync(host, timeout, cancellationToken: ctk), ct);
    }

    public static Task<Defender.CatchResult<PingReply>> PingAsync(IPAddress addr, TimeSpan timeout, CancellationToken ct)
    {
        var ping = new Ping();
        return Defender.TryAsync(ctk => ping.SendPingAsync(addr, timeout, cancellationToken: ctk), ct);
    }
}