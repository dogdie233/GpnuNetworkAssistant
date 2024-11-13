using System.Text.Json;

using AuthOfflineNoMore;

using GpnuNetwork.Core.EPortalAuth;
using GpnuNetwork.Core.Helpers;

var baseDirectory = AppContext.BaseDirectory;
var configPath = Path.Combine(baseDirectory, "config.json");

if (!File.Exists(configPath))
{
    Console.WriteLine($"配置文件不存在，即将重新生成在 {configPath}");
    goto InitConfig;
}

Config? config = null;
await using (var fs = File.OpenRead(configPath))
{
    try
    {
        config = await JsonSerializer.DeserializeAsync<Config>(fs, ConfigSerializerContext.Default.Config);
    }
    catch (JsonException e)
    {
        Console.WriteLine("配置文件json无效，即将删除并重新生成");
        File.Delete(configPath);
    }
}
if (config is null)
    goto InitConfig;

#region Valid Config

{
    var valid = true;

    void AssertConfig(string failMessage, bool isValid)
    {
        if (isValid)
            return;

        Console.WriteLine($"配置无效：{failMessage}");
        valid = false;
    }

    AssertConfig($"用户id({nameof(Config.UserId)}不能为空(null)", config.UserId is not null);
    AssertConfig($"用户密码({nameof(Config.Password)}不能为空(null)", config.Password is not null);
    AssertConfig($"检测用链接({nameof(Config.CheckUrl)}不能为空(null)", config.CheckUrl is not null);
    if (config.CheckUrl is not null)
        AssertConfig("检测用链接无效", Uri.TryCreate(config.CheckUrl, UriKind.Absolute, out _));

    if (!valid)
        goto InitConfig;

    // make compiler happy
    if (config.UserId is null || config.Password is null || config.CheckUrl is null)
        goto InitConfig;
}

#endregion

Console.WriteLine("配置文件读取成功");
Console.WriteLine($"用户ID：{config.UserId}");
Console.WriteLine($"用户密码：{new string('*', config.Password.Length)}");
Console.WriteLine($"检测用链接：{config.CheckUrl}");

NetworkCheckToolBox.InternetCheckUrl = new Uri(config.CheckUrl);

var result = await NetworkCheckToolBox.CheckInternet();

switch (result.Type)
{
    case NetworkCheckToolBox.CheckInternetResult.ResultType.Success:
    {
        Console.WriteLine("网络应该正常");
        break;
    }
    case NetworkCheckToolBox.CheckInternetResult.ResultType.Fail:
    {
        Console.WriteLine("网络检测时发生异常");
        Console.WriteLine(result.GetFailData());
        break;
    }
    case NetworkCheckToolBox.CheckInternetResult.ResultType.Unknown:
    {
        Console.WriteLine("网络状态未知");
        Console.WriteLine(result.GetUnknownData());
        break;
    }
    case NetworkCheckToolBox.CheckInternetResult.ResultType.Auth:
    {
        Console.WriteLine("需要认证");
        var uri = result.GetAuthData();
        Console.WriteLine(uri);

        var auth = AuthContext.Create(uri, config.Encrypt);
        var authResult = await auth.LoginAsync(config.UserId, config.Password);
        Console.WriteLine(authResult.success ? "认证成功" : "认证失败");
        Console.WriteLine(authResult.message);

        break;
    }
}

return;

InitConfig:
if (File.Exists(configPath))
    File.Delete(configPath);
await using (var fs = File.Create(configPath))
{
    await JsonSerializer.SerializeAsync(fs, new Config(), ConfigSerializerContext.Default.Config);
}
Console.WriteLine("配置文件生成成功，请编辑配置文件后重启程序");
return;