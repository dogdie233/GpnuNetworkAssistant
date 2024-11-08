using System.Runtime.CompilerServices;

using GpnuNetwork.Core.Common;
using GpnuNetwork.Core.Interop;

namespace GpnuNetwork.Core.Helpers;

#if Windows
using IcmpApiImpl = Win32IcmpApi;
#else
using IcmpApiImpl = IcmpApi;
#endif

public class IcmpApi : IIcmpApi
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitPingEx(PingEx pingEx)
        => IcmpApiImpl.InitPingEx(pingEx);

    public static void SendPingEx(PingEx pingEx)
        => IcmpApiImpl.SendPingEx(pingEx);
}