using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using GpnuNetwork.Core.Extensions;
using GpnuNetwork.Core.Helpers;
using GpnuNetwork.Core.Utils;

namespace GpnuNetwork.Core.Common;

public class PingEx
{
    public enum StateType
    {
        Created,
        Init,
        Sent,
        Finished,
        Cancelled,
    }

    private const int DEFAULT_SEND_BUFFER_SIZE = 32;
    private static byte[]? defaultSendBuffer;

    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(30);
    private static byte[] DefaultSendPayload
    {
        get
        {
            if (defaultSendBuffer != null)
                return defaultSendBuffer;

            defaultSendBuffer = new byte[DEFAULT_SEND_BUFFER_SIZE];
            for (var i = 0; i < DEFAULT_SEND_BUFFER_SIZE; i++)
                defaultSendBuffer[i] = (byte)('a' + i % 23);
            return defaultSendBuffer;
        }
    }

    private StateType _state = StateType.Created;
    private readonly TaskCompletionSource<PingExReply> _tcs;
    private readonly CancellationTokenSource _timeoutOrCancelSource;
    private SafeHandle? _icmpHandle = null;
    private bool _isReplyBufferAddRef = false;
    private bool _isEchoBufferAddRef = false;
    private ManualResetEvent? _icmpEvent;
    private RegisteredWaitHandle? _waitHandle;
    internal UnmanagedMemorySafeHandle? replyBuffer = null;
    internal UnmanagedMemorySafeHandle? echoBuffer = null;
    internal int replyBufferSize = 0;
    
    public IPAddress Destination { get; init; }
    public IPAddress Source { get; init; }
    public TimeSpan Timeout { get; init; }
    public byte[] EchoPayload { get; init; }
    public PingOptions? Options { get; init; }

    private PingEx(IPAddress dest, IPAddress source, TimeSpan timeout, byte[]? payload = null, PingOptions? options = null)
    {
        Destination = dest;
        Source = source;
        Timeout = timeout;
        EchoPayload = payload ?? DefaultSendPayload;
        Options = options;

        _tcs = new TaskCompletionSource<PingExReply>();
        _timeoutOrCancelSource = new CancellationTokenSource();
    }

    public void Init()
    {
        if (Interlocked.CompareExchange(ref _state, StateType.Init, StateType.Created) != StateType.Created)
            throw new InvalidOperationException("Ping operation already init");

        // init userdata
        IcmpApi.InitPingEx(Destination.IsV6(), ref _icmpHandle);
    }

    public Task<PingExReply> SendAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _state, StateType.Sent, StateType.Init) != StateType.Init)
            throw new InvalidOperationException("Ping operation not init or have been sent");

        // prepare buffer
        PrepareBuffer();
        echoBuffer.DangerousAddRef(ref _isEchoBufferAddRef);
        replyBuffer.DangerousAddRef(ref _isReplyBufferAddRef);

        // register cancellation
        RegisterCallback();
        using var ctr = ct.UnsafeRegister(static state => ((PingEx)state!).Cancel(), this);

        try
        {
            IcmpApi.SendEcho(_icmpHandle, _icmpEvent.GetSafeWaitHandle(), Destination, Source, echoBuffer.DangerousGetHandle(),
                EchoPayload.Length, replyBuffer.DangerousGetHandle(), replyBufferSize, Options, (int)Timeout.TotalMilliseconds);
        }
        catch (Exception e)
        {
            Cleanup();
            _tcs.SetException(e);
        }

        _timeoutOrCancelSource.CancelAfter(Timeout);
        return _tcs.Task;
    }

    [MemberNotNull(nameof(_icmpEvent), nameof(_waitHandle))]
    internal void RegisterCallback()
    {
        if (_icmpEvent is null)
            _icmpEvent = new ManualResetEvent(false);
        else
            _icmpEvent.Reset();

        _waitHandle?.Unregister(_icmpEvent);
        _waitHandle = ThreadPool.RegisterWaitForSingleObject(_icmpEvent, static (state, _) => OnCallbackReceived((PingEx)state!), this, -1, true);
    }

    private void Cleanup()
    {
        _waitHandle?.Unregister(_icmpEvent);
        if (_isEchoBufferAddRef)
        {
            echoBuffer!.DangerousRelease();
            _isEchoBufferAddRef = false;
        }

        if (_isReplyBufferAddRef)
        {
            replyBuffer!.DangerousRelease();
            _isReplyBufferAddRef = false;
        }

        IcmpApi.DisposePingEx(Destination.IsV6(), ref _icmpHandle);
    }

    private void Cancel()
    {
        _timeoutOrCancelSource.Cancel();
        _tcs.SetCanceled();
    }

    [MemberNotNull(nameof(echoBuffer))]
    [MemberNotNull(nameof(replyBuffer))]
    private void PrepareBuffer()
    {
        if (echoBuffer is { IsInvalid: false } && replyBuffer is { IsInvalid: false })
            return;

        echoBuffer = null;
        echoBuffer = UnmanagedMemorySafeHandle.Alloc(EchoPayload.Length);

        replyBuffer = null;
        replyBufferSize = 65791;
        replyBuffer = UnmanagedMemorySafeHandle.Alloc(replyBufferSize);
    }

    private static void OnCallbackReceived(PingEx instance)
    {
        if (instance.replyBuffer is null)
        {
            instance._tcs.TrySetException(new NullReferenceException($"{nameof(replyBuffer)} is null"));
            return;
        }

        try
        {
            var result = IcmpApi.ParseReply(instance.Destination.IsV6(), instance.replyBuffer, instance.replyBufferSize);
            instance._tcs.TrySetResult(result);
        }
        catch (Exception e)
        {
            instance._tcs.TrySetException(e);
        }

        instance.Cleanup();
    }

    // TODO: Allow ping a host by hostname

    #region Factory Method

    public static PingEx Create(IPAddress dest, IPAddress? source = null, byte[]? buffer = null, PingOptions? options = null)
    {
        return Create(dest, DefaultTimeout, source, buffer, options);
    }

    public static PingEx Create(IPAddress dest, TimeSpan timeout, IPAddress? source = null, byte[]? buffer = null, PingOptions? options = null)
    {
        source ??= dest.AddressFamily switch
        {
            AddressFamily.InterNetwork => IPAddress.None,
            AddressFamily.InterNetworkV6 => IPAddress.IPv6None,
            _ => throw new NotSupportedException("Unsupported address family")
        };

        if (dest.AddressFamily != source.AddressFamily)
            throw new ArgumentException("Destination and source address family mismatch");

        return new PingEx(dest, source, timeout, buffer, options);
    }

    public static Task<PingExReply> PingAsync(IPAddress dest, IPAddress? source = null, byte[]? payload = null, PingOptions? options = null, CancellationToken ct = default)
    {
        return PingAsync(dest, DefaultTimeout, source, payload, options, ct);
    }

    public static Task<PingExReply> PingAsync(IPAddress dest, TimeSpan timeout, IPAddress? source = null, byte[]? payload = null, PingOptions? options = null, CancellationToken ct = default)
    {
        var ping = Create(dest, timeout, source, payload, options);
        ping.Init();
        return ping.SendAsync(ct);
    }

    #endregion
}