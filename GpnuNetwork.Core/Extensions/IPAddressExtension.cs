using System.Net;
using System.Net.Sockets;

namespace GpnuNetwork.Core.Extensions;

public static class IPAddressExtension
{
    public static bool IsV4(this IPAddress address)
        => address.AddressFamily == AddressFamily.InterNetwork;

    public static bool IsV6(this IPAddress address)
        => address.AddressFamily == AddressFamily.InterNetworkV6;
}