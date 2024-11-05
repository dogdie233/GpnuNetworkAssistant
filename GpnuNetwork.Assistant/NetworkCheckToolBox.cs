using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace GpnuNetwork.Assistant;

public static partial class NetworkCheckToolBox
{
    public static Uri InternetCheckUrl { get; set; } = new Uri("http://www.msftconnecttest.com/connecttest.txt");

    private static readonly HttpClient notRedirectClient = new(new HttpClientHandler()
    {
        AllowAutoRedirect = false
    });

    [GeneratedRegex("\\.href=['\"](.+?)['\"]")]
    private static partial Regex FindAuthUrlRegex();

    /// <summary>
    /// 判断是否可以连通外网
    /// </summary>
    /// <returns></returns>
    public static async Task<CheckInternetResult> CheckInternet()
    {
        try
        {
            var response = await notRedirectClient.GetAsync(InternetCheckUrl);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                if (content == "Microsoft Connect Test")
                    return CheckInternetResult.CreateSuccess();

                if (!content.StartsWith("<script>"))
                    return CheckInternetResult.CreateUnknown(response);

                var match = FindAuthUrlRegex().Match(content);
                if (!match.Success || match.Groups.Count < 2)
                    return CheckInternetResult.CreateUnknown(response);
                return CheckInternetResult.CreateAuth(new Uri(match.Groups[1].Value));

            }

            if (response.Headers.Location is { } location)
                return CheckInternetResult.CreateAuth(location);
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

    public static long? Ping(string host, TimeSpan timeout)
    {
        var ping = new Ping();
        var reply = ping.Send(host, timeout, Array.Empty<byte>(), new PingOptions());
        return reply?.RoundtripTime;
    }
}