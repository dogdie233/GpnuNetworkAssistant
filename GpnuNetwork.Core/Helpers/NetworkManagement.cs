using System.Diagnostics.Contracts;
using System.Net.NetworkInformation;

using GpnuNetwork.Core.Interop;

namespace GpnuNetwork.Core.Helpers;

#if Windows
using ManagementImpl = Win32NetworkManagement;
#else
using ManagementImpl = INetworkManagement;
#endif

public static class NetworkManagement
{
    // public static Task<AdapterInfo[]> GetAdaptersInfoAsync()
    // {
    //     return Task.Run(ManagementImpl.GetAdaptersInfo);
    // }

    [Pure]
    public static NetworkInterface[] GetInterfaces()
    {
        return NetworkInterface.GetAllNetworkInterfaces();
    }

    [Pure]
    public static NetworkInterface? GetRecommendedInterface(NetworkInterface[] interfaces)
    {
        if (interfaces.Length == 0)
            return null;

        NetworkInterface? best = null;
        foreach (var nic in interfaces)
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            if (nic.GetPhysicalAddress().GetAddressBytes().Length == 0)
                continue;

            if (best == null || nic.GetIPStatistics().BytesReceived > best.GetIPStatistics().BytesReceived)
                best = nic;
        }

        return best;
    }
}