using System.Net;

using Windows.Win32.Networking.WinSock;

using GpnuNetwork.Core.Extensions;

namespace GpnuNetwork.Core.Utils.Win32;

internal static class IPAddessExtension
{
    internal static SOCKADDR_IN6 ToWinSockAddrIn6(this IPAddress address, ushort port = 0)
    {
        if (!address.IsV6())
            throw new ArgumentException("Address is not IPv6");

        var addr = new SOCKADDR_IN6
        {
            sin6_family = ADDRESS_FAMILY.AF_INET6,
            sin6_port = port,
            sin6_flowinfo = 0,
        };
        addr.Anonymous.sin6_scope_id = 0;

        address.TryWriteBytes(addr.sin6_addr.u.Byte.AsSpan(), out _);
        return addr;
    }
}