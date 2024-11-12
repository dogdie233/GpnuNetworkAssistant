using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Web;

using GpnuNetwork.Core.EPortalAuth.Models;
using GpnuNetwork.Core.Utils;

namespace GpnuNetwork.Core.EPortalAuth;

public class AuthContext
{
    private readonly string? _queryString;
    private readonly HttpClient _httpClient;
    private bool _usePasswordEncrypt;

    private AuthContext(string authUrl, bool usePasswordEncrypt = true) : this(new Uri(authUrl), usePasswordEncrypt){}
    private AuthContext(Uri authUri, bool usePasswordEncrypt = true)
    {
        _queryString = authUri.Query is { Length: > 0 } ? authUri.Query[1..] : null;

        var baseUri = new Uri(authUri.GetLeftPart(UriPartial.Authority));
        _httpClient = new HttpClient()
        {
            BaseAddress = baseUri
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0");
        _usePasswordEncrypt = usePasswordEncrypt;
    }

    public async Task<(string? exponent, string? modulus)> GetEncryptKey()
    {
        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("queryString", _queryString ?? string.Empty)
        ]);
        using var res = await _httpClient.PostAsync("eportal/InterFace.do?method=pageInfo", content);
        res.EnsureSuccessStatusCode();
        var pageInfo = await res.Content.ReadFromJsonAsync(PageInfoContext.Default.PageInfo);
        if (pageInfo is not { PublicKeyExponent.Length: > 0, PublicKeyModulus.Length: > 0 })
            return (null, null);

        return (pageInfo.PublicKeyExponent, pageInfo.PublicKeyModulus);
    }

    public async Task<(bool success, string? message)> LoginAsync(string userId, string password)
    {
        string? exponent = null, modulus = null;
        var encrypted = false;
        if (_usePasswordEncrypt)
            (exponent, modulus) = await GetEncryptKey();

        if (exponent is not null && modulus is not null)
        {
            var mac = HttpUtility.ParseQueryString(_queryString ?? string.Empty).Get("mac") ?? "111111111";
            password = EncryptHelper.AuthPasswordEncrypt(password, mac, exponent, modulus);
            encrypted = true;
        }

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "userId", userId },
            { "password", password },
            { "service", string.Empty },
            { "queryString", _queryString ?? string.Empty },
            { "operatorPwd", string.Empty },
            { "operatorUserId", string.Empty },
            { "validcode", string.Empty },
            { "passwordEncrypt", encrypted.ToString().ToLower() }
        });

        using var res = await _httpClient.PostAsync("eportal/InterFace.do?method=login", content);
        res.EnsureSuccessStatusCode();
        var loginResult = await res.Content.ReadFromJsonAsync(LoginResultContext.Default.LoginResult);
        if (loginResult is null)
            return (false, "Invalid return content");

        return (loginResult.Result == "success", loginResult.Message);
    }

    public static AuthContext Create(Uri authUri, bool usePasswordEncrypt = true)
    {
        var context = new AuthContext(authUri, usePasswordEncrypt);
        return context;
    }

    public static AuthContext Create(string authUrl, bool usePasswordEncrypt = true)
    {
        var context = new AuthContext(authUrl, usePasswordEncrypt);
        return context;
    }
}