using System.Text;
using System.Text.Json.Serialization;

namespace AuthOfflineNoMore;

public class Config
{
    public string? UserId { get; set; } = "User Id";
    public string? Password { get; set; } = "Password";
    public string? CheckUrl { get; set; } = "http://connect.rom.miui.com/generate_204";
    public bool Encrypt { get; set; } = true;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
public partial class ConfigSerializerContext : JsonSerializerContext
{

}