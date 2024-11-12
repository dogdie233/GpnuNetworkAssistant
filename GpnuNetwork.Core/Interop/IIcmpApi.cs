using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

using GpnuNetwork.Core.Common;
using GpnuNetwork.Core.Utils;

namespace GpnuNetwork.Core.Interop;

public interface IIcmpApi
{
    public static abstract void InitPingEx(bool isIpv6, ref SafeHandle? icmpHandle);
    public static abstract void SendEcho(SafeHandle? icmpHandle, SafeHandle callback,
        IPAddress destination, IPAddress source, nint payload, int payloadSize, nint replyBuffer, int replyBufferSize,
        PingOptions? options, int timeout);
    public static abstract PingExReply ParseReply(bool isIpv6, UnmanagedMemorySafeHandle replyBuffer, int replyBufferSize, int sentPayloadSize);
    public static abstract void DisposePingEx(bool isIpv6, ref SafeHandle? icmpHandle);
}