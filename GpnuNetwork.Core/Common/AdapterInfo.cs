using System.Net;

namespace GpnuNetwork.Core.Common;

public class AdapterInfo
{
    public string Name { get; set; }
    public string? FriendlyName { get; set; }
    public IPAddress? Address4 { get; set; }
    public IPAddress? Address6 { get; set; }
    public string MacAddress { get; set; }

    public string DisplayName => FriendlyName ?? Name;
}