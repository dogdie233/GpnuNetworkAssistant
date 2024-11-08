using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

using GpnuNetwork.Core.Helpers;
using GpnuNetwork.Core.Utils;

namespace GpnuNetwork.Core.Common;

public partial class PingEx
{
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(30);

    private bool sent = false;
    private readonly TaskCompletionSource<PingReply> _tcs;
    internal SafeHandle? icmpHandler = null;
    internal ManualResetEvent icmpEvent = new (false);
    internal UnmanagedMemorySafeHandle? replyBuffer = null;
    
    public IPAddress Destination { get; init; }
    public IPAddress? Source { get; init; }
    public TimeSpan Timeout { get; init; }
    public byte[]? Buffer { get; init; }
    public PingOptions? Options { get; init; }

    private PingEx(IPAddress dest, TimeSpan timeout, IPAddress? source = null, byte[]? buffer = null, PingOptions? options = null)
    {
        Destination = dest;
        Timeout = timeout;
        Source = source;
        Buffer = buffer;
        Options = options;

        _tcs = new TaskCompletionSource<PingReply>();
    }

    public Task<PingReply> SendAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref sent, true, true))
            throw new InvalidOperationException("Ping operation already sent");

        IcmpApi.InitPingEx(this);

        return _tcs.Task;
    }

    internal void SetResult(PingReply reply)
    {
        _tcs.TrySetResult(reply);
    }

    public static PingEx Create(IPAddress dest, IPAddress? source = null, byte[]? buffer = null, PingOptions? options = null)
    {
        return Create(dest, DefaultTimeout, source, buffer, options);
    }

    public static PingEx Create(IPAddress dest, TimeSpan timeout, IPAddress? source, byte[]? buffer = null, PingOptions? options = null)
    {
        if (source is not null && dest.AddressFamily != source.AddressFamily)
            throw new ArgumentException("Destination and source address family mismatch");

        return new PingEx(dest, timeout, source, buffer, options);
    }

    public static Task<PingReply> PingAsync(IPAddress dest, IPAddress? source = null, byte[]? buffer = null, PingOptions? options = null, CancellationToken ct = default)
    {
        return PingAsync(dest, DefaultTimeout, source, buffer, options, ct);
    }
    
    public static Task<PingReply> PingAsync(IPAddress dest, TimeSpan timeout, IPAddress? source, byte[]? buffer = null, PingOptions? options = null, CancellationToken ct = default)
    {
        var ping = Create(dest, timeout, source, buffer, options);
        return ping.SendAsync(ct);
    }
}