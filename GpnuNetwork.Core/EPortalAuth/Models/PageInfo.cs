using System.Text.Json.Serialization;

namespace GpnuNetwork.Core.EPortalAuth.Models;

public class PageInfo
{
    public string? PublicKeyExponent { get; set; }
    public string? PublicKeyModulus { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(PageInfo))]
public partial class PageInfoContext : JsonSerializerContext
{
}