using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.IpHelper;

using GpnuNetwork.Core.Common;
using GpnuNetwork.Core.Extensions;
using GpnuNetwork.Core.Utils;
using GpnuNetwork.Core.Utils.Win32;

namespace GpnuNetwork.Core.Interop;

public abstract class Win32IcmpApi : IIcmpApi
{
    public static void InitPingEx(bool isIpv6, ref SafeHandle? icmpHandle)
    {
        var handler = isIpv6
            ? PInvoke.Icmp6CreateFile_SafeHandle()
            : PInvoke.IcmpCreateFile_SafeHandle();

        if (handler.IsInvalid)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        icmpHandle = handler;
    }

    public static unsafe void SendEcho(SafeHandle? icmpHandle, SafeHandle callback, IPAddress destination, IPAddress source, IntPtr payload, int payloadSize, IntPtr replyBuffer, int replyBufferSize, PingOptions? options, int timeout)
    {
        if (icmpHandle is not { IsInvalid: false })
            throw new InvalidOperationException("ICMP handle is invalid");

        var win32Options = new IP_OPTION_INFORMATION
        {
            Ttl = (byte)(options?.Ttl ?? 128),
            Flags = (byte)(options is not null && options.DontFragment ? 2 : 0),
            Tos = 0,
            OptionsData = (byte*)nint.Zero,
            OptionsSize = 0
        };

        uint result;
        if (destination.IsV6())
        {
            var sourceAddress = source.ToWinSockAddrIn6();
            var destAddress = destination.ToWinSockAddrIn6();
            result = PInvoke.Icmp6SendEcho2(icmpHandle, callback, null, null, sourceAddress, destAddress, payload.ToPointer(), (ushort)payloadSize, win32Options, replyBuffer.ToPointer(), (uint)replyBufferSize, (uint)timeout);
        }
        else
        {
            var sourceAddress = BitConverter.ToUInt32(source.GetAddressBytes());
            var destAddress = BitConverter.ToUInt32(destination.GetAddressBytes());
            result = PInvoke.IcmpSendEcho2Ex(icmpHandle, callback, null, null, sourceAddress, destAddress, payload.ToPointer(), (ushort)payloadSize, win32Options, replyBuffer.ToPointer(), (uint)replyBufferSize, (uint)timeout);
        }
        
        if (result != 0)
            return;

        var error = Marshal.GetLastPInvokeError();
        if (error != (int)WIN32_ERROR.ERROR_IO_PENDING)
            throw new Win32Exception(error);
    }

    public static unsafe PingExReply ParseReply(bool isIpv6, UnmanagedMemorySafeHandle replyBuffer, int replyBufferSize, int sentPayloadSize)
    {
        return isIpv6
            ? CreatePingReplyFromIcmp6EchoReply(*(ICMPV6_ECHO_REPLY_LH*)replyBuffer.DangerousGetHandle(), replyBuffer.DangerousGetHandle(), sentPayloadSize)
            : CreatePingReplyFromIcmpEchoReply(*(ICMP_ECHO_REPLY*)replyBuffer.DangerousGetHandle());
    }

    public static void DisposePingEx(bool isIpv6, ref SafeHandle? icmpHandler)
    {
        if (icmpHandler is null)
            return;

        icmpHandler.Dispose();
        icmpHandler = null!;
    }

    private static unsafe PingExReply CreatePingReplyFromIcmpEchoReply(in ICMP_ECHO_REPLY reply)
    {
        var address = new IPAddress(reply.Address);
        var statusFromCode = GetStatusFromCode((int) reply.Status);
        long rrt;
        PingOptions? pingOptions;
        byte[] replyData;
        if (statusFromCode == IPStatus.Success)
        {
            rrt = reply.RoundTripTime;
            pingOptions = new PingOptions(reply.Options.Ttl, (reply.Options.Flags & 2) > 0);
            replyData = new byte[reply.DataSize];
            Marshal.Copy((nint)reply.Data, replyData, 0, reply.DataSize);
        }
        else
        {
            rrt = 0L;
            pingOptions = null;
            replyData = [];
        }
        return new PingExReply(address, pingOptions, statusFromCode, rrt, replyData);
    }

    private static PingExReply CreatePingReplyFromIcmp6EchoReply(in ICMPV6_ECHO_REPLY_LH reply, IntPtr dataPtr, int sendSize)
    {
        var address = new IPAddress(MemoryMarshal.Cast<ushort, byte>(reply.Address.sin6_addr.AsReadOnlySpan()), reply.Address.sin6_scope_id);
        var statusFromCode = GetStatusFromCode((int) reply.Status);
        long rtt;
        byte[] replyData;
        if (statusFromCode == IPStatus.Success)
        {
            rtt = reply.RoundTripTime;
            replyData = new byte[sendSize];
            Marshal.Copy(dataPtr + new IntPtr(36), replyData, 0, sendSize);
        }
        else
        {
            rtt = 0L;
            replyData = [];
        }
        return new PingExReply(address, null, statusFromCode, rtt, replyData);
    }

    private static IPStatus GetStatusFromCode(int statusCode)
        => statusCode is 0 or >= 11000 ? (IPStatus) statusCode : throw new Win32Exception(statusCode);
}