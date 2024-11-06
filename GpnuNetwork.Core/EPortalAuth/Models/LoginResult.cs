using System.Text.Json.Serialization;

namespace GpnuNetwork.Core.EPortalAuth.Models;

public class LoginResult
{
    public string UserIndex { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    [JsonPropertyName("forwordurl")] public string? ForwardUrl { get; set; }
    public int KeepaliveInterval { get; set; }
    [JsonPropertyName("casFailErrString")] public string? CasFailErrorString { get; set; }
    public string ValidCodeUrl { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(LoginResult))]
public partial class LoginResultContext : JsonSerializerContext
{
}