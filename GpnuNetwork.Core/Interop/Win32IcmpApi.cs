using System.Buffers.Binary;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;

using GpnuNetwork.Core.Common;
using GpnuNetwork.Core.Extensions;
using GpnuNetwork.Core.Utils;
using GpnuNetwork.Core.Utils.Win32;

using Microsoft.Win32.SafeHandles;

namespace GpnuNetwork.Core.Interop;

public abstract class Win32IcmpApi : IIcmpApi
{
    public static void InitPingEx(PingEx pingEx)
    {
        var handler = pingEx.Destination.AddressFamily switch
        {
            AddressFamily.InterNetwork => PInvoke.IcmpCreateFile_SafeHandle(),
            AddressFamily.InterNetworkV6 => PInvoke.Icmp6CreateFile_SafeHandle(),
            _ => throw new NotSupportedException("Unsupported address family")
        };
        if (handler.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        pingEx.icmpHandler = handler;
    }

    public static unsafe void SendPingEx(PingEx pingEx)
    {
        if (pingEx.icmpHandler is not IcmpCloseHandleSafeHandle { IsInvalid: false } handler)
            throw new InvalidOperationException("Invalid ICMP handler");

        if (pingEx.Destination.AddressFamily is not AddressFamily.InterNetwork and not AddressFamily.InterNetworkV6
            || (pingEx.Source is not null && pingEx.Source.AddressFamily is not AddressFamily.InterNetwork and not AddressFamily.InterNetworkV6))
            throw new NotSupportedException("Unsupported address family");

        IP_OPTION_INFORMATION? options = pingEx.Options is null
            ? default
            : new IP_OPTION_INFORMATION
            {
                Ttl = (byte)pingEx.Options.Ttl,
                Flags = (byte)(pingEx.Options.DontFragment ? 2 : 0)
            };

        pingEx.RegisterCallback(ReplyCallback);

        /* we use new byte[0] but not null because the fixed statement requires a not null array
         * not use Array.Empty<byte>() is because we don't want to fix a shared array
         * collection expression `[]` will be converted to Array.Empty<T>() by the compiler, so we also don't use it
         */
        // ReSharper disable once UseArrayEmptyMethod
#pragma warning disable CA1825
        var sendBuffer = pingEx.Buffer ?? new byte[0];
#pragma warning restore CA1825

        const int size = 65791;
        pingEx.replyBuffer = UnmanagedMemorySafeHandle.Alloc(size);

        // prevent gc NTR my buffer
        var reply = false;
        pingEx.replyBuffer.DangerousAddRef(ref reply);

        var sendSuccess = SendEcho(handler, pingEx.icmpEvent.GetSafeWaitHandle(), pingEx.Source, pingEx.Destination, sendBuffer, pingEx.replyBuffer.DangerousGetHandle().ToPointer(), size, options, (uint)pingEx.Timeout.TotalMilliseconds, out var recvNumber);

        if (reply)
            pingEx.replyBuffer.DangerousRelease();

        if (!sendSuccess)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == (int)WIN32_ERROR.ERROR_IO_PENDING)
                sendSuccess = true;
            else
                throw new Win32Exception(error);
        }
    }

    private static unsafe void ReplyCallback(PingEx pingEx)
    {
        if (pingEx.replyBuffer is null)
            throw new InvalidOperationException("Reply buffer is null");

        pingEx.SetResult(pingEx.Destination.AddressFamily switch
        {
            AddressFamily.InterNetwork => CreatePingReplyFromIcmpEchoReply(*(ICMP_ECHO_REPLY*)pingEx.replyBuffer.DangerousGetHandle()),
            AddressFamily.InterNetworkV6 => CreatePingReplyFromIcmp6EchoReply(*(ICMPV6_ECHO_REPLY_LH*)pingEx.replyBuffer.DangerousGetHandle(), pingEx.replyBuffer.DangerousGetHandle(), pingEx.Buffer?.Length ?? 0),
            _ => throw new NotSupportedException("Unsupported address family")
        });
    }

    private static unsafe bool SendEcho(IcmpCloseHandleSafeHandle handler, SafeHandle @event, IPAddress? source, IPAddress destination, byte[] sendBuffer, void* replyBuffer, uint replyBufferSize, in IP_OPTION_INFORMATION? options, uint timeout, out uint recvNumber)
    {
        // TODO: We have to copy the buffer to unmanaged memory because the buffer may be moved by GC
        if (destination.IsV6())
        {
            var sourceAddress = (source ?? IPAddress.IPv6Any).ToWinSockAddrIn6();
            var destAddress = destination.ToWinSockAddrIn6();
            fixed (void* pSendBuffer = sendBuffer)
            {
                recvNumber = PInvoke.Icmp6SendEcho2(handler, @event, null, null, sourceAddress, destAddress, pSendBuffer, (ushort)sendBuffer.Length, options, replyBuffer, replyBufferSize, timeout);
                return recvNumber != 0;
            }
        }

        {
            var sourceAddress = source is null ? 0 : BitConverter.ToUInt32(source.GetAddressBytes(), 0);
            var destAddress = BitConverter.ToUInt32(destination.GetAddressBytes());
            fixed (void* pSendBuffer = sendBuffer)
            {
                recvNumber = PInvoke.IcmpSendEcho2Ex(handler, @event, null, null, sourceAddress, destAddress, pSendBuffer, (ushort)sendBuffer.Length, options, replyBuffer, replyBufferSize, timeout);
                return recvNumber != 0;
            }
        }
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

private static PingExReply CreatePingReplyFromIcmp6EchoReply(in ICMPV6_ECHO_REPLY_LH reply, IntPtr dataPtr,  int sendSize)
    {
        var address = new IPAddress(MemoryMarshal.Cast<ushort, byte>(reply.Address.sin6_addr.AsReadOnlySpan()), reply.Address.sin6_scope_id);
        var statusFromCode = GetStatusFromCode((int) reply.Status);
        long rrt;
        byte[] replyData;
        if (statusFromCode == IPStatus.Success)
        {
            rrt = reply.RoundTripTime;
            replyData = new byte[sendSize];
            Marshal.Copy(dataPtr + new IntPtr(36), replyData, 0, sendSize);
        }
        else
        {
            rrt = 0L;
            replyData = [];
        }
        return new PingExReply(address, null, statusFromCode, rrt, replyData);
    }

    private static IPStatus GetStatusFromCode(int statusCode)
        => statusCode is 0 or >= 11000 ? (IPStatus) statusCode : throw new Win32Exception(statusCode);
}