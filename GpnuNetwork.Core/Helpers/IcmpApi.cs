using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using GpnuNetwork.Core.Common;
using GpnuNetwork.Core.Interop;
using GpnuNetwork.Core.Utils;

namespace GpnuNetwork.Core.Helpers;

#if Windows
using IcmpApiImpl = Win32IcmpApi;
#else
using IcmpApiImpl = IcmpApi;
#endif

public class IcmpApi : IIcmpApi
{
    public static void InitPingEx(bool isIpv6, ref SafeHandle? icmpHandle)
        => IcmpApiImpl.InitPingEx(isIpv6, ref icmpHandle);

    public static void SendEcho(SafeHandle? icmpHandle, SafeHandle callback, IPAddress destination, IPAddress source, nint payload, int payloadSize, nint replyBuffer, int replyBufferSize, PingOptions? options, int timeout)
        => IcmpApiImpl.SendEcho(icmpHandle, callback, destination, source, payload, payloadSize, replyBuffer, replyBufferSize, options, timeout);

    public static PingExReply ParseReply(bool isIpv6, UnmanagedMemorySafeHandle replyBuffer, int replyBufferSize)
        => IcmpApiImpl.ParseReply(isIpv6, replyBuffer, replyBufferSize);

    public static void DisposePingEx(bool isIpv6, ref SafeHandle? icmpHandle)
        => IcmpApiImpl.DisposePingEx(isIpv6, ref icmpHandle);
}