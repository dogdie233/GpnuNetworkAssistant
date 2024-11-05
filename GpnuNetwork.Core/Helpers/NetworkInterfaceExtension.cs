using System.Diagnostics.Contracts;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;

namespace GpnuNetwork.Core.Helpers;

public static class NetworkInterfaceExtension
{
    [Pure]
    public static IPAddress[] GetIpv4Address(this NetworkInterface ic)
        => ic.GetIPProperties().UnicastAddresses.Select(info => info.Address)
            .Where(addr => addr.AddressFamily == AddressFamily.InterNetwork)
            .ToArray();

    [Pure]
    public static IPAddress[] GetIpv6Address(this NetworkInterface ic)
        => ic.GetIPProperties().UnicastAddresses.Select(info => info.Address)
            .Where(addr => addr.AddressFamily == AddressFamily.InterNetworkV6)
            .ToArray();

    [Pure]
    [SupportedOSPlatform("windows")]
    public static bool IsDhcpEnabled(this NetworkInterface ic)
        => ic.GetIPProperties().GetIPv4Properties().IsDhcpEnabled;

    [Pure]
    public static string ToString(this PhysicalAddress address, char delimiter = ':')
        => string.Join(delimiter, address.GetAddressBytes().Select(b => b.ToString("X2")));

    [Pure]
    public static IPAddress[] GetGateways(this NetworkInterface ic)
        => ic.GetIPProperties().GatewayAddresses.Select(info => info.Address).ToArray();

    [Pure]
    public static IPAddress[] GetDnsServers(this NetworkInterface ic)
        => ic.GetIPProperties().DnsAddresses.ToArray();
}